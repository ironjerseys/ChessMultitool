namespace ChessMultitool;

public partial class ClockPage : ContentPage
{
    private int _whiteTime;
    private int _blackTime;
    private string _currentPlayer = "";
    private PeriodicTimer? _timer; // nullable
    private bool _isTimerRunning = false;

    // Incrément en secondes
    private int _incrementSeconds = 0;

    public ClockPage()
    {
        InitializeComponent();

        // Défauts: 2 minutes + 1 seconde
        MinutesEntry.Text = "2";
        IncrementEntry.Text = "1";

        // Timer toutes les 1s
        EnsureTimer();

        // Initialiser les temps et mettre à jour l'interface utilisateur
        _whiteTime = 2 * 60;
        _blackTime = 2 * 60;
        _incrementSeconds = 1;

        UpdateUI();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        EnsureTimer();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _isTimerRunning = false;
        _currentPlayer = "";
        _timer?.Dispose();
        _timer = null; // marquer comme besoin de recréer
    }

    private void EnsureTimer()
    {
        if (_timer == null)
            _timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
    }

    private void OnSetTimeClicked(object sender, EventArgs e)
    {
        if (!int.TryParse(MinutesEntry.Text, out int minutes))
        {
            minutes = 2;
        }

        if (!int.TryParse(IncrementEntry.Text, out int increment))
        {
            increment = 0;
        }

        _whiteTime = minutes * 60;
        _blackTime = minutes * 60;
        _incrementSeconds = increment;

        _currentPlayer = "";
        _isTimerRunning = false;
        UpdateUI();
    }

    private async void OnWhiteTapped(object sender, TappedEventArgs e)
    {
        // Cliquer sur le haut => c'est le bas qui décrémente
        ApplyIncrementTo("white");
        _currentPlayer = "black";
        await StartTimerAsync();
    }

    private async void OnBlackTapped(object sender, TappedEventArgs e)
    {
        // Cliquer sur le bas => c'est le haut qui décrémente
        ApplyIncrementTo("black");
        _currentPlayer = "white";
        await StartTimerAsync();
    }

    private void ApplyIncrementTo(string player)
    {
        if (_incrementSeconds <= 0) return;

        if (player == "white")
            _whiteTime += _incrementSeconds;
        else if (player == "black")
            _blackTime += _incrementSeconds;
    }

    private async Task StartTimerAsync()
    {
        if (_isTimerRunning)
            return;

        EnsureTimer();
        if (_timer == null)
            return; // sécurité

        _isTimerRunning = true;

        try
        {
            while (await _timer.WaitForNextTickAsync())
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (_currentPlayer == "white")
                    {
                        if (_whiteTime > 0) _whiteTime--;
                        if (_whiteTime <= 0)
                        {
                            _whiteTime = 0;
                            _currentPlayer = "";
                            _isTimerRunning = false;
                        }
                    }
                    else if (_currentPlayer == "black")
                    {
                        if (_blackTime > 0) _blackTime--;
                        if (_blackTime <= 0)
                        {
                            _blackTime = 0;
                            _currentPlayer = "";
                            _isTimerRunning = false;
                        }
                    }
                    UpdateUI();
                });

                if (!_isTimerRunning)
                    break;
            }
        }
        catch (ObjectDisposedException)
        {
            // Peut arriver si la page disparaît pendant l’attente: on ignore proprement
            _isTimerRunning = false;
        }
    }

    private void UpdateUI()
    {
        WhiteTimeLabel.Text = FormatTime(_whiteTime);
        BlackTimeLabel.Text = FormatTime(_blackTime);

        // Couleur de fond
        //   - si temps = 0 => Rouge
        //   - si pas actif => gris
        //   - si actif => vert

        // Zone Blanche
        if (_whiteTime == 0)
            WhiteTimeLabel.BackgroundColor = Colors.Red;
        else if (_currentPlayer != "white")
            WhiteTimeLabel.BackgroundColor = Color.FromArgb("#DDDDDD");
        else
            WhiteTimeLabel.BackgroundColor = Colors.Green;

        // Zone Noire
        if (_blackTime == 0)
            BlackTimeLabel.BackgroundColor = Colors.Red;
        else if (_currentPlayer != "black")
            BlackTimeLabel.BackgroundColor = Color.FromArgb("#CCCCCC");
        else
            BlackTimeLabel.BackgroundColor = Colors.Green;
    }

    private string FormatTime(int seconds)
    {
        int m = seconds / 60;
        int s = seconds % 60;
        return $"{m:00}:{s:00}";
    }
}
