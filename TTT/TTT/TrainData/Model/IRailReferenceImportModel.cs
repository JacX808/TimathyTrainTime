namespace TTT.TrainData.Model;

public interface IRailReferenceImportModel
{
    Task<int> ImportRailAsync(CancellationToken cancellationToken);
    Task<bool> RunCorpusCheckAsync(CancellationToken ct);
}