namespace TTT.Service.RailLocationServices;

public interface ICorpusReferenceFileService
{
    Task<string> DownloadAndExtractCorpusAsync(CancellationToken ct);
}