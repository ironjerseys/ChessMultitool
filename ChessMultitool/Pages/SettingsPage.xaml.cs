using Microsoft.Maui.Controls;

namespace ChessMultitool;

public partial class SettingsPage : ContentPage
{
    private readonly Dictionary<string, Border> themeMap = new();

    public SettingsPage()
    {
        InitializeComponent();
        themeMap["board_brown.png"]       = ThemeBrown;
        themeMap["board_green.png"]       = ThemeGreen;
        themeMap["board_blue.png"]        = ThemeBlue;
        themeMap["board_wood.png"]        = ThemeWood;
        themeMap["board_gray.png"]        = ThemeGray;
        themeMap["board_black_white.png"] = ThemeClassic;
        themeMap["board_purple.png"]      = ThemePurple;

        InitVibrationSwitch();
        InitEvalBarSwitch();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        var current = Preferences.Get("pref_board_image", "board_brown.png");
        HighlightSelected(current);
    }

    private void InitVibrationSwitch()
    {
        VibrationSwitch.IsToggled = Preferences.Get("pref_vibration_moves", true);
    }

    private void InitEvalBarSwitch()
    {
        EvalBarSwitch.IsToggled = Preferences.Get("pref_eval_bar", true);
    }

    private void OnThemeTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not string file) return;
        Application.Current.Resources["BoardImageSource"] = file;
        App.SaveBoard(file);
        HighlightSelected(file);
    }

    private void HighlightSelected(string theme)
    {
        foreach (var kvp in themeMap)
            kvp.Value.Stroke = kvp.Key == theme
                ? Color.FromArgb("#C8963E")
                : Colors.Transparent;
    }

    private void OnVibrationToggled(object sender, ToggledEventArgs e)
    {
        Preferences.Set("pref_vibration_moves", e.Value);
        if (Application.Current?.MainPage is AppShell shell && shell.CurrentPage is ChessGame game)
        {
            game.SetVibrationEnabled(e.Value);
        }
    }

    private void OnEvalBarToggled(object sender, ToggledEventArgs e)
    {
        Preferences.Set("pref_eval_bar", e.Value);
        if (Application.Current?.MainPage is AppShell shell && shell.CurrentPage is ChessGame game)
        {
            game.SetEvalBarVisible(e.Value);
        }
    }
}
