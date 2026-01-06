namespace TTT.TrainData.Records;

// ---------------- BPLAN LOC ----------------

public sealed record BplanLocRow(
    string Tiploc,
    string? Name,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidTo,
    int? OsEasting,
    int? OsNorthing,
    string? StanoxRaw);
