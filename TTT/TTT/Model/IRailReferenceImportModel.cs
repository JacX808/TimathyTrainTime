using TTT.DataSets.RailLocations;

namespace TTT.Model;

public interface IRailReferenceImportModel
{
    Task<int> ImportAllRailAsync(CancellationToken cancellationToken);
    Task<int> ImportRailLocationAsync(CancellationToken cancellationToken);
    Task<int> ImportRailLocationLiteAsync(CancellationToken cancellationToken);
    Task<bool> CorpusCheckAsync(CancellationToken ct);
    Task<List<RailLocationLiteConverted>>  GetAllRailLocationLiteAsync(CancellationToken cancellationToken);
    Task<RailLocations>  GetRailLocationAsync(string stanox, CancellationToken cancellationToken);
}