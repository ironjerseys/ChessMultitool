using ChessLogic;
using ChessMultitool.Logic;
using System.Collections.ObjectModel;
using System.Text;

namespace ChessMultitool;

public partial class ChessGame : ContentPage
{
    private readonly Image[,] pieceImages = new Image[8, 8];
    private readonly BoxView[,] highlights = new BoxView[8, 8];
    private readonly Dictionary<Position, Move> moveCache = new();

    private GameState gameState;
    private Position selectedPos = null;

    private readonly List<string> moveLines = new(); // "1. e4 e5", "2. Cf3 Cc6", ...
    private int plyCount = 0; // demi-coups

    private readonly ObservableCollection<MoveRow> moveTable = new();

    private readonly StringBuilder _movesBuffer = new();
    private int _plyCount = 0;

    public class MoveRow
    {
        public int No { get; set; }
        public string White { get; set; } = "";
        public string Black { get; set; } = "";
    }


    public ChessGame()
    {
        InitializeComponent();
        BoardGrid.SizeChanged += (_, __) =>
        {
            // suit la largeur réelle du plateau (utile au 1er layout / rotation)
            MovesBlock.WidthRequest = BoardGrid.Width;
        };
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

    // Fonction appelée au lancement de l'app
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

    // Deuxieme fonction appelée au lancement de l'app, avant qu'on voit l'échiquier
    private void AddTapGesture()
    {
        var tap = new TapGestureRecognizer();
        tap.Tapped += OnBoardTapped;
        BoardGrid.GestureRecognizers.Add(tap);
    }


    // Premiere fonction appelée dès qu'on touche l'écran, avec les coordonées en parametre
    private void OnBoardTapped(object sender, TappedEventArgs e)
    {
        if (IsMenuOnScreen()) return;

        // Enregistre les coordonnées X Y sur l'écran
        var touchPoint = e.GetPosition(BoardGrid) ?? new Point(0, 0);

        // On calcule la width d'une case de l'échiquier
        var squareSize = BoardGrid.Width / 8;

        // On calcule quelle case a été touchée
        int row = (int)(touchPoint.Y / squareSize);
        int col = (int)(touchPoint.X / squareSize);

        // Exemple de Position Column 3 Row 6 => D2
        // (0,0) semble etre en haut a gauche, à confirmer
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
        // On regarde la liste des moves pour cette piece sur cette case
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


        bool whiteToMove = gameState.CurrentPlayer == Player.White;

        string san = ToSanSimple(gameState, move);

        gameState.MakeMove(move);
        DrawBoard(gameState.Board);

        // Ajoute au flux linéaire (les retours à la ligne se feront automatiquement par le wrap)
        if (whiteToMove)
        {
            int num = (_plyCount / 2) + 1;
            _movesBuffer.Append(num).Append(". ").Append(san).Append(' ');
        }
        else
        {
            _movesBuffer.Append(san).Append(' ');
        }
        _plyCount++;

        // MAJ du label + scroll en bas
        MovesBlock.Text = _movesBuffer.ToString();
        MainThread.BeginInvokeOnMainThread(() =>
            MovesScroll.ScrollToAsync(MovesBlock, ScrollToPosition.End, true)
        );

        if (gameState.IsGameOver())
            ShowGameOver();
    }



    private void HandlePromotion(Position from, Position to)
    {
        pieceImages[to.Row, to.Column].Source = Images.GetImage(gameState.CurrentPlayer, PieceType.Pawn);
        pieceImages[from.Row, from.Column].Source = null;

        // Crée le menu de promotion MAUI
        PromotionMenu promMenu = new PromotionMenu(gameState.CurrentPlayer);

        // Affiche le menu dans ton conteneur (MenuContainer est un ContentView sur ton plateau)
        MenuContainer.Content = promMenu;

        // Quand l'utilisateur sélectionne une pièce
        promMenu.PieceSelected += type =>
        {
            // Supprime le menu après sélection
            MenuContainer.Content = null;

            // Crée un mouvement de promotion avec la pièce choisie
            Move promMove = new PawnPromotion(from, to, type);

            // Joue le coup de promotion
            HandleMove(promMove);
        };
    }


    private void ShowGameOver()
    {
        // Crée le menu Game Over MAUI
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

        plyCount = 0;
        moveLines.Clear();
        MovesBlock.Text = string.Empty;
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

    
    // Cherche d’autres pièces du même type/couleur pouvant aussi aller sur 'move.ToPos'
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

    // Exécute le coup sur une copie pour analyser échec/mat
    private GameState Simulate(GameState stateBefore, Move move)
    {
        var copy = new GameState(stateBefore.CurrentPlayer, stateBefore.Board.Copy());
        copy.MakeMove(move);
        return copy;
    }

    private string CheckSuffix(GameState after)
    {
        var toMove = after.CurrentPlayer;                 // camp qui joue après le coup
        bool inCheck = after.Board.IsInCheck(toMove);
        if (after.IsGameOver() && inCheck) return "#";    // mat
        if (inCheck) return "+";                          // échec
        return "";
    }

    private string ToSanSimple(GameState before, Move move)
    {
        var board = before.Board;
        var from = move.FromPos;
        var to = move.ToPos;
        var piece = board[from];

        // Roque (roi bouge de 2 colonnes)
        if (piece.Type == PieceType.King && Math.Abs(to.Column - from.Column) == 2)
        {
            var sanCastle = to.Column > from.Column ? "O-O" : "O-O-O";
            return sanCastle + CheckSuffix(Simulate(before, move));
        }

        bool isEnPassant = move.Type == MoveType.EnPassant;
        bool isCapture = board[to] != null || isEnPassant;

        string dest = SquareName(to);

        // --- Désambiguïsation minimale (file, rang, ou les deux) ---
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

                if (!shareFile) disamb = fileFrom.ToString();      // Rdf5
                else if (!shareRank) disamb = rankFrom.ToString();      // R4f5
                else disamb = $"{fileFrom}{rankFrom}";  // Rd4f5
            }
        }

        // Corps (pions qui prennent : exd5)
        string core = piece.Type == PieceType.Pawn
            ? (isCapture ? $"{(char)('a' + from.Column)}x{dest}" : dest)
            : $"{pieceLetter}{disamb}{(isCapture ? "x" : "")}{dest}";

        // Promotion (on lit la pièce promue après simulation)
        if (move.Type == MoveType.PawnPromotion)
        {
            var after = Simulate(before, move);
            var promoted = after.Board[to];
            var letter = PieceLetterEn(promoted.Type);
            core += "=" + (string.IsNullOrEmpty(letter) ? "Q" : letter);
        }

        // Échec / mat
        return core + CheckSuffix(Simulate(before, move));
    }
}
