using Microsoft.Maui.Controls;

namespace ChessMultitool;

public partial class SettingsPage : ContentPage
{
    public SettingsPage()
    {
        InitializeComponent();
        // Appliquer le thème par défaut (dark mode)
        ApplyTheme(true);
    }

    private void OnThemeToggled(object sender, ToggledEventArgs e)
    {
        ApplyTheme(e.Value);
    }

    private void ApplyTheme(bool isDarkMode)
    {
        // Mettre à jour la ressource globale pour le style de ContentPage
        // On définit ici CurrentContentPageStyle qui est utilisé dans App.xaml sur toutes les pages.
        Application.Current.Resources["CurrentContentPageStyle"] = isDarkMode
            ? Application.Current.Resources["DarkModeStyle"]
            : Application.Current.Resources["LightModeStyle"];
    }
}