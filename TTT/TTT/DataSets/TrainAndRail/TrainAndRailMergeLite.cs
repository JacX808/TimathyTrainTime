using System.ComponentModel.DataAnnotations;

namespace TTT.DataSets.TrainAndRail;

public class TrainAndRailMergeLite
{
    public int Id { get; set; }
    
    [MaxLength(32)]
    public string TrainId { get; set; } = default!;
    
    [MaxLength(5)]
    public string LocStanox { get; set; } = default!;
    public DateTimeOffset ReportedAt { get; set; }
    
    [MaxLength(4)]
    public string? NextLocStanox { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}