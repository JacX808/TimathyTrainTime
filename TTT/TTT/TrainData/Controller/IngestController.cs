using System.Diagnostics;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TTT.Database;
using TTT.Movement.ConsumerBackgroundService;
using TTT.TrainData.DataSets;

namespace TTT.TrainData.Controller;

[ApiController]
[Route("api/admin/ingest")]
public sealed class IngestController : ControllerBase
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private readonly ILogger<IngestController> _log;
    private readonly TttDbContext _db;
    private readonly IOptions<OpenRailOptions> _opts;

    public IngestController(ILogger<IngestController> log, TttDbContext db, IOptions<OpenRailOptions> opts)
        => (_log, _db, _opts) = (log, db, opts);

    /// <summary>
    /// Pulls messages from TRAIN_MVT_ALL_TOC once and stores Train IDs.
    /// </summary>
    /// <param name="maxMessages">Upper bound on messages to process (default 1000).</param>
    /// <param name="maxSeconds">Upper bound on runtime in seconds (default 15).</param>
    /// <param name="offset">"latest" (default) or "earliest".</param>
    /// <param name="ct"></param>
    [HttpGet("movements")]
    public async Task<IActionResult> IngestOnce(
        [FromQuery] int maxMessages = 1000,
        [FromQuery] int maxSeconds = 15,
        [FromQuery] string offset = "latest",
        CancellationToken ct = default)
    {
        if (!await Gate.WaitAsync(0, ct))
            return Conflict(new { message = "Ingest already running." });

        try
        {
            var cfg = BuildConsumerConfig(_opts.Value, offset);
            if (string.IsNullOrWhiteSpace(cfg.BootstrapServers))
                return BadRequest(new { message = "OpenRail:BootstrapServers not configured." });

            using var consumer = new ConsumerBuilder<Ignore, string>(cfg)
                .SetErrorHandler((_, e) => _log.LogError("Kafka error: {Reason}", e.Reason))
                .Build();

            consumer.Subscribe(_opts.Value.Topic);

            var sw = Stopwatch.StartNew();
            int read = 0, processed = 0, created = 0, batches = 0;

            while (!ct.IsCancellationRequested &&
                   read < maxMessages &&
                   sw.Elapsed < TimeSpan.FromSeconds(maxSeconds))
            {
                var cr = consumer.Consume(TimeSpan.FromMilliseconds(500));
                if (cr is null) continue;

                read++;

                if (TryParseArray(cr.Message.Value, out var array))
                {
                    foreach (var el in array) processed += await HandleEnvelope(el, ct);
                }
                else if (TryParseObject(cr.Message.Value, out var obj))
                {
                    processed += await HandleEnvelope(obj, ct);
                }

                // Flush every 250 processed to keep context small
                if (processed / 250 > batches)
                {
                    created += await _db.SaveChangesAsync(ct);
                    batches = processed / 250;
                }

                consumer.Commit(cr);
            }

            created += await _db.SaveChangesAsync(ct);

            return Ok(new
            {
                topic = _opts.Value.Topic,
                readMessages = read,
                processedEnvelopes = processed,
                dbWrites = created,
                elapsedMs = sw.ElapsedMilliseconds
            });
        }
        finally
        {
            Gate.Release();
        }
    }

    private ConsumerConfig BuildConsumerConfig(OpenRailOptions o, string offset)
    {
        var cfg = new ConsumerConfig
        {
            BootstrapServers = o.BootstrapServers,
            GroupId = $"{o.GroupId}-{Guid.NewGuid():N}",
            AutoOffsetReset = offset?.Equals("earliest", StringComparison.OrdinalIgnoreCase) == true
                ? AutoOffsetReset.Earliest
                : AutoOffsetReset.Latest,
            EnableAutoCommit = false
        };

        if (o.EnableTls && !string.IsNullOrWhiteSpace(o.Username))
        {
            cfg.SecurityProtocol = SecurityProtocol.SaslSsl;
            cfg.SaslMechanism = SaslMechanism.Plain; // change if your broker requires SCRAM
            cfg.SaslUsername = o.Username;
            cfg.SaslPassword = o.Password;
        }

        return cfg;
    }

    private static bool TryParseArray(string json, out JsonElement.ArrayEnumerator items)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            items = doc.RootElement.EnumerateArray();
            // We cannot return the enumerator from a disposed doc — so:
        }
        items = default;
        return false;
    }

    private static bool TryParseObject(string json, out JsonElement item)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind == JsonValueKind.Object)
        {
            item = doc.RootElement.Clone(); // clone for lifetime
            return true;
        }
        item = default;
        return false;
    }

    private async Task<int> HandleEnvelope(JsonElement el, CancellationToken ct)
    {
        // Clone to keep lifetime independent of the outer document
        var cloned = el.ValueKind == JsonValueKind.Object ? el.Clone() : default;

        var env = cloned.Deserialize<TrustEnvelope>();
        if (env is null) return 0;

        switch (env.Header?.MsgType)
        {
            case "0001": // activation -> authoritative TrainId
                {
                    var b = env.Body.Deserialize<TrainActivationBody>();
                    if (b is null) return 0;

                    var existing = await _db.TrainRuns.AsTracking()
                        .SingleOrDefaultAsync(x => x.TrainId == b.Train_id, ct);

                    if (existing is null)
                    {
                        _db.TrainRuns.Add(new TttDbContext.TrainRun
                        {
                            TrainId = b.Train_id,
                            FirstSeenUtc = DateTimeOffset.UtcNow
                        });
                    }
                    else
                    {
                        existing.FirstSeenUtc = existing.FirstSeenUtc == default
                            ? DateTimeOffset.UtcNow
                            : existing.FirstSeenUtc;
                        _db.TrainRuns.Update(existing);
                    }
                    return 1;
                }

            case "0003": // movement -> also includes train_id (useful if activation was missed)
                {
                    var b = env.Body.Deserialize<TrainMovementBody>();
                    if (b is null) return 0;

                    var existing = await _db.TrainRuns.AsTracking()
                        .SingleOrDefaultAsync(x => x.TrainId == b.train_id, ct);

                    if (existing is null)
                    {
                        _db.TrainRuns.Add(new TttDbContext.TrainRun
                        {
                            TrainId = b.train_id,
                            FirstSeenUtc = DateTimeOffset.UtcNow
                        });
                    }
                    else
                    {
                        // touch it so LastSeen could be added later if you track it
                    }
                    return 1;
                }

            default:
                return 0; // ignore other types for now
        }
    }
}
