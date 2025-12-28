using System.Text.Json.Serialization;

namespace ChessMultitool.Models;

public class MoveData
{
    [JsonPropertyName("image")]
    public string Image { get; set; }

    [JsonPropertyName("move")]
    public string Move
    {
        get; set;
    }
}