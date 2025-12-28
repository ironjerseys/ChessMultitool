using Microsoft.Maui.Storage; // for Preferences

namespace ChessMultitool
{
    public partial class App : Application
    {
        const string PrefThemeDark = "pref_theme_dark";
        const string PrefBoardImage = "pref_board_image";

        public App()
        {
            // Read prefs first and set theme BEFORE loading XAML so AppThemeBinding resolves correctly
            bool dark = Preferences.Get(PrefThemeDark, true);
            UserAppTheme = dark ? AppTheme.Dark : AppTheme.Light;
            InitializeComponent();
            ApplyThemeResources(dark);

            // Board image
            var boardImg = Preferences.Get(PrefBoardImage, "board_brown.png");
            Resources["BoardImageSource"] = boardImg;

            MainPage = new AppShell();
        }

        private void ApplyThemeResources(bool dark)
        {
            if (dark)
            {
                Resources["GlobalBackgroundColor"] = Color.FromArgb("#333333");
                Resources["GlobalTextColor"] = Color.FromArgb("#FFFFFF");
                Resources["GlobalControlBackgroundColor"] = Color.FromArgb("#444444");
            }
            else
            {
                Resources["GlobalBackgroundColor"] = Color.FromArgb("#FFFFFF");
                Resources["GlobalTextColor"] = Color.FromArgb("#000000");
                Resources["GlobalControlBackgroundColor"] = Color.FromArgb("#FFFFFF");
            }
        }

        public static void SaveTheme(bool dark)
        {
            Preferences.Set(PrefThemeDark, dark);
        }

        public static void SaveBoard(string file)
        {
            Preferences.Set(PrefBoardImage, file);
        }

        public static void SetAppTheme(bool dark)
        {
            if (Current is App app)
            {
                app.UserAppTheme = dark ? AppTheme.Dark : AppTheme.Light;
                app.ApplyThemeResources(dark);
                SaveTheme(dark);
            }
        }
    }
}
