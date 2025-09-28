using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ChessLogic;

public class MiniMaxEngine
{
    const int INF = 1_000_000;

    // Valeurs simples (centi-pions)
    static readonly Dictionary<PieceType, int> Val = new()
    {
        { PieceType.Pawn,   100 },
        { PieceType.Knight, 320 },
        { PieceType.Bishop, 330 },
        { PieceType.Rook,   500 },
        { PieceType.Queen,  900 },
        { PieceType.King,     0 },
    };

    public Move? FindBestMove(GameState state, int depth = 3, int timeMs = 2000)
    {
        var sw = Stopwatch.StartNew();
        Move? best = null;
        int alpha = -INF, beta = INF;

        var moves = GenerateAllLegalMoves(state);
        OrderMoves(state, moves);

        foreach (var m in moves)
        {
            var next = Copy(state);
            next.MakeMove(m);
            int score = -Search(next, depth - 1, -beta, -alpha, sw, timeMs);
            if (score > alpha) { alpha = score; best = m; }
            if (sw.ElapsedMilliseconds > timeMs) break;
        }
        return best ?? moves.FirstOrDefault();
    }

    int Search(GameState s, int depth, int alpha, int beta, Stopwatch sw, int timeMs)
    {
        if (depth <= 0 || sw.ElapsedMilliseconds > timeMs)
            return Quiescence(s, alpha, beta);

        var moves = GenerateAllLegalMoves(s);
        if (moves.Count == 0) return TerminalScore(s);

        OrderMoves(s, moves);

        foreach (var m in moves)
        {
            var next = Copy(s);
            next.MakeMove(m);
            int score = -Search(next, depth - 1, -beta, -alpha, sw, timeMs);
            if (score >= beta) return beta; // cutoff
            if (score > alpha) alpha = score;
        }
        return alpha;
    }

    int Quiescence(GameState s, int alpha, int beta)
    {
        int stand = Evaluate(s);
        if (stand >= beta) return beta;
        if (alpha < stand) alpha = stand;

        foreach (var m in GenerateAllLegalMoves(s).Where(m => IsTactical(s, m)))
        {
            var next = Copy(s);
            next.MakeMove(m);
            int score = -Quiescence(next, -beta, -alpha);
            if (score >= beta) return beta;
            if (score > alpha) alpha = score;
        }
        return alpha;
    }

    int TerminalScore(GameState s)
    {
        var toMove = s.CurrentPlayer;
        bool inCheck = s.Board.IsInCheck(toMove);
        if (inCheck) return -INF + 1000; // mat contre le camp au trait
        return 0; // pat
    }

    int Evaluate(GameState s)
    {
        int score = 0;
        for (int r = 0; r < 8; r++)
            for (int c = 0; c < 8; c++)
            {
                var p = s.Board[r, c];
                if (p == null) continue;
                int v = Val[p.Type];
                score += (p.Color == Player.White) ? v : -v;
            }

        // petit bonus mobilité
        int mob = GenerateAllLegalMoves(s).Count;
        score += (s.CurrentPlayer == Player.White) ? mob : -mob;

        // Negamax : point de vue du trait
        return (s.CurrentPlayer == Player.White) ? score : -score;
    }

    List<Move> GenerateAllLegalMoves(GameState s)
    {
        var list = new List<Move>(64);
        for (int r = 0; r < 8; r++)
            for (int c = 0; c < 8; c++)
            {
                var p = s.Board[r, c];
                if (p == null || p.Color != s.CurrentPlayer) continue;
                list.AddRange(s.LegalMovesForPiece(new Position(r, c)));
            }
        return list;
    }

    private static bool IsCapture(GameState s, Move m)
        => s.Board[m.ToPos] != null || m.Type == MoveType.EnPassant;

    private static bool IsPromotion(Move m)
        => m.Type == MoveType.PawnPromotion;

    void OrderMoves(GameState s, List<Move> moves)
    {
        moves.Sort((a, b) =>
        {
            int sa = (IsPromotion(a) ? 2 : IsCapture(s, a) ? 1 : 0);
            int sb = (IsPromotion(b) ? 2 : IsCapture(s, b) ? 1 : 0);
            return sb.CompareTo(sa);
        });
    }

    bool IsTactical(GameState s, Move m)
        => IsPromotion(m) || IsCapture(s, m);

    GameState Copy(GameState s)
        => new GameState(s.CurrentPlayer, s.Board.Copy());
}
