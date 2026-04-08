using ChessLogic;
using Xunit;

namespace ChessMultitool.Tests;

public class ComprehensiveLogicTests
{
    [Fact]
    public void Fen_FromFen_ParsesInitialPositionAndSideToMove()
    {
        var state = Fen.FromFen("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");

        Assert.Equal(Player.White, state.CurrentPlayer);
        Assert.Equal(PieceType.King, state.Board[new Position(7, 4)].Type);
        Assert.Equal(Player.White, state.Board[new Position(7, 4)].Color);
        Assert.Equal(PieceType.King, state.Board[new Position(0, 4)].Type);
        Assert.Equal(Player.Black, state.Board[new Position(0, 4)].Color);
    }

    [Theory]
    [InlineData("8/8/8/8/8/8/8 w - - 0 1")]
    [InlineData("8/8/8/8/8/8/8/8")]
    [InlineData("8/8/8/8/8/8/8/9 w - - 0 1")]
    [InlineData("8/8/8/8/8/8/8/X7 w - - 0 1")]
    public void Fen_FromFen_InvalidInput_Throws(string fen)
    {
        Assert.ThrowsAny<ArgumentException>(() => Fen.FromFen(fen));
    }

    [Fact]
    public void Fen_FromFen_SetsEnPassantSkipForOpponent()
    {
        var state = Fen.FromFen("8/8/8/4Pp2/8/8/8/8 w - f6 0 1");

        Assert.Equal(new Position(2, 5), state.Board.GetPawnSkipPosition(Player.Black));
        Assert.Null(state.Board.GetPawnSkipPosition(Player.White));
    }

    [Fact]
    public void StateString_InitialBoard_ContainsCastlingRightsAndNoEnPassant()
    {
        var state = new GameState(Player.White, Board.Initial());

        var text = new StateString(state.CurrentPlayer, state.Board).ToString();

        Assert.Equal("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq -", text);
    }

    [Fact]
    public void Board_InsufficientMaterial_DetectsKingBishopVsKingBishopSameColor()
    {
        var board = EmptyBoardWithKings();
        board[new Position(5, 0)] = new Bishop(Player.White); // couleur noire
        board[new Position(3, 2)] = new Bishop(Player.Black); // couleur noire

        Assert.True(board.InsufficientMaterial());
    }

    [Fact]
    public void Board_InsufficientMaterial_IsFalseWhenExtraMajorPieceExists()
    {
        var board = EmptyBoardWithKings();
        board[new Position(5, 0)] = new Bishop(Player.White);
        board[new Position(3, 2)] = new Bishop(Player.Black);
        board[new Position(0, 7)] = new Rook(Player.Black);

        Assert.False(board.InsufficientMaterial());
    }

    [Fact]
    public void Board_CanCaptureEnPassant_ReturnsTrueWhenPawnCanCapture()
    {
        var board = new Board();
        board[new Position(7, 4)] = new King(Player.White);
        board[new Position(0, 4)] = new King(Player.Black);
        board[new Position(3, 4)] = new Pawn(Player.White);
        board.SetPawnSkipPosition(Player.Black, new Position(2, 5));

        Assert.True(board.CanCaptureEnPassant(Player.White));
    }

    [Fact]
    public void Pawn_GetMoves_FromStart_IncludesSingleAndDoublePush()
    {
        var board = new Board();
        var from = new Position(6, 4);
        board[from] = new Pawn(Player.White);

        var moves = board[from].GetMoves(from, board).ToList();

        Assert.Contains(moves, m => m.Type == MoveType.Normal && m.ToPos == new Position(5, 4));
        Assert.Contains(moves, m => m.Type == MoveType.DoublePawn && m.ToPos == new Position(4, 4));
    }

    [Fact]
    public void Pawn_GetMoves_GeneratesFourPromotionOptionsOnForwardMove()
    {
        var board = new Board();
        var from = new Position(1, 0);
        board[from] = new Pawn(Player.White);

        var promotionMoves = board[from].GetMoves(from, board)
            .Where(m => m.Type == MoveType.PawnPromotion)
            .ToList();

        Assert.Equal(4, promotionMoves.Count);
    }

    [Fact]
    public void Move_Execute_EnPassantRemovesCapturedPawn()
    {
        var board = new Board();
        var whitePawnFrom = new Position(3, 4);
        var blackPawnCaptured = new Position(3, 5);
        var target = new Position(2, 5);
        board[whitePawnFrom] = new Pawn(Player.White);
        board[blackPawnCaptured] = new Pawn(Player.Black);

        var move = new EnPassant(whitePawnFrom, target);
        _ = move.Execute(board);

        Assert.Null(board[whitePawnFrom]);
        Assert.Null(board[blackPawnCaptured]);
        Assert.NotNull(board[target]);
        Assert.Equal(Player.White, board[target].Color);
    }

    [Fact]
    public void Move_Execute_CastleKingSide_MovesKingAndRook()
    {
        var board = new Board();
        var kingPos = new Position(7, 4);
        board[kingPos] = new King(Player.White);
        board[new Position(7, 7)] = new Rook(Player.White);

        var move = new Castle(MoveType.CastleKS, kingPos);
        _ = move.Execute(board);

        Assert.Null(board[kingPos]);
        Assert.Equal(PieceType.King, board[new Position(7, 6)].Type);
        Assert.Equal(PieceType.Rook, board[new Position(7, 5)].Type);
    }

    [Fact]
    public void Move_Execute_PawnPromotion_PromotesToSelectedPiece()
    {
        var board = new Board();
        var from = new Position(1, 0);
        var to = new Position(0, 0);
        board[from] = new Pawn(Player.White);

        var move = new PawnPromotion(from, to, PieceType.Knight);
        _ = move.Execute(board);

        Assert.Null(board[from]);
        Assert.Equal(PieceType.Knight, board[to].Type);
        Assert.True(board[to].HasMoved);
    }

    [Fact]
    public void GameState_LegalMovesForPiece_ReturnsEmptyForOpponentsPiece()
    {
        var state = new GameState(Player.White, Board.Initial());

        var moves = state.LegalMovesForPiece(new Position(1, 0));

        Assert.Empty(moves);
    }

    [Fact]
    public void GameState_MakeMove_SetsCheckmateResult()
    {
        // White to move, Qf7#
        var state = Fen.FromFen("6k1/5Q2/6K1/8/8/8/8/8 w - - 0 1");
        var move = new NormalMove(new Position(1, 5), new Position(1, 6));

        state.MakeMove(move);

        Assert.True(state.IsGameOver());
        Assert.Equal(Player.White, state.Result.Winner);
        Assert.Equal(EndReason.Checkmate, state.Result.Reason);
    }

    [Fact]
    public void GameState_MakeMove_DetectsThreefoldRepetition()
    {
        var state = Fen.FromFen("8/8/8/8/8/8/N7/k1K5 w - - 0 1");

        for (var i = 0; i < 2; i++)
        {
            state.MakeMove(new NormalMove(new Position(6, 0), new Position(4, 1))); // Na2-b4
            state.MakeMove(new NormalMove(new Position(7, 0), new Position(6, 0))); // Ka1-a2
            state.MakeMove(new NormalMove(new Position(4, 1), new Position(6, 0))); // Nb4-a2
            state.MakeMove(new NormalMove(new Position(6, 0), new Position(7, 0))); // Ka2-a1
        }

        Assert.True(state.IsGameOver());
        Assert.Equal(EndReason.ThreefoldRepetition, state.Result.Reason);
    }

    [Fact]
    public void Position_OperatorsAndSquareColor_WorkAsExpected()
    {
        var pos = new Position(4, 4);
        var shifted = pos + Direction.NorthWest;

        Assert.Equal(new Position(3, 3), shifted);
        Assert.Equal(Player.White, pos.SquareColor());
        Assert.Equal(Player.Black, shifted.SquareColor());
        Assert.True(pos != shifted);
        Assert.True(pos == new Position(4, 4));
    }

    [Fact]
    public void PlayerExtensions_Opponent_ReturnsExpectedValue()
    {
        Assert.Equal(Player.Black, Player.White.Opponent());
        Assert.Equal(Player.White, Player.Black.Opponent());
        Assert.Equal(Player.None, Player.None.Opponent());
    }

    [Fact]
    public void MiniMaxEngine_FindBestMove_ReturnsMoveAndStats()
    {
        var engine = new MiniMaxEngine();
        var state = Fen.FromFen("4k3/8/8/8/8/8/4q3/4K3 b - - 0 1");

        long generated = 0;
        long visited = 0;
        long leaves = 0;

        var best = engine.FindBestMove(state, depth: 2, timeMs: 500, onStats: (g, v, l) =>
        {
            generated = g;
            visited = v;
            leaves = l;
        });

        Assert.NotNull(best);
        Assert.True(generated > 0);
        Assert.True(visited > 0);
        Assert.True(leaves > 0);
    }

    private static Board EmptyBoardWithKings()
    {
        var board = new Board
        {
            [new Position(7, 4)] = new King(Player.White),
            [new Position(0, 4)] = new King(Player.Black)
        };
        return board;
    }
}
