namespace TTT.Movement.ConsumerBackgroundService;

public sealed class TrainMovementBody
{
    public string event_type { get; set; } = default!;
    public string loc_stanox { get; set; } = default!;
    public string train_id { get; set; } = default!;
    public long actual_timestamp { get; set; }
    public string variation_status { get; set; } = default!;
    public string? platform { get; set; }
    public string? direction_ind { get; set; }
    public string? line_ind { get; set; }
    public string? next_report_stanox { get; set; }
    public string toc_id { get; set; } = default!;
}