using System.Text.Json;
using System.Text.RegularExpressions;

namespace ChessMultitool;

public partial class StatsPage : ContentPage
{
    public StatsPage()
    {
        InitializeComponent();
    }

    private async void OnLoadStatsClicked(object sender, EventArgs e)
    {
        // Réinitialise l'affichage
        ErrorLabel.IsVisible = false;
        StatsContainer.IsVisible = false;

        // Récupère le username
        string username = UsernameEntry.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(username))
        {
            ErrorLabel.Text = "Please enter a username.";
            ErrorLabel.IsVisible = true;
            return;
        }

        // Démarre l'indicateur de chargement
        BusyIndicator.IsVisible = true;
        BusyIndicator.IsRunning = true;

        try
        {
            // 1) Récupère la liste des parties
            List<Game> games = await FetchGamesFromChessAPI(username);

            // 2) Calcule les stats
            ChessStats stats = ComputeStats(games);

            // 3) Affiche
            DisplayStats(stats);
        }
        catch (Exception ex)
        {
            ErrorLabel.Text = $"Error: {ex.Message}";
            ErrorLabel.IsVisible = true;
        }
        finally
        {
            // Arrête l'indicateur
            BusyIndicator.IsRunning = false;
            BusyIndicator.IsVisible = false;
        }
    }

    //====================
    // 1) APPEL API
    //====================
    private async Task<List<Game>> FetchGamesFromChessAPI(string username)
    {
        using var client = new HttpClient();

        DateTime now = DateTime.UtcNow;
        int year = now.Year;
        int month = now.Month; // 1-12

        // exemple: https://api.chess.com/pub/player/<username>/games/2023/09
        string url = $"https://api.chess.com/pub/player/{username}/games/{year}/{month:00}";
        Console.WriteLine($"DEBUG: calling API => {url}");

        HttpResponseMessage resp = await client.GetAsync(url);
        Console.WriteLine($"DEBUG: response code = {resp.StatusCode}");

        if (!resp.IsSuccessStatusCode)
            throw new Exception($"HTTP Error: {resp.StatusCode}");

        string content = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);

        // doc.RootElement.GetProperty("games") => array
        if (!doc.RootElement.TryGetProperty("games", out JsonElement gamesProp)
            || gamesProp.ValueKind != JsonValueKind.Array)
        {
            Console.WriteLine("DEBUG: no games or 'games' not found => returning empty list");
            return new List<Game>();
        }

        List<Game> results = new();
        foreach (JsonElement g in gamesProp.EnumerateArray())
        {
            Game parsed = ParseGame(g, username);
            results.Add(parsed);
        }

        Console.WriteLine($"DEBUG: total games fetched = {results.Count}");
        return results;
    }

    // Convertit un JSON => Game
    private Game ParseGame(JsonElement jsonMap, string myPseudo)
    {
        // On récupère les sous-propriétés "white" et "black"
        JsonElement whiteElt = jsonMap.TryGetProperty("white", out var w) ? w : default;
        JsonElement blackElt = jsonMap.TryGetProperty("black", out var b) ? b : default;

        // username
        string wUser = whiteElt.TryGetProperty("username", out var wUserProp)
            ? wUserProp.GetString()?.ToLower() ?? "" : "";
        string bUser = blackElt.TryGetProperty("username", out var bUserProp)
            ? bUserProp.GetString()?.ToLower() ?? "" : "";
        string pseudo = myPseudo.ToLower();

        // Couleur 
        string color = (bUser == pseudo) ? "black" : "white";

        // Résultat
        string whiteRes = whiteElt.TryGetProperty("result", out var wResProp)
            ? wResProp.GetString() ?? "" : "";
        string blackRes = blackElt.TryGetProperty("result", out var bResProp)
            ? bResProp.GetString() ?? "" : "";

        string myRes = (color == "white")
            ? ConvertResult(whiteRes)
            : ConvertResult(blackRes);

        // Elo
        int whiteElo = whiteElt.TryGetProperty("rating", out var wEloProp)
            ? wEloProp.GetInt32() : 0;
        int blackElo = blackElt.TryGetProperty("rating", out var bEloProp)
            ? bEloProp.GetInt32() : 0;
        int myElo = (color == "white") ? whiteElo : blackElo;

        // timeControl
        string timeControl = jsonMap.TryGetProperty("time_control", out var tcProp)
            ? tcProp.GetString() ?? "" : "";

        // end_time
        int endTime = jsonMap.TryGetProperty("end_time", out var endTimeProp)
            ? endTimeProp.GetInt32() : 0;

        // PGN => extraire la liste de coups
        string pgn = jsonMap.TryGetProperty("pgn", out var pgnProp)
            ? pgnProp.GetString() ?? "" : "";
        List<string> movesList = ExtractMovesFromPgn(pgn);

        return new Game
        {
            MyColor = color,
            MyResult = myRes,
            MyElo = myElo,
            TimeControl = timeControl,
            Moves = movesList,
            EndTime = endTime
        };
    }

    private string ConvertResult(string raw)
    {
        return raw switch
        {
            "win" => "win",
            "draw" => "draw",
            _ => "lost"
        };
    }

    private List<string> ExtractMovesFromPgn(string pgn)
    {
        // Regex similaire à Flutter
        // ex. on cherche "1.e4", "1...e5", etc.
        var regex = new Regex(@"(\d+\.+\s*[^\s]+)");
        var matches = regex.Matches(pgn);
        return matches.Select(m => m.Value).ToList();
    }

    //====================
    // 2) CALCUL STATS
    //====================
    private ChessStats ComputeStats(List<Game> games)
    {
        // On reproduit la logique Flutter
        int totalGames = games.Count;

        int bulletMostRecent = 0;
        int bulletElo = 0;

        int blitzMostRecent = 0;
        int blitzElo = 0;

        int rapidMostRecent = 0;
        int rapidElo = 0;

        int whiteWins = 0, whiteDraws = 0, whiteLosses = 0;
        int blackWins = 0, blackDraws = 0, blackLosses = 0;

        int e4Wins = 0, e4Draws = 0, e4Losses = 0, e4Count = 0;
        int d4Wins = 0, d4Draws = 0, d4Losses = 0, d4Count = 0;

        int minMoves = int.MaxValue;
        int maxMoves = 0;

        foreach (var g in games)
        {
            string cat = SetCategoryFromTimeControl(g.TimeControl);

            // Elo
            if (cat == "bullet")
            {
                if (g.EndTime > bulletMostRecent)
                {
                    bulletMostRecent = g.EndTime;
                    bulletElo = g.MyElo;
                }
            }
            else if (cat == "blitz")
            {
                if (g.EndTime > blitzMostRecent)
                {
                    blitzMostRecent = g.EndTime;
                    blitzElo = g.MyElo;
                }
            }
            else if (cat == "rapid")
            {
                if (g.EndTime > rapidMostRecent)
                {
                    rapidMostRecent = g.EndTime;
                    rapidElo = g.MyElo;
                }
            }

            // White / Black
            if (g.MyColor == "white")
            {
                switch (g.MyResult)
                {
                    case "win": whiteWins++; break;
                    case "draw": whiteDraws++; break;
                    default: whiteLosses++; break;
                }
            }
            else
            {
                switch (g.MyResult)
                {
                    case "win": blackWins++; break;
                    case "draw": blackDraws++; break;
                    default: blackLosses++; break;
                }
            }

            // e4 / d4
            if (g.Moves.Any())
            {
                string first = g.Moves.First().ToLower();
                if (first.Contains("1.e4"))
                {
                    e4Count++;
                    switch (g.MyResult)
                    {
                        case "win": e4Wins++; break;
                        case "draw": e4Draws++; break;
                        default: e4Losses++; break;
                    }
                }
                else if (first.Contains("1.d4"))
                {
                    d4Count++;
                    switch (g.MyResult)
                    {
                        case "win": d4Wins++; break;
                        case "draw": d4Draws++; break;
                        default: d4Losses++; break;
                    }
                }
            }

            // min/max moves
            int movesCount = g.Moves.Count;
            if (movesCount < minMoves) minMoves = movesCount;
            if (movesCount > maxMoves) maxMoves = movesCount;
        }

        int whiteTotal = whiteWins + whiteDraws + whiteLosses;
        double percentWinWhite = (whiteTotal == 0) ? 0 : (double)whiteWins / whiteTotal;
        double percentDrawWhite = (whiteTotal == 0) ? 0 : (double)whiteDraws / whiteTotal;
        double percentLostWhite = (whiteTotal == 0) ? 0 : (double)whiteLosses / whiteTotal;

        int blackTotal = blackWins + blackDraws + blackLosses;
        double percentWinBlack = (blackTotal == 0) ? 0 : (double)blackWins / blackTotal;
        double percentDrawBlack = (blackTotal == 0) ? 0 : (double)blackDraws / blackTotal;
        double percentLostBlack = (blackTotal == 0) ? 0 : (double)blackLosses / blackTotal;

        double e4WinRate = (e4Count == 0) ? 0 : (double)e4Wins / e4Count;
        double d4WinRate = (d4Count == 0) ? 0 : (double)d4Wins / d4Count;

        int longestGame = maxMoves == 0 ? 0 : maxMoves;
        int shortestGame = minMoves == int.MaxValue ? 0 : minMoves;

        // placeholders
        double percentSameCastling = 0.0;
        double percentOppositeCastling = 0.0;
        double meanMovesByPiece = 0.0;

        return new ChessStats
        {
            TotalGames = totalGames,
            BulletElo = bulletElo,
            BlitzElo = blitzElo,
            RapidElo = rapidElo,

            PercentWinWhite = percentWinWhite,
            PercentDrawWhite = percentDrawWhite,
            PercentLostWhite = percentLostWhite,
            PercentWinBlack = percentWinBlack,
            PercentDrawBlack = percentDrawBlack,
            PercentLostBlack = percentLostBlack,

            E4WinRate = e4WinRate,
            D4WinRate = d4WinRate,

            PercentSameCastling = percentSameCastling,
            PercentOppositeCastling = percentOppositeCastling,

            LongestGameMoves = longestGame,
            ShortestGameMoves = shortestGame,
            MeanMovesByPiece = meanMovesByPiece
        };
    }

    private string SetCategoryFromTimeControl(string? timeControl)
    {
        switch (timeControl)
        {
            case "60":
            case "120":
            case "120+1":
                return "bullet";
            case "180":
            case "180+2":
            case "300":
                return "blitz";
            case "600":
            case "600+5":
            case "1800":
                return "rapid";
            default:
                return "";
        }
    }

    //====================
    // 3) AFFICHER
    //====================
    private void DisplayStats(ChessStats st)
    {
        if (st.TotalGames == 0)
        {
            NumGamesLabel.Text = "No games found this month.";
            StatsContainer.IsVisible = true;
            return;
        }

        // Nombre de parties
        NumGamesLabel.Text = $"Number of Games: {st.TotalGames}";

        // ELO
        BulletEloLabel.Text = st.BulletElo.ToString();
        BlitzEloLabel.Text = st.BlitzElo.ToString();
        RapidEloLabel.Text = st.RapidElo.ToString();

        // White
        WhiteStatsLabel.Text =
            $"White: W {(st.PercentWinWhite * 100):0.0}% / D {(st.PercentDrawWhite * 100):0.0}% / L {(st.PercentLostWhite * 100):0.0}%";
        // Black
        BlackStatsLabel.Text =
            $"Black: W {(st.PercentWinBlack * 100):0.0}% / D {(st.PercentDrawBlack * 100):0.0}% / L {(st.PercentLostBlack * 100):0.0}%";

        // e4 / d4
        E4Label.Text = $"1.e4 Win rate: {(st.E4WinRate * 100):0.0}%";
        D4Label.Text = $"1.d4 Win rate: {(st.D4WinRate * 100):0.0}%";

        // placeholders
        SameSideLabel.Text = $"Same-side castling: {(st.PercentSameCastling * 100):0.0}%";
        OppositeSideLabel.Text = $"Opposite castling: {(st.PercentOppositeCastling * 100):0.0}%";

        LongestGameLabel.Text = $"Longest game (moves): {st.LongestGameMoves}";
        ShortestGameLabel.Text = $"Shortest game (moves): {st.ShortestGameMoves}";
        MeanMovesLabel.Text = $"Avg moves/piece: {st.MeanMovesByPiece:0.00}";

        // Affiche la section stats
        StatsContainer.IsVisible = true;
    }
}

//============================
// CLASSES MODELES
//============================

public class Game
{
    public string MyColor { get; set; } = "";
    public string MyResult { get; set; } = "";
    public int MyElo { get; set; }
    public string TimeControl { get; set; } = "";
    public List<string> Moves { get; set; } = new();
    public int EndTime { get; set; }
}

public class ChessStats
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
