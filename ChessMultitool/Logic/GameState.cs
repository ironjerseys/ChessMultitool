namespace ChessLogic;

public class GameState
{
    public Board Board { get; }
    public Player CurrentPlayer { get; private set; }
    public Result Result { get; private set; } = null;

    private int noCaptureOrPawnMoves = 0;
    private string stateString;

    private readonly Dictionary<string, int> stateHistory = new Dictionary<string, int>();

    public GameState(Player player, Board board)
    {
        CurrentPlayer = player;
        Board = board;

        stateString = new StateString(CurrentPlayer, board).ToString();
        stateHistory[stateString] = 1;
    }

    public IEnumerable<Move> LegalMovesForPiece(Position pos)
    {
        if (Board.IsEmpty(pos) || Board[pos].Color != CurrentPlayer)
        {
            return Enumerable.Empty<Move>();
        }

        Piece piece = Board[pos];
        IEnumerable<Move> moveCandidates = piece.GetMoves(pos, Board);
        return moveCandidates.Where(move => move.IsLegal(Board));
    }

    public void MakeMove(Move move)
    {
        Board.SetPawnSkipPosition(CurrentPlayer, null);
        bool captureOrPawn = move.Execute(Board);

        if (captureOrPawn)
        {
            noCaptureOrPawnMoves = 0;
            stateHistory.Clear();
        }
        else
        {
            noCaptureOrPawnMoves++;
        }

        CurrentPlayer = CurrentPlayer.Opponent();
        UpdateStateString();
        CheckForGameOver();
    }

    public IEnumerable<Move> AllLegalMovesFor(Player player)
    {
        IEnumerable<Move> moveCandidates = Board.PiecePositionsFor(player).SelectMany(pos =>
        {
            Piece piece = Board[pos];
            return piece.GetMoves(pos, Board);
        });

        return moveCandidates.Where(move => move.IsLegal(Board));
    }

    private void CheckForGameOver()
    {
        if (!AllLegalMovesFor(CurrentPlayer).Any())
        {
            if (Board.IsInCheck(CurrentPlayer))
            {
                Result = Result.Win(CurrentPlayer.Opponent());
            }
            else
            {
                Result = Result.Draw(EndReason.Stalemate);
            }
        }
        else if (Board.InsufficientMaterial())
        {
            Result = Result.Draw(EndReason.InsufficientMaterial);
        }
        else if (FiftyMoveRule())
        {
            Result = Result.Draw(EndReason.FiftyMoveRule);
        }
        else if (ThreefoldRepetition())
        {
            Result = Result.Draw(EndReason.ThreefoldRepetition);
        }
    }

    public bool IsGameOver()
    {
        return Result != null;
    }

    private bool FiftyMoveRule()
    {
        int fullMoves = noCaptureOrPawnMoves / 2;
        return fullMoves == 50;
    }

    private void UpdateStateString()
    {
        stateString = new StateString(CurrentPlayer, Board).ToString();

        if (!stateHistory.ContainsKey(stateString))
        {
            stateHistory[stateString] = 1;
        }
        else
        {
            stateHistory[stateString]++;
        }
    }

    private bool ThreefoldRepetition()
    {
        return stateHistory[stateString] == 3;
    }

    /// <summary>
    /// Données minimales pour annuler un coup pendant la recherche.
    /// On restaure seulement : pièces des cases touchées + en-passant state + joueur au trait.
    /// </summary>
    public readonly struct SearchUndo
    {
        public readonly Player PrevPlayer;

        public readonly Position? PrevEpWhite;
        public readonly Position? PrevEpBlack;

        public readonly byte Count;

        public readonly Position P0;
        public readonly Position P1;
        public readonly Position P2;
        public readonly Position P3;

        public readonly Piece? A0;
        public readonly Piece? A1;
        public readonly Piece? A2;
        public readonly Piece? A3;

        public SearchUndo(
            Player prevPlayer,
            Position? prevEpWhite, Position? prevEpBlack,
            byte count,
            Position p0, Piece? a0,
            Position p1, Piece? a1,
            Position p2, Piece? a2,
            Position p3, Piece? a3)
        {
            PrevPlayer = prevPlayer;
            PrevEpWhite = prevEpWhite;
            PrevEpBlack = prevEpBlack;
            Count = count;

            P0 = p0; A0 = a0;
            P1 = p1; A1 = a1;
            P2 = p2; A2 = a2;
            P3 = p3; A3 = a3;
        }
    }

    /// <summary>
    /// Applique un coup pour le moteur (sans historique, sans règles de fin, sans stateString).
    /// Réversible via UnmakeMoveFast.
    /// </summary>
    public void MakeMoveFast(Move move, out SearchUndo undo)
    {
        // Snapshot minimal
        var prevPlayer = CurrentPlayer;
        var prevEpWhite = Board.GetPawnSkipPosition(Player.White);
        var prevEpBlack = Board.GetPawnSkipPosition(Player.Black);

        // Quelles cases vont changer ? (max 4)
        CollectTouchedSquares(move, Board, out byte count,
        out Position p0, out Position p1, out Position p2, out Position p3);

        // Sauvegarde pièces AVANT (en copiant pour restaurer aussi les flags internes éventuels)

        Piece? a0 = null, a1 = null, a2 = null, a3 = null;


        if (count > 0) a0 = Board[p0]?.Copy();
        if (count > 1) a1 = Board[p1]?.Copy();
        if (count > 2) a2 = Board[p2]?.Copy();
        if (count > 3) a3 = Board[p3]?.Copy();

        undo = new SearchUndo(
            prevPlayer,
            prevEpWhite, prevEpBlack,
            count,
            p0, a0,
            p1, a1,
            p2, a2,
            p3, a3
        );

        // Même comportement que MakeMove : l'EP “opportunité” du joueur au trait expire maintenant
        Board.SetPawnSkipPosition(CurrentPlayer, null);

        // Applique le coup (on réutilise ton code existant)
        _ = move.Execute(Board);

        // Switch trait
        CurrentPlayer = CurrentPlayer.Opponent();
    }

    /// <summary>
    /// Annule un coup appliqué par MakeMoveFast.
    /// </summary>
    public void UnmakeMoveFast(in SearchUndo undo)
    {
        // Restaure le trait d'abord
        CurrentPlayer = undo.PrevPlayer;

        // Restaure les pièces des cases touchées
        if (undo.Count > 0) Board.SetPiece(undo.P0, undo.A0);
        if (undo.Count > 1) Board.SetPiece(undo.P1, undo.A1);
        if (undo.Count > 2) Board.SetPiece(undo.P2, undo.A2);
        if (undo.Count > 3) Board.SetPiece(undo.P3, undo.A3);

        // Restaure EP state
        Board.SetPawnSkipPosition(Player.White, undo.PrevEpWhite);
        Board.SetPawnSkipPosition(Player.Black, undo.PrevEpBlack);
    }

    private static void CollectTouchedSquares(Move move, Board board, out byte count,
        out Position p0,
        out Position p1,
        out Position p2,
        out Position p3)
    {
        // From / To toujours touchées
        p0 = move.FromPos;
        p1 = move.ToPos;

        // Valeurs “placeholder” (elles ne seront utilisées que si count > 2/3)
        p2 = p0;
        p3 = p0;

        count = 2;

        // En passant : une 3e case (le pion capturé)
        if (move.Type == MoveType.EnPassant)
        {
            p2 = new Position(move.FromPos.Row, move.ToPos.Column);
            count = 3;
            return;
        }

        // Roque : détecté comme “roi qui bouge de 2 colonnes” (comme dans ton SAN)
        var piece = board[move.FromPos];
        if (piece != null && piece.Type == PieceType.King && Math.Abs(move.ToPos.Column - move.FromPos.Column) == 2)
        {
            int row = move.FromPos.Row;
            bool kingSide = move.ToPos.Column > move.FromPos.Column;

            var rookFrom = new Position(row, kingSide ? 7 : 0);
            var rookTo = new Position(row, kingSide ? 5 : 3);

            p2 = rookFrom;
            p3 = rookTo;
            count = 4;
        }
    }
}
