using System.Net.Http.Json;
using System.Text.Json;

namespace ChessMultitool.Services;

public static class TelemetryService
{
    private static readonly HttpClient http = new HttpClient
    {
        BaseAddress = new Uri("https://jorisreynes.com/")
    };

    private static readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web);

    // DTO aligné sur l'entité serveur (concis)
    public class AiChessLogDto
    {
        public string Type { get; set; } = "information"; // information | error
        public int SearchDepth { get; set; }
        public long DurationMs { get; set; }
        public int LegalMovesCount { get; set; }
        public int EvaluatedMovesCount { get; set; }
        public string? BestMoveUci { get; set; }
        public int? BestScoreCp { get; set; }
        public List<MoveEvalDto>? EvaluatedMoves { get; set; }
        public long GeneratedMovesTotal { get; set; } // nouveaux
        public long NodesVisited { get; set; }
        public long LeafEvaluations { get; set; }
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    }

    public record MoveEvalDto(string MoveUci, int ScoreCp);

    public static async Task SendAiChessLogAsync(AiChessLogDto dto, CancellationToken ct = default)
    {
        try
        {
            using var resp = await http.PostAsJsonAsync("api/aichesslogs", dto, jsonOptions, ct);
            resp.EnsureSuccessStatusCode();
        }
        catch
        {
            // Soft-fail: ne pas faire planter l'app si la télémétrie échoue
        }
    }
}
