using System.Text.Json.Serialization;

namespace TTT.DataSets.Trust;

sealed class TrustHeader
{
    [JsonPropertyName("msg_type")]
    public string MsgType { get; set; } = default!;
}