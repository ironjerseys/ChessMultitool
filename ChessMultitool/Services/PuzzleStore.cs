using ChessMultitool.Models;

namespace ChessMultitool.Services;

/// <summary>
/// Shared in-memory store so PuzzlesPage and PuzzleListPage work on the same data.
/// PuzzlesPage loads the puzzles and publishes them here; the library page reads them
/// and can request a specific puzzle via PendingIndex.
/// </summary>
public static class PuzzleStore
{
    public static List<LichessPuzzle> Puzzles { get; set; } = new();

    /// <summary>Index of the puzzle currently shown on PuzzlesPage.</summary>
    public static int CurrentIndex { get; set; }

    /// <summary>Set by the library page; consumed by PuzzlesPage on next appearing.</summary>
    public static int? PendingIndex { get; set; }

    public static event Action? PuzzlesLoaded;

    public static void NotifyLoaded() => PuzzlesLoaded?.Invoke();
}
