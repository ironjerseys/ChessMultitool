using System.Text.Json;
using ChessMultitool.Models;
using ChessLogic;

namespace ChessMultitool.Services;

public static class PuzzlesService
{
    private static readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web);

    private const string CacheFile = "puzzles_cache.json";
    private const string ProgressKey = "puzzles_progress_index";
    private const string CsvFile = "lichess_db_puzzle.csv"; // bundled in Resources/Raw with LogicalName set

    public static async Task<List<LichessPuzzle>> EnsurePuzzlesCachedAsync(int minCount = 100, CancellationToken ct = default)
    {
        var cached = await TryLoadCacheAsync();
        if (cached.Count >= minCount)
            return cached;

        var list = await LoadFromCsvAsync(minCount, ct);
        if (list.Count > 0)
            await SaveCacheAsync(list);
        return list;
    }

    public static int GetProgressIndex() => Preferences.Get(ProgressKey, 0);
    public static void SaveProgressIndex(int index) => Preferences.Set(ProgressKey, index);

    private static async Task<List<LichessPuzzle>> TryLoadCacheAsync()
    {
        try
        {
            var path = Path.Combine(FileSystem.AppDataDirectory, CacheFile);
            if (!File.Exists(path)) return new List<LichessPuzzle>();
            var json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<List<LichessPuzzle>>(json, jsonOptions) ?? new List<LichessPuzzle>();
        }
        catch { return new List<LichessPuzzle>(); }
    }

    private static async Task SaveCacheAsync(List<LichessPuzzle> puzzles)
    {
        try
        {
            var path = Path.Combine(FileSystem.AppDataDirectory, CacheFile);
            var json = JsonSerializer.Serialize(puzzles, jsonOptions);
            await File.WriteAllTextAsync(path, json);
        }
        catch { }
    }

    private static async Task<List<LichessPuzzle>> LoadFromCsvAsync(int maxCount, CancellationToken ct)
    {
        var result = new List<LichessPuzzle>(maxCount);
        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync(CsvFile);
            using var reader = new StreamReader(stream);
            // skip header
            string? line = await reader.ReadLineAsync();
            while (!reader.EndOfStream && result.Count < maxCount)
            {
                ct.ThrowIfCancellationRequested();
                line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;
                var p = ParseCsvLine(line);
                if (p != null) result.Add(p);
            }
        }
        catch { }
        return result;
    }

    // Expected columns (standard lichess puzzles CSV):
    // PuzzleId,FEN,Moves,Rating,RatingDeviation,Popularity,NbPlays,Themes,GameUrl,OpeningTags
    private static LichessPuzzle? ParseCsvLine(string line)
    {
        // Basic CSV split (columns do not contain commas in standard dataset)
        var parts = line.Split(',');
        if (parts.Length < 3) return null;
        try
        {
            string id = parts[0].Trim();
            string fen = parts[1].Trim();
            string moves = parts[2].Trim();
            int rating = 1500;
            if (parts.Length >= 4) int.TryParse(parts[3], out rating);
            string[] themes = Array.Empty<string>();
            if (parts.Length >= 8)
            {
                themes = parts[7].Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            }
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(fen) || string.IsNullOrWhiteSpace(moves))
                return null;
            return new LichessPuzzle
            {
                Id = id,
                Fen = fen,
                Moves = moves,
                Rating = rating,
                Themes = themes
            };
        }
        catch { return null; }
    }

    public static GameState LoadPuzzlePosition(LichessPuzzle p) => Fen.FromFen(p.Fen);

    public static IEnumerable<string> GetSolutionMoves(LichessPuzzle p) => (p.Moves ?? string.Empty)
        .Split(' ', StringSplitOptions.RemoveEmptyEntries);
}
