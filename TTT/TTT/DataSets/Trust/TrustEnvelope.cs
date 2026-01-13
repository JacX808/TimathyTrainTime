using System.Text.Json;
using System.Text.Json.Serialization;

namespace TTT.DataSets.Trust;

sealed class TrustEnvelope
{
    [JsonPropertyName("header")]
    public TrustHeader Header { get; set; } = default!;
    
    [JsonPropertyName("body")]
    public JsonElement Body { get; set; }
}
