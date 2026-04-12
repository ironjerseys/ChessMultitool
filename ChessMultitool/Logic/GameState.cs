namespace ChessLogic;

public class GameState
{
    public Board Board { get; }
    public Player CurrentPlayer { get; private set; }
    public Result Result { get; private set; } = null;

    private int noCaptureOrPawnMoves = 0;
    private string stateString;

    private readonly Dictionary<string, int> stateHistory = new Dictionary<string, int>();

    // ── Zobrist Hashing ──────────────────────────────────────────────────────
    // Piece index: (int)PieceType * 2 + (White=0, Black=1)  →  12 values (0..11)
    // Note: castle rights intentionally excluded — handled via piece positions.
    private static readonly ulong[,,] ZobristPieceKeys = new ulong[8, 8, 12];
    private static readonly ulong     ZobristSideKey;
    private static readonly ulong[,]  ZobristEpKeys = new ulong[2, 8]; // [playerIdx, col]

    private ulong _hash;
    public  ulong ZobristHash => _hash;

    static GameState()
    {
        var rng = new Random(0x5EED_BEEF);
        ulong NextUlong()
        {
            ulong hi = (ulong)(uint)rng.Next() << 32;
            ulong lo = (ulong)(uint)rng.Next();
            return hi | lo;
        }

        for (int r = 0; r < 8; r++)
        for (int c = 0; c < 8; c++)
        for (int p = 0; p < 12; p++)
            ZobristPieceKeys[r, c, p] = NextUlong();

        ZobristSideKey = NextUlong();

        for (int pl = 0; pl < 2; pl++)
        for (int col = 0; col < 8; col++)
            ZobristEpKeys[pl, col] = NextUlong();
    }

    private static int PieceIndex(Piece piece)
        => (int)piece.Type * 2 + (piece.Color == Player.White ? 0 : 1);

    // Used only for the initial hash in the constructor (O(64) is fine once).
    private ulong ComputeHash()
    {
        ulong h = 0;
        for (int r = 0; r < 8; r++)
        for (int c = 0; c < 8; c++)
        {
            var piece = Board[r, c];
            if (piece != null)
                h ^= ZobristPieceKeys[r, c, PieceIndex(piece)];
        }
        if (CurrentPlayer == Player.Black) h ^= ZobristSideKey;
        var epW = Board.GetPawnSkipPosition(Player.White);
        if (epW != null) h ^= ZobristEpKeys[0, epW.Column];
        var epB = Board.GetPawnSkipPosition(Player.Black);
        if (epB != null) h ^= ZobristEpKeys[1, epB.Column];
        return h;
    }
    // ─────────────────────────────────────────────────────────────────────────

    public GameState(Player player, Board board)
    {
        CurrentPlayer = player;
        Board = board;

        stateString = new StateString(CurrentPlayer, board).ToString();
        stateHistory[stateString] = 1;

        _hash = ComputeHash();
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
    /// </summary>
    public readonly struct SearchUndo
    {
        public readonly Player PrevPlayer;

        public readonly Position? PrevEpWhite;
        public readonly Position? PrevEpBlack;

        public readonly ulong OldHash;

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
            ulong oldHash,
            byte count,
            Position p0, Piece? a0,
            Position p1, Piece? a1,
            Position p2, Piece? a2,
            Position p3, Piece? a3)
        {
            PrevPlayer  = prevPlayer;
            PrevEpWhite = prevEpWhite;
            PrevEpBlack = prevEpBlack;
            OldHash     = oldHash;
            Count       = count;

            P0 = p0; A0 = a0;
            P1 = p1; A1 = a1;
            P2 = p2; A2 = a2;
            P3 = p3; A3 = a3;
        }
    }

    /// <summary>
    /// Applique un coup pour le moteur (sans historique, sans règles de fin).
    /// Réversible via UnmakeMoveFast.
    /// Le hash Zobrist est mis à jour de façon incrémentale (O(4) au lieu de O(64)).
    /// </summary>
    public void MakeMoveFast(Move move, out SearchUndo undo)
    {
        var prevPlayer  = CurrentPlayer;
        var prevEpWhite = Board.GetPawnSkipPosition(Player.White);
        var prevEpBlack = Board.GetPawnSkipPosition(Player.Black);
        ulong prevHash  = _hash;

        CollectTouchedSquares(move, Board, out byte count,
            out Position p0, out Position p1, out Position p2, out Position p3);

        // Sauvegarder les pièces AVANT le coup (pour undo et pour hash XOR)
        Piece? a0 = null, a1 = null, a2 = null, a3 = null;
        if (count > 0) a0 = Board[p0]?.Copy();
        if (count > 1) a1 = Board[p1]?.Copy();
        if (count > 2) a2 = Board[p2]?.Copy();
        if (count > 3) a3 = Board[p3]?.Copy();

        undo = new SearchUndo(
            prevPlayer, prevEpWhite, prevEpBlack, prevHash, count,
            p0, a0, p1, a1, p2, a2, p3, a3);

        // ── Phase 1 : XOR out état AVANT le coup ─────────────────────────────
        if (a0 != null) _hash ^= ZobristPieceKeys[p0.Row, p0.Column, PieceIndex(a0)];
        if (a1 != null) _hash ^= ZobristPieceKeys[p1.Row, p1.Column, PieceIndex(a1)];
        if (count > 2 && a2 != null) _hash ^= ZobristPieceKeys[p2.Row, p2.Column, PieceIndex(a2)];
        if (count > 3 && a3 != null) _hash ^= ZobristPieceKeys[p3.Row, p3.Column, PieceIndex(a3)];
        if (prevEpWhite != null) _hash ^= ZobristEpKeys[0, prevEpWhite.Column];
        if (prevEpBlack != null) _hash ^= ZobristEpKeys[1, prevEpBlack.Column];

        // ── Exécution du coup ─────────────────────────────────────────────────
        Board.SetPawnSkipPosition(CurrentPlayer, null);
        _ = move.Execute(Board);
        CurrentPlayer = CurrentPlayer.Opponent();

        // ── Phase 2 : XOR in état APRÈS le coup ──────────────────────────────
        var np0 = Board[p0]; if (np0 != null) _hash ^= ZobristPieceKeys[p0.Row, p0.Column, PieceIndex(np0)];
        var np1 = Board[p1]; if (np1 != null) _hash ^= ZobristPieceKeys[p1.Row, p1.Column, PieceIndex(np1)];
        if (count > 2) { var np2 = Board[p2]; if (np2 != null) _hash ^= ZobristPieceKeys[p2.Row, p2.Column, PieceIndex(np2)]; }
        if (count > 3) { var np3 = Board[p3]; if (np3 != null) _hash ^= ZobristPieceKeys[p3.Row, p3.Column, PieceIndex(np3)]; }
        _hash ^= ZobristSideKey;
        var newEpW = Board.GetPawnSkipPosition(Player.White);
        var newEpB = Board.GetPawnSkipPosition(Player.Black);
        if (newEpW != null) _hash ^= ZobristEpKeys[0, newEpW.Column];
        if (newEpB != null) _hash ^= ZobristEpKeys[1, newEpB.Column];
    }

    /// <summary>
    /// Annule un coup appliqué par MakeMoveFast.
    /// </summary>
    public void UnmakeMoveFast(in SearchUndo undo)
    {
        CurrentPlayer = undo.PrevPlayer;

        if (undo.Count > 0) Board.SetPiece(undo.P0, undo.A0);
        if (undo.Count > 1) Board.SetPiece(undo.P1, undo.A1);
        if (undo.Count > 2) Board.SetPiece(undo.P2, undo.A2);
        if (undo.Count > 3) Board.SetPiece(undo.P3, undo.A3);

        Board.SetPawnSkipPosition(Player.White, undo.PrevEpWhite);
        Board.SetPawnSkipPosition(Player.Black, undo.PrevEpBlack);

        _hash = undo.OldHash;
    }

    // ── Null Move (pour le pruning dans la recherche) ────────────────────────
    public readonly struct NullMoveUndo
    {
        public readonly Player    PrevPlayer;
        public readonly Position? PrevEpWhite;
        public readonly Position? PrevEpBlack;
        public readonly ulong     OldHash;

        public NullMoveUndo(Player prevPlayer, Position? prevEpWhite, Position? prevEpBlack, ulong oldHash)
        {
            PrevPlayer  = prevPlayer;
            PrevEpWhite = prevEpWhite;
            PrevEpBlack = prevEpBlack;
            OldHash     = oldHash;
        }
    }

    /// <summary>
    /// Passe le trait à l'adversaire sans jouer de coup (null move).
    /// L'opportunité en-passant du camp au trait expire.
    /// Le hash est mis à jour de façon incrémentale.
    /// </summary>
    public void MakeNullMove(out NullMoveUndo undo)
    {
        var prevPlayer  = CurrentPlayer;
        var prevEpWhite = Board.GetPawnSkipPosition(Player.White);
        var prevEpBlack = Board.GetPawnSkipPosition(Player.Black);
        ulong prevHash  = _hash;

        // XOR out l'EP du camp au trait (il expire)
        var myEp = Board.GetPawnSkipPosition(CurrentPlayer);
        if (myEp != null)
        {
            int playerIdx = CurrentPlayer == Player.White ? 0 : 1;
            _hash ^= ZobristEpKeys[playerIdx, myEp.Column];
        }

        Board.SetPawnSkipPosition(CurrentPlayer, null);
        _hash ^= ZobristSideKey;
        CurrentPlayer = CurrentPlayer.Opponent();

        undo = new NullMoveUndo(prevPlayer, prevEpWhite, prevEpBlack, prevHash);
    }

    /// <summary>
    /// Annule un null move appliqué par MakeNullMove.
    /// </summary>
    public void UnmakeNullMove(in NullMoveUndo undo)
    {
        CurrentPlayer = undo.PrevPlayer;
        Board.SetPawnSkipPosition(Player.White, undo.PrevEpWhite);
        Board.SetPawnSkipPosition(Player.Black, undo.PrevEpBlack);
        _hash = undo.OldHash;
    }
    // ─────────────────────────────────────────────────────────────────────────

    private static void CollectTouchedSquares(Move move, Board board, out byte count,
        out Position p0,
        out Position p1,
        out Position p2,
        out Position p3)
    {
        p0 = move.FromPos;
        p1 = move.ToPos;
        p2 = p0;
        p3 = p0;

        count = 2;

        if (move.Type == MoveType.EnPassant)
        {
            p2 = new Position(move.FromPos.Row, move.ToPos.Column);
            count = 3;
            return;
        }

        var piece = board[move.FromPos];
        if (piece != null && piece.Type == PieceType.King && Math.Abs(move.ToPos.Column - move.FromPos.Column) == 2)
        {
            int row = move.FromPos.Row;
            bool kingSide = move.ToPos.Column > move.FromPos.Column;

            var rookFrom = new Position(row, kingSide ? 7 : 0);
            var rookTo   = new Position(row, kingSide ? 5 : 3);

            p2 = rookFrom;
            p3 = rookTo;
            count = 4;
        }
    }
}
