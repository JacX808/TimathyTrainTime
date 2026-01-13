namespace TTT.Service.RailLocationServices;

public interface ICorpusService
{
    Task<Dictionary<string, string>> LoadCorpusTipLocMapAsync(string path, CancellationToken ct);
}