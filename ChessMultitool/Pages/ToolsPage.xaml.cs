namespace ChessMultitool;

public partial class ToolsPage : ContentPage
{
    public ToolsPage()
    {
        InitializeComponent();
    }

    private async void OnOpenOpeningsClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new OpeningsPage());
    }

    private async void OnOpenAchievementsClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new AchievementsPage());
    }

    private async void OnOpenClockClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new ClockPage());
    }
}
