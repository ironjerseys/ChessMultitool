using ChessMultitool.Models;
using ChessMultitool.Services;

namespace ChessMultitool;

public partial class PuzzleListPage : ContentPage
{
    private const int PageSize = 50;
    private int currentPage = 0;

    private const string AnyThemeLabel = "All themes";
    private const string AnyRatingLabel = "All ratings";

    // Tranches de rating proposées dans le filtre: (label, min, max)
    private static readonly (string Label, int Min, int Max)[] RatingBuckets =
    {
        (AnyRatingLabel, 0, int.MaxValue),
        ("< 1000", 0, 999),
        ("1000 – 1400", 1000, 1399),
        ("1400 – 1800", 1400, 1799),
        ("1800 – 2200", 1800, 2199),
        ("2200+", 2200, int.MaxValue),
    };

    private bool filtersInitialized = false;

    // Indices globaux (dans PuzzleStore.Puzzles) des puzzles passant les filtres
    private List<int> filteredIndices = new();

    public PuzzleListPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (PuzzleStore.Puzzles.Count == 0)
        {
            LoadingView.IsVisible = true;
            PuzzleList.IsVisible = false;
            PuzzleStore.PuzzlesLoaded += OnPuzzlesLoaded;
        }
        else
        {
            InitFilters();
            ApplyFilters();
            ShowPage(currentPage);
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        PuzzleStore.PuzzlesLoaded -= OnPuzzlesLoaded;
    }

    private void OnPuzzlesLoaded()
    {
        Dispatcher.Dispatch(() =>
        {
            PuzzleStore.PuzzlesLoaded -= OnPuzzlesLoaded;
            InitFilters();
            ApplyFilters();
            ShowPage(0);
        });
    }

    /// <summary>Remplit les pickers de filtres (une seule fois) à partir des puzzles chargés.</summary>
    private void InitFilters()
    {
        if (filtersInitialized || PuzzleStore.Puzzles.Count == 0) return;
        filtersInitialized = true;

        var themes = PuzzleStore.Puzzles
            .SelectMany(p => p.Themes)
            .Distinct()
            .OrderBy(t => t)
            .ToList();
        var themeItems = new List<string> { AnyThemeLabel };
        themeItems.AddRange(themes);
        ThemeFilterPicker.ItemsSource = themeItems;
        ThemeFilterPicker.SelectedIndex = 0;

        RatingFilterPicker.ItemsSource = RatingBuckets.Select(b => b.Label).ToList();
        RatingFilterPicker.SelectedIndex = 0;
    }

    /// <summary>Recalcule la liste des indices passant les filtres courants.</summary>
    private void ApplyFilters()
    {
        var puzzles = PuzzleStore.Puzzles;

        string theme = ThemeFilterPicker.SelectedIndex > 0
            ? (string)ThemeFilterPicker.ItemsSource[ThemeFilterPicker.SelectedIndex]
            : null;

        var bucket = RatingFilterPicker.SelectedIndex >= 0
            ? RatingBuckets[RatingFilterPicker.SelectedIndex]
            : RatingBuckets[0];

        filteredIndices = puzzles
            .Select((p, i) => (p, i))
            .Where(x => theme == null || x.p.Themes.Contains(theme))
            .Where(x => x.p.Rating >= bucket.Min && x.p.Rating <= bucket.Max)
            .Select(x => x.i)
            .ToList();
    }

    private void OnFilterChanged(object? sender, EventArgs e)
    {
        if (!filtersInitialized) return;
        ApplyFilters();
        ShowPage(0);
    }

    private void ShowPage(int page)
    {
        var puzzles = PuzzleStore.Puzzles;
        if (puzzles.Count == 0) return;

        LoadingView.IsVisible = false;
        PuzzleList.IsVisible = true;

        if (filteredIndices.Count == 0)
        {
            PuzzleList.ItemsSource = new List<PuzzleListItem>();
            PageLabel.Text = "0 / 0";
            PageCountLabel.Text = "0 puzzles";
            PrevPageBtn.IsEnabled = false;
            NextPageBtn.IsEnabled = false;
            return;
        }

        int totalPages = (int)Math.Ceiling(filteredIndices.Count / (double)PageSize);
        currentPage = Math.Clamp(page, 0, totalPages - 1);
        int startIndex = currentPage * PageSize;
        int currentPuzzleIndex = PuzzleStore.CurrentIndex;

        var items = filteredIndices
            .Skip(startIndex)
            .Take(PageSize)
            .Select(gi => new PuzzleListItem(puzzles[gi], gi, gi == currentPuzzleIndex))
            .ToList();

        PuzzleList.ItemsSource = items;

        PageLabel.Text = $"{currentPage + 1} / {totalPages}";
        PageCountLabel.Text = $"{filteredIndices.Count} puzzles";
        PrevPageBtn.IsEnabled = currentPage > 0;
        NextPageBtn.IsEnabled = currentPage < totalPages - 1;
    }

    private void OnPrevPage(object? sender, EventArgs e)
    {
        if (currentPage > 0) ShowPage(currentPage - 1);
    }

    private void OnNextPage(object? sender, EventArgs e)
    {
        ShowPage(currentPage + 1);
    }

    private async void OnPuzzleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not PuzzleListItem item) return;
        PuzzleStore.PendingIndex = item.GlobalIndex;
        await Shell.Current.GoToAsync("//PuzzlesPage");
    }
}

public class PuzzleListItem
{
    public LichessPuzzle Puzzle { get; }
    public int GlobalIndex { get; }
    public bool IsCurrent { get; }
    public string RatingDisplay => $"★ {Puzzle.Rating}";
    public string ThemesDisplay => Puzzle.Themes.Length > 0
        ? string.Join(" · ", Puzzle.Themes.Take(3))
        : Puzzle.Id;

    public PuzzleListItem(LichessPuzzle puzzle, int globalIndex, bool isCurrent)
    {
        Puzzle = puzzle;
        GlobalIndex = globalIndex;
        IsCurrent = isCurrent;
    }
}
