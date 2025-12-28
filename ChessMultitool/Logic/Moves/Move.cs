namespace ChessLogic;

public abstract class Move
{
    public abstract MoveType Type { get; }
    public abstract Position FromPos { get; }
    public abstract Position ToPos { get; }

    public abstract bool Execute(Board board);

    public virtual bool IsLegal(Board board)
    {
        Player player = board[FromPos].Color;
        Board boardCopy = board.Copy();
        Execute(boardCopy);
        return !boardCopy.IsInCheck(player);
    }

    public string ToAlgebraic(Board board)
    {
        // version ultra-simple : on renvoie
        //  - “e4” pour un pion
        //  - “Nf3”, “Bc4”, etc. pour pièces
        //  - sans indications + ni # ni prise “x”
        char file = (char)('a' + ToPos.Column);
        int rank = 8 - ToPos.Row;

        string pieceLetter = Type == MoveType.Normal && board[FromPos].Type == PieceType.Pawn
            ? string.Empty
            : board[FromPos].Type switch
            {
                PieceType.Knight => "N",
                PieceType.Bishop => "B",
                PieceType.Rook => "R",
                PieceType.Queen => "Q",
                PieceType.King => "K",
                _ => string.Empty
            };

        return $"{pieceLetter}{file}{rank}";
    }
}
