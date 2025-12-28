using System.Text.Json.Serialization;

namespace ChessMultitool.Models
{
    public class TrapsData
    {
        [JsonPropertyName("traps")]
        public Dictionary<string, Trap> Traps { get; set; }
    }
}
