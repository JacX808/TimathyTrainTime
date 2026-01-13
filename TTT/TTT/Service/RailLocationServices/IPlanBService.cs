using TTT.Records;

namespace TTT.Service.RailLocationServices;

public interface IPlanBService
{
    IAsyncEnumerable<BplanLocRow> ReadBplanLocAsync(string path, CancellationToken ct);
}