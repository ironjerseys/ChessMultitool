using Microsoft.Maui.Controls;

namespace ChessMultitool;

public partial class SettingsPage : ContentPage
{
    // Mapping Nom lisible -> Fichier image
    private readonly Dictionary<string, string> boardMap = new()
    {
        { "Brown", "board_brown.png" },
        { "Green", "board_green.png" },
        { "Blue",  "board_blue.png" },
        { "Gray",  "board_gray.png" },
        { "Purple","board_purple.png" },
        { "Black & White", "board_black_white.png" },
    };

    public SettingsPage()
    {
        InitializeComponent();
        InitThemeSwitch();
        InitBoardPicker();
        InitVibrationSwitch();
    }

    private void InitThemeSwitch()
    {
        bool dark = Preferences.Get("pref_theme_dark", true);
        ThemeSwitch.IsToggled = dark;
        ApplyTheme(dark); // ensure resources reflect preference immediately
    }

    private void InitBoardPicker()
    {
        BoardPicker.ItemsSource = boardMap.Keys.ToList();

        if (Application.Current.Resources.TryGetValue("BoardImageSource", out var val) && val is string currentFile)
        {
            var match = boardMap.FirstOrDefault(kv => kv.Value == currentFile).Key;
            if (!string.IsNullOrEmpty(match))
            {
                BoardPicker.SelectedItem = match;
                return;
            }
        }
        BoardPicker.SelectedItem = boardMap.Keys.First();
        Application.Current.Resources["BoardImageSource"] = boardMap[(string)BoardPicker.SelectedItem];
    }

    private void InitVibrationSwitch()
    {
        bool vib = Preferences.Get("pref_vibration_moves", true);
        VibrationSwitch.IsToggled = vib;
    }


    private void OnBoardChanged(object sender, EventArgs e)
    {
        if (BoardPicker.SelectedItem is string display && boardMap.TryGetValue(display, out var file))
        {
            Application.Current.Resources["BoardImageSource"] = file;
            App.SaveBoard(file);
        }
    }

    private void OnThemeToggled(object sender, ToggledEventArgs e)
    {
        ApplyTheme(e.Value);
        App.SaveTheme(e.Value);
    }

    private void OnVibrationToggled(object sender, ToggledEventArgs e)
    {
        Preferences.Set("pref_vibration_moves", e.Value);
        if (Application.Current?.MainPage is AppShell shell && shell.CurrentPage is ChessGame game)
        {
            game.SetVibrationEnabled(e.Value);
        }
    }

    private void OnAiConsiderToggled(object sender, ToggledEventArgs e)
    {
        Preferences.Set("pref_ai_consider_highlight", e.Value);
    }

    private void OnEvalBarToggled(object sender, ToggledEventArgs e)
    {
        Preferences.Set("pref_eval_bar", e.Value);
        if (Application.Current?.MainPage is AppShell shell && shell.CurrentPage is ChessGame game)
        {
            game.SetEvalBarVisible(e.Value);
        }
    }

    private void ApplyTheme(bool dark)
    {
        Application.Current.Resources["GlobalBackgroundColor"] = dark ? Color.FromArgb("#333333") : Color.FromArgb("#FFFFFF");
        Application.Current.Resources["GlobalTextColor"] = dark ? Color.FromArgb("#FFFFFF") : Color.FromArgb("#000000");
        Application.Current.Resources["GlobalControlBackgroundColor"] = dark ? Color.FromArgb("#444444") : Color.FromArgb("#FFFFFF");
    }
}