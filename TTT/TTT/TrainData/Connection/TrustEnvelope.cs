using System.Text.Json;

namespace TTT.Movement.ConsumerBackgroundService;

public sealed class TrustEnvelope
{
    public TrustHeader Header { get; init; } = null!;
    public JsonElement Body { get; init; }
}