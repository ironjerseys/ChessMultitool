using ChessLogic;

namespace ChessMultitool.Logic;

public static class Images
{
    private static readonly Dictionary<PieceType, ImageSource> whiteSources = new()
    {
        { PieceType.Pawn, LoadImage("pawnw.png") },
        { PieceType.Bishop, LoadImage("bishopw.png") },
        { PieceType.Knight, LoadImage("knightw.png") },
        { PieceType.Rook, LoadImage("rookw.png") },
        { PieceType.Queen, LoadImage("queenw.png") },
        { PieceType.King, LoadImage("kingw.png") }
    };

    private static readonly Dictionary<PieceType, ImageSource> blackSources = new()
    {
        { PieceType.Pawn, LoadImage("pawnb.png") },
        { PieceType.Bishop, LoadImage("bishopb.png") },
        { PieceType.Knight, LoadImage("knightb.png") },
        { PieceType.Rook, LoadImage("rookb.png") },
        { PieceType.Queen, LoadImage("queenb.png") },
        { PieceType.King, LoadImage("kingb.png") }
    };

    private static ImageSource LoadImage(string filePath)
    {
        return ImageSource.FromFile(filePath);
    }

    public static ImageSource GetImage(Player color, PieceType type)
    {
        return color switch
        {
            Player.White => whiteSources[type],
            Player.Black => blackSources[type],
            _ => null
        };
    }

    public static ImageSource GetImage(Piece piece)
    {
        if (piece == null)
        {
            return null;
        }

        return GetImage(piece.Color, piece.Type);
    }
}
