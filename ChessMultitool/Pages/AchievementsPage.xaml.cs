using ChessMultitool.Services;

namespace ChessMultitool;

public partial class AchievementsPage : ContentPage
{
    public AchievementsPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        var all = await AchievementService.GetAllAsync();
        var openings = await AchievementService.GetAllOpeningNamesAsync();

        // Quick wins thresholds (full moves)
        var thresholds = new[] { 60, 50, 40, 30, 20 };
        var quick = new List<Row>();
        foreach (var t in thresholds)
        {
            bool achieved = all.TryGetValue($"ai_quick_win_under:{t}", out var c) && c > 0;
            quick.Add(new Row { Name = $"Beat AI under {t} moves", Achieved = achieved });
        }
        QuickWinsList.ItemsSource = quick;

        // Openings (based only on picker selection at start)
        var openingRows = new List<Row>();
        foreach (var name in openings)
        {
            int count = all.TryGetValue($"opening_selected_win:{name}", out var n) ? n : 0;
            openingRows.Add(new Row { Name = name, Achieved = count > 0 });
        }
        WithList.ItemsSource = openingRows.OrderBy(r => r.Name).ToList();

        // Optionally hide AI section if present in XAML
        try { AiList.IsVisible = false; } catch { }
    }

    public class Row
    {
        public string Name { get; set; }
        public bool Achieved { get; set; }
    }
}
