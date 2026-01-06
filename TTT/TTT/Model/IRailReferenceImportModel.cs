using TTT.DataSets.RailLocations;

namespace TTT.Model;

public interface IRailReferenceImportModel
{
    Task<int> ImportRailAsync(CancellationToken cancellationToken);
    Task<bool> RunCorpusCheckAsync(CancellationToken ct);
    Task<List<RailLocationLite>>  GetAllRailLocationLiteAsync(CancellationToken cancellationToken);
    Task<RailLocation>  GetRailLocationAsync(string stanox, CancellationToken cancellationToken);
}