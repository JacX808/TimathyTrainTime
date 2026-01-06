using TTT.TrainData.Records;

namespace TTT.TrainData.Service.RailLoctaionServices;

public interface IPlanBService
{
    IAsyncEnumerable<BplanLocRow> ReadBplanLocAsync(string path, CancellationToken ct);
}