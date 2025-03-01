using System.Timers;

namespace ChessMultitool;

public partial class ClockPage : ContentPage
{
    private int _whiteTime;
    private int _blackTime;
    private string _currentPlayer = "";
    private System.Timers.Timer _timer;

    public ClockPage()
    {
        InitializeComponent();
        MinutesEntry.Text = "2";

        // Timer toutes les 1s
        _timer = new System.Timers.Timer(1000);
        _timer.Elapsed += OnTimerElapsed;
        _timer.AutoReset = true;
        _timer.Enabled = false;
    }

    private void OnSetTimeClicked(object sender, EventArgs e)
    {
        int minutes;
        if (!int.TryParse(MinutesEntry.Text, out minutes))
            minutes = 2;

        _whiteTime = minutes * 60;
        _blackTime = minutes * 60;
        _currentPlayer = "";
        _timer.Stop();
        UpdateUI();
    }

    private void OnWhiteTapped(object sender, TappedEventArgs e)
    {
        // Cliquer sur le haut => c'est le bas qui décrémente
        _currentPlayer = "black";
        StartTimer();
    }

    private void OnBlackTapped(object sender, TappedEventArgs e)
    {
        // Cliquer sur le bas => c'est le haut qui décrémente
        _currentPlayer = "white";
        StartTimer();
    }

    private void StartTimer()
    {
        _timer.Stop();
        if (!string.IsNullOrEmpty(_currentPlayer))
            _timer.Start();
    }

    private void OnTimerElapsed(object sender, ElapsedEventArgs e)
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
                    _timer.Stop();
                }
            }
            else if (_currentPlayer == "black")
            {
                if (_blackTime > 0) _blackTime--;
                if (_blackTime <= 0)
                {
                    _blackTime = 0;
                    _currentPlayer = "";
                    _timer.Stop();
                }
            }
            UpdateUI();
        });
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

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _timer?.Stop();
    }
}
