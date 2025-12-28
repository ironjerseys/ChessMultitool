using ChessLogic;
using ChessMultitool.Models;
using ChessMultitool.Services;
using ChessMultitool.Logic;

namespace ChessMultitool;

public partial class PuzzlesPage : ContentPage
{
    private readonly Image[,] pieceImgs = new Image[8,8];
    private readonly BoxView[,] highlights = new BoxView[8,8];
    private readonly HashSet<(int row,int col)> highlightedCells = new();
    private Position? lastMoveFrom = null;
    private Position? lastMoveTo = null;

    private readonly Grid[,] overlayCells = new Grid[8,8];

    private List<LichessPuzzle> puzzles = new();
    private int index = 0;
    private GameState state = new GameState(Player.White, Board.Initial());

    private List<string> solution = new();
    private int solPtr = 0; // index of next expected solution move
    private int playedCount = 0; // number of solution moves actually executed (includes auto replies)
    private Position? selected = null;
    private bool isFlipped = false; // true when black to move at root
    private int navPtr = 0; // replay cursor within played moves
    private int hintStage = 0; // 0: none, 1: from highlighted, 2: to highlighted
    private PawnPromotion? pendingPromotion; // store move waiting for selection
    private Player humanSide = Player.White; // side user plays in puzzle (derived after first auto move)

    private int? lastFromUiR, lastFromUiC, lastToUiR, lastToUiC; // track UI coords to clear correctly after flip

    public PuzzlesPage()
    {
        InitializeComponent();
        BoardGrid.SizeChanged += (s,e) => { BoardGrid.HeightRequest = BoardGrid.Width; };
        InitBoard();
        AddTapGesture();
        _ = LoadAsync();
    }

    private void AddTapGesture()
    {
        var tap = new TapGestureRecognizer();
        tap.Tapped += OnBoardTapped;
        BoardGrid.GestureRecognizers.Add(tap);
    }

    private async Task LoadAsync()
    {
        index = Preferences.Get("puzzles_index", 0);
        // Load a large set so Next can advance beyond 50
        puzzles = await PuzzlesService.EnsurePuzzlesCachedAsync(5000);
        if (puzzles.Count == 0) return;
        index = Math.Clamp(index, 0, puzzles.Count - 1);
        LoadPuzzle();
    }

    private void ClearAllHighlights()
    {
        for (int r=0;r<8;r++)
            for (int c=0;c<8;c++)
                highlights[r,c].BackgroundColor = Colors.Transparent;
        highlightedCells.Clear();
        lastMoveFrom = null; lastMoveTo = null;
        lastFromUiR = lastFromUiC = lastToUiR = lastToUiC = null;
    }

    private void ClearBoardHighlights()
    {
        for (int r = 0; r < 8; r++)
            for (int c = 0; c < 8; c++)
                highlights[r,c].BackgroundColor = Colors.Transparent;
        highlightedCells.Clear();
        lastMoveFrom = null; lastMoveTo = null;
    }

    private void LoadPuzzle()
    {
        RetryBtn.IsVisible = false;
        NextBtn.IsVisible = false; // hide Next at start of each puzzle
        selected = null;
        solPtr = 0;
        playedCount = 0;
        navPtr = 0;
        hintStage = 0;
        pendingPromotion = null;
        ClearAllHighlights();
        ClearOverlay();
        PromotionContainer.IsVisible = false;

        var p = puzzles[index];
        state = PuzzlesService.LoadPuzzlePosition(p);
        solution = PuzzlesService.GetSolutionMoves(p).ToList();
        isFlipped = state.CurrentPlayer == Player.Black; // initial orientation
        DrawBoard(state.Board); // show initial position first

        // After initial render, auto-play the first solution move (computer), then keep current flip logic
        if (solution.Count > 0)
        {
            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(16), () =>
            {
                ExecuteSolutionMoveInternal(solution[0]);
                humanSide = state.CurrentPlayer;
                // Preserve last move before clearing and flipping
                var lmFrom = lastMoveFrom; var lmTo = lastMoveTo;
                ClearAllHighlights();
                isFlipped = humanSide == Player.Black;
                DrawBoard(state.Board);
                if (lmFrom != null && lmTo != null) HighlightLastMove(lmFrom, lmTo);
                UpdateTurnLabel();
                UpdateMoveNavButtons();
            });
        }
        else
        {
            humanSide = state.CurrentPlayer;
        }

        UpdateTurnLabel();
        UpdateMoveNavButtons();
    }

    private void ExecuteSolutionMoveInternal(string uci)
    {
        var (f,t) = ParseUciPositions(uci);
        ApplyUci(uci);
        HighlightLastMove(f,t);
        playedCount++;
        solPtr = playedCount;
        navPtr = playedCount;
        UpdateTurnLabel();
        UpdateMoveNavButtons();
        hintStage = 0;
        if (playedCount == solution.Count) NextBtn.IsVisible = true;
    }

    // Build UCI for attempted move; detect promotion (store pendingPromotion)
    private string? TryBuildUciMove(Position from, Position to)
    {
        var legals = state.LegalMovesForPiece(from);
        pendingPromotion = null;
        foreach (var m in legals)
        {
            if (m.ToPos.Row == to.Row && m.ToPos.Column == to.Column)
            {
                if (m.Type == MoveType.PawnPromotion)
                {
                    pendingPromotion = (PawnPromotion)m; // we'll ask user which piece
                    return ToUci(from) + ToUci(to); // piece suffix added after selection
                }
                return ToUci(from) + ToUci(to);
            }
        }
        return null;
    }

    private void ShowMoveTargets(Position from)
    {
        HideHighlights();
        foreach (var m in state.LegalMovesForPiece(from))
        {
            var (ur,uc) = ToUi(m.ToPos.Row,m.ToPos.Column);
            highlights[ur,uc].BackgroundColor = new Color(0.49f,1f,0.49f,0.6f);
            highlightedCells.Add((ur,uc));
        }
    }

    private void HideHighlights()
    {
        foreach (var (r,c) in highlightedCells)
            highlights[r,c].BackgroundColor = Colors.Transparent;
        highlightedCells.Clear();
    }

    private void ClearLastMoveHighlight()
    {
        if (lastFromUiR!=null && lastFromUiC!=null)
            highlights[lastFromUiR.Value,lastFromUiC.Value].BackgroundColor = Colors.Transparent;
        if (lastToUiR!=null && lastToUiC!=null)
            highlights[lastToUiR.Value,lastToUiC.Value].BackgroundColor = Colors.Transparent;
        lastMoveFrom = null; lastMoveTo = null;
        lastFromUiR = lastFromUiC = lastToUiR = lastToUiC = null;
    }

    private void HighlightLastMove(Position from, Position to)
    {
        // Clear previous last move using stored UI coords (independent of current orientation)
        if (lastFromUiR!=null && lastFromUiC!=null)
            highlights[lastFromUiR.Value,lastFromUiC.Value].BackgroundColor = Colors.Transparent;
        if (lastToUiR!=null && lastToUiC!=null)
            highlights[lastToUiR.Value,lastToUiC.Value].BackgroundColor = Colors.Transparent;

        var yellow = Color.FromArgb("#D7FF3C").WithAlpha(0.5f);
        var (fr,fc) = ToUi(from.Row,from.Column);
        var (tr,tc) = ToUi(to.Row,to.Column);
        highlights[fr,fc].BackgroundColor = yellow;
        highlights[tr,tc].BackgroundColor = yellow;
        lastMoveFrom = from; lastMoveTo = to;
        lastFromUiR = fr; lastFromUiC = fc; lastToUiR = tr; lastToUiC = tc;
    }

    private (Position from, Position to) ParseUciPositions(string uci)
    {
        var from = new Position(7 - (uci[1] - '1'), uci[0] - 'a');
        var to = new Position(7 - (uci[3] - '1'), uci[2] - 'a');
        return (from,to);
    }

    private static string ToUci(Position p)
    {
        string f(int col) => ((char)('a' + col)).ToString();
        string r(int row) => (8 - row).ToString();
        return f(p.Column) + r(p.Row);
    }

    private void ApplyUci(string uci)
    {
        var (from,to) = ParseUciPositions(uci);
        var legals = state.LegalMovesForPiece(from);
        Move? chosen = null;
        foreach (var m in legals)
        {
            if (m.ToPos.Row == to.Row && m.ToPos.Column == to.Column)
            {
                if (uci.Length == 5)
                {
                    var t = uci[4] switch
                    {
                        'n' => PieceType.Knight,
                        'b' => PieceType.Bishop,
                        'r' => PieceType.Rook,
                        _ => PieceType.Queen
                    };
                    chosen = new PawnPromotion(from,to,t);
                }
                else chosen = m;
                break;
            }
        }
        if (chosen == null) return;
        state.MakeMove(chosen);
        DrawBoard(state.Board);
    }

    private void UpdateTurnLabel()
    {
        TurnLabel.Text = state.CurrentPlayer == Player.White ? "White to move" : "Black to move";
    }

    private void InitBoard()
    {
        for (int i = 0; i < 8; i++)
        {
            HighlightGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });
            HighlightGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
            PieceGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });
            PieceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
            OverlayGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });
            OverlayGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        }
        for (int r = 0; r < 8; r++)
            for (int c = 0; c < 8; c++)
            {
                var box = new BoxView { BackgroundColor = Colors.Transparent };
                Grid.SetRow(box, r); Grid.SetColumn(box, c);
                HighlightGrid.Children.Add(box);
                highlights[r,c] = box;

                var img = new Image { Aspect = Aspect.AspectFit };
                Grid.SetRow(img, r); Grid.SetColumn(img, c);
                PieceGrid.Children.Add(img);
                pieceImgs[r,c] = img;

                var cell = new Grid { BackgroundColor = Colors.Transparent, InputTransparent = true };
                Grid.SetRow(cell, r); Grid.SetColumn(cell, c);
                OverlayGrid.Children.Add(cell);
                overlayCells[r,c] = cell;
            }
    }

    private void ClearOverlay()
    {
        foreach (var cell in overlayCells)
            cell.Children.Clear();
        // restore any hidden base images
        for (int r=0;r<8;r++)
            for (int c=0;c<8;c++)
                pieceImgs[r,c].Opacity = 1;
    }

    private void ShowWrongAtUi(int uiRow,int uiCol)
    {
        var g = overlayCells[uiRow, uiCol];
        g.Children.Add(new Label
        {
            Text = "?",
            TextColor = Colors.Red,
            FontSize = 28,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            FontAttributes = FontAttributes.Bold,
            BackgroundColor = Colors.Transparent
        });
    }

    private void DrawBoard(Board board)
    {
        for (int r = 0; r < 8; r++)
            for (int c = 0; c < 8; c++)
            {
                var piece = board[r,c];
                var (ur,uc) = ToUi(r,c);
                pieceImgs[ur,uc].Source = Images.GetImage(piece);
                pieceImgs[ur,uc].Opacity = 1; // reset if previously hidden for ghost
            }
    }

    private void UpdateMoveNavButtons()
    {
        PrevMoveBtn.IsEnabled = navPtr > 0;
        NextMoveBtn.IsEnabled = navPtr < playedCount;
    }

    private void OnPrevMoveClicked(object sender, EventArgs e)
    {
        if (navPtr == 0) return;
        navPtr--;
        RebuildTo(navPtr);
        UpdateMoveNavButtons();
        ClearOverlay();
        hintStage = 0;
    }

    private void OnNextMoveClicked(object sender, EventArgs e)
    {
        if (navPtr >= playedCount) return;
        navPtr++;
        RebuildTo(navPtr);
        UpdateMoveNavButtons();
        ClearOverlay();
        hintStage = 0;
    }

    private async void OnNextPuzzle(object sender, EventArgs e)
    {
        if (playedCount != solution.Count) return; // only allow when solved
        if (index < puzzles.Count - 1)
        {
            index++;
            Preferences.Set("puzzles_index", index);
            LoadPuzzle();
        }
        else
        {
            // Try to extend cache, then wrap if still at the end
            puzzles = await PuzzlesService.EnsurePuzzlesCachedAsync(puzzles.Count + 500);
            if (index < puzzles.Count - 1)
            {
                index++;
            }
            else
            {
                index = 0; // wrap to start
            }
            Preferences.Set("puzzles_index", index);
            LoadPuzzle();
        }
    }

    // Adjust RebuildTo to keep board orientation for human side
    private void RebuildTo(int count)
    {
        var p = puzzles[index];
        state = PuzzlesService.LoadPuzzlePosition(p);
        ClearAllHighlights();
        ClearOverlay();
        pendingPromotion = null;
        for (int i=0;i<count;i++)
        {
            var uci = solution[i];
            var (f,t) = ParseUciPositions(uci);
            ApplyUci(uci);
            HighlightLastMove(f,t);
        }
        isFlipped = humanSide == Player.Black; // ensure consistent orientation
        DrawBoard(state.Board);
        if (lastMoveFrom!=null && lastMoveTo!=null) HighlightLastMove(lastMoveFrom,lastMoveTo); // refresh under current orientation
        UpdateTurnLabel();
        hintStage = 0;
    }

    private void ExecuteSolutionMove(string uci)
    {
        var (f,t) = ParseUciPositions(uci);
        ApplyUci(uci);
        HighlightLastMove(f,t);
        playedCount++;
        solPtr = playedCount;
        navPtr = playedCount; // jump replay cursor to end after new move
        UpdateTurnLabel();
        DrawBoard(state.Board);
        UpdateMoveNavButtons();
        hintStage = 0; // reset hint for next expected move
        if (playedCount == solution.Count) NextBtn.IsVisible = true;
    }

    private void OnBoardTapped(object sender, TappedEventArgs e)
    {
        if (puzzles.Count == 0 || solution.Count == 0 || PromotionContainer.IsVisible) return;
        var touchPoint = e.GetPosition(BoardGrid) ?? new Point(0, 0);
        var squareSize = BoardGrid.Width / 8;
        int uiRow = Math.Clamp((int)(touchPoint.Y / squareSize),0,7);
        int uiCol = Math.Clamp((int)(touchPoint.X / squareSize),0,7);
        var (row,col) = FromUi(uiRow,uiCol);
        var pos = new Position(row, col);

        if (selected == null)
        {
            if (!state.Board.IsEmpty(pos) && state.Board[pos].Color == state.CurrentPlayer)
            {
                selected = pos;
                ShowMoveTargets(pos);
            }
            return;
        }
        else
        {
            var fromSel = selected;
            var moveUci = TryBuildUciMove(selected, pos);
            selected = null;
            HideHighlights();
            if (moveUci == null) return;
            if (solPtr >= solution.Count) return;
            string expected = solution[solPtr];

            if (pendingPromotion != null)
            {
                // Show menu for promotion choice
                ShowPromotionMenu(fromSel, pos, expected, moveUci);
                return;
            }

            if (moveUci.Equals(expected, StringComparison.OrdinalIgnoreCase))
            {
                ClearOverlay();
                ExecuteSolutionMove(moveUci);
                if (solPtr < solution.Count)
                {
                    var reply = solution[solPtr];
                    ExecuteSolutionMove(reply);
                }
            }
            else
            {
                // Visualize wrong attempt: move piece visually (ghost) to target and show question mark
                ShowWrongAttempt(fromSel, pos);
            }
        }
    }

    private void ShowPromotionMenu(Position from, Position to, string expected, string baseUci)
    {
        PromotionContainer.IsVisible = true;
        // configure images for side to move
        PromotionMenuView = new PromotionMenu(state.CurrentPlayer);
        PromotionContainer.Children.Clear();
        PromotionContainer.Children.Add(new Border
        {
            StrokeThickness = 1,
            Stroke = (Color)Application.Current.Resources["GlobalTextColor"],
            BackgroundColor = (Color)Application.Current.Resources["GlobalControlBackgroundColor"],
            Padding = 8,
            Content = PromotionMenuView
        });
        PromotionMenuView.PieceSelected += type =>
        {
            PromotionContainer.IsVisible = false;
            var suffix = type switch
            {
                PieceType.Knight => 'n',
                PieceType.Bishop => 'b',
                PieceType.Rook => 'r',
                _ => 'q'
            };
            string fullUci = baseUci + suffix;
            // validate vs expected
            if (fullUci.Equals(expected, StringComparison.OrdinalIgnoreCase))
            {
                ClearOverlay();
                ExecuteSolutionMove(fullUci);
                if (solPtr < solution.Count)
                {
                    var reply = solution[solPtr];
                    ExecuteSolutionMove(reply);
                }
            }
            else
            {
                ShowWrongAttempt(from,to);
            }
            pendingPromotion = null;
        };
    }

    private void ShowWrongAttempt(Position fromSel, Position pos)
    {
        ClearOverlay();
        var piece = state.Board[fromSel];
        if (piece != null)
        {
            var (fromUiR, fromUiC) = ToUi(fromSel.Row, fromSel.Column);
            pieceImgs[fromUiR, fromUiC].Opacity = 0;
            var (tUiR,tUiC) = ToUi(pos.Row,pos.Column);
            pieceImgs[tUiR, tUiC].Opacity = 0;
            var ghost = new Image { Aspect = Aspect.AspectFit, Opacity = 0.8, Source = Images.GetImage(piece) };
            overlayCells[tUiR,tUiC].Children.Add(ghost);
            ShowWrongAtUi(tUiR,tUiC);
        }
        RetryBtn.IsVisible = true;
        NextBtn.IsVisible = false; // hide Next on wrong attempt
        UpdateMoveNavButtons(); // keep nav state consistent
    }

    private void OnRetry(object sender, EventArgs e)
    {
        int target = playedCount;
        if (target == 0 && solution.Count > 0) target = 1; // keep first auto computer move
        if (target % 2 == 0 && target > 0) target--; // revert to after previous computer move
        playedCount = target; solPtr = target; navPtr = target;
        RebuildTo(target);
        ClearOverlay();
        RetryBtn.IsVisible = false;
        NextBtn.IsVisible = false; // ensure Next stays hidden until solved
        hintStage = 0;
        pendingPromotion = null;
        UpdateTurnLabel();
        UpdateMoveNavButtons();
    }

    private (int uiRow,int uiCol) ToUi(int row,int col)
    {
        if (!isFlipped) return (row,col);
        return (7 - row, 7 - col);
    }

    private (int row,int col) FromUi(int uiRow,int uiCol)
    {
        if (!isFlipped) return (uiRow,uiCol);
        return (7 - uiRow, 7 - uiCol);
    }

    private void OnHintClicked(object sender, EventArgs e)
    {
        if (PromotionContainer.IsVisible) return; // ignore during promotion choice
        if (solution.Count <= solPtr) return;
        var (from, to) = ParseUciPositions(solution[solPtr]);
        if (hintStage == 0)
        {
            HideHighlights();
            var (ur,uc) = ToUi(from.Row,from.Column);
            highlights[ur,uc].BackgroundColor = new Color(1f,0.84f,0f,0.6f);
            highlightedCells.Add((ur,uc));
            hintStage = 1;
        }
        else if (hintStage == 1)
        {
            var (tr,tc) = ToUi(to.Row,to.Column);
            highlights[tr,tc].BackgroundColor = new Color(1f,0.84f,0f,0.6f);
            highlightedCells.Add((tr,tc));
            hintStage = 2;
        }
        // If already 2, do nothing further until next move resets hintStage
    }
}
