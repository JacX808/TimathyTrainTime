using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;
using ProjNet.IO.CoordinateSystems;
using TTT.DataSets;
using TTT.DataSets.Train;

namespace TTT.Utility;

public abstract class Mappers
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
    
    internal static class OsgbToWgs84
    {
        // EPSG:27700 WKT (OSGB36 / British National Grid) with TOWGS84 parameters.
        private const string Epsg27700Wkt =
            "PROJCS[\"OSGB 1936 / British National Grid\",GEOGCS[\"OSGB 1936\",DATUM[\"OSGB_1936\",SPHEROID[\"Airy 1830\",6377563.396,299.3249646],TOWGS84[446.448,-125.157,542.06,0.15,0.247,0.842,-20.489]],PRIMEM[\"Greenwich\",0],UNIT[\"degree\",0.0174532925199433]],PROJECTION[\"Transverse_Mercator\"],PARAMETER[\"latitude_of_origin\",49],PARAMETER[\"central_meridian\",-2],PARAMETER[\"scale_factor\",0.9996012717],PARAMETER[\"false_easting\",400000],PARAMETER[\"false_northing\",-100000],UNIT[\"metre\",1],AXIS[\"Easting\",EAST],AXIS[\"Northing\",NORTH]]";

        // EPSG:4326 WKT (WGS84)
        private const string Epsg4326Wkt =
            "GEOGCS[\"WGS 84\",DATUM[\"WGS_1984\",SPHEROID[\"WGS 84\",6378137,298.257223563]],PRIMEM[\"Greenwich\",0],UNIT[\"degree\",0.0174532925199433]]";

        private static readonly Lazy<ICoordinateTransformation> Transform = new(() =>
        {
            var osgb = CoordinateSystemWktReader.Parse(Epsg27700Wkt);
            var wgs84 = CoordinateSystemWktReader.Parse(Epsg4326Wkt);
            var factory = new CoordinateTransformationFactory();
            return factory.CreateFromCoordinateSystems((CoordinateSystem)osgb, (CoordinateSystem)wgs84);
        });

        /// <summary>
        /// Returns (lat, lon). ProjNet outputs geographic coordinates as (lon, lat).
        /// </summary>
        public static (double lat, double lon) Convert(int easting, int northing)
        {
            var xy = new[] { (double)easting, (double)northing };
            var lonLat = Transform.Value.MathTransform.Transform(xy);
            var lon = lonLat[0];
            var lat = lonLat[1];
            return (lat, lon);
        }
    }
}