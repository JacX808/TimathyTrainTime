using System.ComponentModel.DataAnnotations;

namespace TTT.DataSets.RailLocations;

public class RailLocationLite
{
    [Required, MaxLength(5)]
    public string Stanox { get; set; } = default!;
    
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}