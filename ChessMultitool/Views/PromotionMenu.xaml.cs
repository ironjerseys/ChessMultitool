using ChessLogic;
using ChessMultitool.Logic;

namespace ChessMultitool;

public partial class PromotionMenu : ContentView
{
    public event Action<PieceType> PieceSelected;

    // Parameterless constructor to support XAML instantiation
    public PromotionMenu() : this(Player.White) { }

    public PromotionMenu(Player player)
    {
        InitializeComponent();

        // Charge les images dynamiquement
        QueenImg.Source = Images.GetImage(player, PieceType.Queen);
        BishopImg.Source = Images.GetImage(player, PieceType.Bishop);
        RookImg.Source = Images.GetImage(player, PieceType.Rook);
        KnightImg.Source = Images.GetImage(player, PieceType.Knight);
    }

    private void OnQueenTapped(object sender, EventArgs e)
    {
        PieceSelected?.Invoke(PieceType.Queen);
    }

    private void OnBishopTapped(object sender, EventArgs e)
    {
        PieceSelected?.Invoke(PieceType.Bishop);
    }

    private void OnRookTapped(object sender, EventArgs e)
    {
        PieceSelected?.Invoke(PieceType.Rook);
    }

    private void OnKnightTapped(object sender, EventArgs e)
    {
        PieceSelected?.Invoke(PieceType.Knight);
    }
}
