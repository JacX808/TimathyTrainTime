using System.Text.Json;
using Apache.NMS;
using Apache.NMS.ActiveMQ;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TTT.DataSets.Options;
using TTT.DataSets.Train;
using TTT.DataSets.Trust;
using TTT.Utility;

namespace TTT.Model;

/// <summary>
/// 
/// </summary>
public sealed class MovementsIngestionModel(IOptions<NetRailOptions> options, ITrainDataModel trainDataModel,
    ILogger<MovementsIngestionModel> log, IMinimumTrainDataModel trainMinimumDataModel,
    ITrainAndRailMergeModel trainAndRailMergeModel) : IMovementsIngestionModel
{
    
    private bool ConnectedToNationalRail { get; set; } = false;
    private IMessageConsumer? _consumer;

    private async Task<bool> StartNationRailConnection(string topic)
    {
        try
        {
            var factory = new ConnectionFactory(options.Value.ConnectUrl);
            var connectionAsync = string.IsNullOrWhiteSpace(options.Value.Username)
                ? await factory.CreateConnectionAsync()
                : await factory.CreateConnectionAsync(options.Value.Username, options.Value.Password);

            if (options.Value.UseDurableSubscription)
                connectionAsync.ClientId = options.Value.ClientId;

            await connectionAsync.StartAsync();
            
            var session = await connectionAsync?.CreateSessionAsync(AcknowledgementMode.AutoAcknowledge)!; 
            var dest = await session.GetTopicAsync(topic);
            
            _consumer = options.Value.UseDurableSubscription
                ? await session?.CreateDurableConsumerAsync(dest, $"{connectionAsync?.ClientId}-{topic}",
                    null, false)! : await session?.CreateConsumerAsync(dest)!;
            
            ConnectedToNationalRail = true;
        }
        catch (Exception e)
        {
            log.LogError("Connection to National rail failed");
            return false;
        }
        
        return true;
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
    
    /// <summary>
    /// Used to get all Activation and MovementEvents data from National Rail
    /// </summary>
    /// <param name="element"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private async Task<int> HandleEnvelopeAsync(JsonElement element, CancellationToken cancellationToken)
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
                var run = await trainDataModel.FindTrainRunAsync(activationBody.TrainId, cancellationToken);
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
                    await trainDataModel.AddTrainRunAsync(run, cancellationToken);
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
                await trainDataModel.AddMovementEventAsync(movementEvent, cancellationToken);

                // Latest position snapshot
                var position = await trainDataModel.FindCurrentPositionAsync(movementBody.TrainId, cancellationToken)
                               ?? new CurrentTrainPosition { TrainId = movementBody.TrainId };

                position.LocStanox = movementBody.LocStanox;
                position.ReportedAt = FixDst(movementBody.ActualTimestamp);
                position.Direction = movementBody.DirectionInd;
                position.Line = null; // movementBody.;
                position.VariationStatus = movementBody.VariationStatus;

                await trainDataModel.UpsertCurrentPositionAsync(position, cancellationToken);

                // Touch TrainRun (in case activation missed)
                var run = await trainDataModel.FindTrainRunAsync(movementBody.TrainId, cancellationToken);
                if (run is null)
                {
                    await trainDataModel.AddTrainRunAsync(new TrainRun
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
                    await trainDataModel.SaveChangesAsync(cancellationToken);
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
    
    /// <summary>
    /// Used to get only the necessary TrainData from a movement Event.
    /// </summary>
    /// <param name="element"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private async Task<int> HandleEnvelopeLiteAsync(JsonElement element, CancellationToken cancellationToken)
    {
        var envelope = element.Deserialize<TrustEnvelope>();

        if (envelope?.Header.MsgType is null)
            return 0;
        
        switch (envelope.Header.MsgType)
        {
            case "0001": // Activation
            {
                // to be done if needed
                return 0;
            }

            case "0003": // Movement
            {
                var movementBody = envelope.Body.Deserialize<TrainMovementBody>();
                if (movementBody is null) 
                    return 0;

                await trainMinimumDataModel.AddMinimumTrainDataAsync(new TrainMinimumData
                {
                    TrainId = movementBody.TrainId,
                    LocStanox = movementBody.LocStanox,
                    NextLocStanox = movementBody.NextReportStanox ?? "N/A",
                    VariationStatus = movementBody.VariationStatus,
                    LastSeenUtc = DateTimeOffset.UtcNow
                },cancellationToken);

                // Ignore duplicate exceptions (at-least-once)
                try
                {
                    await trainMinimumDataModel.SaveChangesAsync(cancellationToken);
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

    public async Task<int> IntegstOnceServiceAsync(string topic, int maxMessages, int maxSeconds,
        CancellationToken cancellationToken)
    {
        try
        {
            var startNew = System.Diagnostics.Stopwatch.StartNew();

            if (!ConnectedToNationalRail)
            {
                await StartNationRailConnection(topic);
            }
            

            int read = 0, processed = 0;

            while (!cancellationToken.IsCancellationRequested && read < maxMessages &&
                   startNew.Elapsed < TimeSpan.FromSeconds(maxSeconds))
            {
                var message = await _consumer.ReceiveAsync(TimeSpan.FromMilliseconds(500)) as ITextMessage;
                if (message is null) continue;

                read++;

                try
                {
                    var doc = JsonDocument.Parse(message.Text);
                    var root = doc.RootElement;

                    if (root.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var element in root.EnumerateArray())
                            processed += await HandleEnvelopeLiteAsync(element.Clone(), cancellationToken);
                    }
                    else if (root.ValueKind == JsonValueKind.Object)
                    {
                        processed += await HandleEnvelopeLiteAsync(root.Clone(), cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    log.LogError(ex, "Failed timeOffset process message payload.");
                    return processed;
                }
            }

            log.LogInformation($"Database has been updated at {DateTime.Now}. Processed: {processed}.");

            log.LogInformation("Started MergeTrainAndRailData");
            await trainAndRailMergeModel.MergeTrainAndRailDataAsync(cancellationToken);
            
            return processed;
        }
        catch (Exception exception) // TODO add custom exception
        {
            log.LogError($"Failed timeOffset process message payload: {exception.StackTrace}");
            return 0;
        }
    }
}

