using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using TTT.Database;
using TTT.DataSets;
using TTT.Movement.ConsumerBackgroundService;
using TTT.TrainData.DataSets;

namespace TTT.Movement;

// BackgroundService
public sealed class TrainMovementsConsumer : BackgroundService
{
    private readonly ILogger<TrainMovementsConsumer> _log;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<OpenRailOptions> _opts;

    public TrainMovementsConsumer(
        ILogger<TrainMovementsConsumer> log,
        IServiceScopeFactory scopeFactory,
        IOptions<OpenRailOptions> opts)
    {
        _log = log; _scopeFactory = scopeFactory; _opts = opts;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var cfg = new ConsumerConfig
        {
            BootstrapServers = _opts.Value.BootstrapServers,
            GroupId = _opts.Value.GroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            SecurityProtocol = _opts.Value.EnableTls ? SecurityProtocol.SaslSsl : SecurityProtocol.SaslPlaintext,
            SaslMechanism = SaslMechanism.Plain,
            SaslUsername = _opts.Value.Username,
            SaslPassword = _opts.Value.Password,
        };

        using var consumer = new ConsumerBuilder<Ignore, string>(cfg).Build();
        consumer.Subscribe(_opts.Value.Topic);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var cr = consumer.Consume(ct);
                if (string.IsNullOrWhiteSpace(cr.Message.Value)) continue;

                // TRUST sends arrays of envelopes; sometimes empty arrays
                var doc = JsonDocument.Parse(cr.Message.Value);
                if (doc.RootElement.ValueKind != JsonValueKind.Array) continue;

                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<TttDbContext>();

                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    var env = el.Deserialize<TrustEnvelope>();
                    if (env?.Header?.MsgType != "0003") continue; // movement only for now
                    
                    var body = env.Body.Deserialize<TrainMovementBody>();

                    if (body is null) continue;

                    // Fix DST quirk: these fields can be 1h ahead during BST (per wiki). You can wrap this in a helper.
                    var reportedAt = DateTimeOffset.FromUnixTimeMilliseconds(body.actual_timestamp);

                    // Idempotent insert + position upsert
                    var evt = new MovementEvent
                    {
                        TrainId = body.train_id,
                        EventType = body.event_type,
                        ActualTimestampMs = body.actual_timestamp,
                        LocStanox = body.loc_stanox,
                        Platform = body.platform,
                        VariationStatus = body.variation_status,
                        NextReportStanox = body.next_report_stanox,
                        TocId = body.toc_id
                    };

                    db.MovementEvents.Add(evt);

                    var pos = await db.CurrentPositions.FindAsync(new object[]{ body.train_id }, ct)
                              ?? new CurrentTrainPosition { TrainId = body.train_id };

                    pos.LocStanox = body.loc_stanox;
                    pos.ReportedAt = reportedAt;
                    pos.Direction = body.direction_ind;
                    pos.Line = body.line_ind;

                    db.CurrentPositions.Update(pos);
                }

                await db.SaveChangesAsync(ct);
                consumer.Commit(cr);
            }
            catch (ConsumeException ex)
            {
                _log.LogError(ex, "Kafka consume error");
            }
            catch (OperationCanceledException) { /* shutting down */ }
            catch (Exception ex)
            {
                _log.LogError(ex, "Processing error");
            }
        }
    }
}
