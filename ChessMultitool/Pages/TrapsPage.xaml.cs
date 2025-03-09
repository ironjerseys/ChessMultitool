using ChessMultitool.Models;

namespace ChessMultitool;

public partial class TrapsPage : ContentPage
{
    private TrapsData trapsData;
    private string selectedTrap;
    private string selectedVariation;
    private List<MoveData> moves;
    private int currentMoveIndex = 0;

    public TrapsPage()
    {
        InitializeComponent();
        moveImage.Source = "start.png";
        LoadTraps();
    }

    async void LoadTraps()
    {
        trapsData = await TrapService.LoadTrapsAsync();
        // Remplit le premier Picker avec les clés des ouvertures
        trapsPicker.ItemsSource = trapsData.Traps.Keys.ToList();
    }

    void OnTrapChanged(object sender, EventArgs e)
    {
        if (trapsPicker.SelectedIndex != -1)
        {
            selectedTrap = trapsPicker.Items[trapsPicker.SelectedIndex];

            // Met à jour le Picker des variations pour l'ouverture sélectionnée
            var variations = trapsData.Traps[selectedTrap].Keys.ToList();
            variationsPicker.ItemsSource = variations;

            if (variations.Any())
            {
                variationsPicker.SelectedIndex = 0;
                selectedVariation = variations[0];
                moves = trapsData.Traps[selectedTrap][selectedVariation];
                currentMoveIndex = 0;
            }
            else
            {
                variationsPicker.SelectedIndex = -1;
                moves = null;
                currentMoveIndex = 0;
            }
            UpdateUI();
        }
    }

    void OnVariationChanged(object sender, EventArgs e)
    {
        if (variationsPicker.SelectedIndex != -1)
        {
            selectedVariation = variationsPicker.Items[variationsPicker.SelectedIndex];
            moves = trapsData.Traps[selectedTrap][selectedVariation];
            currentMoveIndex = 0;
        }
        UpdateUI();
    }

    void UpdateUI()
    {
        if (moves != null && moves.Any())
        {
            var move = moves[currentMoveIndex];
            moveImage.Source = move.Image;
            moveLabel.Text = move.Move;
        }
        else
        {
            // Affiche une image de démarrage par défaut si aucun mouvement n'est chargé
            moveImage.Source = "start.png";
            moveLabel.Text = string.Empty;
        }
    }

    void OnPreviousClicked(object sender, EventArgs e)
    {
        if (moves != null && currentMoveIndex > 0)
        {
            currentMoveIndex--;
            UpdateUI();
        }
    }

    void OnNextClicked(object sender, EventArgs e)
    {
        if (moves != null && currentMoveIndex < moves.Count - 1)
        {
            currentMoveIndex++;
            UpdateUI();
        }
    }
}
