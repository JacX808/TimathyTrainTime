using System.Text.Json.Serialization;

namespace TTT.TrainData.DataSets;

sealed class TrustHeader
{
    [JsonPropertyName("msg_type")]
    public string MsgType { get; set; } = default!;
}