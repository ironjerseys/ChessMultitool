using System;
using Microsoft.Maui.Controls;

namespace ChessMultitool
{
    public partial class AILevelSelectionPage : ContentPage
    {
        public AILevelSelectionPage()
        {
            InitializeComponent();
        }

        private async void OnLevel1Clicked(object sender, EventArgs e) => await StartGameWithAI(1);
        private async void OnLevel2Clicked(object sender, EventArgs e) => await StartGameWithAI(2);
        private async void OnLevel3Clicked(object sender, EventArgs e) => await StartGameWithAI(3);
        private async void OnLevel4Clicked(object sender, EventArgs e) => await StartGameWithAI(4);
        private async void OnLevel5Clicked(object sender, EventArgs e) => await StartGameWithAI(5);
        private async void OnLevel6Clicked(object sender, EventArgs e) => await StartGameWithAI(6);
        private async void OnOpeningsClicked(object sender, EventArgs e) => await Navigation.PushAsync(new OpeningsPage());

        private async Task StartGameWithAI(int level)
        {
            // Définir les paramètres en fonction du niveau de l'IA.
            int searchDepth = level switch
            {
                1 => 2,
                2 => 3,
                3 => 4,
                4 => 5,
                5 => 6,
                6 => 7,
                _ => 2
            };

            TimeSpan thinkingTime = level switch
            {
                1 => TimeSpan.FromSeconds(2),
                2 => TimeSpan.FromSeconds(3),
                3 => TimeSpan.FromSeconds(4),
                4 => TimeSpan.FromSeconds(5),
                5 => TimeSpan.FromSeconds(6),
                6 => TimeSpan.FromSeconds(7),
                _ => TimeSpan.FromSeconds(2)
            };

            // Naviguer vers la page de jeu avec les paramètres de l'IA
            await Navigation.PushAsync(new ChessGame(searchDepth: searchDepth, thinkingTime: thinkingTime));
        }
    }
}