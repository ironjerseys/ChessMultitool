namespace ChessMultitool.Models;

public class LichessPuzzle
{
    public string Id { get; set; } = string.Empty;
    public string Fen { get; set; } = string.Empty;
    public string Moves { get; set; } = string.Empty; // space-separated UCI
    public int Rating { get; set; }
    public string[] Themes { get; set; } = Array.Empty<string>();
}
