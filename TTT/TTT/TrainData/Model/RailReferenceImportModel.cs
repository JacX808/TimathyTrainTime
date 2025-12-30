using Microsoft.Extensions.Options;
using TTT.TrainData.DataSets.Options;
using TTT.TrainData.Exceptions;

namespace TTT.TrainData.Model;

public sealed class RailReferenceImportModel(RailReferenceDataImporter railReferenceDataImporter,
    IOptions<RailReferenceImportOptions> opts,
    ILogger<RailReferenceImportModel> log) : IRailReferenceImportModel
{
    public async Task<bool> ImportRailAsync(CancellationToken cancellationToken)
    {
        var optsValue = opts.Value;
        
        try
        {
            log.LogInformation("Auto-importing rail reference data...");
            await railReferenceDataImporter.ImportAsync(optsValue.CorpusPath, optsValue.BplanPath, cancellationToken);
        }
        catch (RailReferenceImportException referenceImportException)
        {
            log.LogError(referenceImportException,
                "Error importing rail reference data. CorpusPath={CorpusPath}, BplanPath={BplanPath}",
                optsValue.CorpusPath,
                optsValue.BplanPath);
            
            return false;
        }
        
        log.LogInformation("Rail reference data imported.");
        return true;
    }
    
}
