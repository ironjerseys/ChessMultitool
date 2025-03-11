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

            var averageMovesByPieces = GetAverageMovesByPieceAsync(currentGamesList, playerUsername);

            // 3) Affiche
            DisplayStats(currentGamesList, averageMovesByPieces, playerUsername);
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

    private AverageMovesByPiece GetAverageMovesByPieceAsync(List<Game> games, string playerUsername)
    {

        int totalGames = games.Count;

        int pawnMoves = 0, knightMoves = 0, bishopMoves = 0;
        int rookMoves = 0, queenMoves = 0, kingMoves = 0;

        // On compte les coups pour chaque partie
        foreach (var game in games)
        {
            var movesDict = CountPieceMoves(game.Moves);
            pawnMoves += movesDict["pawn"];
            knightMoves += movesDict["knight"];
            bishopMoves += movesDict["bishop"];
            rookMoves += movesDict["rook"];
            queenMoves += movesDict["queen"];
            kingMoves += movesDict["king"];
        }

        // On calcule les moyennes
        var averageMoves = new AverageMovesByPiece
        {
            PlayerUsername = playerUsername,
            AvgPawnMoves = (double)pawnMoves / totalGames,
            AvgKnightMoves = (double)knightMoves / totalGames,
            AvgBishopMoves = (double)bishopMoves / totalGames,
            AvgRookMoves = (double)rookMoves / totalGames,
            AvgQueenMoves = (double)queenMoves / totalGames,
            AvgKingMoves = (double)kingMoves / totalGames
        };

        return averageMoves;
    }

    private Dictionary<string, int> CountPieceMoves(string movesString)
    {
        var movesCount = new Dictionary<string, int>
            {
                { "pawn",   0 },
                { "knight", 0 },
                { "bishop", 0 },
                { "rook",   0 },
                { "queen",  0 },
                { "king",   0 }
            };

        if (string.IsNullOrWhiteSpace(movesString))
            return movesCount;

        var moves = movesString.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var move in moves)
        {
            // Ignorer les numéros de tour "1.", "2." etc.
            if (char.IsDigit(move[0]) && move.Contains('.'))
                continue;

            // On se base sur la notation classique
            if (move.StartsWith("N")) movesCount["knight"]++;
            else if (move.StartsWith("B")) movesCount["bishop"]++;
            else if (move.StartsWith("R")) movesCount["rook"]++;
            else if (move.StartsWith("Q")) movesCount["queen"]++;
            else if (move.StartsWith("K")) movesCount["king"]++;
            else if (char.IsLower(move[0]))
                movesCount["pawn"]++;
        }

        return movesCount;
    }



    //====================
    // 3) AFFICHER
    //====================
    private void DisplayStats(List<Game> games, AverageMovesByPiece avgMoves, string playerUsername)
    {
        if (games == null || games.Count == 0)
        {
            NumGamesLabel.Text = "No games found this month.";
            StatsContainer.IsVisible = true;
            return;
        }

        // Filtrer les parties du joueur
        var playerGames = games.Where(g => g.PlayerUsername?.ToLower() == playerUsername.ToLower()).ToList();
        if (playerGames.Count == 0)
        {
            NumGamesLabel.Text = "No games found for this player.";
            StatsContainer.IsVisible = true;
            return;
        }

        // Nombre total de parties
        NumGamesLabel.Text = $"Games played this month: {playerGames.Count}";

        // Dernier ELO par catégorie
        var latestBullet = playerGames.Where(g => g.Category == "bullet").OrderByDescending(g => g.DateAndEndTime).FirstOrDefault()?.PlayerElo ?? 0;
        var latestBlitz = playerGames.Where(g => g.Category == "blitz").OrderByDescending(g => g.DateAndEndTime).FirstOrDefault()?.PlayerElo ?? 0;
        var latestRapid = playerGames.Where(g => g.Category == "rapid").OrderByDescending(g => g.DateAndEndTime).FirstOrDefault()?.PlayerElo ?? 0;

        BulletEloLabel.Text = latestBullet.ToString();
        BlitzEloLabel.Text = latestBlitz.ToString();
        RapidEloLabel.Text = latestRapid.ToString();

        // Résultats par couleur
        var whiteGames = playerGames.Where(g => g.White?.ToLower() == playerUsername.ToLower()).ToList();
        var blackGames = playerGames.Where(g => g.Black?.ToLower() == playerUsername.ToLower()).ToList();

        double percentWinWhite = whiteGames.Count > 0 ? whiteGames.Count(g => g.ResultForPlayer == "won") / (double)whiteGames.Count : 0;
        double percentDrawWhite = whiteGames.Count > 0 ? whiteGames.Count(g => g.ResultForPlayer == "drawn") / (double)whiteGames.Count : 0;
        double percentLostWhite = whiteGames.Count > 0 ? whiteGames.Count(g => g.ResultForPlayer == "lost") / (double)whiteGames.Count : 0;

        double percentWinBlack = blackGames.Count > 0 ? blackGames.Count(g => g.ResultForPlayer == "won") / (double)blackGames.Count : 0;
        double percentDrawBlack = blackGames.Count > 0 ? blackGames.Count(g => g.ResultForPlayer == "drawn") / (double)blackGames.Count : 0;
        double percentLostBlack = blackGames.Count > 0 ? blackGames.Count(g => g.ResultForPlayer == "lost") / (double)blackGames.Count : 0;

        WhiteStatsLabel.Text = $"White: Win {(percentWinWhite * 100):0.0}% / Draw {(percentDrawWhite * 100):0.0}% / Loss {(percentLostWhite * 100):0.0}%";
        BlackStatsLabel.Text = $"Black: Win {(percentWinBlack * 100):0.0}% / Draw {(percentDrawBlack * 100):0.0}% / Loss {(percentLostBlack * 100):0.0}%";

        // Stats d'ouvertures : e4 / d4
        int e4Games = playerGames.Count(g => g.Moves != null && g.Moves.Trim().StartsWith("1. e4"));
        int e4Wins = playerGames.Count(g => g.Moves != null && g.Moves.Trim().StartsWith("1. e4") && g.ResultForPlayer == "won");

        int d4Games = playerGames.Count(g => g.Moves != null && g.Moves.Trim().StartsWith("1. d4"));
        int d4Wins = playerGames.Count(g => g.Moves != null && g.Moves.Trim().StartsWith("1. d4") && g.ResultForPlayer == "won");

        double e4WinRate = e4Games > 0 ? (double)e4Wins / e4Games : 0;
        double d4WinRate = d4Games > 0 ? (double)d4Wins / d4Games : 0;

        E4Label.Text = $"1.e4 Win rate: {(e4WinRate * 100):0.0}%";
        D4Label.Text = $"1.d4 Win rate: {(d4WinRate * 100):0.0}%";

        // Castling : placeholders (valeurs fictives à calculer selon tes règles si nécessaire)
        SameSideLabel.Text = $"Same-side castling: 0%";
        OppositeSideLabel.Text = $"Opposite castling: 0%";

        // Longest and shortest game
        int longestGame = playerGames.Max(g =>
        {
            if (string.IsNullOrWhiteSpace(g.Moves)) return 0;

            // Cherche tous les numéros de coups via regex : "1.", "2.", "3.", etc.
            var matches = Regex.Matches(g.Moves, @"\d+\.");

            if (matches.Count == 0) return 0;

            // Prend le dernier numéro (ex: "33.")
            var lastMove = matches[^1].Value;

            // Extrait le nombre (en enlevant le ".")
            if (int.TryParse(lastMove.TrimEnd('.'), out int moveNumber))
                return moveNumber;

            return 0;
        });

        int shortestGame = playerGames.Min(g =>
        {
            if (string.IsNullOrWhiteSpace(g.Moves)) return 0;

            // Cherche tous les numéros de coups via regex : "1.", "2.", "3.", etc.
            var matches = Regex.Matches(g.Moves, @"\d+\.");

            if (matches.Count == 0) return 0;

            // Prend le dernier numéro (ex: "33.")
            var lastMove = matches[^1].Value;

            // Extrait le nombre (en enlevant le ".")
            if (int.TryParse(lastMove.TrimEnd('.'), out int moveNumber))
                return moveNumber;

            return 0;
        });

        LongestGameLabel.Text = $"Longest game (moves): {longestGame}";
        ShortestGameLabel.Text = $"Shortest game (moves): {shortestGame}";

        // Moyenne de coups (approximative, ici nombre total de coups / parties)
        double meanMoves = playerGames.Average(g => g.Moves?.Split(' ').Length / 2.0 ?? 0);

        AvgPawnMovesLabel.Text = $"{avgMoves.AvgPawnMoves:0}";
        AvgKnightMovesLabel.Text = $"{avgMoves.AvgKnightMoves:0}";
        AvgBishopMovesLabel.Text = $"{avgMoves.AvgBishopMoves:0}";
        AvgRookMovesLabel.Text = $"{avgMoves.AvgRookMoves:0}";
        AvgQueenMovesLabel.Text = $"{avgMoves.AvgQueenMoves:0}";
        AvgKingMovesLabel.Text = $"{avgMoves.AvgKingMoves:0}";

        // Affichage final
        StatsContainer.IsVisible = true;
    }
}