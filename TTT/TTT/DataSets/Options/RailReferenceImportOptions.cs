namespace TTT.DataSets.Options;

public sealed class RailReferenceImportOptions
{
    public string CorpusPath { get; set; } = default!;
    public string BplanPath { get; set; } = default!;
    public bool AutoImportOnStartup { get; set; } = false;
}