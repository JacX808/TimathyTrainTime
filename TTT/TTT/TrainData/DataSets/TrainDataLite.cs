namespace TTT.TrainData.DataSets;

public class TrainDataLite
{
    /// <summary>
    /// Train id
    /// </summary>
    public string TrainId { get; set; } = null!;
    
    /// <summary>
    /// Train last seen checkpoint
    /// </summary>
    public string LocStanox { get; set; } = null!;

    /// <summary>
    /// Direction the train is going
    /// </summary>
    public string? Direction { get; set; } = null!;
}