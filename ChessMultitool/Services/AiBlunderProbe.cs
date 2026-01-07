#if DEBUG
using ChessLogic;

namespace ChessMultitool.Services;

public static class AiBlunderProbe
{
    public sealed class BlunderProbeResult
    {
        public bool AnyImmediateCaptureOnToSquare { get; set; }
        public bool PawnCapturesQueen { get; set; }
        public List<string> CapturesUci { get; set; } = new();
    }

    private static bool SameSquare(Position a, Position b)
        => a.Row == b.Row && a.Column == b.Column;

    public static BlunderProbeResult AnalyzeAfterBestMove(GameState stateBefore, Move bestMove)
    {
        // On note la pièce qui bouge (avant de jouer)
        var movedPiece = stateBefore.Board[bestMove.FromPos];

        // IMPORTANT: on analyse sur une copie (debug only), pour ne pas toucher la partie réelle
        var copy = new GameState(stateBefore.CurrentPlayer, stateBefore.Board.Copy());
        copy.MakeMove(bestMove); // après ça, CurrentPlayer est l'adversaire

        var replies = copy.AllLegalMovesFor(copy.CurrentPlayer).ToList();

        var captureReplies = replies.Where(r => SameSquare(r.ToPos, bestMove.ToPos)).ToList();

        var result = new BlunderProbeResult
        {
            AnyImmediateCaptureOnToSquare = captureReplies.Count > 0,
        };

        foreach (var r in captureReplies)
            result.CapturesUci.Add(ToUci(r));

        if (movedPiece != null && movedPiece.Type == PieceType.Queen)
        {
            result.PawnCapturesQueen = captureReplies.Any(r =>
            {
                var p = copy.Board[r.FromPos];
                return p != null && p.Type == PieceType.Pawn;
            });
        }

        return result;
    }

    // Adapte si tu as déjà un ToUci centralisé
    private static string ToUci(Move m)
    {
        static string Sq(Position p)
        {
            char file = (char)('a' + p.Column);
            int rank = 8 - p.Row;
            return $"{file}{rank}";
        }

        return Sq(m.FromPos) + Sq(m.ToPos);
    }
}
#endif
