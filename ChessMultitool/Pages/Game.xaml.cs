using ChessLogic;
using ChessMultitool.Logic;
using System.Collections.ObjectModel;
using Microsoft.Maui.Storage; // Preferences
using Microsoft.Maui.Devices; // Vibration
using ChessMultitool.Services;
using System.Threading;
using System.Linq;

namespace ChessMultitool;

public partial class ChessGame : ContentPage
{
    /// <summary>Cache d'images des pièces par case [row, col].</summary>
    private readonly Image[,] pieceImages = new Image[8, 8];
    /// <summary>Overlay pour surlignage des cases.</summary>
    private readonly BoxView[,] highlights = new BoxView[8, 8];

    /// <summary>Cases actuellement surlignées (positions UI).</summary>
    private readonly HashSet<(int row, int col)> hightlightedCells = new();

    /// <summary>Positions de départ et d'arrivée du dernier coup pour surlignage jaune.</summary>
    private Position? lastMoveFrom = null;
    private Position? lastMoveTo = null;

    /// <summary>Cache des coups légaux depuis la case sélectionnée: destination -> coup.</summary>
    private readonly Dictionary<Position, Move> moveCache = new();

    /// <summary>État courant de la partie.</summary>
    private GameState gameState;
    /// <summary>Case sélectionnée (origine) si le joueur a choisi une pièce.</summary>
    private Position selectedPos = null;

    /// <summary>Historique des GameState (ply 0 = position initiale).</summary>
    private readonly List<GameState> history = new();
    /// <summary>Liste observable des demi-coups en notation SAN simple pour l'UI.</summary>
    private readonly ObservableCollection<MoveItem> moves = new();
    /// <summary>Ply actuellement visualisé (replay).</summary>
    private int viewPly = 0;
    /// <summary>Nombre de demi-coups réellement joués (courant live).</summary>
    private int plyCount = 0;

    /// <summary>True si on joue contre l'IA.</summary>
    private bool vsAi = true;
    /// <summary>Camp joué par l'IA.</summary>
    private Player aiPlays = Player.Black; // recalculé selon humanColor
    /// <summary>Camp joué par l'humain.</summary>
    private Player humanColor = Player.White;
    /// <summary>Indique que l'IA doit jouer immédiatement après l'affichage initial.</summary>
    private bool firstAIMovePending = false;
    /// <summary>L'IA est en train de chercher un coup.</summary>
    private bool isThinking = false;
    /// <summary>Orientation du plateau: true si le plateau est retourné (noirs en bas).</summary>
    private bool isFlipped = false; // orientation UI

    /// <summary>Temps maximum de réflexion de l'IA (détermine la difficulté).</summary>
    private TimeSpan thinkingTime = TimeSpan.FromSeconds(2);

    /// <summary>Activation vibration sur les coups humains.</summary>
    private bool vibrationEnabled = true;

    /// <summary>Ligne d'ouverture optionnelle à suivre (liste de coups SAN numérotés).</summary>
    private List<string> openingLine = null; // list of SAN-like strings with move numbers
    /// <summary>True si on utilise encore le livre d'ouverture.</summary>
    private bool useOpeningBook = false;

    /// <summary>Pièces capturées par les Blancs (donc pièces noires). Utilisé pour affichage matériel.</summary>
    private readonly List<Piece> whiteCaptured = new();
    /// <summary>Pièces capturées par les Noirs (donc pièces blanches).</summary>
    private readonly List<Piece> blackCaptured = new();

    /// <summary>Bonus matériel lié aux promotions (valeur pièce promue - valeur pion).</summary>
    private int whitePromotionBonus = 0;
    private int blackPromotionBonus = 0;

    /// <summary>Item affiché dans la liste des coups.</summary>
    public class MoveItem
    {
        public int Ply { get; init; } // 1..N (correspond à history index)
        public string Text { get; init; } = ""; // ex: "1. e4" ou "e5"
    }

    /// <summary>Initialisation commune à tous les constructeurs (plateau, préférences, événements).</summary>
    private void BaseInit()
    {
        InitializeComponent();
        vibrationEnabled = Preferences.Get("pref_vibration_moves", true);
        InitBoardGridSizeSync();
        CreateGrids();
        gameState = new GameState(Player.White, Board.Initial());
        history.Add(Clone(gameState));
        // Charge préférence de flip; par défaut: retourné si l'humain joue les noirs
        isFlipped = Preferences.Get("pref_board_flipped", humanColor == Player.Black);
        DrawBoard(gameState.Board);
        MovesView.ItemsSource = moves;
        AddTapGesture();
        UpdateNavButtons();
        UpdateCapturesUI();

        bool showEval = Preferences.Get("pref_eval_bar", true);
        EvalBarContainer.IsVisible = showEval;
    }

    /// <summary>Constructeur par défaut (humain blanc contre IA noire).</summary>
    public ChessGame()
    {
        BaseInit();
    }

    private readonly string? selectedOpeningForGame;
    private readonly int? selectedAiRating;

    /// <summary>Constructeur complet sans paramètre de profondeur (gérée en interne dans le moteur).</summary>
    public ChessGame(Player humanColor, TimeSpan? thinkingTime = null, List<string> openingLine = null, string? selectedOpeningName = null, int? aiRating = null)
    {
        this.humanColor = humanColor;
        if (thinkingTime.HasValue) this.thinkingTime = thinkingTime.Value;
        this.openingLine = openingLine;
        this.useOpeningBook = openingLine != null && openingLine.Count > 0;
        this.selectedOpeningForGame = selectedOpeningName;
        this.selectedAiRating = aiRating;
        BaseInit();
        aiPlays = (humanColor == Player.White) ? Player.Black : Player.White;
        if (aiPlays == Player.White)
        {
            // L'IA doit jouer en premier après affichage
            firstAIMovePending = true;
        }
    }

    /// <summary>Quand la page apparaît, déclenche le premier coup IA si nécessaire.</summary>
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (firstAIMovePending)
        {
            firstAIMovePending = false;
            await PlayAiMoveAsync();
        }
    }

    /// <summary>Clone indépendant du GameState (copie du plateau + joueur courant).</summary>
    private static GameState Clone(GameState stateToClone)
    {
        return new GameState(stateToClone.CurrentPlayer, stateToClone.Board.Copy());
    }

    /// <summary>Synchronise la hauteur du grid pour conserver un plateau carré.</summary>
    private void InitBoardGridSizeSync()
    {
        BoardGrid.SizeChanged += (sender, args) =>
        {
            BoardGrid.HeightRequest = BoardGrid.Width;
        };
    }

    /// <summary>Crée les cellules UI (images pièces + box de surlignage).</summary>
    private void CreateGrids()
    {
        for (int index = 0; index < 8; index++)
        {
            HighlightGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });
            HighlightGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
            PieceGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });
            PieceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        }

        for (int boardRow = 0; boardRow < 8; boardRow++)
        {
            for (int boardCol = 0; boardCol < 8; boardCol++)
            {
                var highlightBox = new BoxView { BackgroundColor = Colors.Transparent };
                Grid.SetRow(highlightBox, boardRow);
                Grid.SetColumn(highlightBox, boardCol);
                HighlightGrid.Children.Add(highlightBox);
                highlights[boardRow, boardCol] = highlightBox;

                var pieceImage = new Image { Aspect = Aspect.AspectFit };
                Grid.SetRow(pieceImage, boardRow);
                Grid.SetColumn(pieceImage, boardCol);
                PieceGrid.Children.Add(pieceImage);
                pieceImages[boardRow, boardCol] = pieceImage;
            }
        }
    }

    /// <summary>Convertit des coordonnées plateau vers coordonnées UI selon orientation.</summary>
    private (int uiR, int uiC) ToUi(int boardR, int boardC)
    {
        if (isFlipped)
        {
            return (7 - boardR, 7 - boardC);
        }
        else
        {
            return (boardR, boardC);
        }
    }

    /// <summary>Convertit des coordonnées UI vers plateau selon orientation.</summary>
    private Position FromUi(int uiRow, int uiColumn)
    {
        if (isFlipped)
        {
            return new Position(7 - uiRow, 7 - uiColumn);
        }
        else
        {
            return new Position(uiRow, uiColumn);
        }
    }

    /// <summary>Redessine toutes les pièces sur le plateau (en tenant compte du flip).</summary>
    private void DrawBoard(Board board)
    {
        for (int rowIndex = 0; rowIndex < 8; rowIndex++)
            for (int colIndex = 0; colIndex < 8; colIndex++)
                pieceImages[rowIndex, colIndex].Source = null;

        for (int boardRow = 0; boardRow < 8; boardRow++)
        {
            for (int boardCol = 0; boardCol < 8; boardCol++)
            {
                var (uiR, uiC) = ToUi(boardRow, boardCol);
                pieceImages[uiR, uiC].Source = Images.GetImage(board[boardRow, boardCol]);
            }
        }
    }

    /// <summary>Ajoute un gesture tap sur le grid pour gérer la sélection de coups.</summary>
    private void AddTapGesture()
    {
        var tapRecognizer = new TapGestureRecognizer();
        tapRecognizer.Tapped += OnBoardTapped;
        BoardGrid.GestureRecognizers.Add(tapRecognizer);
    }

    /// <summary>Gère un tap sur le plateau: sélection origine/destination ou navigation retour au live.</summary>
    private void OnBoardTapped(object sender, TappedEventArgs e)
    {
        if (isThinking || IsMenuOnScreen()) return;
        if (gameState.IsGameOver()) return; // block interactions after game ends

        if (viewPly != history.Count - 1)
        {
            viewPly = history.Count - 1;
            DrawBoard(history[viewPly].Board);
            UpdateSelection();
            UpdateNavButtons();
            return;
        }

        if (vsAi && gameState.CurrentPlayer != humanColor) return;

        var tapPoint = e.GetPosition(BoardGrid) ?? new Point(0, 0);
        var squareSize = BoardGrid.Width / 8;
        int uiRow = (int)(tapPoint.Y / squareSize);
        int uiCol = (int)(tapPoint.X / squareSize);
        if (uiRow > 7) uiRow = 7;
        if (uiCol > 7) uiCol = 7;

        var boardPos = FromUi(uiRow, uiCol);

        if (selectedPos == null)
        {
            OnFromPositionSelected(boardPos);
        }
        else
        {
            OnToPositionSelected(boardPos);
        }
    }

    /// <summary>Traite la sélection d'une case origine, met en cache ses coups si au trait.</summary>
    private void OnFromPositionSelected(Position position)
    {
        if (gameState.IsGameOver()) return;
        var movesForPiece = gameState.LegalMovesForPiece(position);
        if (movesForPiece.Any())
        {
            selectedPos = position;
            CacheMoves(movesForPiece);
            ShowHighlights(moveCache.Keys);
        }
    }

    /// <summary>Traite la sélection de la case destination: exécute coup ou promotion.</summary>
    private void OnToPositionSelected(Position destination)
    {
        if (gameState.IsGameOver()) return;
        selectedPos = null;
        HideHighlights();

        if (moveCache.TryGetValue(destination, out var move))
        {
            if (move.Type == MoveType.PawnPromotion)
                HandlePromotion(move.FromPos, move.ToPos);
            else
                HandleMove(move);
        }
    }

    /// <summary>Met en cache les coups légaux (destination -> Move) pour la pièce sélectionnée.</summary>
    private void CacheMoves(IEnumerable<Move> legalMoves)
    {
        moveCache.Clear();
        foreach (var legalMove in legalMoves)
            moveCache[legalMove.ToPos] = legalMove;
    }

    /// <summary>Affiche un surlignage sur les positions passées (avec couleur facultative).</summary>
    private void ShowHighlights(IEnumerable<Position> targets, Color? color = null, bool track = true)
    {
        var chosenColor = color ?? new Color(0.49f, 1f, 0.49f, 0.6f);
        foreach (var targetPos in targets)
        {
            var (uiRow, uiCol) = ToUi(targetPos.Row, targetPos.Column);
            highlights[uiRow, uiCol].BackgroundColor = chosenColor;
            if (track)
            {
                hightlightedCells.Add((uiRow, uiCol));
            }
        }
    }

    /// <summary>Efface tous les surlignages temporaires (sauf dernier coup si non réinitialisé).</summary>
    private void HideHighlights()
    {
        foreach (var (uiR, uiC) in hightlightedCells)
        {
            highlights[uiR, uiC].BackgroundColor = Colors.Transparent;
        }
        hightlightedCells.Clear();
    }

    /// <summary>Supprime le surlignage jaune du dernier coup.</summary>
    private void ClearLastMoveHighlight()
    {
        if (lastMoveFrom is Position lastFromPos)
        {
            var (uiRow, uiCol) = ToUi(lastFromPos.Row, lastFromPos.Column);
            highlights[uiRow, uiCol].BackgroundColor = Colors.Transparent;
        }
        if (lastMoveTo is Position lastToPos)
        {
            var (uiRow, uiCol) = ToUi(lastToPos.Row, lastToPos.Column);
            highlights[uiRow, uiCol].BackgroundColor = Colors.Transparent;
        }
        lastMoveFrom = null;
        lastMoveTo = null;
    }

    /// <summary>Surligne en jaune les deux cases du coup joué (origine + destination).</summary>
    private void HighlightLastMove(Position from, Position to)
    {
        ClearLastMoveHighlight();
        var yellow = Color.FromHex("#D7FF3C").WithAlpha(0.5f);
        ShowHighlights(new[] { from, to }, yellow, track: false);
        lastMoveFrom = from;
        lastMoveTo = to;
    }

    /// <summary>Exécute un coup (ou promotion) et met à jour historique, surlignages, matériel, vibration, IA.</summary>
    private void HandleMove(Move move)
    {
        bool wasWhiteToMove = gameState.CurrentPlayer == Player.White;
        string san = ToSanSimple(gameState, move);

        TrackCaptureIfAny(move);
        var promoterColor = gameState.CurrentPlayer; // sauvegarde avant MakeMove
        gameState.MakeMove(move);

        if (move.Type == MoveType.PawnPromotion)
        {
            var promotedPiece = gameState.Board[move.ToPos];
            int bonus = Math.Max(0, PieceValue(promotedPiece.Type) - 1);
            if (promoterColor == Player.White) whitePromotionBonus += bonus; else if (promoterColor == Player.Black) blackPromotionBonus += bonus;
        }

        DrawBoard(gameState.Board);
        HighlightLastMove(move.FromPos, move.ToPos);

        history.Add(Clone(gameState));
        viewPly = history.Count - 1;

        if (wasWhiteToMove)
        {
            int turnNumber = (plyCount / 2) + 1;
            moves.Add(new MoveItem { Ply = plyCount + 1, Text = $"{turnNumber}. {san}" });
        }
        else
        {
            moves.Add(new MoveItem { Ply = plyCount + 1, Text = san });
        }
        plyCount++;

        ScrollToCurrent();
        UpdateSelection();
        UpdateNavButtons();
        UpdateCapturesUI();

        if (vibrationEnabled && vsAi && gameState.CurrentPlayer != aiPlays)
        {
            try { Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(40)); } catch { }
        }

        if (gameState.IsGameOver())
        {
            ShowGameOver();
        }

        if (vsAi && gameState.CurrentPlayer == aiPlays)
        {
            _ = PlayAiMoveAsync();
        }
    }

    /// <summary>Ajoute une pièce capturée aux listes de matériel (en passant inclus).</summary>
    private void TrackCaptureIfAny(Move move)
    {
        Piece capturedPiece = null;
        var board = gameState.Board;
        if (move.Type == MoveType.EnPassant)
        {
            var epFrom = move.FromPos;
            var epTo = move.ToPos;
            var capturedPos = new Position(epFrom.Row, epTo.Column);
            capturedPiece = board[capturedPos];
        }
        else
        {
            capturedPiece = board[move.ToPos];
        }
        if (capturedPiece == null) return;
        if (gameState.CurrentPlayer == Player.White)
            whiteCaptured.Add(capturedPiece.Copy());
        else
            blackCaptured.Add(capturedPiece.Copy());
    }

    /// <summary>Retourne la valeur matérielle simplifiée d'un type de pièce.</summary>
    private static int PieceValue(PieceType type) => type switch
    {
        PieceType.Pawn => 1,
        PieceType.Knight => 3,
        PieceType.Bishop => 3,
        PieceType.Rook => 5,
        PieceType.Queen => 10,
        _ => 0
    };

    /// <summary>Calcule le score matériel cumulé d'une liste de pièces.</summary>
    private int MaterialScore(IEnumerable<Piece> pieces)
    {
        return pieces.Sum(piece => PieceValue(piece.Type));
    }

    /// <summary>Mise à jour de l'UI des pièces capturées et de l'avantage matériel.</summary>
    private void UpdateCapturesUI()
    {
        WhiteCapturesPanel.Children.Clear();
        BlackCapturesPanel.Children.Clear();

        foreach (var captured in whiteCaptured.OrderBy(p => PieceValue(p.Type)))
        {
            WhiteCapturesPanel.Children.Add(new Image
            {
                Source = Images.GetImage(captured),
                HeightRequest = 16,
                WidthRequest = 16,
                Aspect = Aspect.AspectFit
            });
        }
        foreach (var captured in blackCaptured.OrderBy(p => PieceValue(p.Type)))
        {
            BlackCapturesPanel.Children.Add(new Image
            {
                Source = Images.GetImage(captured),
                HeightRequest = 16,
                WidthRequest = 16,
                Aspect = Aspect.AspectFit
            });
        }

        int whiteScore = MaterialScore(whiteCaptured) + whitePromotionBonus;
        int blackScore = MaterialScore(blackCaptured) + blackPromotionBonus;

        int whiteDiff = whiteScore - blackScore;
        int blackDiff = blackScore - whiteScore;

        WhiteAdvantageLabel.Text = whiteDiff > 0 ? $"+{whiteDiff}" : string.Empty;
        BlackAdvantageLabel.Text = blackDiff > 0 ? $"+{blackDiff}" : string.Empty;
    }

    /// <summary>Fait défiler la liste des coups jusqu'au dernier coup joué.</summary>
    private void ScrollToCurrent()
    {
        if (moves.Count == 0) return;
        var lastMoveItem = moves[^1];
        MovesView.ScrollTo(lastMoveItem, position: ScrollToPosition.Center, animate: true);
        MovesView.SelectedItem = lastMoveItem;
    }

    /// <summary>Met à jour l'état des boutons de navigation (précedent/suivant).</summary>
    private void UpdateNavButtons()
    {
        PrevBtn.IsEnabled = viewPly > 0;
        NextBtn.IsEnabled = viewPly < history.Count - 1;
    }

    /// <summary>Clique bouton coup précédent.</summary>
    private void OnPrevMoveClicked(object sender, EventArgs e)
    {
        if (viewPly <= 0) return; viewPly--; ApplyViewPly();
    }

    /// <summary>Clique bouton coup suivant.</summary>
    private void OnNextMoveClicked(object sender, EventArgs e)
    {
        if (viewPly >= history.Count - 1) return; viewPly++; ApplyViewPly();
    }

    /// <summary>Sélection d'un coup dans la liste pour naviguer dans l'historique.</summary>
    private void OnMoveSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is MoveItem selectedItem)
        {
            if (selectedItem.Ply >= 0 && selectedItem.Ply < history.Count)
            {
                viewPly = selectedItem.Ply;
                ApplyViewPly();
            }
        }
    }

    /// <summary>Reconstruit l'affichage plateau selon viewPly.</summary>
    private void ApplyViewPly()
    {
        var stateAtView = history[viewPly];
        DrawBoard(stateAtView.Board);
        UpdateSelection();
        UpdateNavButtons();
    }

    /// <summary>Met à jour la sélection visuelle dans la liste des coups selon viewPly.</summary>
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

    /// <summary>Affiche le menu de promotion et exécute la promotion choisie.</summary>
    private void HandlePromotion(Position from, Position to)
    {
        var (uiFromR, uiFromC) = ToUi(from.Row, from.Column);
        var (uiToR, uiToC) = ToUi(to.Row, to.Column);
        pieceImages[uiToR, uiToC].Source = Images.GetImage(gameState.CurrentPlayer, PieceType.Pawn);
        pieceImages[uiFromR, uiFromC].Source = null;

        PromotionMenu menu = new(gameState.CurrentPlayer);
        MenuContainer.Content = menu;
        menu.PieceSelected += type =>
        {
            MenuContainer.Content = null;
            Move promMove = new PawnPromotion(from, to, type);
            HandleMove(promMove);
        };
    }

    private CancellationTokenSource? thinkingAnimCts;
    private CancellationTokenSource? thinkingBgCts;

    private BoxView? aiPulse;
    private CancellationTokenSource? aiPulseCts;

    private BoxView? aiConsiderPulse;
    private CancellationTokenSource? aiConsiderCts;

    /// <summary>Démarre l'animation de surlignage pulsé sur la case origine du coup en cours d'évaluation par l'IA.</summary>
    private void StartAiConsiderPulse(Position from)
    {
        if (!Preferences.Get("pref_ai_consider_highlight", true)) return;
        var (uiR, uiC) = ToUi(from.Row, from.Column);
        if (aiConsiderPulse == null)
        {
            aiConsiderPulse = new BoxView
            {
                Color = Color.FromArgb("#ff0000"),
                Opacity = 0,
                InputTransparent = true,
                IsVisible = true
            };
            HighlightGrid.Children.Add(aiConsiderPulse);
        }
        Grid.SetRow(aiConsiderPulse, uiR);
        Grid.SetColumn(aiConsiderPulse, uiC);
        aiConsiderPulse.IsVisible = true;

        aiConsiderCts?.Cancel();
        aiConsiderCts = new CancellationTokenSource();

        var pulse = new Animation();
        pulse.Add(0, 0.5, new Animation(v => aiConsiderPulse.Opacity = v, 0.10, 0.28));
        pulse.Add(0.5, 1, new Animation(v => aiConsiderPulse.Opacity = v, 0.28, 0.10));
        pulse.Commit(this, "AiConsiderPulse", rate: 16, length: 900, easing: Easing.SinInOut,
            finished: (v, c) => { }, repeat: () => aiConsiderCts != null && !aiConsiderCts.IsCancellationRequested);
    }

    /// <summary>Stoppe l'animation de considération IA et masque le pulse.</summary>
    private void StopAiConsiderPulse()
    {
        aiConsiderCts?.Cancel();
        aiConsiderCts = null;
        this.AbortAnimation("AiConsiderPulse");
        if (aiConsiderPulse != null)
        {
            aiConsiderPulse.IsVisible = false;
            aiConsiderPulse.Opacity = 0;
        }
    }

    /// <summary>Détermine et joue le coup de l'IA (livre d'ouverture ou moteur), puis journalisation en Debug.</summary>
    private async Task PlayAiMoveAsync()
    {
        try
        {
            isThinking = true;
            DisableInput();
            var thinkStart = DateTime.UtcNow;

            var rootMoves = gameState.AllLegalMovesFor(gameState.CurrentPlayer).ToList();

            if (useOpeningBook)
            {
                var bookMove = GetBookMoveIfAvailable();
                if (bookMove != null)
                {
                    HandleMove(bookMove);
#if DEBUG
                    await LogAiTelemetryAsync(thinkStart, DateTime.UtcNow, rootMoves.Count, new List<(Move, int)> { (bookMove, 0) }, bookMove, 0, 0, 0);
#endif
                    return;
                }
                else
                {
                    useOpeningBook = false;
            }
            }

            var engine = new MiniMaxEngine();
            var evaluatedList = new List<(Move move, int score)>();
            Move? bestMove = null;
            long generatedMovesTotal = 0, nodesVisited = 0, leafEvaluations = 0;
            await Task.Run(() =>
            {
                var rootPlayer = gameState.CurrentPlayer;
                // Appel sans profondeur explicite: le moteur décide jusqu'où aller dans le temps imparti
                bestMove = engine.FindBestMove(
                    gameState,
                    timeMs: (int)thinkingTime.TotalMilliseconds,
                    onConsider: move => MainThread.BeginInvokeOnMainThread(() => StartAiConsiderPulse(move.FromPos)),
                    onEvaluated: (move, score) => evaluatedList.Add((move, score)),
                    onStats: (generated, nodes, leafs) => { generatedMovesTotal = generated; nodesVisited = nodes; leafEvaluations = leafs; },
                    onEvalUpdate: evaluationCp => MainThread.BeginInvokeOnMainThread(() => UpdateEvalBarFromScore(evaluationCp, rootPlayer))
                );
            });

            StopAiConsiderPulse();

            if (bestMove != null)
            {
                HandleMove(bestMove);
            }

            if (evaluatedList.Count > 0)
            {
                var finalScore = evaluatedList.FirstOrDefault(e => e.move == bestMove).score;
                var previousPlayer = gameState.CurrentPlayer == Player.White ? Player.Black : Player.White; // joueur racine avant makeMove
                MainThread.BeginInvokeOnMainThread(() => UpdateEvalBarFromScore(finalScore, previousPlayer));
            }

#if DEBUG
            await LogAiTelemetryAsync(thinkStart, DateTime.UtcNow, rootMoves.Count, evaluatedList, bestMove, generatedMovesTotal, nodesVisited, leafEvaluations);
#endif
        }
        finally
        {
            EnableInput();
            isThinking = false;
        }
    }

    /// <summary>Convertit un Move en notation UCI (ex: e2e4).</summary>
    private static string ToUci(Move move)
    {
        string File(int col) => ((char)('a' + col)).ToString();
        string Rank(int row) => (8 - row).ToString();
        return File(move.FromPos.Column) + Rank(move.FromPos.Row) + File(move.ToPos.Column) + Rank(move.ToPos.Row);
    }

    /// <summary>Envoie la télémétrie IA (Debug) regroupant performances et scores sur les coups évalués.</summary>
    private async Task LogAiTelemetryAsync(DateTime startUtc, DateTime endUtc, int legalRootCount, List<(Move move, int score)> evaluated, Move? best,
        long generatedMovesTotal, long nodesVisited, long leafEvaluations)
    {
        if (!vsAi) return;
        try
        {
            var dto = new TelemetryService.AiChessLogDto
            {
                SearchDepth = 0,
                DurationMs = (long)(endUtc - startUtc).TotalMilliseconds,
                LegalMovesCount = legalRootCount,
                EvaluatedMovesCount = evaluated.Count,
                BestMoveUci = best != null ? ToUci(best) : null,
                BestScoreCp = best != null ? evaluated.FirstOrDefault(e => e.move == best).score : null,
                GeneratedMovesTotal = generatedMovesTotal,
                NodesVisited = nodesVisited,
                LeafEvaluations = leafEvaluations,
                EvaluatedMoves = evaluated.Select(e => new TelemetryService.MoveEvalDto(ToUci(e.move), e.score)).ToList()
            };
            await TelemetryService.SendAiChessLogAsync(dto);
        }
        catch { }
    }

    /// <summary>Vrai si un menu modal (ex: promotion) est visible.</summary>
    private bool IsMenuOnScreen()
    {
        return MenuContainer.Content != null;
    }

    /// <summary>Désactive l'interaction directe avec le plateau (utilisé pendant réflexion IA).</summary>
    private void DisableInput()
    {
        BoardGrid.InputTransparent = true;
    }

    /// <summary>Ré-active l'interaction directe avec le plateau.</summary>
    private void EnableInput()
    {
        BoardGrid.InputTransparent = false;
    }

    /// <summary>Modifie l'état vibration sur coups humains.</summary>
    public void SetVibrationEnabled(bool enabled)
    {
        vibrationEnabled = enabled;
    }

    /// <summary>Affiche ou masque la barre d'évaluation.</summary>
    public void SetEvalBarVisible(bool visible)
    {
        EvalBarContainer.IsVisible = visible;
    }

    /// <summary>Mise à jour de la barre d'évaluation (Score centipion -> ratio visuel White/Black).</summary>
    private void UpdateEvalBarFromScore(int scoreCp, Player perspective)
    {
        int whitePovCp = (perspective == Player.White) ? scoreCp : -scoreCp;
        double norm = Math.Clamp((whitePovCp + 600.0) / 1200.0, 0, 1);

        EvalBarGrid.ColumnDefinitions.Clear();
        EvalBarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(norm, GridUnitType.Star) });
        EvalBarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1 - norm, GridUnitType.Star) });

        EvalLabel.Text = (whitePovCp / 100.0).ToString("0.00");
    }

    /// <summary>Tente de retourner un coup issu du livre d'ouverture à partir de la ligne fournie.</summary>
    private Move GetBookMoveIfAvailable()
    {
        if (openingLine == null || openingLine.Count == 0) return null;
        if (gameState.CurrentPlayer != aiPlays) return null;
        int index = plyCount;
        if (index < 0 || index >= openingLine.Count) return null;
        string fullMove = openingLine[index];
        string alg = SanitizeOpeningMove(fullMove);
        var legals = gameState.AllLegalMovesFor(gameState.CurrentPlayer);
        return legals.FirstOrDefault(move => move.ToAlgebraic(gameState.Board) == alg);
    }

    /// <summary>Normalise un coup d'ouverture texte (supprime numérotation, captures, ellipses).</summary>
    private static string SanitizeOpeningMove(string fullMove)
    {
        var partsDot = fullMove.Split('.');
        string alg = partsDot.Length > 1 ? partsDot[^1] : fullMove;
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

    /// <summary>Réinitialise l'UI de fin de partie et relance la partie.</summary>
    private void OnRestartClicked(object sender, EventArgs e)
    {
        GameOverBanner.IsVisible = false;
        RestartGame();
    }

    /// <summary>Affiche l'overlay fin de partie et déclenche la mise à jour achievements.</summary>
    private void ShowGameOver()
    {
        WinnerText.Text = GetWinnerText(gameState.Result.Winner);
        ReasonText.Text = GetReasonText(gameState.Result.Reason, gameState.CurrentPlayer);
        GameOverBanner.IsVisible = true;
        _ = UpdateAchievementsIfAnyAsync();
    }

    /// <summary>Mise à jour des achievements (victoire rapide, victoire d'ouverture, stats par rating) si conditions remplies.</summary>
    private async Task UpdateAchievementsIfAnyAsync()
    {
        if (!vsAi || !gameState.IsGameOver() || gameState.Result == null) return;

        if (selectedAiRating.HasValue)
        {
            bool humanWon = gameState.Result.Winner == humanColor;
            try { await AchievementService.AddAiGameByRatingAsync(selectedAiRating.Value, humanWon); } catch { }
        }

        if (gameState.Result.Winner != humanColor) return;

        int fullMoves = (plyCount + 1) / 2;
        int[] thresholdsFull = { 60, 50, 40, 30, 20 };
        foreach (var threshold in thresholdsFull)
        {
            if (fullMoves <= threshold)
            {
                try { await AchievementService.AddQuickWinAsync(threshold); } catch { }
            }
        }

        if (!string.IsNullOrWhiteSpace(selectedOpeningForGame))
        {
            try { await AchievementService.AddSelectedOpeningWinAsync(selectedOpeningForGame); } catch { }
        }
    }

    /// <summary>Réinitialise entièrement la partie (matériel, historique, orientation, livre d'ouverture).</summary>
    private void RestartGame()
    {
        selectedPos = null;
        HideHighlights();
        ClearLastMoveHighlight();
        moveCache.Clear();

        gameState = new GameState(Player.White, Board.Initial());
        history.Clear();
        history.Add(Clone(gameState));
        viewPly = 0;
        moves.Clear();
        plyCount = 0;
        aiPlays = (humanColor == Player.White) ? Player.Black : Player.White;
        isFlipped = humanColor == Player.Black;
        useOpeningBook = openingLine != null && openingLine.Count > 0;
        whiteCaptured.Clear();
        blackCaptured.Clear();
        whitePromotionBonus = 0;
        blackPromotionBonus = 0;
        DrawBoard(gameState.Board);
        UpdateCapturesUI();
        firstAIMovePending = aiPlays == Player.White;
        if (firstAIMovePending)
        {
            _ = PlayAiMoveAsync();
            firstAIMovePending = false;
        }
        UpdateNavButtons();
        UpdateSelection();
    }

    /// <summary>Texte de victoire pour le joueur gagnant (anglais conservé pour cohérence UI existante).</summary>
    private string GetWinnerText(Player winner)
    {
        return winner switch
        {
            Player.White => "WHITE WINS!",
            Player.Black => "BLACK WINS!",
            _ => "IT'S A DRAW"
        };
    }

    /// <summary>Texte de la raison de fin de partie (anglais pour cohérence UI).</summary>
    private string GetReasonText(EndReason reason, Player currentPlayer)
    {
        return reason switch
        {
            EndReason.Stalemate => "STALEMATE",
            EndReason.Checkmate => "CHECKMATE",
            EndReason.FiftyMoveRule => "FIFTY-MOVE RULE",
            EndReason.InsufficientMaterial => "INSUFFICIENT MATERIAL",
            EndReason.ThreefoldRepetition => "THREEFOLD REPETITION",
            _ => string.Empty
        };
    }

    /// <summary>Renvoie les positions de pièces du même type pouvant aller sur la même case (pour désambiguisation SAN).</summary>
    private List<Position> FindConflicts(GameState state, Move move, PieceType type)
    {
        var board = state.Board;
        var color = board[move.FromPos].Color;
        var target = move.ToPos;
        var conflictList = new List<Position>();
        for (int rowIndex = 0; rowIndex < 8; rowIndex++)
            for (int colIndex = 0; colIndex < 8; colIndex++)
            {
                var piece = board[rowIndex, colIndex];
                if (piece == null) continue;
                if (piece.Type != type || piece.Color != color) continue;
                if (rowIndex == move.FromPos.Row && colIndex == move.FromPos.Column) continue;
                var pos = new Position(rowIndex, colIndex);
                var legalMoves = state.LegalMovesForPiece(pos);
                if (legalMoves.Any(m => m.ToPos == target)) conflictList.Add(pos);
            }
        return conflictList;
    }

    /// <summary>Simule un coup sur une copie pour évaluer suffixes (+/#).</summary>
    private GameState Simulate(GameState before, Move move)
    {
        var copyState = new GameState(before.CurrentPlayer, before.Board.Copy());
        copyState.MakeMove(move);
        return copyState;
    }

    /// <summary>Calcule le suffixe SAN (+ échec / # mat / vide sinon).</summary>
    private string CheckSuffix(GameState after)
    {
        var toMove = after.CurrentPlayer;
        bool inCheck = after.Board.IsInCheck(toMove);
        if (after.IsGameOver() && inCheck) return "#";
        if (inCheck) return "+";
        return string.Empty;
    }

    /// <summary>Produit une notation SAN simplifiée du coup donné (sans info subtile de prise en passant etc.).</summary>
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

        char fileChar = (char)('a' + to.Column);
        int rankInt = 8 - to.Row;
        string destination = $"{fileChar}{rankInt}";

        string pieceLetter = piece.Type == PieceType.Pawn ? string.Empty : PieceLetterEn(piece.Type);
        string disambiguation = string.Empty;
        if (piece.Type != PieceType.Pawn)
        {
            var conflicts = FindConflicts(before, move, piece.Type);
            if (conflicts.Count > 0)
            {
                bool shareFile = conflicts.Any(p => p.Column == from.Column);
                bool shareRank = conflicts.Any(p => p.Row == from.Row);
                char fileFrom = (char)('a' + from.Column);
                int rankFrom = 8 - from.Row;
                if (!shareFile) disambiguation = fileFrom.ToString();
                else if (!shareRank) disambiguation = rankFrom.ToString();
                else disambiguation = $"{fileFrom}{rankFrom}";
            }
        }

        string core = piece.Type == PieceType.Pawn
            ? (isCapture ? $"{(char)('a' + from.Column)}x{destination}" : destination)
            : $"{pieceLetter}{disambiguation}{(isCapture ? "x" : string.Empty)}{destination}";

        if (move.Type == MoveType.PawnPromotion)
        {
            var after = Simulate(before, move);
            var promotedPiece = after.Board[to];
            var promotedLetter = PieceLetterEn(promotedPiece?.Type ?? PieceType.Queen);
            if (string.IsNullOrEmpty(promotedLetter)) promotedLetter = "Q";
            core += "=" + promotedLetter;
            return core + CheckSuffix(after);
        }

        return core + CheckSuffix(Simulate(before, move));
    }

    /// <summary>Lettre SAN pour une pièce donnée (vide pour pion).</summary>
    private static string PieceLetterEn(PieceType type)
    {
        return type switch
        {
            PieceType.King => "K",
            PieceType.Queen => "Q",
            PieceType.Rook => "R",
            PieceType.Bishop => "B",
            PieceType.Knight => "N",
            _ => string.Empty
        };
    }

    /// <summary>Inverse l'orientation du plateau et réapplique les surlignages pertinents.</summary>
    private void OnFlipBoardClicked(object sender, EventArgs e)
    {
        isFlipped = !isFlipped;
        Preferences.Set("pref_board_flipped", isFlipped);
        DrawBoard(gameState.Board);
        UpdateSelection();
        if (lastMoveFrom != null && lastMoveTo != null)
        {
            HighlightLastMove(lastMoveFrom, lastMoveTo);
        }
    }
}
