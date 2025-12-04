using TTT.DataSets;

namespace TTT.Utility.Converters;

public class Entity
{
    internal static MovementEvent ToEntity(TrainMovementBody m) => new MovementEvent
    {
        // Core ids
        TrainId = m.TrainId,
        LocStanox = m.LocStanox,

        // Timestamps
        ActualTimestampMs  = Converters.ToLong(m.ActualTimestamp),
        GbttTimestampMs    = m.GbttTimestamp,
        PlannedTimestampMs = Converters.ToLong(m.PlannedTimestamp),

        // Event details
        PlannedEventType = m.PlannedEventType ?? m.EventType,
        EventType        = m.EventType,
        EventSource      = m.EventSource,

        // Flags / indicators
        CorrectionInd        = Converters.ToBool(m.CorrectionInd),
        OffrouteInd          = Converters.ToBool(m.OffrouteInd),
        TrainTerminated      = Converters.ToBool(m.TrainTerminated),
        DelayMonitoringPoint = Converters.ToBool(m.DelayMonitoringPoint),
        AutoExpected         = Converters.ToBool(m.AutoExpected),

        // Direction / platform / line / route
        DirectionInd = m.DirectionInd,
        Platform     = m.Platform?.Trim(),
        Line         = "", // is captured elsewhere
        Route        = m.Route,

        // Codes
        TrainServiceCode = m.TrainServiceCode,
        DivisionCode     = m.DivisionCode,
        TocId            = m.TocId,

        // Performance / variation
        TimetableVariation = m.TimetableVariation,
        VariationStatus    = m.VariationStatus,

        // Next / reporting
        NextReportStanox   = m.NextReportStanox,
        NextReportRunTime  = m.NextReportRunTime,
        ReportingStanox    = m.ReportingStanox
    };
}