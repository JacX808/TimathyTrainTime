using System.Text.Json;
using Apache.NMS;
using Apache.NMS.ActiveMQ;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TTT.Database;
using TTT.OpenRail;
using TTT.TrainData.Controller;
using TTT.TrainData.DataSets;
using TTT.TrainData.Model;
using TTT.TrainData.Utility;

namespace TTT.TrainData.Services;

/// <summary>
/// 
/// </summary>
public sealed class MovementsIngestionService : BackgroundService, IMovementsIngestionService
{
    private readonly ILogger<MovementsIngestionService> _log;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly NetRailOptions _options;
    private readonly ITrainDataModel _trainDataModel;

    /// <summary>
    /// Add database connection for endpoints to add data from National Rail data
    /// </summary>
    /// <param name="options"></param>
    /// <param name="scopeFactory"></param>
    /// <param name="log"></param>
    public MovementsIngestionService(
        IOptions<NetRailOptions> options,
        IServiceScopeFactory scopeFactory,
        ILogger<MovementsIngestionService> log)
    {
        _options = options.Value;
        _scopeFactory = scopeFactory;
        _log = log;
        
        var scope = _scopeFactory.CreateScope();
        _trainDataModel = scope.ServiceProvider.GetRequiredService<ITrainDataModel>();

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
                catch (Exception e)
                {
                    // ignored
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
        var factory = new ConnectionFactory(_options.ConnectUrl);
        var conn = string.IsNullOrWhiteSpace(_options.Username)
            ? await factory.CreateConnectionAsync()
            : await factory.CreateConnectionAsync(_options.Username, _options.Password);

        if (_options.UseDurableSubscription)
            conn.ClientId = _options.ClientId;

        await conn.StartAsync();
        var session = await conn.CreateSessionAsync(AcknowledgementMode.AutoAcknowledge);

        var consumers = new List<IMessageConsumer>();
        
        foreach (var topic in _options.Topics)
        {
            // TODO - Need better solution
            if (consumers.Count < 1)
            {
                var dest = await session.GetTopicAsync(topic);

                var consumer = _options.UseDurableSubscription
                    ? await session.CreateDurableConsumerAsync(dest, $"{_options.ClientId}-{topic}", null, false)
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
            var doc = JsonDocument.Parse(text.Text);
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
            if (env is null || env.Header?.MsgType is null) return;

            switch (env.Header.MsgType)
            {
                case "0001": // Activation
                {
                    var b = env.Body.Deserialize<TrainActivationBody>();
                    if (b is null) return;
                    _ = UpsertTrainRun(b);
                    break;
                }
                case "0003": // Movement
                {
                    var b = env.Body.Deserialize<TrainMovementBody>();
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
        var scope = _scopeFactory.CreateScope();
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
        var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TttDbContext>();

        // History (idempotent by unique index)
        var movementEvent = Mappers.TrainMoveBodyToMoveEvent(trainMovementBody);
        db.MovementEvents.Add(movementEvent);

        // Latest position (upsert)
        var pos = await db.CurrentTrainPosition.FindAsync(trainMovementBody.TrainId) ??
                  new CurrentTrainPosition { TrainId = trainMovementBody.TrainId };

        pos.LocStanox = trainMovementBody.LocStanox;
        pos.ReportedAt = FixDst(trainMovementBody.ActualTimestamp);
        pos.Direction = trainMovementBody.DirectionInd;
        pos.Line = "";
        pos.VariationStatus = trainMovementBody.VariationStatus;

        db.CurrentTrainPosition.Update(pos);

        // TrainRun touch (in case activation was missed)
        var run = await db.TrainRuns.FindAsync(trainMovementBody.TrainId);
        if (run is null)
            db.TrainRuns.Add(new TrainRun
            {
                TrainId = trainMovementBody.TrainId, TocId = trainMovementBody.TocId,
                FirstSeenUtc = DateTimeOffset.UtcNow,
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

    public async Task<int> HandleEnvelopeAsync(JsonElement element, CancellationToken cancellationToken)
    {
        var envelope = element.Deserialize<TrustEnvelope>();

        if (envelope?.Header.MsgType is null)
            return 0;
        
        switch (envelope.Header.MsgType)
        {
            case "0001": // Activation
            {
                var activationBody = envelope.Body.Deserialize<TrainActivationBody>();
                if (activationBody is null) return 0;

                var now = DateTimeOffset.UtcNow;
                var run = await _trainDataModel.FindTrainRunAsync(activationBody.TrainId, cancellationToken);
                if (run is null)
                {
                    run = new TrainRun
                    {
                        TrainId = activationBody.TrainId,
                        TrainUid = activationBody.TrainUid,
                        TocId = activationBody.TocId,
                        ServiceDate = ServiceDateFromActivation(activationBody),
                        FirstSeenUtc = now,
                        LastSeenUtc = now
                    };
                    await _trainDataModel.AddTrainRunAsync(run, cancellationToken);
                }
                else
                {
                    run.TrainUid ??= activationBody.TrainUid;
                    run.TocId ??= activationBody.TocId;
                    run.ServiceDate ??= ServiceDateFromActivation(activationBody);
                    run.LastSeenUtc = now;
                }

                return 1;
            }

            case "0003": // Movement
            {
                var movementBody = envelope.Body.Deserialize<TrainMovementBody>();
                if (movementBody is null) 
                    return 0;

                // History (unique key protects against duplicates)
                var movementEvent = Mappers.TrainMoveBodyToMoveEvent(movementBody);
                await _trainDataModel.AddMovementEventAsync(movementEvent, cancellationToken);

                // Latest position snapshot
                var position = await _trainDataModel.FindCurrentPositionAsync(movementBody.TrainId, cancellationToken)
                               ?? new CurrentTrainPosition { TrainId = movementBody.TrainId };

                position.LocStanox = movementBody.LocStanox;
                position.ReportedAt = FixDst(movementBody.ActualTimestamp);
                position.Direction = movementBody.DirectionInd;
                position.Line = null; // movementBody.;
                position.VariationStatus = movementBody.VariationStatus;

                await _trainDataModel.UpsertCurrentPositionAsync(position, cancellationToken);

                // Touch TrainRun (in case activation missed)
                var run = await _trainDataModel.FindTrainRunAsync(movementBody.TrainId, cancellationToken);
                if (run is null)
                {
                    await _trainDataModel.AddTrainRunAsync(new TrainRun
                    {
                        TrainId = movementBody.TrainId,
                        TocId = movementBody.TocId,
                        FirstSeenUtc = DateTimeOffset.UtcNow,
                        LastSeenUtc = DateTimeOffset.UtcNow
                    }, cancellationToken);
                }
                else
                {
                    run.LastSeenUtc = DateTimeOffset.UtcNow;
                }

                // Ignore duplicate exceptions (at-least-once)
                try
                {
                    await _trainDataModel.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException)
                {
                    /* dup */
                }

                return 1;
            }
        }

        return 0;
    }

    public async Task IntegstOnceServiceAsync(string topic, int maxMessages, int maxSeconds,
        CancellationToken cancellationToken)
    {
        try
        {
            var startNew = System.Diagnostics.Stopwatch.StartNew();

            // OpenWire connection (use provider factory; no "activemq:" prefix)
            var factory = new ConnectionFactory(_options.ConnectUrl);
            var connectionAsync = string.IsNullOrWhiteSpace(_options.Username)
                ? await factory.CreateConnectionAsync()
                : await factory.CreateConnectionAsync(_options.Username, _options.Password);

            if (_options.UseDurableSubscription)
                connectionAsync.ClientId = _options.ClientId ?? "ttt-nrod-client";

            connectionAsync.Start();
            var session = connectionAsync.CreateSession(AcknowledgementMode.AutoAcknowledge);
            var dest = session.GetTopic(topic);

            var consumer = _options.UseDurableSubscription
                ? await session.CreateDurableConsumerAsync(dest, $"{connectionAsync.ClientId}-{topic}", null, false)
                : await session.CreateConsumerAsync(dest);

            int read = 0, processed = 0, saved = 0;

            while (!cancellationToken.IsCancellationRequested && read < maxMessages &&
                   startNew.Elapsed < TimeSpan.FromSeconds(maxSeconds))
            {
                var message = consumer.Receive(TimeSpan.FromMilliseconds(500)) as ITextMessage;
                if (message is null) continue;

                read++;

                try
                {
                    var doc = JsonDocument.Parse(message.Text);
                    var root = doc.RootElement;

                    if (root.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var element in root.EnumerateArray())
                            processed += await HandleEnvelopeAsync(element.Clone(), cancellationToken);
                    }
                    else if (root.ValueKind == JsonValueKind.Object)
                    {
                        processed += await HandleEnvelopeAsync(root.Clone(), cancellationToken);
                    }
                }
                catch (Exception ex) // TODO add custom exception
                {
                    _log.LogError(ex, "Failed to process message payload.");
                    // TODO add error 500 
                }
            }
            
            _log.LogInformation($"Database has been updated at {DateTime.Now}");
        }
        catch (Exception exception) // TODO add custom exception
        {
            _log.LogError($"Failed to process message payload: {exception.StackTrace}");
        }
    }
}

