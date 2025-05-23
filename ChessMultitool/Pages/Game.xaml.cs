using ChessLogic;
using ChessUI;

namespace ChessMultitool;

public partial class ChessGame : ContentPage
{
    private readonly Image[,] pieceImages = new Image[8, 8];
    private readonly BoxView[,] highlights = new BoxView[8, 8];
    private readonly Dictionary<Position, Move> moveCache = new();

    private GameState gameState;
    private Position selectedPos = null;

    public ChessGame()
    {
        InitializeComponent();
        CreateGrids();
        gameState = new GameState(Player.White, Board.Initial());
        DrawBoard(gameState.Board);
        AddTapGesture();
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
        for (int row = 0; row < 8; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                var piece = board[row, col];
                pieceImages[row, col].Source = Images.GetImage(piece);
            }
        }
    }

    private void AddTapGesture()
    {
        var tap = new TapGestureRecognizer();
        tap.Tapped += OnBoardTapped;
        BoardGrid.GestureRecognizers.Add(tap);
    }

    private void OnBoardTapped(object sender, TappedEventArgs e)
    {
        if (IsMenuOnScreen()) return;

        var touchPoint = e.GetPosition(BoardGrid) ?? new Point(0, 0);
        var squareSize = BoardGrid.Width / 8;
        int row = (int)(touchPoint.Y / squareSize);
        int col = (int)(touchPoint.X / squareSize);

        var pos = new Position(row, col);

        if (selectedPos == null)
        {
            OnFromPositionSelected(pos);
        }
        else
        {
            OnToPositionSelected(pos);
        }
    }

    private void OnFromPositionSelected(Position pos)
    {
        var moves = gameState.LegalMovesForPiece(pos);
        if (moves.Any())
        {
            selectedPos = pos;
            CacheMoves(moves);
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
            {
                HandlePromotion(move.FromPos, move.ToPos);
            }
            else
            {
                HandleMove(move);
            }
        }
    }

    private void CacheMoves(IEnumerable<Move> moves)
    {
        moveCache.Clear();
        foreach (var move in moves)
        {
            moveCache[move.ToPos] = move;
        }
    }

    private void ShowHighlights()
    {
        var color = new Color(0.49f, 1f, 0.49f, 0.6f); // #7DFF7D semi-transparent
        foreach (var to in moveCache.Keys)
        {
            highlights[to.Row, to.Column].BackgroundColor = color;
        }
    }

    private void HideHighlights()
    {
        foreach (var to in moveCache.Keys)
        {
            highlights[to.Row, to.Column].BackgroundColor = Colors.Transparent;
        }
    }

    private void HandleMove(Move move)
    {
        gameState.MakeMove(move);
        DrawBoard(gameState.Board);

        if (gameState.IsGameOver())
        {
            ShowGameOver();
        }
    }

    private void HandlePromotion(Position from, Position to)
    {
        pieceImages[to.Row, to.Column].Source = Images.GetImage(gameState.CurrentPlayer, PieceType.Pawn);
        pieceImages[from.Row, from.Column].Source = null;

        // Cr�e le menu de promotion MAUI
        PromotionMenu promMenu = new PromotionMenu(gameState.CurrentPlayer);

        // Affiche le menu dans ton conteneur (MenuContainer est un ContentView sur ton plateau)
        MenuContainer.Content = promMenu;

        // Quand l'utilisateur s�lectionne une pi�ce
        promMenu.PieceSelected += type =>
        {
            // Supprime le menu apr�s s�lection
            MenuContainer.Content = null;

            // Cr�e un mouvement de promotion avec la pi�ce choisie
            Move promMove = new PawnPromotion(from, to, type);

            // Joue le coup de promotion
            HandleMove(promMove);
        };
    }


    private void ShowGameOver()
    {
        // Cr�e le menu Game Over MAUI
        GameOverMenu gameOverMenu = new GameOverMenu(gameState);

        // Affiche le menu dans ton conteneur MenuContainer
        MenuContainer.Content = gameOverMenu;

        // Gestion du clic sur un des boutons du menu
        gameOverMenu.OptionSelected += option =>
        {
            if (option == Option.Restart)
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
    }
}
