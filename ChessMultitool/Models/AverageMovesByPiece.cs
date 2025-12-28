namespace ChessMultitool.Models;

class AverageMovesByPiece
{
    public string PlayerUsername { get; set; } = string.Empty;

    public double AvgPawnMoves { get; set; } = 0;
    public double AvgKnightMoves { get; set; } = 0;
    public double AvgBishopMoves { get; set; } = 0;
    public double AvgRookMoves { get; set; } = 0;
    public double AvgQueenMoves { get; set; } = 0;
    public double AvgKingMoves { get; set; } = 0;
}
