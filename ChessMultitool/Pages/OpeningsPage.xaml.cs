using ChessMultitool.Models;

namespace ChessMultitool;

public partial class OpeningsPage : ContentPage
{
    private OpeningsData openingsData;
    private string selectedOpening;
    private string selectedVariation;
    private List<MoveData> moves;
    private int currentMoveIndex = 0;

    public OpeningsPage()
    {
        InitializeComponent();
        moveImage.Source = "start.png";
        LoadOpenings();
    }

    async void LoadOpenings()
    {
        openingsData = await OpeningService.LoadOpeningsAsync();
        // Remplit le premier Picker avec les clés des ouvertures
        openingsPicker.ItemsSource = openingsData.Openings.Keys.ToList();
    }

    void OnOpeningChanged(object sender, EventArgs e)
    {
        if (openingsPicker.SelectedIndex != -1)
        {
            selectedOpening = openingsPicker.Items[openingsPicker.SelectedIndex];

            // Met à jour le Picker des variations pour l'ouverture sélectionnée
            var variations = openingsData.Openings[selectedOpening].Keys.ToList();
            variationsPicker.ItemsSource = variations;
            variationsPicker.SelectedIndex = -1;

            moves = null;
            currentMoveIndex = 0;
            UpdateUI();
        }
    }

    void OnVariationChanged(object sender, EventArgs e)
    {
        if (variationsPicker.SelectedIndex != -1)
        {
            selectedVariation = variationsPicker.Items[variationsPicker.SelectedIndex];
            moves = openingsData.Openings[selectedOpening][selectedVariation];
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
