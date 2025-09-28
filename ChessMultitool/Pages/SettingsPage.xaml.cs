using Microsoft.Maui.Controls;

namespace ChessMultitool;

public partial class SettingsPage : ContentPage
{
    public SettingsPage()
    {
        InitializeComponent();
        // Appliquer le th�me par d�faut (dark mode)
        ApplyTheme(true);
    }

    private void OnThemeToggled(object sender, ToggledEventArgs e)
    {
        ApplyTheme(e.Value);
    }

    private void ApplyTheme(bool isDarkMode)
    {
        // Mettre � jour la ressource globale pour le style de ContentPage
        // On d�finit ici CurrentContentPageStyle qui est utilis� dans App.xaml sur toutes les pages.
        Application.Current.Resources["CurrentContentPageStyle"] = isDarkMode
            ? Application.Current.Resources["DarkModeStyle"]
            : Application.Current.Resources["LightModeStyle"];
    }
}