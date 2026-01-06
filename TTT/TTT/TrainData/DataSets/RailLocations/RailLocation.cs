using System.ComponentModel.DataAnnotations;

namespace TTT.TrainData.DataSets.RailLocations;

public sealed class RailLocation
{
    public int Id { get; set; }

    /// <summary>STANOX, kept as 5-char string (leading zeros preserved)</summary>
    [Required, MaxLength(5)]
    public string Stanox { get; set; } = default!;

    /// <summary>TIPLOC, up to 7 chars</summary>
    [Required, MaxLength(7)]
    public string Tiploc { get; set; } = default!;

    [MaxLength(32)]
    public string? Name { get; set; }

    public int? OsEasting { get; set; }
    public int? OsNorthing { get; set; }

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public DateTimeOffset? ValidFrom { get; set; }
    public DateTimeOffset? ValidTo { get; set; }

    [MaxLength(32)]
    public string Source { get; set; } = "BPLAN+CORPUS";

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}