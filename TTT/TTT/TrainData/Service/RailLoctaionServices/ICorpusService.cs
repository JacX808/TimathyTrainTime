namespace TTT.TrainData.Service.RailLoctaionServices;

public interface ICorpusService
{
    Task<Dictionary<string, string>> LoadCorpusTipLocMapAsync(string path, CancellationToken ct);
}