using ChessLogic;
using ChessMultitool.Logic;
using Newtonsoft.Json;

namespace ChessMultitool;

public partial class OpeningsPage : ContentPage
{
    private Dictionary<string, Dictionary<string, List<string>>> openings;    // ouverture -> variation -> coups
    private string selectedOpening;
    private string selectedVariation;
    private List<string> moveList;
    private int currentMoveIndex;

    private readonly Image[,] pieceImgs = new Image[8, 8];
    private GameState gameState;

    public OpeningsPage()
    {
        InitializeComponent();
        // Le board s'ajuste pour garder un ratio carré
        BoardGrid.SizeChanged += (s, e) =>
        {
            BoardGrid.HeightRequest = BoardGrid.Width;
        };
        InitBoard();
        gameState = new GameState(Player.White, Board.Initial());
        DrawBoard(gameState.Board);
        LoadOpenings();
    }

    #region Board setup
    void InitBoard()
    {
        for (int i = 0; i < 8; i++)
        {
            HighlightGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });
            HighlightGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
            PieceGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });
            PieceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        }

        for (int r = 0; r < 8; r++)
            for (int c = 0; c < 8; c++)
            {
                var img = new Image { Aspect = Aspect.AspectFit };
                Grid.SetRow(img, r);
                Grid.SetColumn(img, c);
                PieceGrid.Children.Add(img);
                pieceImgs[r, c] = img;
            }
    }

    void DrawBoard(Board board)
    {
        for (int r = 0; r < 8; r++)
            for (int c = 0; c < 8; c++)
                pieceImgs[r, c].Source = Images.GetImage(board[r, c]);
    }
    #endregion

    #region Load JSON
    async void LoadOpenings()
    {
        using var stream = await FileSystem.OpenAppPackageFileAsync("openings.json");
        using var reader = new StreamReader(stream);
        string json = await reader.ReadToEndAsync();

        var raw = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, Dictionary<string, List<string>>>>>(json);
        openings = raw["openings"];
        var keys = openings.Keys.ToList();
        openingsPicker.ItemsSource = keys;
        if (keys.Count > 0)
        {
            openingsPicker.SelectedIndex = 0; // triggers OnOpeningChanged
        }
    }

    void OnOpeningChanged(object sender, EventArgs e)
    {
        if (openingsPicker.SelectedIndex == -1) return;
        selectedOpening = openingsPicker.Items[openingsPicker.SelectedIndex];
        var vars = openings[selectedOpening].Keys.ToList();
        variationsPicker.ItemsSource = vars;

        if (vars.Count > 0)
        {
            // Always select first variation
            bool indexChanged = variationsPicker.SelectedIndex != 0;
            variationsPicker.SelectedIndex = 0; // may not trigger event if already 0

            // Ensure board is reset even if SelectedIndexChanged doesn't fire
            selectedVariation = vars[0];
            moveList = openings[selectedOpening][selectedVariation];
            currentMoveIndex = 0;
            ResetBoard();
        }
    }
    #endregion

    #region Pickers
    void OnVariationChanged(object sender, EventArgs e)
    {
        if (variationsPicker.SelectedIndex == -1) return;
        selectedVariation = variationsPicker.Items[variationsPicker.SelectedIndex];
        moveList = openings[selectedOpening][selectedVariation];
        currentMoveIndex = 0;
        ResetBoard();
    }
    #endregion

    #region Navigation
    void OnPreviousClicked(object sender, EventArgs e)
    {
        if (moveList == null || currentMoveIndex == 0) return;
        currentMoveIndex--;
        RebuildPosition();
        moveLabel.Text = moveList[currentMoveIndex];
    }

    void OnNextClicked(object sender, EventArgs e)
    {
        if (moveList == null || currentMoveIndex >= moveList.Count) return;
        PlayAlgebraic(moveList[currentMoveIndex]);
        moveLabel.Text = moveList[currentMoveIndex];
        currentMoveIndex++;
    }
    #endregion

    #region Moteur
    void ResetBoard()
    {
        gameState = new GameState(Player.White, Board.Initial());
        DrawBoard(gameState.Board);
        moveLabel.Text = string.Empty;
    }

    void RebuildPosition()
    {
        ResetBoard();
        for (int i = 0; i < currentMoveIndex; i++)
        {
            PlayAlgebraic(moveList[i], false);
        }
        DrawBoard(gameState.Board);
    }

    void PlayAlgebraic(string notation, bool update = true)
    {
        var move = FindMove(notation);
        if (move == null) return;

        gameState.MakeMove(move);
        if (update) DrawBoard(gameState.Board);
    }

    Move FindMove(string fullMove)
    {
        // nettoie “1.e4” ou “1...cxd4” -> “e4”, “cxd4”
        string alg = fullMove.Split('.').Last().Replace("...", "").Trim();

        // Si capture : “cxd4” -> “d4”, “Nxd4” -> “Nd4”
        if (alg.Contains('x'))
        {
            var parts = alg.Split('x');

            // Pièce (lettre majuscule) ou pion (lettre minuscule)
            if (char.IsUpper(parts[0][0]))
                alg = parts[0][0] + parts[1];   // ex. "N" + "d4" → "Nd4"
            else
                alg = parts[1];                 // ex. "cxd4" -> "d4"
        }

        var legals = gameState.AllLegalMovesFor(gameState.CurrentPlayer);
        return legals.FirstOrDefault(m => m.ToAlgebraic(gameState.Board) == alg);
    }
    #endregion
}
