using ChessLogic;
using Newtonsoft.Json;

namespace ChessMultitool;

/// <summary>
/// Page de configuration avant de lancer une partie contre l'IA.
/// Permet de choisir la couleur du joueur humain et éventuellement une ouverture/variation.
/// Le niveau de l'IA est unique: temps de réflexion fixe (modifiable ici si nécessaire).
/// </summary>
public partial class AILevelSelectionPage : ContentPage
{
    /// <summary>Modes de sélection de couleur.</summary>
    private enum ColorMode { White, Black, Alternate }

    /// <summary>Mode de couleur choisi (blanc par défaut).</summary>
    private ColorMode colorMode = ColorMode.White;

    /// <summary>Clé de préférence: dernière couleur jouée en mode alterné ("white"/"black").</summary>
    private const string LastAlternateColorKey = "pref_alternate_last_color";

    /// <summary>Temps de réflexion fixe de l'IA pour chaque coup (détermine la difficulté).</summary>
    private TimeSpan aiThinkingTime = TimeSpan.FromSeconds(2);

    /// <summary>Données des ouvertures: ouverture -> variation -> liste de coups normalisés.</summary>
    private Dictionary<string, Dictionary<string, List<string>>> openings;
    /// <summary>Noms des ouvertures disponibles.</summary>
    private List<string> openingNames = new();
    /// <summary>Variations pour l'ouverture actuellement sélectionnée.</summary>
    private List<string> variationNames = new();

    private const string AnyOpeningLabel = "Any (no restriction)";
    private const string AutoVariationLabel = "Auto (Main Line)";

    /// <summary>Constructeur: initialise l'UI et charge les ouvertures.</summary>
    public AILevelSelectionPage()
    {
        InitializeComponent();
        UpdateColorButtons();
        LoadOpenings();
    }

    /// <summary>Click sélection Blanc.</summary>
    private void OnWhiteClicked(object sender, EventArgs e)
    {
        colorMode = ColorMode.White;
        UpdateColorButtons();
    }

    /// <summary>Click sélection Noir.</summary>
    private void OnBlackClicked(object sender, EventArgs e)
    {
        colorMode = ColorMode.Black;
        UpdateColorButtons();
    }

    /// <summary>Click sélection Alterné (une partie avec les blancs, la suivante avec les noirs).</summary>
    private void OnAlternateClicked(object sender, EventArgs e)
    {
        colorMode = ColorMode.Alternate;
        UpdateColorButtons();
    }

    /// <summary>Met à jour l'état visuel des cartes de couleur.</summary>
    private void UpdateColorButtons()
    {
        var accent = Color.FromArgb("#C8963E");
        WhiteCard.Stroke = (colorMode == ColorMode.White) ? accent : Colors.Transparent;
        AlternateCard.Stroke = (colorMode == ColorMode.Alternate) ? accent : Colors.Transparent;
        BlackCard.Stroke = (colorMode == ColorMode.Black) ? accent : Colors.Transparent;
    }

    /// <summary>Résout la couleur effective pour la partie à lancer.
    /// En mode alterné, joue l'opposé de la dernière couleur jouée et mémorise le choix.</summary>
    private Player ResolveHumanColor()
    {
        if (colorMode == ColorMode.White) return Player.White;
        if (colorMode == ColorMode.Black) return Player.Black;

        var last = Preferences.Get(LastAlternateColorKey, "black");
        var next = last == "white" ? Player.Black : Player.White;
        Preferences.Set(LastAlternateColorKey, next == Player.White ? "white" : "black");
        return next;
    }

    /// <summary>Charge la base d'ouvertures depuis le fichier JSON embarqué.</summary>
    private async void LoadOpenings()
    {
        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync("openings.json");
            using var reader = new StreamReader(stream);
            string json = await reader.ReadToEndAsync();
            var raw = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, Dictionary<string, List<string>>>>>(json);
            openings = raw["openings"]; // exception si format invalide
            openingNames = openings.Keys.OrderBy(k => k).ToList();
            var openingItems = new List<string> { AnyOpeningLabel };
            openingItems.AddRange(openingNames);
            OpeningPicker.ItemsSource = openingItems;
            OpeningPicker.SelectedIndex = 0;
            VariationPicker.ItemsSource = new List<string> { AutoVariationLabel };
            VariationPicker.SelectedIndex = 0;
        }
        catch
        {
            // Fallback minimal si le JSON n'est pas dispo
            openings = null;
            OpeningPicker.ItemsSource = new List<string> { AnyOpeningLabel };
            OpeningPicker.SelectedIndex = 0;
            VariationPicker.ItemsSource = new List<string> { AutoVariationLabel };
            VariationPicker.SelectedIndex = 0;
        }
    }

    /// <summary>Gestion du changement d'ouverture (rebuilt liste de variations).</summary>
    private void OnOpeningChanged(object sender, EventArgs e)
    {
        if (OpeningPicker.SelectedIndex <= 0)
        {
            VariationPicker.ItemsSource = new List<string> { AutoVariationLabel };
            VariationPicker.SelectedIndex = 0;
            variationNames = new();
            return;
        }
        if (openings == null) return;
        var openingName = openingNames[OpeningPicker.SelectedIndex - 1];
        variationNames = openings[openingName].Keys.OrderBy(k => k).ToList();
        var items = new List<string> { AutoVariationLabel };
        items.AddRange(variationNames);
        VariationPicker.ItemsSource = items;
        VariationPicker.SelectedIndex = 0;
    }

    /// <summary>Variation changée (rien à faire, utilisé à la validation).</summary>
    private void OnVariationChanged(object sender, EventArgs e) { }

    /// <summary>Démarre la partie en construisant éventuellement la ligne d'ouverture choisie.</summary>
    private async void OnStartClicked(object sender, EventArgs e)
    {
        List<string> chosenLine = null;
        string selectedOpeningName = null;

        if (openings != null && OpeningPicker.SelectedIndex > 0)
        {
            var openingName = openingNames[OpeningPicker.SelectedIndex - 1];
            selectedOpeningName = openingName;
            if (variationNames == null || variationNames.Count == 0)
            {
                variationNames = openings[openingName].Keys.OrderBy(k => k).ToList();
            }
            string variationName = null;
            if (VariationPicker.SelectedIndex > 0)
            {
                variationName = variationNames[VariationPicker.SelectedIndex - 1];
            }
            else
            {
                variationName = variationNames.FirstOrDefault(v => v.Equals("Main Line", StringComparison.OrdinalIgnoreCase))
                                 ?? variationNames.FirstOrDefault();
            }
            if (!string.IsNullOrEmpty(variationName))
            {
                chosenLine = openings[openingName][variationName];
            }
        }

        // IA rating indicatif (unique) si besoin pour achievements
        int aiRating = 600;

        await Navigation.PushAsync(new ChessGame(ResolveHumanColor(), aiThinkingTime, chosenLine, selectedOpeningName, aiRating));
    }

    /// <summary>Ouvre la page d'exploration des ouvertures.</summary>
    private async void OnOpeningsClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new OpeningsPage());
    }

    /// <summary>Ouvre la page des succès / achievements.</summary>
    private async void OnAchievementsClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new AchievementsPage());
    }
}