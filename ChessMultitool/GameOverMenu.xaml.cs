using ChessLogic;
using ChessUI;

namespace ChessMultitool;

public partial class GameOverMenu : ContentView
{
    public event Action<Option> OptionSelected;

    public GameOverMenu(GameState gameState)
    {
        InitializeComponent();

        Result result = gameState.Result;
        WinnerText.Text = GetWinnerText(result.Winner);
        ReasonText.Text = GetReasonText(result.Reason, gameState.CurrentPlayer);
    }

    private static string GetWinnerText(Player winner)
    {
        return winner switch
        {
            Player.White => "WHITE WINS!",
            Player.Black => "BLACK WINS!",
            _ => "IT'S A DRAW"
        };
    }

    private static string PlayerString(Player player)
    {
        return player switch
        {
            Player.White => "WHITE",
            Player.Black => "BLACK",
            _ => ""
        };
    }

    private static string GetReasonText(EndReason reason, Player currentPlayer)
    {
        return reason switch
        {
            EndReason.Stalemate => $"STALEMATE",
            EndReason.Checkmate => $"CHECKMATE",
            EndReason.FiftyMoveRule => "FIFTY-MOVE RULE",
            EndReason.InsufficientMaterial => "INSUFFICIENT MATERIAL",
            EndReason.ThreefoldRepetition => "THREEFOLD REPETITION",
            _ => ""
        };
    }

    private void Restart_Click(object sender, EventArgs e)
    {
        OptionSelected?.Invoke(Option.Restart);
    }
}
