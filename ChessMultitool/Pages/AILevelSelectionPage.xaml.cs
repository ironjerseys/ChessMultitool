using System;
using Microsoft.Maui.Controls;
using ChessLogic;

namespace ChessMultitool
{
    public partial class AILevelSelectionPage : ContentPage
    {
        private int pendingSearchDepth = 3;
        private TimeSpan pendingThinking = TimeSpan.FromSeconds(3);
        private Player humanColor = Player.White; // par defaut

        public AILevelSelectionPage()
        {
            InitializeComponent();
            UpdateColorButtons();
        }

        private void UpdateColorButtons()
        {
            // Bouton blanc
            WhiteBtn.BackgroundColor = Colors.White;
            WhiteBtn.Text = string.Empty;
            WhiteBtn.BorderWidth = (humanColor == Player.White) ? 3 : 1;
            WhiteBtn.BorderColor = (humanColor == Player.White) ? Colors.DeepSkyBlue : Colors.Gray;

            // Bouton noir
            BlackBtn.BackgroundColor = Colors.Black;
            BlackBtn.Text = string.Empty;
            BlackBtn.BorderWidth = (humanColor == Player.Black) ? 3 : 1;
            BlackBtn.BorderColor = (humanColor == Player.Black) ? Colors.DeepSkyBlue : Colors.Gray;
        }

        private void OnWhiteClicked(object sender, EventArgs e)
        {
            humanColor = Player.White;
            UpdateColorButtons();
        }

        private void OnBlackClicked(object sender, EventArgs e)
        {
            humanColor = Player.Black;
            UpdateColorButtons();
        }

        private async void OnStartClicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new ChessGame(humanColor, pendingSearchDepth, pendingThinking));
        }

        private async void OnLevel1Clicked(object sender, EventArgs e) => await SetLevelAndMaybeAutostart(1);
        private async void OnLevel2Clicked(object sender, EventArgs e) => await SetLevelAndMaybeAutostart(2);
        private async void OnLevel3Clicked(object sender, EventArgs e) => await SetLevelAndMaybeAutostart(3);
        private async void OnLevel4Clicked(object sender, EventArgs e) => await SetLevelAndMaybeAutostart(4);
        private async void OnLevel5Clicked(object sender, EventArgs e) => await SetLevelAndMaybeAutostart(5);
        private async void OnLevel6Clicked(object sender, EventArgs e) => await SetLevelAndMaybeAutostart(6);

        private async Task SetLevelAndMaybeAutostart(int level)
        {
            (pendingSearchDepth, pendingThinking) = level switch
            {
                1 => (2, TimeSpan.FromSeconds(2)),
                2 => (3, TimeSpan.FromSeconds(3)),
                3 => (4, TimeSpan.FromSeconds(4)),
                4 => (5, TimeSpan.FromSeconds(5)),
                5 => (6, TimeSpan.FromSeconds(6)),
                6 => (7, TimeSpan.FromSeconds(7)),
                _ => (2, TimeSpan.FromSeconds(2))
            };
            // Option : démarrer immédiatement après choix niveau
            await Navigation.PushAsync(new ChessGame(humanColor, pendingSearchDepth, pendingThinking));
        }

        private async void OnOpeningsClicked(object sender, EventArgs e) => await Navigation.PushAsync(new OpeningsPage());
    }
}