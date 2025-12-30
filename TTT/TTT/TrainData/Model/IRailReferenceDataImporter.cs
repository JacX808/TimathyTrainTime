namespace TTT.TrainData.Model;

public interface IRailReferenceDataImporter
{
    Task<int> ImportAsync(string corpusPath, string bplanPath, CancellationToken ct);
}