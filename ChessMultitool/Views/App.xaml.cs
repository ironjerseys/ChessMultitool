using Microsoft.Maui.Storage; // for Preferences

namespace ChessMultitool
{
    public partial class App : Application
    {
        const string PrefBoardImage = "pref_board_image";

        public App()
        {
            // Dark-only minimal theme (shared design with ChessPuzzles)
            UserAppTheme = AppTheme.Dark;
            InitializeComponent();
            ApplyThemeResources();

            // Board image
            var boardImg = Preferences.Get(PrefBoardImage, "board_brown.png");
            Resources["BoardImageSource"] = boardImg;

            MainPage = new AppShell();
        }

        private void ApplyThemeResources()
        {
            Resources["GlobalBackgroundColor"] = Color.FromArgb("#111111");
            Resources["GlobalTextColor"] = Color.FromArgb("#EEEEEE");
            Resources["GlobalControlBackgroundColor"] = Color.FromArgb("#1E1E1E");
        }

        public static void SaveBoard(string file)
        {
            Preferences.Set(PrefBoardImage, file);
        }
    }
}
