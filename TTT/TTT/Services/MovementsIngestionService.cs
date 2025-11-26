using System.Text.Json;
using Apache.NMS;
using Apache.NMS.ActiveMQ;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TTT.Database;
using TTT.DataSets;
using TTT.OpenRail;
using TTT.Utility.Converters;
using TTT.Utility.Trust;

namespace TTT.Services;

/// <summary>
/// 
/// </summary>
public sealed class MovementsIngestionService : BackgroundService
{
    private readonly ILogger<MovementsIngestionService> _log;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly NetRailOptions _opts;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="log"></param>
    /// <param name="scopeFactory"></param>
    /// <param name="opts"></param>
    public MovementsIngestionService(
        ILogger<MovementsIngestionService> log,
        IServiceScopeFactory scopeFactory,
        IOptions<NetRailOptions> opts)
    {
        _log = log;
        _scopeFactory = scopeFactory;
        _opts = opts.Value;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="stoppingToken"></param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("MovementsIngestionService starting.");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOpenWireLoop(stoppingToken);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Ingestion crashed; retrying in 2s");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                }
                catch
                {
                }
            }
        }

        _log.LogInformation("MovementsIngestionService stopped.");
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="cancellationToken"></param>
    private async Task RunOpenWireLoop(CancellationToken cancellationToken)
    {
        // Use provider factory with OpenWire tcp:// URI (no "activemq:" prefix)
        var factory = new ConnectionFactory(_opts.ConnectUrl);
        using var conn = string.IsNullOrWhiteSpace(_opts.Username)
            ? await factory.CreateConnectionAsync()
            : await factory.CreateConnectionAsync(_opts.Username, _opts.Password);

        if (_opts.UseDurableSubscription)
            conn.ClientId = _opts.ClientId;

        await conn.StartAsync();
        using var session = await conn.CreateSessionAsync(AcknowledgementMode.AutoAcknowledge);

        var consumers = new List<IMessageConsumer>();
        foreach (var topic in _opts.Topics)
        {
            // TODO - Need better solution
            if (consumers.Count < 1)
            {
                var dest = await session.GetTopicAsync(topic);
        
                var consumer = _opts.UseDurableSubscription
                    ? await session.CreateDurableConsumerAsync(dest, $"{_opts.ClientId}-{topic}", null, false)
                    : await session.CreateConsumerAsync(dest);

                consumer.Listener += HandleMessage; // fire-and-forget
                consumers.Add(consumer);

            }
        }

        // Keep the connection alive until cancellation
        while (!cancellationToken.IsCancellationRequested)
            await Task.Delay(500, cancellationToken);

        // disposal of consumers/session/connection happens via using{}
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="msg"></param>
    private void HandleMessage(IMessage msg)
    {
        if (msg is not ITextMessage text || string.IsNullOrWhiteSpace(text.Text))
            return;

        try
        {
            using var doc = JsonDocument.Parse(text.Text);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
                foreach (var el in root.EnumerateArray())
                    ProcessEnvelope(el.Clone());
            else if (root.ValueKind == JsonValueKind.Object)
                ProcessEnvelope(root.Clone());
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to parse TRUST payload");
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="el"></param>
    private void ProcessEnvelope(JsonElement el)
    {
        try
        {
            var env = el.Deserialize<TrustEnvelope>();
            if (env is null || env.header?.MsgType is null) return;

            switch (env.header.MsgType)
            {
                case "0001": // Activation
                {
                    var b = env.body.Deserialize<TrainActivationBody>();
                    if (b is null) return;
                    _ = UpsertTrainRun(b);
                    break;
                }
                case "0003": // Movement
                {
                    var b = env.body.Deserialize<TrainMovementBody>();
                    if (b is null) return;
                    _ = UpsertMovement(b);
                    break;
                }
                default:
                    break;
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "ProcessEnvelope error");
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="a"></param>
    private async Task UpsertTrainRun(TrainActivationBody a)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TttDbContext>();

        var now = DateTimeOffset.UtcNow;
        var run = await db.TrainRuns.FindAsync(a.TrainId);
        if (run is null)
        {
            run = new TrainRun
            {
                TrainId = a.TrainId,
                TrainUid = a.TrainUid,
                TocId = a.TocId,
                ServiceDate = ServiceDateFromActivation(a),
                FirstSeenUtc = now,
                LastSeenUtc = now
            };
            db.TrainRuns.Add(run);
        }
        else
        {
            run.TrainUid ??= a.TrainUid;
            run.TocId ??= a.TocId;
            run.ServiceDate ??= ServiceDateFromActivation(a);
            run.LastSeenUtc = now;
        }

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="trainMovementBody"></param>
    private async Task UpsertMovement(TrainMovementBody trainMovementBody)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TttDbContext>();

        // History (idempotent by unique index)
        var movementEvent = Entity.ToEntity(trainMovementBody);
        db.MovementEvents.Add(movementEvent);

        // Latest position (upsert)
        var pos = await db.CurrentPositions.FindAsync(trainMovementBody.TrainId) ??
                  new CurrentTrainPosition { TrainId = trainMovementBody.TrainId };

        pos.LocStanox = trainMovementBody.LocStanox;
        pos.ReportedAt = FixDst(trainMovementBody.ActualTimestamp);
        pos.Direction = trainMovementBody.DirectionInd;
        pos.Line = "";
        pos.VariationStatus = trainMovementBody.VariationStatus;

        db.CurrentPositions.Update(pos);

        // TrainRun touch (in case activation was missed)
        var run = await db.TrainRuns.FindAsync(trainMovementBody.TrainId);
        if (run is null)
            db.TrainRuns.Add(new TrainRun
            {
                TrainId = trainMovementBody.TrainId, TocId = trainMovementBody.TocId, FirstSeenUtc = DateTimeOffset.UtcNow,
                LastSeenUtc = DateTimeOffset.UtcNow
            });
        else
            run.LastSeenUtc = DateTimeOffset.UtcNow;

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            /* duplicate event -> ignore */
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="a"></param>
    /// <returns></returns>
    private static DateOnly? ServiceDateFromActivation(TrainActivationBody a)
    {
        if (a.OriginDepTimestamp > 0)
            return DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeMilliseconds(a.OriginDepTimestamp).UtcDateTime
                .Date);
        if (!string.IsNullOrWhiteSpace(a.TpOriginTimestamp) && DateOnly.TryParse(a.TpOriginTimestamp, out var d))
            return d;
        return null;
    }

    // TODO: Fix IsDaylightSavingTime
    private static DateTimeOffset FixDst(long epochMs)
    {
        var t = DateTimeOffset.FromUnixTimeMilliseconds(epochMs);
        var tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
        var local = TimeZoneInfo.ConvertTime(t, tz);
        return t; //TimeZoneInfo.IsDaylightSavingTime(local) ? t.AddHours(-1) : t;
    }
}
