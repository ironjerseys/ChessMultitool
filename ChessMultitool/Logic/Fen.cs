namespace ChessLogic;

public static class Fen
{
    // Create a GameState from a FEN string
    public static GameState FromFen(string fen)
    {
        // FEN: piece placement, active color, castling, en passant, halfmove, fullmove
        var parts = fen.Split(' ');
        if (parts.Length < 2) throw new ArgumentException("Invalid FEN");
        var board = new Board();

        // 1) Pieces
        var rows = parts[0].Split('/');
        if (rows.Length != 8) throw new ArgumentException("Invalid FEN rows");
        for (int r = 0; r < 8; r++)
        {
            string row = rows[r];
            int c = 0;
            foreach (char ch in row)
            {
                if (char.IsDigit(ch))
                {
                    c += (ch - '0');
                }
                else
                {
                    var (color, type) = FromFenPiece(ch);
                    board[r, c] = CreatePiece(color, type);
                    c++;
                }
            }
            if (c != 8) throw new ArgumentException("Invalid FEN row width");
        }

        // 2) Active color
        var active = parts[1] == "w" ? Player.White : Player.Black;
        var state = new GameState(active, board);

        // 3) Castling rights (ignored for now due to model using HasMoved flags)
        // 4) En passant target
        if (parts.Length >= 4 && parts[3] != "-")
        {
            var ep = ParseSquare(parts[3]);
            // Store as the skip position for the player who just moved
            // Board expects SetPawnSkipPosition( player , pos ) is the square behind double pawn push for the side to move's opponent
            // FEN en passant target is the square the pawn passed over; the capturer belongs to the side to move
            board.SetPawnSkipPosition(active.Opponent(), ep);
        }

        return state;
    }

    private static (Player color, PieceType type) FromFenPiece(char ch)
    {
        Player color = char.IsUpper(ch) ? Player.White : Player.Black;
        char p = char.ToLowerInvariant(ch);
        PieceType type = p switch
        {
            'k' => PieceType.King,
            'q' => PieceType.Queen,
            'r' => PieceType.Rook,
            'b' => PieceType.Bishop,
            'n' => PieceType.Knight,
            'p' => PieceType.Pawn,
            _ => throw new ArgumentException("Invalid piece")
        };
        return (color, type);
    }

    private static Piece CreatePiece(Player color, PieceType type) => type switch
    {
        PieceType.King => new King(color),
        PieceType.Queen => new Queen(color),
        PieceType.Rook => new Rook(color),
        PieceType.Bishop => new Bishop(color),
        PieceType.Knight => new Knight(color),
        PieceType.Pawn => new Pawn(color),
        _ => throw new ArgumentOutOfRangeException()
    };

    private static Position ParseSquare(string sq)
    {
        if (sq.Length != 2) throw new ArgumentException("Invalid square");
        int file = sq[0] - 'a';
        int rank = sq[1] - '1';
        int row = 7 - rank;
        int col = file;
        return new Position(row, col);
    }
}
