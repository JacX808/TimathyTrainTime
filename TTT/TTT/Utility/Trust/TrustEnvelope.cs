using System.Text.Json;
using TTT.DataSets;

namespace TTT.Utility.Trust;

sealed class TrustEnvelope
{
    public TrustHeader header { get; set; } = default!;
    public JsonElement body { get; set; }
}
