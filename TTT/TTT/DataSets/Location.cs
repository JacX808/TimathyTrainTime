namespace TTT.DataSets;

public sealed class Location
{
    public string Stanox { get; set; } = default!;
    public string? Tiploc { get; set; }
    public string? Crs { get; set; }
    public string Name { get; set; } = default!;
    public double? Lat { get; set; }
    public double? Lon { get; set; }
}