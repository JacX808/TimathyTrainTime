// File: Services/IMovementsIngestionModel.cs

using System.Text.Json;

namespace TTT.Model;

public interface IMovementsIngestionModel
{
    /// <summary>
    /// Pulls TRUST movement data once (bounded by max messages / seconds) and upserts into the DB.
    /// </summary>
    internal Task<int> IntegstOnceServiceAsync(string topic, int maxMessages, int maxSeconds,
        CancellationToken cancellationToken);
}