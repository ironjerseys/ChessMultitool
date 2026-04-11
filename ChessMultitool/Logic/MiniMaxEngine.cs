using System.Diagnostics;

namespace ChessLogic;

public class MiniMaxEngine
{
    private const int INF = 1_000_000;
    private const int QUIESCENCE_MAX_DEPTH = 4;

    // ── Valeurs des pièces (centi-pions) ─────────────────────────────────────
    private static readonly Dictionary<PieceType, int> PieceValues = new()
    {
        { PieceType.Pawn,   100 },
        { PieceType.Knight, 320 },
        { PieceType.Bishop, 330 },
        { PieceType.Rook,   500 },
        { PieceType.Queen,  900 },
        { PieceType.King,     0 },
    };

    // ── Table de valeurs positionnelles pour les pions ────────────────────────
    static readonly int[,] PawnTableWhite = new int[8, 8]
    {
        { 0,   0,   0,   0,   0,   0,   0,   0 },
        { 5,  10,  10, -10, -10,  10,  10,   5 },
        { 5,  10,  20,  20,  20,  20,  10,   5 },
        { 0,   5,  10,  25,  25,  10,   5,   0 },
        { 0,   5,   5,  15,  15,   5,   5,   0 },
        { 0,   0,   0,  10,  10,   0,   0,   0 },
        { 0,   0,   0,   0,   0,   0,   0,   0 },
        { 0,   0,   0,   0,   0,   0,   0,   0 },
    };

    // ── Table de transposition ────────────────────────────────────────────────
    private const int    TT_SIZE    = 1 << 20; // ~1 million d'entrées ≈ 16 Mo
    private const byte   BOUND_UPPER = 0;       // score est une borne supérieure (fail-low)
    private const byte   BOUND_LOWER = 1;       // score est une borne inférieure (fail-high / beta cutoff)
    private const byte   BOUND_EXACT = 2;       // score exact

    private struct TTEntry
    {
        public ulong Hash;
        public int   Score;
        public byte  Depth;
        public byte  Bound;
    }

    private readonly TTEntry[] _tt = new TTEntry[TT_SIZE];

    private void TTStore(ulong hash, int score, int depth, byte bound)
    {
        // Ne pas stocker les scores de mat (dépendants du ply)
        if (Math.Abs(score) > INF - 1000) return;

        int idx = (int)(hash & (TT_SIZE - 1));
        ref TTEntry entry = ref _tt[idx];

        // Stratégie : conserver l'entrée si elle est à une plus grande profondeur
        // (sauf si c'est une position différente)
        if (entry.Hash == hash && entry.Depth > depth) return;

        entry.Hash  = hash;
        entry.Score = score;
        entry.Depth = (byte)Math.Min(depth, 255);
        entry.Bound = bound;
    }

    private bool TTProbe(ulong hash, int depth, int alpha, int beta, out int score)
    {
        score = 0;
        int idx = (int)(hash & (TT_SIZE - 1));
        ref TTEntry entry = ref _tt[idx];

        if (entry.Hash != hash || entry.Depth < depth) return false;

        score = entry.Score;
        if (entry.Bound == BOUND_EXACT) return true;
        if (entry.Bound == BOUND_LOWER && score >= beta)  return true;
        if (entry.Bound == BOUND_UPPER && score <= alpha) return true;
        return false;
    }

    // ── Killer Moves (2 par ply, jusqu'à 64 plies) ───────────────────────────
    private const int MAX_PLY = 64;
    private readonly Move?[,] _killers = new Move?[MAX_PLY, 2];

    private void StoreKiller(int ply, Move move)
    {
        if (ply >= MAX_PLY) return;
        if (!SameMove(_killers[ply, 0], move))
        {
            _killers[ply, 1] = _killers[ply, 0];
            _killers[ply, 0] = move;
        }
    }

    private bool IsKiller(int ply, Move move)
    {
        if (ply >= MAX_PLY) return false;
        return SameMove(_killers[ply, 0], move) || SameMove(_killers[ply, 1], move);
    }

    private static bool SameMove(Move? a, Move? b)
    {
        if (a == null || b == null) return false;
        return a.FromPos == b.FromPos && a.ToPos == b.ToPos && a.Type == b.Type;
    }

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Approfondissement itératif : de la profondeur 1 à depth,
    /// limité par timeMs. Retourne le meilleur coup trouvé à la dernière
    /// profondeur complètement terminée.
    /// </summary>
    public Move? FindBestMove(
        GameState state,
        int depth = 8,
        int timeMs = 2000,
        Action<Move, int>? onEvaluated = null,
        Action<long, long, long>? onStats = null,
        Action<int>? onEvalUpdate = null)
    {
        var sw = Stopwatch.StartNew();

        var searchState = new GameState(state.CurrentPlayer, state.Board.Copy());

        long nodesVisited       = 0;
        long generatedMovesTotal = 0;
        long leafEvaluations    = 0;

        var rootMoves = GenerateAllLegalMoves(searchState);
        if (rootMoves.Count == 0)
        {
            onStats?.Invoke(generatedMovesTotal, nodesVisited, leafEvaluations);
            return null;
        }

        OrderMoves(searchState, rootMoves, 0);

        // Réinitialiser les killers pour cette nouvelle recherche
        Array.Clear(_killers, 0, _killers.Length);

        Move? bestSoFar       = null;
        int   bestScoreSoFar  = -INF;
        var   lastDepthScores = new Dictionary<Move, int>(rootMoves.Count);

        for (int currentDepth = 1; currentDepth <= depth; currentDepth++)
        {
            if (sw.ElapsedMilliseconds >= timeMs) break;

            bool completedDepth = true;
            int  alpha          = -INF;
            int  beta           = INF;
            Move? bestAtThisDepth = null;

            lastDepthScores.Clear();

            foreach (var move in rootMoves)
            {
                if (sw.ElapsedMilliseconds >= timeMs)
                {
                    completedDepth = false;
                    break;
                }

                searchState.MakeMoveFast(move, out var undo);

                int score;
                try
                {
                    score = -Search(
                        searchState,
                        currentDepth - 1,
                        -beta, -alpha,
                        ply: 1,
                        allowNullMove: true,
                        sw, timeMs,
                        ref nodesVisited, ref generatedMovesTotal, ref leafEvaluations
                    );
                }
                finally
                {
                    searchState.UnmakeMoveFast(undo);
                }

                lastDepthScores[move] = score;

                if (score > alpha)
                {
                    alpha             = score;
                    bestAtThisDepth   = move;
                }
            }

            if (!completedDepth || bestAtThisDepth == null)
            {
                if (bestSoFar == null && bestAtThisDepth != null)
                {
                    bestSoFar      = bestAtThisDepth;
                    bestScoreSoFar = alpha;
                }
                break;
            }

            bestSoFar      = bestAtThisDepth;
            bestScoreSoFar = alpha;

            // Réordonner les coups racine selon les scores de cette profondeur
            rootMoves.Sort((a, b) =>
            {
                int sb = lastDepthScores.TryGetValue(b, out var vb) ? vb : int.MinValue;
                int sa = lastDepthScores.TryGetValue(a, out var va) ? va : int.MinValue;
                return sb.CompareTo(sa);
            });
        }

        onStats?.Invoke(generatedMovesTotal, nodesVisited, leafEvaluations);

        if (bestSoFar != null)
            onEvalUpdate?.Invoke(bestScoreSoFar);

        return bestSoFar ?? rootMoves[0];
    }

    /// <summary>
    /// Recherche récursive negamax avec alpha-beta, table de transposition,
    /// killer moves et null move pruning.
    /// </summary>
    private int Search(
        GameState currentState,
        int depthRemaining,
        int alpha, int beta,
        int ply,
        bool allowNullMove,
        Stopwatch stopwatch, int timeMs,
        ref long nodesVisited, ref long generatedMovesTotal, ref long leafEvaluations)
    {
        nodesVisited++;

        if (stopwatch.ElapsedMilliseconds > timeMs)
            return Evaluate(currentState);

        // ── Table de transposition : probe ────────────────────────────────────
        ulong hash = currentState.ZobristHash;
        if (TTProbe(hash, depthRemaining, alpha, beta, out int ttScore))
            return ttScore;

        // ── Profondeur épuisée → quiescence ───────────────────────────────────
        if (depthRemaining <= 0)
        {
            return Quiescence(
                currentState, alpha, beta,
                QUIESCENCE_MAX_DEPTH, ply,
                stopwatch, timeMs,
                ref nodesVisited, ref generatedMovesTotal, ref leafEvaluations);
        }

        bool inCheck = currentState.Board.IsInCheck(currentState.CurrentPlayer);

        // ── Null Move Pruning ─────────────────────────────────────────────────
        // Conditions : pas en échec, pas deux null moves de suite, profondeur ≥ 3,
        // pas en finale (risque de zugzwang).
        if (allowNullMove
            && !inCheck
            && depthRemaining >= 3
            && !IsEndgameBoard(currentState))
        {
            const int R = 2; // réduction de profondeur
            currentState.MakeNullMove(out var nullUndo);

            int nullScore = -Search(
                currentState,
                depthRemaining - 1 - R,
                -beta, -beta + 1,
                ply + 1,
                allowNullMove: false,
                stopwatch, timeMs,
                ref nodesVisited, ref generatedMovesTotal, ref leafEvaluations);

            currentState.UnmakeNullMove(nullUndo);

            if (nullScore >= beta)
            {
                TTStore(hash, beta, depthRemaining, BOUND_LOWER);
                return beta;
            }
        }

        // ── Génération et tri des coups ───────────────────────────────────────
        var legalMoves = GenerateAllLegalMoves(currentState);
        generatedMovesTotal += legalMoves.Count;

        if (legalMoves.Count == 0) return TerminalScore(currentState);

        OrderMoves(currentState, legalMoves, ply);

        byte bound = BOUND_UPPER;

        foreach (var move in legalMoves)
        {
            currentState.MakeMoveFast(move, out var undo);

            int score = -Search(
                currentState,
                depthRemaining - 1,
                -beta, -alpha,
                ply + 1,
                allowNullMove: true,
                stopwatch, timeMs,
                ref nodesVisited, ref generatedMovesTotal, ref leafEvaluations);

            currentState.UnmakeMoveFast(undo);

            if (score >= beta)
            {
                // Coupure beta : stocker comme killer si c'est un coup silencieux
                if (!IsCapture(currentState, move) && !IsPromotion(move))
                    StoreKiller(ply, move);

                TTStore(hash, score, depthRemaining, BOUND_LOWER);
                return beta;
            }

            if (score > alpha)
            {
                alpha = score;
                bound = BOUND_EXACT;
            }

            if (stopwatch.ElapsedMilliseconds > timeMs) break;
        }

        TTStore(hash, alpha, depthRemaining, bound);
        return alpha;
    }

    /// <summary>
    /// Recherche de quiétude : stand-pat puis prolongation sur coups tactiques.
    /// </summary>
    private int Quiescence(
        GameState currentState,
        int alpha, int beta,
        int qDepth,
        int ply,
        Stopwatch stopwatch, int timeMs,
        ref long nodesVisited, ref long generatedMovesTotal, ref long leafEvaluations)
    {
        nodesVisited++;

        if (stopwatch.ElapsedMilliseconds > timeMs || qDepth <= 0)
            return Evaluate(currentState);

        leafEvaluations++;
        int standPat = Evaluate(currentState);

        bool inCheck = currentState.Board.IsInCheck(currentState.CurrentPlayer);
        if (!inCheck && standPat >= beta) return beta;
        if (standPat > alpha) alpha = standPat;

        var allMoves = GenerateAllLegalMoves(currentState);
        generatedMovesTotal += allMoves.Count;

        // Position terminale (mat / pat) détectée en quiescence
        if (allMoves.Count == 0) return TerminalScore(currentState);

        foreach (var move in allMoves)
        {
            if (!IsTacticalWithCheck(currentState, move)) continue;

            currentState.MakeMoveFast(move, out var undo);

            int score = -Quiescence(
                currentState, -beta, -alpha,
                qDepth - 1, ply + 1,
                stopwatch, timeMs,
                ref nodesVisited, ref generatedMovesTotal, ref leafEvaluations);

            currentState.UnmakeMoveFast(undo);

            if (score >= beta) return beta;
            if (score > alpha) alpha = score;
        }

        return alpha;
    }

    /// <summary>Score terminal pour mat / pat.</summary>
    private int TerminalScore(GameState s)
    {
        bool inCheck = s.Board.IsInCheck(s.CurrentPlayer);
        return inCheck ? -INF + 1000 : 0;
    }

    /// <summary>
    /// Évalue la position : matériel + bonus positionnels.
    /// Retourne le score du point de vue du camp au trait (negamax).
    /// </summary>
    private int Evaluate(GameState state)
    {
        int materialWhite = 0, materialBlack = 0;
        int nonPawnMaterialWhite = 0, nonPawnMaterialBlack = 0;

        Position? whiteKingPosition = null;
        Position? blackKingPosition = null;

        int[] whitePawnsByFile = new int[8];
        int[] blackPawnsByFile = new int[8];

        for (int rowIndex = 0; rowIndex < 8; rowIndex++)
        {
            for (int columnIndex = 0; columnIndex < 8; columnIndex++)
            {
                var piece = state.Board[rowIndex, columnIndex];
                if (piece == null) continue;

                int pieceBaseValue = PieceValues[piece.Type];

                if (piece.Color == Player.White)
                {
                    materialWhite += pieceBaseValue;
                    if (piece.Type != PieceType.Pawn && piece.Type != PieceType.King)
                        nonPawnMaterialWhite += pieceBaseValue;
                }
                else
                {
                    materialBlack += pieceBaseValue;
                    if (piece.Type != PieceType.Pawn && piece.Type != PieceType.King)
                        nonPawnMaterialBlack += pieceBaseValue;
                }

                switch (piece.Type)
                {
                    case PieceType.Pawn:
                        if (piece.Color == Player.White)
                        {
                            whitePawnsByFile[columnIndex]++;
                            materialWhite += PawnTableWhite[rowIndex, columnIndex];
                        }
                        else
                        {
                            blackPawnsByFile[columnIndex]++;
                            materialBlack += PawnTableWhite[7 - rowIndex, columnIndex];
                        }
                        break;

                    case PieceType.Knight:
                        if (rowIndex >= 2 && rowIndex <= 5 && columnIndex >= 2 && columnIndex <= 5)
                        {
                            if (piece.Color == Player.White) materialWhite += 10;
                            else materialBlack += 10;
                        }
                        break;

                    case PieceType.Bishop:
                        if (rowIndex >= 2 && rowIndex <= 5 && columnIndex >= 2 && columnIndex <= 5)
                        {
                            if (piece.Color == Player.White) materialWhite += 8;
                            else materialBlack += 8;
                        }
                        break;

                    case PieceType.Rook:
                        if (columnIndex == 3 || columnIndex == 4)
                        {
                            if (piece.Color == Player.White) materialWhite += 5;
                            else materialBlack += 5;
                        }
                        break;

                    case PieceType.King:
                        if (piece.Color == Player.White) whiteKingPosition = new Position(rowIndex, columnIndex);
                        else blackKingPosition = new Position(rowIndex, columnIndex);
                        break;
                }
            }
        }

        int positionScore = materialWhite - materialBlack;
        bool isEndgamePhase = IsEndgame(nonPawnMaterialWhite, nonPawnMaterialBlack);

        if (whiteKingPosition is Position wk && blackKingPosition is Position bk)
        {
            bool wkCentral = wk.Row >= 2 && wk.Row <= 5 && wk.Column >= 2 && wk.Column <= 5;
            bool bkCentral = bk.Row >= 2 && bk.Row <= 5 && bk.Column >= 2 && bk.Column <= 5;

            if (isEndgamePhase)
            {
                if (wkCentral) positionScore += 20;
                if (bkCentral) positionScore -= 20;
            }
            else
            {
                if (wkCentral) positionScore -= 30;
                if (bkCentral) positionScore += 30;
            }
        }

        for (int fileIndex = 0; fileIndex < 8; fileIndex++)
        {
            if (whitePawnsByFile[fileIndex] > 1) positionScore -= 10 * (whitePawnsByFile[fileIndex] - 1);
            if (blackPawnsByFile[fileIndex] > 1) positionScore += 10 * (blackPawnsByFile[fileIndex] - 1);
        }

        return state.CurrentPlayer == Player.White ? positionScore : -positionScore;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Détecte si le plateau est en phase de finale (peu de matériel).</summary>
    private bool IsEndgameBoard(GameState state)
    {
        int nonPawnMaterial = 0;
        for (int r = 0; r < 8; r++)
        for (int c = 0; c < 8; c++)
        {
            var piece = state.Board[r, c];
            if (piece != null && piece.Type != PieceType.Pawn && piece.Type != PieceType.King)
                nonPawnMaterial += PieceValues[piece.Type];
        }
        return nonPawnMaterial <= 2 * PieceValues[PieceType.Rook];
    }

    /// <summary>
    /// Génère tous les coups légaux pour le camp au trait.
    /// Version rapide : la légalité est vérifiée via MakeMoveFast/IsInCheck/UnmakeMoveFast
    /// (O(2-4 copies de pièces) au lieu de Board.Copy() qui copie 32+ pièces par coup).
    /// Seul le roque utilise encore l'ancien chemin (vérification "ne pas passer par un échec").
    /// </summary>
    private List<Move> GenerateAllLegalMoves(GameState s)
    {
        var list = new List<Move>(64);
        var player = s.CurrentPlayer;

        for (int r = 0; r < 8; r++)
        for (int c = 0; c < 8; c++)
        {
            var piece = s.Board[r, c];
            if (piece == null || piece.Color != player) continue;

            var pos = new Position(r, c);

            foreach (var move in piece.GetMoves(pos, s.Board))
            {
                bool legal;

                if (move.Type == MoveType.CastleKS || move.Type == MoveType.CastleQS)
                {
                    // Le roque nécessite une vérification spéciale (roi ne doit pas
                    // passer par une case en échec) → chemin original avec Board.Copy().
                    legal = move.IsLegal(s.Board);
                }
                else
                {
                    // Vérification rapide : joue le coup, teste si le roi est en échec,
                    // puis annule. Évite la copie complète du plateau.
                    s.MakeMoveFast(move, out var undo);
                    legal = !s.Board.IsInCheck(player);
                    s.UnmakeMoveFast(undo);
                }

                if (legal) list.Add(move);
            }
        }

        return list;
    }

    private static bool IsCapture(GameState s, Move m)
        => s.Board[m.ToPos] != null || m.Type == MoveType.EnPassant;

    private static bool IsPromotion(Move m)
        => m.Type == MoveType.PawnPromotion;

    /// <summary>Coup "tactique" pour la quiescence : capture, promotion ou coup qui donne échec.</summary>
    private bool IsTacticalWithCheck(GameState state, Move m)
    {
        if (IsPromotion(m) || IsCapture(state, m)) return true;

        state.MakeMoveFast(m, out var undo);
        bool givesCheck = state.Board.IsInCheck(state.CurrentPlayer);
        state.UnmakeMoveFast(undo);

        return givesCheck;
    }

    /// <summary>
    /// Tri des coups :
    ///   1. Promotions (10 000)
    ///   2. Captures, triées MVV-LVA (1 000 + valeur_victime × 10 − valeur_attaquant)
    ///   3. Killer moves (900)
    ///   4. Coups silencieux (0)
    /// </summary>
    private void OrderMoves(GameState s, List<Move> moves, int ply)
    {
        moves.Sort((a, b) => MoveScore(s, b, ply).CompareTo(MoveScore(s, a, ply)));
    }

    private int MoveScore(GameState s, Move m, int ply)
    {
        if (IsPromotion(m)) return 10_000;

        if (IsCapture(s, m))
        {
            int victimValue = m.Type == MoveType.EnPassant
                ? PieceValues[PieceType.Pawn]
                : (s.Board[m.ToPos] != null ? PieceValues[s.Board[m.ToPos].Type] : 0);
            int attackerValue = s.Board[m.FromPos] != null ? PieceValues[s.Board[m.FromPos].Type] : 0;
            // MVV-LVA : prioriser les captures de haute valeur faites par des pièces de faible valeur
            return 1_000 + victimValue * 10 - attackerValue;
        }

        if (IsKiller(ply, m)) return 900;

        return 0;
    }

    private bool IsEndgame(int nonPawnWhite, int nonPawnBlack)
        => nonPawnWhite + nonPawnBlack <= 2 * PieceValues[PieceType.Rook];
}
