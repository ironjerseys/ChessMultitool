namespace ChessMultitool.Models;

class ChessStats
{
    public int TotalGames { get; set; }
    public int BulletElo { get; set; }
    public int BlitzElo { get; set; }
    public int RapidElo { get; set; }

    public double PercentWinWhite { get; set; }
    public double PercentDrawWhite { get; set; }
    public double PercentLostWhite { get; set; }
    public double PercentWinBlack { get; set; }
    public double PercentDrawBlack { get; set; }
    public double PercentLostBlack { get; set; }

    public double E4WinRate { get; set; }
    public double D4WinRate { get; set; }

    public double PercentSameCastling { get; set; }
    public double PercentOppositeCastling { get; set; }

    public int LongestGameMoves { get; set; }
    public int ShortestGameMoves { get; set; }

    public double MeanMovesByPiece { get; set; }
}
