using Microsoft.Maui.Storage;
using Newtonsoft.Json;

namespace ChessMultitool.Services;

public static class AchievementService
{
    private const string PrefKey = "achievements_json";

    // achievements map: key -> count (e.g., "win_with:Italian Game" = 3)
    private static Dictionary<string, int> cache;

    // openings: opening -> variation -> list of normalized moves
    private static Dictionary<string, Dictionary<string, List<string>>> openings;

    private static readonly int[] AiRatings = new[] { 200, 400, 600, 800, 1000 };

    public static async Task InitializeAsync()
    {
        if (cache == null)
        {
            var json = Preferences.Get(PrefKey, string.Empty);
            cache = string.IsNullOrWhiteSpace(json)
                ? new Dictionary<string, int>()
                : JsonConvert.DeserializeObject<Dictionary<string, int>>(json) ?? new Dictionary<string, int>();
        }
        if (openings == null)
        {
            try
            {
                using var stream = await FileSystem.OpenAppPackageFileAsync("openings.json");
                using var reader = new StreamReader(stream);
                string json = await reader.ReadToEndAsync();
                var raw = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, Dictionary<string, List<string>>>>>(json);
                var root = raw["openings"]; // ouverture -> variation -> moves
                // Normalize all moves once
                openings = root.ToDictionary(
                    o => o.Key,
                    o => o.Value.ToDictionary(
                        v => v.Key,
                        v => v.Value.Select(Sanitize).ToList()
                    ));
            }
            catch
            {
                openings = new();
            }
        }
    }

    public static async Task AddAiGameByRatingAsync(int rating, bool won)
    {
        await InitializeAsync();
        int bucket = ClosestRatingBucket(rating);
        Inc($"ai_games_rating:{bucket}");
        if (won) Inc($"ai_win_rating:{bucket}");
    }

    private static int ClosestRatingBucket(int rating)
    {
        int closest = AiRatings[0];
        int minDiff = Math.Abs(rating - closest);
        foreach (var r in AiRatings)
        {
            int d = Math.Abs(rating - r);
            if (d < minDiff) { minDiff = d; closest = r; }
        }
        return closest;
    }

    public static async Task<(int wins, int games)> GetAiStatsForRatingAsync(int rating)
    {
        await InitializeAsync();
        int bucket = ClosestRatingBucket(rating);
        int wins = cache.TryGetValue($"ai_win_rating:{bucket}", out var w) ? w : 0;
        int games = cache.TryGetValue($"ai_games_rating:{bucket}", out var g) ? g : 0;
        return (wins, games);
    }

    public static async Task<Dictionary<int, (int wins, int games)>> GetAllAiStatsByRatingAsync()
    {
        await InitializeAsync();
        var dict = new Dictionary<int, (int wins, int games)>();
        foreach (var r in AiRatings)
        {
            var (w, g) = await GetAiStatsForRatingAsync(r);
            dict[r] = (w, g);
        }
        return dict;
    }

    public static async Task AddSelectedOpeningWinAsync(string openingName)
    {
        if (string.IsNullOrWhiteSpace(openingName)) return;
        await InitializeAsync();
        Inc($"opening_selected_win:{openingName}");
    }

    public static async Task AddWinWithAsync(string openingName)
    {
        if (string.IsNullOrWhiteSpace(openingName)) return;
        await InitializeAsync();
        Inc($"win_with:{openingName}");
    }

    public static async Task AddWinAgainstAsync(string openingName)
    {
        if (string.IsNullOrWhiteSpace(openingName)) return;
        await InitializeAsync();
        Inc($"win_against:{openingName}");
    }

    public static async Task AddQuickWinAsync(int thresholdHalfMoves)
    {
        await InitializeAsync();
        Inc($"ai_quick_win_under:{thresholdHalfMoves}");
    }

    private static void Inc(string key)
    {
        if (!cache.ContainsKey(key)) cache[key] = 0;
        cache[key]++;
        Preferences.Set(PrefKey, JsonConvert.SerializeObject(cache));
    }

    public static async Task<Dictionary<string, int>> GetAllAsync()
    {
        await InitializeAsync();
        // return copy
        return new Dictionary<string, int>(cache);
    }

    public static async Task<IReadOnlyList<string>> GetAllOpeningNamesAsync()
    {
        await InitializeAsync();
        return openings?.Keys?.OrderBy(n => n).ToList() ?? new List<string>();
    }

    public static async Task<string> IdentifyOpeningFromMovesAsync(IEnumerable<string> movesNormalized)
    {
        await InitializeAsync();
        if (openings == null || openings.Count == 0) return null;
        var seq = movesNormalized?.ToList() ?? new();
        if (seq.Count == 0) return null;

        string bestOpening = null;
        int bestLen = 0;
        foreach (var (opening, vars) in openings)
        {
            foreach (var line in vars.Values)
            {
                int len = PrefixLen(seq, line);
                if (len > bestLen && len >= 2) // require at least 2 half-moves match
                {
                    bestLen = len;
                    bestOpening = opening;
                }
            }
        }
        return bestOpening;
    }

    private static int PrefixLen(IList<string> a, IList<string> b)
    {
        int n = Math.Min(a.Count, b.Count);
        int i = 0;
        for (; i < n; i++)
        {
            if (!string.Equals(a[i], b[i], StringComparison.Ordinal)) break;
        }
        return i;
    }

    public static string Sanitize(string fullMove)
    {
        if (string.IsNullOrWhiteSpace(fullMove)) return string.Empty;
        string s = fullMove.Replace(" ", string.Empty);
        var partsDot = s.Split('.');
        string alg = partsDot.Length > 1 ? partsDot[^1] : s;
        alg = alg.Replace("...", string.Empty).Trim();
        if (alg.Contains('x'))
        {
            var parts = alg.Split('x');
            if (!string.IsNullOrEmpty(parts[0]) && char.IsUpper(parts[0][0]))
                alg = parts[0][0] + parts[1];
            else
                alg = parts[1];
        }
        return alg;
    }
}
