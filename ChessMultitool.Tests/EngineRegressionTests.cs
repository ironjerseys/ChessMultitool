using System.Diagnostics;
using ChessLogic;
using Xunit;
using Xunit.Abstractions;

namespace ChessMultitool.Tests;

public sealed class EngineRegressionTests
{
    private readonly ITestOutputHelper output;

    public EngineRegressionTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Theory]
    [InlineData(1, 20)]
    [InlineData(2, 400)]
    [InlineData(3, 8902)]
    public void Perft_InitialPosition_MatchesReferenceCounts(int depth, long expectedNodes)
    {
        var state = new GameState(Player.White, Board.Initial());

        var nodes = Perft(state, depth);

        Assert.Equal(expectedNodes, nodes);
    }

    [Fact]
    public void MakeUnmakeFast_RestoresBoardStateAfterSearchTreeTraversal()
    {
        var state = new GameState(Player.White, Board.Initial());
        var snapshot = new StateString(state.CurrentPlayer, state.Board).ToString();

        _ = Perft(state, 3);

        var afterTraversal = new StateString(state.CurrentPlayer, state.Board).ToString();
        Assert.Equal(snapshot, afterTraversal);
    }

    [Fact]
    public void EnginePerformance_ReportsUsefulMetrics()
    {
        var engine = new MiniMaxEngine();
        var state = Fen.FromFen("r1bqkbnr/pppp1ppp/2n5/4p3/3P4/5N2/PPP1PPPP/RNBQKB1R b KQkq - 1 3");

        long generatedMoves = 0;
        long visitedNodes = 0;
        long leafEvaluations = 0;

        var stopwatch = Stopwatch.StartNew();
        var bestMove = engine.FindBestMove(
            state,
            depth: 4,
            timeMs: 1500,
            onStats: (generated, visited, leafs) =>
            {
                generatedMoves = generated;
                visitedNodes = visited;
                leafEvaluations = leafs;
            });
        stopwatch.Stop();

        var elapsedSeconds = Math.Max(stopwatch.Elapsed.TotalSeconds, 1e-6);
        var nodesPerSecond = visitedNodes / elapsedSeconds;
        var leavesPerSecond = leafEvaluations / elapsedSeconds;
        var averageCostPerNodeNs = visitedNodes > 0
            ? stopwatch.Elapsed.TotalMilliseconds * 1_000_000d / visitedNodes
            : double.PositiveInfinity;

        output.WriteLine($"Elapsed: {stopwatch.ElapsedMilliseconds} ms");
        output.WriteLine($"BestMove: {bestMove}");
        output.WriteLine($"VisitedNodes: {visitedNodes}");
        output.WriteLine($"GeneratedMoves: {generatedMoves}");
        output.WriteLine($"LeafEvaluations: {leafEvaluations}");
        output.WriteLine($"Nodes/s: {nodesPerSecond:N0}");
        output.WriteLine($"Leaves/s: {leavesPerSecond:N0}");
        output.WriteLine($"Average cost per node (ns): {averageCostPerNodeNs:N0}");

        Assert.NotNull(bestMove);
        Assert.True(visitedNodes > 0, "Le moteur doit visiter au moins un nœud.");
        Assert.True(leafEvaluations > 0, "Le moteur doit évaluer au moins une feuille.");
        Assert.True(nodesPerSecond > 100, "Le débit en nodes/s est anormalement bas.");
        Assert.True(averageCostPerNodeNs > 0, "Le coût moyen par nœud doit être positif.");
    }

    private static long Perft(GameState state, int depth)
    {
        if (depth == 0)
        {
            return 1;
        }

        var moves = state.AllLegalMovesFor(state.CurrentPlayer).ToList();
        if (depth == 1)
        {
            return moves.Count;
        }

        long nodes = 0;
        foreach (var move in moves)
        {
            state.MakeMoveFast(move, out var undo);
            try
            {
                nodes += Perft(state, depth - 1);
            }
            finally
            {
                state.UnmakeMoveFast(undo);
            }
        }

        return nodes;
    }
}
