using ChessMultitool.Models;
using Newtonsoft.Json.Linq;
using System.Globalization;
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
        string playerUsername = UsernameEntry.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(playerUsername))
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
            // 1) Récupère les données de l’API
            string dataList = await GetGamesFromChessComAsync(playerUsername);

            // 2)  Formatte la liste de parties
            List<Game> currentGamesList = CreateFormattedGamesList(dataList, playerUsername);


            // 3) Affiche
            //DisplayStats(stats);
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
    private async Task<string> GetGamesFromChessComAsync(string username)
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
        //using var doc = JsonDocument.Parse(content);

        //// doc.RootElement.GetProperty("games") => array
        //if (!doc.RootElement.TryGetProperty("games", out JsonElement gamesProp) || gamesProp.ValueKind != JsonValueKind.Array)
        //{
        //    Console.WriteLine("DEBUG: no games or 'games' not found => returning empty list");
        //    return string.Empty;
        //}

        return content;
    }

    private List<Game> CreateFormattedGamesList(string data, string username)
    {
        var gamesToReturn = new List<Game>();

        var obj = JObject.Parse(data);
        var gamesArray = obj["games"] as JArray;

        foreach (var gameJson in gamesArray)
        {
            var gamePgn = gameJson["pgn"]?.ToString() ?? "";
            var white = gameJson["white"] as JObject;
            double accuracy = 0;

            // Si l'objet "accuracies" existe dans le JSON
            if (gameJson["accuracies"] != null && white != null)
            {
                var isWhitePlayer = (white["username"]?.ToString() ?? "") == username;
                accuracy = isWhitePlayer
                    ? (double)gameJson["accuracies"]["white"]
                    : (double)gameJson["accuracies"]["black"];
            }

            using (var reader = new StringReader(gamePgn))
            {
                string line;
                Game currentGame = null;

                while ((line = reader.ReadLine()) != null)
                {
                    if (line.StartsWith("[Event "))
                    {
                        // Dès qu'on detecte un nouvel Event,
                        // on ajoute le précédent s'il est plus récent que lastGameDateAndTime
                        if (currentGame != null)
                        {
                            gamesToReturn.Add(currentGame);
                        }
                        currentGame = new Game();
                    }

                    if (accuracy != 0 && currentGame != null && (currentGame.Accuracy ?? 0) == 0)
                    {
                        currentGame.Accuracy = accuracy;
                    }

                    if (currentGame != null && line.StartsWith("["))
                    {
                        var key = line.Substring(1, line.IndexOf(' ') - 1);
                        var value = line.Substring(
                            line.IndexOf('"') + 1,
                            line.LastIndexOf('"') - line.IndexOf('"') - 1);

                        switch (key)
                        {
                            case "Event":
                                currentGame.Event = value;
                                break;
                            case "Site":
                                currentGame.Site = value;
                                break;
                            case "Date":
                                currentGame.Date = DateTime.ParseExact(
                                    value, "yyyy.MM.dd", CultureInfo.InvariantCulture);
                                break;
                            case "Round":
                                currentGame.Round = value;
                                break;
                            case "White":
                                currentGame.White = value;
                                break;
                            case "Black":
                                currentGame.Black = value;
                                break;
                            case "Result":
                                currentGame.Result = value;
                                break;
                            case "WhiteElo":
                                currentGame.WhiteElo = int.Parse(value);
                                break;
                            case "BlackElo":
                                currentGame.BlackElo = int.Parse(value);
                                break;
                            case "TimeControl":
                                currentGame.TimeControl = value;
                                break;
                            case "EndTime":
                                currentGame.EndTime = TimeSpan.Parse(value);
                                if (currentGame.Date != DateTime.MinValue)
                                {
                                    currentGame.DateAndEndTime = currentGame.Date.Add(currentGame.EndTime);
                                }
                                break;
                            case "Termination":
                                currentGame.Termination = value;
                                break;
                            case "ECO":
                                currentGame.Eco = value;
                                break;
                            case "ECOUrl":
                                var parts = value.Split('/');
                                currentGame.Opening = parts.Last();
                                break;
                        }
                    }
                    else if (currentGame != null && !string.IsNullOrWhiteSpace(line))
                    {
                        currentGame.Moves = (currentGame.Moves ?? "") + line + " ";
                    }
                }

                if (currentGame != null)
                {
                    currentGame.PlayerElo =
                        (username == currentGame.White) ? currentGame.WhiteElo : currentGame.BlackElo;
                    currentGame.PlayerUsername = username;
                    currentGame.Moves = FormatMoves(currentGame.Moves);
                    currentGame.Category = SetCategoryFromTimeControl(currentGame.TimeControl);
                    currentGame.ResultForPlayer =
                        FindResultForPlayer(currentGame.Termination, currentGame.PlayerUsername);
                    currentGame.EndOfGameBy = HowEndedTheGame(currentGame.Termination);


                    gamesToReturn.Add(currentGame);

                }
            }
        }
        return gamesToReturn;
    }

    private static string FormatMoves(string moves)
    {
        if (moves == null) return string.Empty;
        string cleanedString = Regex.Replace(moves, "\\{[^}]+\\}", "");
        var movesArray = cleanedString.Split(" ");
        var filteredMoves = movesArray.Where(move => !move.Contains("...")).ToArray();
        return string.Join(" ", filteredMoves).Replace("  ", " ");
    }

    private static string FindResultForPlayer(string termination, string playerUsername)
    {
        if (termination.Contains("Partie nulle", StringComparison.OrdinalIgnoreCase)
            || termination.Contains("drawn", StringComparison.OrdinalIgnoreCase))
        {
            return "drawn";
        }
        else if (termination.Contains(playerUsername, StringComparison.OrdinalIgnoreCase))
        {
            return "won";
        }
        else
        {
            return "lost";
        }
    }

    private static string SetCategoryFromTimeControl(string timeControl)
    {
        return timeControl switch
        {
            "60" or "120" or "120+1" => "bullet",
            "180" or "180+2" or "300" => "blitz",
            "600" or "600+5" or "1800" => "rapid",
            _ => ""
        };
    }

    private static string HowEndedTheGame(string termination)
    {
        if (termination.Contains("temps", StringComparison.OrdinalIgnoreCase)
            || termination.Contains("time", StringComparison.OrdinalIgnoreCase))
            return "time";
        if (termination.Contains("échec et mat", StringComparison.OrdinalIgnoreCase)
            || termination.Contains("checkmate", StringComparison.OrdinalIgnoreCase))
            return "checkmate";
        if (termination.Contains("abandon", StringComparison.OrdinalIgnoreCase)
            || termination.Contains("resignation", StringComparison.OrdinalIgnoreCase))
            return "abandonment";
        if (termination.Contains("accord mutuel", StringComparison.OrdinalIgnoreCase)
            || termination.Contains("mutual agreement", StringComparison.OrdinalIgnoreCase))
            return "agreement";
        if (termination.Contains("manque de matériel", StringComparison.OrdinalIgnoreCase)
            || termination.Contains("insufficient material", StringComparison.OrdinalIgnoreCase))
            return "lack of equipment";
        if (termination.Contains("pat", StringComparison.OrdinalIgnoreCase)
            || termination.Contains("stalemate", StringComparison.OrdinalIgnoreCase))
            return "pat";
        if (termination.Contains("répétition", StringComparison.OrdinalIgnoreCase)
            || termination.Contains("repetition", StringComparison.OrdinalIgnoreCase))
            return "repeat";

        return "";
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
        NumGamesLabel.Text = $"Games played this month: {st.TotalGames}";

        // ELO
        BulletEloLabel.Text = st.BulletElo.ToString();
        BlitzEloLabel.Text = st.BlitzElo.ToString();
        RapidEloLabel.Text = st.RapidElo.ToString();

        // White
        WhiteStatsLabel.Text =
            $"White: Win {(st.PercentWinWhite * 100):0.0}% / Draw {(st.PercentDrawWhite * 100):0.0}% / Loss {(st.PercentLostWhite * 100):0.0}%";
        // Black
        BlackStatsLabel.Text =
            $"Black: Win {(st.PercentWinBlack * 100):0.0}% / Draw {(st.PercentDrawBlack * 100):0.0}% / Loss {(st.PercentLostBlack * 100):0.0}%";

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