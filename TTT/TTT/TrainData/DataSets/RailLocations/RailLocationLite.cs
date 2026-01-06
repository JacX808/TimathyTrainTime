using System.ComponentModel.DataAnnotations;

namespace TTT.TrainData.DataSets.RailLocations;

public class RailLocationLite
{
    /// <summary>STANOX, kept as 5-char string (leading zeros preserved)</summary>
    [Required, MaxLength(5)]
    public string Stanox { get; set; } = default!;
    
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}