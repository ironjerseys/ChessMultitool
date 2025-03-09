namespace ChessMultitool
{
    public partial class ClockPage : ContentPage
    {
        private int _whiteTime;
        private int _blackTime;
        private string _currentPlayer = "";
        private PeriodicTimer _timer;
        private bool _isTimerRunning = false;

        public ClockPage()
        {
            InitializeComponent();
            MinutesEntry.Text = "2";

            // Timer toutes les 1s
            _timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

            // Initialiser les temps et mettre à jour l'interface utilisateur
            _whiteTime = 2 * 60;
            _blackTime = 2 * 60;
            UpdateUI();
        }

        private void OnSetTimeClicked(object sender, EventArgs e)
        {
            int minutes;
            if (!int.TryParse(MinutesEntry.Text, out minutes))
                minutes = 2;

            _whiteTime = minutes * 60;
            _blackTime = minutes * 60;
            _currentPlayer = "";
            _isTimerRunning = false;
            UpdateUI();
        }

        private async void OnWhiteTapped(object sender, TappedEventArgs e)
        {
            // Cliquer sur le haut => c'est le bas qui décrémente
            _currentPlayer = "black";
            await StartTimerAsync();
        }

        private async void OnBlackTapped(object sender, TappedEventArgs e)
        {
            // Cliquer sur le bas => c'est le haut qui décrémente
            _currentPlayer = "white";
            await StartTimerAsync();
        }

        private async Task StartTimerAsync()
        {
            if (_isTimerRunning)
            {
                return;
            }

            _isTimerRunning = true;

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
                {
                    break;
                }
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

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _isTimerRunning = false;
            _timer?.Dispose();
        }
    }
}
