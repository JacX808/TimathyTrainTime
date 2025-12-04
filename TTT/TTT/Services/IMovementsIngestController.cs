// File: Services/IMovementsIngestionService.cs
using System.Text.Json;

namespace TTT.Services;

public interface IMovementsIngestionService
{
    /// <summary>
    /// Pulls TRUST movement data once (bounded by max messages / seconds) and upserts into the DB.
    /// </summary>
    internal Task IntegstOnceServiceAsync(string topic, int maxMessages, int maxSeconds,
        CancellationToken cancellationToken);

    /// <summary>
    /// Processes a single TRUST envelope (already parsed JSON object/element).
    /// Returns 1 if processed, 0 otherwise.
    /// </summary>
    Task<int> HandleEnvelopeAsync(JsonElement element, CancellationToken cancellationToken);
}