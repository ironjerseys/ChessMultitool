#if DEBUG
namespace ChessMultitool.Services;

public sealed class AiDebugTraceEntry
{
    public DateTime Utc { get; set; } = DateTime.UtcNow;

    // Pour retrouver la position
    public string? StateString { get; set; } // ou FEN si tu en as un

    public long DurationMs { get; set; }
    public int LegalRootCount { get; set; }

    public string? BestMoveUci { get; set; }
    public int? BestScoreCp { get; set; }

    public long GeneratedMovesTotal { get; set; }
    public long NodesVisited { get; set; }
    public long LeafEvaluations { get; set; }

    // Top N coups racine
    public List<(string MoveUci, int ScoreCp)>? TopRootMoves { get; set; }

    // Diagnostic “dame pendue”
    public bool? AnyImmediateCaptureOnToSquare { get; set; }
    public bool? PawnCapturesQueen { get; set; }
    public List<string>? ImmediateCapturesUci { get; set; }
}
#endif
