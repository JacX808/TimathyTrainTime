namespace TTT.Records;

public sealed record BplanLocRow(
    string Tiploc,
    string? Name,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidTo,
    int? OsEasting,
    int? OsNorthing,
    string? StanoxRaw);
