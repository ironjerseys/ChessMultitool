using ChessLogic;
using ChessMultitool.Logic;

namespace ChessMultitool;

/// <summary>
/// Plateau d'analyse libre: l'utilisateur déplace lui-même les pièces des deux camps,
/// sans IA, pour explorer une position (ouverte depuis un puzzle).
/// </summary>
public partial class AnalysisPage : ContentPage
{
    private readonly Image[,] pieceImgs = new Image[8, 8];
    private readonly BoxView[,] highlights = new BoxView[8, 8];
    private readonly HashSet<(int row, int col)> highlightedCells = new();

    private readonly string startFen;
    private readonly List<string> startMoves; // coups UCI appliqués depuis le FEN pour la position de départ
    private GameState state;
    private readonly List<string> playedMoves = new(); // coups UCI joués dans l'analyse (pour l'undo)
    private Position? selected = null;
    private bool isFlipped;
    private PawnPromotion? pendingPromotion;
    private int? lastFromUiR, lastFromUiC, lastToUiR, lastToUiC;
    private Position? lastMoveFrom, lastMoveTo;

    public AnalysisPage(string fen, IEnumerable<string> appliedMoves, bool flipped)
    {
        InitializeComponent();
        startFen = fen;
        startMoves = appliedMoves.ToList();
        isFlipped = flipped;

        BoardGrid.SizeChanged += (s, e) => { BoardGrid.HeightRequest = BoardGrid.Width; };
        InitBoard();
        AddTapGesture();
        ResetToStart();
    }

    private void InitBoard()
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
                var box = new BoxView { BackgroundColor = Colors.Transparent };
                Grid.SetRow(box, r); Grid.SetColumn(box, c);
                HighlightGrid.Children.Add(box);
                highlights[r, c] = box;

                var img = new Image { Aspect = Aspect.AspectFit };
                Grid.SetRow(img, r); Grid.SetColumn(img, c);
                PieceGrid.Children.Add(img);
                pieceImgs[r, c] = img;
            }
    }

    private void AddTapGesture()
    {
        var tap = new TapGestureRecognizer();
        tap.Tapped += OnBoardTapped;
        BoardGrid.GestureRecognizers.Add(tap);
    }

    /// <summary>Reconstruit la position de départ (FEN + coups du puzzle déjà joués).</summary>
    private void ResetToStart()
    {
        state = Fen.FromFen(startFen);
        foreach (var uci in startMoves)
            ApplyUci(uci);
        playedMoves.Clear();
        selected = null;
        pendingPromotion = null;
        PromotionContainer.IsVisible = false;
        ClearAllHighlights();
        DrawBoard(state.Board);
        UpdateTurnLabel();
        UpdateButtons();
    }

    /// <summary>Rejoue la position depuis le départ jusqu'à count coups d'analyse.</summary>
    private void RebuildTo(int count)
    {
        state = Fen.FromFen(startFen);
        foreach (var uci in startMoves)
            ApplyUci(uci);
        for (int i = 0; i < count; i++)
            ApplyUci(playedMoves[i]);
        if (playedMoves.Count > count)
            playedMoves.RemoveRange(count, playedMoves.Count - count);
        selected = null;
        pendingPromotion = null;
        ClearAllHighlights();
        DrawBoard(state.Board);
        UpdateTurnLabel();
        UpdateButtons();
    }

    private void OnBackClicked(object sender, EventArgs e) => Navigation.PopAsync();

    private void OnUndoClicked(object sender, EventArgs e)
    {
        if (playedMoves.Count == 0) return;
        RebuildTo(playedMoves.Count - 1);
    }

    private void OnFlipClicked(object sender, EventArgs e)
    {
        isFlipped = !isFlipped;
        var lmFrom = lastMoveFrom; var lmTo = lastMoveTo;
        selected = null;
        ClearAllHighlights();
        DrawBoard(state.Board);
        if (lmFrom != null && lmTo != null) HighlightLastMove(lmFrom, lmTo);
    }

    private void OnResetClicked(object sender, EventArgs e) => ResetToStart();

    private void UpdateButtons()
    {
        UndoBtn.IsEnabled = playedMoves.Count > 0;
        ResetBtn.IsEnabled = true;
    }

    private void OnBoardTapped(object sender, TappedEventArgs e)
    {
        if (PromotionContainer.IsVisible) return;
        var touchPoint = e.GetPosition(BoardGrid) ?? new Point(0, 0);
        var squareSize = BoardGrid.Width / 8;
        int uiRow = Math.Clamp((int)(touchPoint.Y / squareSize), 0, 7);
        int uiCol = Math.Clamp((int)(touchPoint.X / squareSize), 0, 7);
        var (row, col) = FromUi(uiRow, uiCol);
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

        var fromSel = selected;
        selected = null;
        HideHighlights();

        // Re-sélection d'une autre pièce du même camp
        if (!state.Board.IsEmpty(pos) && state.Board[pos].Color == state.CurrentPlayer && !pos.Equals(fromSel))
        {
            selected = pos;
            ShowMoveTargets(pos);
            return;
        }

        var legals = state.LegalMovesForPiece(fromSel);
        Move? chosen = null;
        foreach (var m in legals)
        {
            if (m.ToPos.Row == pos.Row && m.ToPos.Column == pos.Column)
            {
                chosen = m;
                break;
            }
        }
        if (chosen == null) return;

        if (chosen.Type == MoveType.PawnPromotion)
        {
            pendingPromotion = (PawnPromotion)chosen;
            ShowPromotionMenu(fromSel, pos);
            return;
        }

        ExecuteMove(fromSel, pos, ToUci(fromSel) + ToUci(pos), chosen);
    }

    private void ShowPromotionMenu(Position from, Position to)
    {
        PromotionContainer.IsVisible = true;
        PromotionMenuView = new PromotionMenu(state.CurrentPlayer);
        PromotionContainer.Children.Clear();
        PromotionContainer.Children.Add(new Border
        {
            StrokeThickness = 1,
            Stroke = Color.FromArgb("#333333"),
            BackgroundColor = Color.FromArgb("#1E1E1E"),
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
            var move = new PawnPromotion(from, to, type);
            ExecuteMove(from, to, ToUci(from) + ToUci(to) + suffix, move);
            pendingPromotion = null;
        };
    }

    private void ExecuteMove(Position from, Position to, string uci, Move move)
    {
        state.MakeMove(move);
        playedMoves.Add(uci);
        DrawBoard(state.Board);
        HighlightLastMove(from, to);
        UpdateTurnLabel();
        UpdateButtons();
    }

    private void ApplyUci(string uci)
    {
        var from = new Position(7 - (uci[1] - '1'), uci[0] - 'a');
        var to = new Position(7 - (uci[3] - '1'), uci[2] - 'a');
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
                    chosen = new PawnPromotion(from, to, t);
                }
                else chosen = m;
                break;
            }
        }
        if (chosen == null) return;
        state.MakeMove(chosen);
        lastMoveFrom = from; lastMoveTo = to;
    }

    private static string ToUci(Position p)
    {
        return ((char)('a' + p.Column)).ToString() + (8 - p.Row);
    }

    private void ShowMoveTargets(Position from)
    {
        HideHighlights();
        foreach (var m in state.LegalMovesForPiece(from))
        {
            var (ur, uc) = ToUi(m.ToPos.Row, m.ToPos.Column);
            highlights[ur, uc].BackgroundColor = new Color(0.49f, 1f, 0.49f, 0.6f);
            highlightedCells.Add((ur, uc));
        }
    }

    private void HideHighlights()
    {
        foreach (var (r, c) in highlightedCells)
            highlights[r, c].BackgroundColor = Colors.Transparent;
        highlightedCells.Clear();
    }

    private void ClearAllHighlights()
    {
        for (int r = 0; r < 8; r++)
            for (int c = 0; c < 8; c++)
                highlights[r, c].BackgroundColor = Colors.Transparent;
        highlightedCells.Clear();
        lastMoveFrom = null; lastMoveTo = null;
        lastFromUiR = lastFromUiC = lastToUiR = lastToUiC = null;
    }

    private void HighlightLastMove(Position from, Position to)
    {
        if (lastFromUiR != null && lastFromUiC != null)
            highlights[lastFromUiR.Value, lastFromUiC.Value].BackgroundColor = Colors.Transparent;
        if (lastToUiR != null && lastToUiC != null)
            highlights[lastToUiR.Value, lastToUiC.Value].BackgroundColor = Colors.Transparent;

        var yellow = Color.FromArgb("#D7FF3C").WithAlpha(0.5f);
        var (fr, fc) = ToUi(from.Row, from.Column);
        var (tr, tc) = ToUi(to.Row, to.Column);
        highlights[fr, fc].BackgroundColor = yellow;
        highlights[tr, tc].BackgroundColor = yellow;
        lastMoveFrom = from; lastMoveTo = to;
        lastFromUiR = fr; lastFromUiC = fc; lastToUiR = tr; lastToUiC = tc;
    }

    private void DrawBoard(Board board)
    {
        for (int r = 0; r < 8; r++)
            for (int c = 0; c < 8; c++)
            {
                var (ur, uc) = ToUi(r, c);
                pieceImgs[ur, uc].Source = Images.GetImage(board[r, c]);
            }
    }

    private void UpdateTurnLabel()
    {
        bool white = state.CurrentPlayer == Player.White;
        TurnLabel.Text = white ? "White to move" : "Black to move";
        SideDot.Color = white ? Color.FromArgb("#EEEEEE") : Color.FromArgb("#444444");
    }

    private (int uiRow, int uiCol) ToUi(int row, int col)
        => isFlipped ? (7 - row, 7 - col) : (row, col);

    private (int row, int col) FromUi(int uiRow, int uiCol)
        => isFlipped ? (7 - uiRow, 7 - uiCol) : (uiRow, uiCol);
}
