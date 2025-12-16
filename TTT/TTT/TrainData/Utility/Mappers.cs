using TTT.TrainData.DataSets;

namespace TTT.TrainData.Utility;

public class Mappers
{
    /// <summary>
    /// Maps TrainMovementBody timeOffset MovementEvent
    /// </summary>
    /// <param name="movementBody"></param>
    /// <returns></returns>
    internal static MovementEvent TrainMoveBodyToMoveEvent(TrainMovementBody movementBody) => new MovementEvent
    {
        // Core ids
        TrainId = movementBody.TrainId,
        LocStanox = movementBody.LocStanox,

        // Timestamps
        ActualTimestampMs  = Converters.ToLong(movementBody.ActualTimestamp),
        GbttTimestampMs    = movementBody.GbttTimestamp,
        PlannedTimestampMs = Converters.ToLong(movementBody.PlannedTimestamp),

        // Event details
        PlannedEventType = movementBody.PlannedEventType,
        EventType        = movementBody.EventType,
        EventSource      = movementBody.EventSource,

        // Flags / indicators
        CorrectionInd        = Converters.ToBool(movementBody.CorrectionInd),
        OffrouteInd          = Converters.ToBool(movementBody.OffrouteInd),
        TrainTerminated      = Converters.ToBool(movementBody.TrainTerminated),
        DelayMonitoringPoint = Converters.ToBool(movementBody.DelayMonitoringPoint),
        AutoExpected         = Converters.ToBool(movementBody.AutoExpected),

        // Direction / platform / line / route
        DirectionInd = movementBody.DirectionInd,
        Platform     = movementBody.Platform?.Trim(),
        Line         = "", // is captured elsewhere
        Route        = movementBody.Route,

        // Codes
        TrainServiceCode = movementBody.TrainServiceCode,
        DivisionCode     = movementBody.DivisionCode,
        TocId            = movementBody.TocId,

        // Performance / variation
        TimetableVariation = movementBody.TimetableVariation,
        VariationStatus    = movementBody.VariationStatus,

        // Next / reporting
        NextReportStanox   = movementBody.NextReportStanox,
        NextReportRunTime  = movementBody.NextReportRunTime,
        ReportingStanox    = movementBody.ReportingStanox
    };
}