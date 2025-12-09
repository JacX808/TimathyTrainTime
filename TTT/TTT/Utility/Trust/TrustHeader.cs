using System.Text.Json.Serialization;

namespace TTT.Utility.Trust;

sealed class TrustHeader
{
    [JsonPropertyName("msg_type")]
    public string MsgType { get; set; } = default!;
}