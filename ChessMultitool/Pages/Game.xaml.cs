using ChessLogic;
using ChessMultitool.Logic;
using System.Collections.ObjectModel;

namespace ChessMultitool;

public partial class ChessGame : ContentPage
{
    private readonly Image[,] pieceImages = new Image[8, 8];
    private readonly BoxView[,] highlights = new BoxView[8, 8];
    private readonly Dictionary<Position, Move> moveCache = new();

    private GameState gameState;
    private Position selectedPos = null;

    // Historique des positions (ply 0 = position initiale)
    private readonly List<GameState> history = new();
    // Collection bindée aux coups (un élément = un demi-coup)
    private readonly ObservableCollection<MoveItem> moves = new();
    // Ply actuellement visualisé
    private int viewPly = 0;
    // Compteur demi-coups joués (live)
    private int plyCount = 0;

    private bool vsAi = true;
    private Player aiPlays = Player.Black;     // l'IA joue Noir
    private bool isThinking = false;

    private int searchDepth = 3;
    private TimeSpan thinkingTime = TimeSpan.FromSeconds(2);

    public class MoveItem
    {
        public int Ply { get; init; } // 1..N (correspond à history index)
        public string Text { get; init; } = ""; // ex: "1. e4" ou "e5"
    }

    public ChessGame()
    {
        InitializeComponent();
        InitBoardGridSizeSync();
        CreateGrids();
        gameState = new GameState(Player.White, Board.Initial());
        history.Add(Clone(gameState));
        DrawBoard(gameState.Board);
        MovesView.ItemsSource = moves;
        AddTapGesture();
        UpdateNavButtons();
    }

    public ChessGame(int searchDepth, TimeSpan thinkingTime) : this()
    {
        this.searchDepth = searchDepth;
        this.thinkingTime = thinkingTime;
    }

    private static GameState Clone(GameState s) => new GameState(s.CurrentPlayer, s.Board.Copy());

    private void InitBoardGridSizeSync()
    {
        BoardGrid.SizeChanged += (s, e) =>
        {
            // Conserve un plateau carré
            BoardGrid.HeightRequest = BoardGrid.Width;
        };
    }

    private void CreateGrids()
    {
        for (int i = 0; i < 8; i++)
        {
            HighlightGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });
            HighlightGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
            PieceGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });
            PieceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        }

        for (int row = 0; row < 8; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                var box = new BoxView { BackgroundColor = Colors.Transparent };
                Grid.SetRow(box, row);
                Grid.SetColumn(box, col);
                HighlightGrid.Children.Add(box);
                highlights[row, col] = box;

                var img = new Image { Aspect = Aspect.AspectFit };
                Grid.SetRow(img, row);
                Grid.SetColumn(img, col);
                PieceGrid.Children.Add(img);
                pieceImages[row, col] = img;
            }
        }
    }

    private void DrawBoard(Board board)
    {
        for (int r = 0; r < 8; r++)
            for (int c = 0; c < 8; c++)
                pieceImages[r, c].Source = Images.GetImage(board[r, c]);
    }

    private void AddTapGesture()
    {
        var tap = new TapGestureRecognizer();
        tap.Tapped += OnBoardTapped;
        BoardGrid.GestureRecognizers.Add(tap);
    }

    private void OnBoardTapped(object sender, TappedEventArgs e)
    {
        if (isThinking || IsMenuOnScreen()) return;

        // Si on n'est pas sur la position live, revenir au live avant d'autoriser un coup
        if (viewPly != history.Count - 1)
        {
            viewPly = history.Count - 1;
            DrawBoard(history[viewPly].Board);
            UpdateSelection();
            UpdateNavButtons();
            return;
        }

        var touchPoint = e.GetPosition(BoardGrid) ?? new Point(0, 0);
        var squareSize = BoardGrid.Width / 8;
        int row = (int)(touchPoint.Y / squareSize);
        int col = (int)(touchPoint.X / squareSize);
        if (row > 7) row = 7;
        if (col > 7) col = 7;
        var pos = new Position(row, col);

        if (selectedPos == null) OnFromPositionSelected(pos);
        else OnToPositionSelected(pos);
    }

    private void OnFromPositionSelected(Position pos)
    {
        var movesForPiece = gameState.LegalMovesForPiece(pos);
        if (movesForPiece.Any())
        {
            selectedPos = pos;
            CacheMoves(movesForPiece);
            ShowHighlights();
        }
    }

    private void OnToPositionSelected(Position pos)
    {
        selectedPos = null;
        HideHighlights();

        if (moveCache.TryGetValue(pos, out var move))
        {
            if (move.Type == MoveType.PawnPromotion)
                HandlePromotion(move.FromPos, move.ToPos);
            else
                HandleMove(move);
        }
    }

    private void CacheMoves(IEnumerable<Move> legal)
    {
        moveCache.Clear();
        foreach (var m in legal)
            moveCache[m.ToPos] = m;
    }

    private void ShowHighlights()
    {
        var color = new Color(0.49f, 1f, 0.49f, 0.6f);
        foreach (var to in moveCache.Keys)
            highlights[to.Row, to.Column].BackgroundColor = color;
    }

    private void HideHighlights()
    {
        foreach (var to in moveCache.Keys)
            highlights[to.Row, to.Column].BackgroundColor = Colors.Transparent;
    }

    private void HandleMove(Move move)
    {
        bool whiteToMove = gameState.CurrentPlayer == Player.White;
        string san = ToSanSimple(gameState, move);

        gameState.MakeMove(move);
        DrawBoard(gameState.Board);

        history.Add(Clone(gameState));
        viewPly = history.Count - 1;

        if (whiteToMove)
        {
            int turn = (plyCount / 2) + 1;
            moves.Add(new MoveItem { Ply = plyCount + 1, Text = $"{turn}. {san}" });
        }
        else
        {
            moves.Add(new MoveItem { Ply = plyCount + 1, Text = san });
        }
        plyCount++;

        ScrollToCurrent();
        UpdateSelection();
        UpdateNavButtons();

        if (gameState.IsGameOver())
            ShowGameOver();

        if (vsAi && gameState.CurrentPlayer == aiPlays)
            _ = PlayAiMoveAsync();
    }

    private void ScrollToCurrent()
    {
        if (moves.Count == 0) return;
        var last = moves[^1];
        MovesView.ScrollTo(last, position: ScrollToPosition.Center, animate: true);
        MovesView.SelectedItem = last;
    }

    private void UpdateNavButtons()
    {
        PrevBtn.IsEnabled = viewPly > 0;
        NextBtn.IsEnabled = viewPly < history.Count - 1;
    }

    private void OnPrevMoveClicked(object sender, EventArgs e)
    {
        if (viewPly <= 0) return;
        viewPly--;
        ApplyViewPly();
    }

    private void OnNextMoveClicked(object sender, EventArgs e)
    {
        if (viewPly >= history.Count - 1) return;
        viewPly++;
        ApplyViewPly();
    }

    private void OnMoveSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is MoveItem mi)
        {
            if (mi.Ply >= 0 && mi.Ply < history.Count)
            {
                viewPly = mi.Ply;
                ApplyViewPly();
            }
        }
    }

    private void ApplyViewPly()
    {
        var state = history[viewPly];
        DrawBoard(state.Board);
        UpdateSelection();
        UpdateNavButtons();
    }

    private void UpdateSelection()
    {
        if (viewPly == 0)
        {
            MovesView.SelectedItem = null;
            return;
        }
        var item = moves.FirstOrDefault(m => m.Ply == viewPly);
        if (item != null)
        {
            MovesView.SelectedItem = item;
            MovesView.ScrollTo(item, position: ScrollToPosition.Center, animate: true);
        }
    }

    private void HandlePromotion(Position from, Position to)
    {
        pieceImages[to.Row, to.Column].Source = Images.GetImage(gameState.CurrentPlayer, PieceType.Pawn);
        pieceImages[from.Row, from.Column].Source = null;

        PromotionMenu menu = new(gameState.CurrentPlayer);
        MenuContainer.Content = menu;
        menu.PieceSelected += type =>
        {
            MenuContainer.Content = null;
            Move promMove = new PawnPromotion(from, to, type);
            HandleMove(promMove);
        };
    }

    private async Task PlayAiMoveAsync()
    {
        try
        {
            isThinking = true;
            DisableInput();
            var engine = new MiniMaxEngine();
            var best = await Task.Run(() => engine.FindBestMove(gameState, depth: searchDepth, timeMs: (int)thinkingTime.TotalMilliseconds));
            if (best != null)
                HandleMove(best);
        }
        finally
        {
            EnableInput();
            isThinking = false;
        }
    }

    private void DisableInput() => BoardGrid.InputTransparent = true;
    private void EnableInput() => BoardGrid.InputTransparent = false;

    private void ShowGameOver()
    {
        GameOverMenu menu = new(gameState);
        MenuContainer.Content = menu;
        menu.OptionSelected += opt =>
        {
            if (opt == Option.Restart)
            {
                MenuContainer.Content = null;
                RestartGame();
            }
        };
    }

    private bool IsMenuOnScreen() => MenuContainer.Content != null;

    private void RestartGame()
    {
        selectedPos = null;
        HideHighlights();
        moveCache.Clear();

        gameState = new GameState(Player.White, Board.Initial());
        DrawBoard(gameState.Board);

        history.Clear();
        history.Add(Clone(gameState));
        viewPly = 0;

        moves.Clear();
        plyCount = 0;

        UpdateNavButtons();
        UpdateSelection();
    }

    private static string PieceLetterEn(PieceType t) => t switch
    {
        PieceType.King => "K",
        PieceType.Queen => "Q",
        PieceType.Rook => "R",
        PieceType.Bishop => "B",
        PieceType.Knight => "N",
        _ => ""
    };

    private static string SquareName(Position p)
    {
        char file = (char)('a' + p.Column);
        int rank = 8 - p.Row;
        return $"{file}{rank}";
    }

    private List<Position> FindConflicts(GameState state, Move move, PieceType type)
    {
        var board = state.Board;
        var color = board[move.FromPos].Color;
        var target = move.ToPos;
        var list = new List<Position>();
        for (int r = 0; r < 8; r++)
            for (int c = 0; c < 8; c++)
            {
                var p = board[r, c];
                if (p == null) continue;
                if (p.Type != type || p.Color != color) continue;
                if (r == move.FromPos.Row && c == move.FromPos.Column) continue;
                var pos = new Position(r, c);
                var legal = state.LegalMovesForPiece(pos);
                if (legal.Any(m => m.ToPos == target))
                    list.Add(pos);
            }
        return list;
    }

    private GameState Simulate(GameState before, Move move)
    {
        var copy = new GameState(before.CurrentPlayer, before.Board.Copy());
        copy.MakeMove(move);
        return copy;
    }

    private string CheckSuffix(GameState after)
    {
        var toMove = after.CurrentPlayer;
        bool inCheck = after.Board.IsInCheck(toMove);
        if (after.IsGameOver() && inCheck) return "#";
        if (inCheck) return "+";
        return "";
    }

    private string ToSanSimple(GameState before, Move move)
    {
        var board = before.Board;
        var from = move.FromPos;
        var to = move.ToPos;
        var piece = board[from];

        if (piece.Type == PieceType.King && Math.Abs(to.Column - from.Column) == 2)
        {
            var sanCastle = to.Column > from.Column ? "O-O" : "O-O-O";
            return sanCastle + CheckSuffix(Simulate(before, move));
        }

        bool isEnPassant = move.Type == MoveType.EnPassant;
        bool isCapture = board[to] != null || isEnPassant;

        string dest = SquareName(to);

        string pieceLetter = piece.Type == PieceType.Pawn ? "" : PieceLetterEn(piece.Type);
        string disamb = "";
        if (piece.Type != PieceType.Pawn)
        {
            var conflicts = FindConflicts(before, move, piece.Type);
            if (conflicts.Count > 0)
            {
                bool shareFile = conflicts.Any(p => p.Column == from.Column);
                bool shareRank = conflicts.Any(p => p.Row == from.Row);
                char fileFrom = (char)('a' + from.Column);
                int rankFrom = 8 - from.Row;
                if (!shareFile) disamb = fileFrom.ToString();
                else if (!shareRank) disamb = rankFrom.ToString();
                else disamb = $"{fileFrom}{rankFrom}";
            }
        }

        string core = piece.Type == PieceType.Pawn
            ? (isCapture ? $"{(char)('a' + from.Column)}x{dest}" : dest)
            : $"{pieceLetter}{disamb}{(isCapture ? "x" : "")}{dest}";

        if (move.Type == MoveType.PawnPromotion)
        {
            var after = Simulate(before, move);
            var promoted = after.Board[to];
            var letter = PieceLetterEn(promoted.Type);
            core += "=" + (string.IsNullOrEmpty(letter) ? "Q" : letter);
        }

        return core + CheckSuffix(Simulate(before, move));
    }
}
