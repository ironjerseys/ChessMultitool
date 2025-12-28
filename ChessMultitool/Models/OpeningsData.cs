using System.Text.Json.Serialization;

namespace ChessMultitool.Models
{
    public class OpeningsData
    {
        [JsonPropertyName("openings")]
        public Dictionary<string, Opening> Openings { get; set; }
    }
}
