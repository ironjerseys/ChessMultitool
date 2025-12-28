using System.Diagnostics;

namespace ChessLogic;

public class MiniMaxEngine
{
    private const int INF = 1_000_000;
    private const int QUIESCENCE_MAX_DEPTH = 4;

    // Valeurs de base des pièces (centi-pions)
    private static readonly Dictionary<PieceType, int> PieceValues = new()
    {
        { PieceType.Pawn,   100 },
        { PieceType.Knight, 320 },
        { PieceType.Bishop, 330 },
        { PieceType.Rook,   500 },
        { PieceType.Queen,  900 },
        { PieceType.King,     0 },
    };


    // Option : tables de pièces simples (à affiner)
    static readonly int[,] PawnTableWhite = new int[8, 8]
    {
        // rank 8 -> 1 (index 0 = rang 0 de ton array, à adapter à ton sens de l’échiquier)
        { 0,   0,   0,   0,   0,   0,   0,   0 },
        { 5,  10,  10, -10, -10,  10,  10,   5 },
        { 5,  10,  20,  20,  20,  20,  10,   5 },
        { 0,   5,  10,  25,  25,  10,   5,   0 },
        { 0,   5,   5,  15,  15,   5,   5,   0 },
        { 0,   0,   0,  10,  10,   0,   0,   0 },
        { 0,   0,   0,   0,   0,   0,   0,   0 },
        { 0,   0,   0,   0,   0,   0,   0,   0 },
    };

    /// <summary>
    /// Approfondissement itératif : de la profondeur 1 à depth,
    /// limité par timeMs. Retourne le meilleur coup trouvé à la dernière
    /// profondeur complètement terminée.
    /// </summary>
    public Move? FindBestMove(
        GameState state,
        int depth = 3,
        int timeMs = 2000,
        Action<Move>? onConsider = null,
        Action<Move, int>? onEvaluated = null,
        Action<long, long, long>? onStats = null,
        Action<int>? onEvalUpdate = null)
    {
        var sw = Stopwatch.StartNew();

        long nodesVisited = 0;
        long generatedMovesTotal = 0;
        long leafEvaluations = 0;

        var rootMoves = GenerateAllLegalMoves(state);
        if (rootMoves.Count == 0)
        {
            onStats?.Invoke(generatedMovesTotal, nodesVisited, leafEvaluations);
            return null;
        }

        // Move ordering initial : tactiques d'abord
        OrderMoves(state, rootMoves);

        Move? bestSoFar = null;
        int bestScoreSoFar = -INF;
        var lastDepthScores = new Dictionary<Move, int>();
        bool timeUp = false;

        for (int currentDepth = 1; currentDepth <= depth; currentDepth++)
        {
            int alpha = -INF;
            int beta = INF;
            Move? bestAtThisDepth = null;

            foreach (var move in rootMoves)
            {
                if (sw.ElapsedMilliseconds > timeMs)
                {
                    timeUp = true;
                    break;
                }

                onConsider?.Invoke(move);

                var next = Copy(state);
                next.MakeMove(move);

                int score = -Search(
                    next,
                    currentDepth - 1,
                    -beta,
                    -alpha,
                    sw,
                    timeMs,
                    ref nodesVisited,
                    ref generatedMovesTotal,
                    ref leafEvaluations,
                    onEvalUpdate);

                onEvaluated?.Invoke(move, score);
                lastDepthScores[move] = score;

                if (score > alpha)
                {
                    alpha = score;
                    bestAtThisDepth = move;
                    onEvalUpdate?.Invoke(alpha);
                }
            }

            if (bestAtThisDepth == null)
                break;

            // Profondeur terminée : on valide ce résultat
            bestSoFar = bestAtThisDepth;
            bestScoreSoFar = alpha;

            // Réordonne les coups racine selon les scores de la dernière profondeur
            rootMoves = rootMoves
                .OrderByDescending(m => lastDepthScores.TryGetValue(m, out var sc) ? sc : int.MinValue)
                .ToList();

            if (timeUp)
                break;
        }

        onStats?.Invoke(generatedMovesTotal, nodesVisited, leafEvaluations);
        return bestSoFar ?? rootMoves.FirstOrDefault();
    }

    /// <summary>
    /// Recherche récursive negamax avec élagage alpha-beta.
    /// Quand depthRemaining <= 0 on bascule en quiescence.
    /// Si le temps est dépassé, on renvoie une évaluation statique.
    /// </summary>
    private int Search(
        GameState currentState,
        int depthRemaining,
        int alpha,
        int beta,
        Stopwatch stopwatch,
        int timeMs,
        ref long nodesVisited,
        ref long generatedMovesTotal,
        ref long leafEvaluations,
        Action<int>? onEvalUpdate)
    {
        nodesVisited++;

        // Limite de temps : pas de quiescence, on s'arrête net ici
        if (stopwatch.ElapsedMilliseconds > timeMs)
            return Evaluate(currentState);

        // Profondeur principale épuisée → quiescence
        if (depthRemaining <= 0)
        {
            return Quiescence(
                currentState,
                alpha,
                beta,
                QUIESCENCE_MAX_DEPTH,
                stopwatch,
                timeMs,
                ref nodesVisited,
                ref generatedMovesTotal,
                ref leafEvaluations);
        }

        var legalMoves = GenerateAllLegalMoves(currentState);
        generatedMovesTotal += legalMoves.Count;

        if (legalMoves.Count == 0)
            return TerminalScore(currentState);

        OrderMoves(currentState, legalMoves);

        foreach (var move in legalMoves)
        {
            var nextState = Copy(currentState);
            nextState.MakeMove(move);

            int score = -Search(
                nextState,
                depthRemaining - 1,
                -beta,
                -alpha,
                stopwatch,
                timeMs,
                ref nodesVisited,
                ref generatedMovesTotal,
                ref leafEvaluations,
                onEvalUpdate);

            if (score >= beta)
                return beta; // élagage beta

            if (score > alpha)
            {
                alpha = score;
                onEvalUpdate?.Invoke(alpha);
            }

            if (stopwatch.ElapsedMilliseconds > timeMs)
                break;
        }

        return alpha;
    }

    /// <summary>
    /// Recherche de quiétude : stand-pat puis prolongation
    /// uniquement sur coups tactiques (captures / promotions / échecs).
    /// </summary>
    private int Quiescence(
        GameState currentState,
        int alpha,
        int beta,
        int qDepth,
        Stopwatch stopwatch,
        int timeMs,
        ref long nodesVisited,
        ref long generatedMovesTotal,
        ref long leafEvaluations)
    {
        nodesVisited++;

        // Sécurité temps / profondeur
        if (stopwatch.ElapsedMilliseconds > timeMs || qDepth <= 0)
            return Evaluate(currentState);

        leafEvaluations++;
        int standPat = Evaluate(currentState);

        // Si on n'est pas en échec et que le stand-pat dépasse beta, cutoff
        bool inCheck = currentState.Board.IsInCheck(currentState.CurrentPlayer);
        if (!inCheck && standPat >= beta)
            return beta;

        if (standPat > alpha)
            alpha = standPat;

        var allMoves = GenerateAllLegalMoves(currentState);
        generatedMovesTotal += allMoves.Count;

        foreach (var move in allMoves)
        {
            if (!IsTacticalWithCheck(currentState, move))
                continue;

            var next = Copy(currentState);
            next.MakeMove(move);

            int score = -Quiescence(
                next,
                -beta,
                -alpha,
                qDepth - 1,
                stopwatch,
                timeMs,
                ref nodesVisited,
                ref generatedMovesTotal,
                ref leafEvaluations);

            if (score >= beta)
                return beta;

            if (score > alpha)
                alpha = score;
        }

        return alpha;
    }

    /// <summary>
    /// Score terminal pour mat / pat.
    /// </summary>
    private int TerminalScore(GameState s)
    {
        var toMove = s.CurrentPlayer;
        bool inCheck = s.Board.IsInCheck(toMove);
        if (inCheck)
            return -INF + 1000; // mat contre le camp au trait
        return 0; // pat
    }

    /// <summary>
    /// Évalue la position : matériel + quelques bonus positionnels simples,
    /// puis renvoie le score du point de vue du camp au trait (negamax).
    /// </summary>
    private int Evaluate(GameState state)
    {
        int materialWhite = 0;
        int materialBlack = 0;

        int nonPawnMaterialWhite = 0;
        int nonPawnMaterialBlack = 0;

        Position? whiteKingPosition = null;
        Position? blackKingPosition = null;

        // Nombre de pions par colonne (fichier)
        int[] whitePawnsByFile = new int[8];
        int[] blackPawnsByFile = new int[8];

        // Parcours du plateau une seule fois
        for (int rowIndex = 0; rowIndex < 8; rowIndex++)
        {
            for (int columnIndex = 0; columnIndex < 8; columnIndex++)
            {
                var piece = state.Board[rowIndex, columnIndex];
                if (piece == null)
                {
                    continue;
                }

                int pieceBaseValue = PieceValues[piece.Type];

                // Matériel brut + suivi du matériel "non-pion" pour détecter la finale
                if (piece.Color == Player.White)
                {
                    materialWhite += pieceBaseValue;

                    if (piece.Type != PieceType.Pawn && piece.Type != PieceType.King)
                    {
                        nonPawnMaterialWhite += pieceBaseValue;
                    }
                }
                else
                {
                    materialBlack += pieceBaseValue;

                    if (piece.Type != PieceType.Pawn && piece.Type != PieceType.King)
                    {
                        nonPawnMaterialBlack += pieceBaseValue;
                    }
                }

                // Bonus / pénalités positionnelles simples
                switch (piece.Type)
                {
                    case PieceType.Pawn:
                        if (piece.Color == Player.White)
                        {
                            whitePawnsByFile[columnIndex]++;
                            // Table de valeurs par case pour les pions blancs
                            materialWhite += PawnTableWhite[rowIndex, columnIndex];
                        }
                        else
                        {
                            blackPawnsByFile[columnIndex]++;
                            // On "miroire" verticalement la table pour les noirs
                            materialBlack += PawnTableWhite[7 - rowIndex, columnIndex];
                        }
                        break;

                    case PieceType.Knight:
                        bool knightIsCentral =
                            rowIndex >= 2 && rowIndex <= 5 &&
                            columnIndex >= 2 && columnIndex <= 5;

                        if (knightIsCentral)
                        {
                            const int knightCentralBonus = 10;
                            if (piece.Color == Player.White)
                            {
                                materialWhite += knightCentralBonus;
                            }
                            else
                            {
                                materialBlack += knightCentralBonus;
                            }
                        }
                        break;

                    case PieceType.Bishop:
                        bool bishopIsCentral =
                            rowIndex >= 2 && rowIndex <= 5 &&
                            columnIndex >= 2 && columnIndex <= 5;

                        if (bishopIsCentral)
                        {
                            const int bishopCentralBonus = 8;
                            if (piece.Color == Player.White)
                            {
                                materialWhite += bishopCentralBonus;
                            }
                            else
                            {
                                materialBlack += bishopCentralBonus;
                            }
                        }
                        break;

                    case PieceType.Rook:
                        bool rookOnCentralFile = (columnIndex == 3 || columnIndex == 4);
                        if (rookOnCentralFile)
                        {
                            const int rookCentralFileBonus = 5;
                            if (piece.Color == Player.White)
                            {
                                materialWhite += rookCentralFileBonus;
                            }
                            else
                            {
                                materialBlack += rookCentralFileBonus;
                            }
                        }
                        break;

                    case PieceType.King:
                        if (piece.Color == Player.White)
                        {
                            whiteKingPosition = new Position(rowIndex, columnIndex);
                        }
                        else
                        {
                            blackKingPosition = new Position(rowIndex, columnIndex);
                        }
                        break;
                }
            }
        }

        // Score matériel + petits bonus
        int positionScore = materialWhite - materialBlack;

        // Détection simple de la finale (basée sur le matériel non-pion)
        bool isEndgamePhase = IsEndgame(nonPawnMaterialWhite, nonPawnMaterialBlack);

        // Heuristiques roi (sécurité en milieu de jeu, activité en finale)
        if (whiteKingPosition is Position whiteKingSquare &&
            blackKingPosition is Position blackKingSquare)
        {
            bool whiteKingCentral =
                whiteKingSquare.Row >= 2 && whiteKingSquare.Row <= 5 &&
                whiteKingSquare.Column >= 2 && whiteKingSquare.Column <= 5;

            bool blackKingCentral =
                blackKingSquare.Row >= 2 && blackKingSquare.Row <= 5 &&
                blackKingSquare.Column >= 2 && blackKingSquare.Column <= 5;

            if (isEndgamePhase)
            {
                // En finale : roi central = bon
                const int kingActivityBonus = 20;
                if (whiteKingCentral)
                {
                    positionScore += kingActivityBonus;
                }
                if (blackKingCentral)
                {
                    positionScore -= kingActivityBonus;
                }
            }
            else
            {
                // Milieu de jeu : roi trop central = dangereux
                const int kingCenterPenalty = 30;
                if (whiteKingCentral)
                {
                    positionScore -= kingCenterPenalty;
                }
                if (blackKingCentral)
                {
                    positionScore += kingCenterPenalty;
                }
            }
        }

        // Structure de pions très simple : pions doublés
        for (int fileIndex = 0; fileIndex < 8; fileIndex++)
        {
            int whitePawnCountOnFile = whitePawnsByFile[fileIndex];
            int blackPawnCountOnFile = blackPawnsByFile[fileIndex];

            if (whitePawnCountOnFile > 1)
            {
                const int doubledPawnPenalty = 10;
                positionScore -= doubledPawnPenalty * (whitePawnCountOnFile - 1);
            }

            if (blackPawnCountOnFile > 1)
            {
                const int doubledPawnPenalty = 10;
                positionScore += doubledPawnPenalty * (blackPawnCountOnFile - 1);
            }
        }

        // Negamax : score du point de vue du camp au trait
        return state.CurrentPlayer == Player.White
            ? positionScore
            : -positionScore;
    }



    private int CountMovesForColor(GameState baseState, Player color)
    {
        int count = 0;

        for (int r = 0; r < 8; r++)
        {
            for (int c = 0; c < 8; c++)
            {
                var p = baseState.Board[r, c];
                if (p == null || p.Color != color) continue;

                var moves = baseState.LegalMovesForPiece(new Position(r, c));
                count += moves.Count(); // <-- avec les parenthèses
            }
        }

        return count;
    }

    private List<Move> GenerateAllLegalMoves(GameState s)
    {
        var list = new List<Move>(64);
        for (int r = 0; r < 8; r++)
        {
            for (int c = 0; c < 8; c++)
            {
                var p = s.Board[r, c];
                if (p == null || p.Color != s.CurrentPlayer) continue;
                list.AddRange(s.LegalMovesForPiece(new Position(r, c)));
            }
        }
        return list;
    }

    private static bool IsCapture(GameState s, Move m)
    {
        return s.Board[m.ToPos] != null || m.Type == MoveType.EnPassant;
    }

    private static bool IsPromotion(Move m)
    {
        return m.Type == MoveType.PawnPromotion;
    }

    /// <summary>
    /// "Tactique" au sens quiescence : capture, promotion, ou coup qui donne échec.
    /// </summary>
    private bool IsTacticalWithCheck(GameState state, Move m)
    {
        if (IsPromotion(m) || IsCapture(state, m))
            return true;

        var next = Copy(state);
        next.MakeMove(m);
        return next.Board.IsInCheck(next.CurrentPlayer);
    }

    /// <summary>
    /// Tri des coups : promotions puis captures, sinon neutres.
    /// </summary>
    private void OrderMoves(GameState s, List<Move> moves)
    {
        moves.Sort((a, b) =>
        {
            int sa = (IsPromotion(a) ? 2 : IsCapture(s, a) ? 1 : 0);
            int sb = (IsPromotion(b) ? 2 : IsCapture(s, b) ? 1 : 0);
            return sb.CompareTo(sa);
        });
    }

    private GameState Copy(GameState s)
    {
        return new GameState(s.CurrentPlayer, s.Board.Copy());
    }

    private bool IsEndgame(int nonPawnWhite, int nonPawnBlack)
    {
        int total = nonPawnWhite + nonPawnBlack;
        return total <= 2 * PieceValues[PieceType.Rook]; // heuristique : <= valeur 2 tours
    }

}
