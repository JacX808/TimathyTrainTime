namespace TTT.TrainData.Model;

public interface IRailReferenceImportModel
{
    Task<bool> ImportRailAsync(CancellationToken cancellationToken);
}