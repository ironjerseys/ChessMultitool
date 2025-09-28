using ChessMultitool.Pages;

namespace ChessMultitool
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            Routing.RegisterRoute(nameof(AILevelSelectionPage), typeof(AILevelSelectionPage));
        }
    }
}
