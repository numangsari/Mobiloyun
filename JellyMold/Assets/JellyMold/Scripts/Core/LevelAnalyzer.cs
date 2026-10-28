using System;
using System.Collections.Generic;

namespace JellyMold.Core
{
    /// <summary>Difficulty indicators used when picking and ordering levels.</summary>
    public sealed class LevelMetrics
    {
        public int Optimal;
        public int StatesExplored;

        /// <summary>Most molds filled by a single press along the optimal solution.</summary>
        public int MaxFillsInOneMove;

        /// <summary>Presses in the optimal solution that fill at least one mold.</summary>
        public int FillingMoves;

        /// <summary>Share of random valid-move playouts that win within the budget (lower = harder).</summary>
        public double RandomWinRate;

        /// <summary>Distinct first presses that still allow an optimal solution.</summary>
        public int OptimalFirstMoves;

        /// <summary>First presses that move something at all.</summary>
        public int ValidFirstMoves;

        /// <summary>First presses after which the level can no longer be won within the move budget.</summary>
        public int FatalFirstMoves;

        /// <summary>Share of valid first presses that are fatal: a cheap "how much must I think" signal.</summary>
        public double TrapRate => ValidFirstMoves == 0 ? 0 : (double)FatalFirstMoves / ValidFirstMoves;

        public int ColorCount;
        public int JellyCount;
        public int WallCount;

        public override string ToString()
        {
            return "opt=" + Optimal + " fills/move=" + MaxFillsInOneMove + " fillMoves=" + FillingMoves
                + " rnd=" + RandomWinRate.ToString("0.000") + " optFirst=" + OptimalFirstMoves
                + " traps=" + FatalFirstMoves + "/" + ValidFirstMoves
                + " colors=" + ColorCount + " jellies=" + JellyCount + " walls=" + WallCount + " states=" + StatesExplored;
        }
    }

    public static class LevelAnalyzer
    {
        /// <summary>Returns null when the board is not solvable within the state limit.</summary>
        public static LevelMetrics Analyze(BoardState start, int moveBudget, int playouts = 400, int seed = 1, int maxStates = GameConfig.DefaultSolverStateLimit)
        {
            SolveResult solve = PuzzleSolver.Solve(start, int.MaxValue, maxStates);
            if (!solve.Solvable) return null;

            var metrics = new LevelMetrics { Optimal = solve.Length, StatesExplored = solve.StatesExplored };
            BoardState s = start;
            foreach (PressMove m in solve.Solution)
            {
                MoveResult r = MoveResolver.Apply(s, m);
                metrics.MaxFillsInOneMove = Math.Max(metrics.MaxFillsInOneMove, r.MoldsFilled);
                if (r.MoldsFilled > 0) metrics.FillingMoves++;
                s = r.After;
            }

            List<PressMove> moves = PressMove.AllMoves(start.Layout);
            foreach (PressMove m in moves)
            {
                BoardState next = MoveResolver.TryApply(start, m);
                if (next == null) continue;
                metrics.ValidFirstMoves++;
                SolveResult rest = PuzzleSolver.Solve(next, Math.Max(0, moveBudget - 1), maxStates);
                if (rest.Solvable && rest.Length == solve.Length - 1) metrics.OptimalFirstMoves++;
                if (!rest.Solvable) metrics.FatalFirstMoves++;
            }

            metrics.RandomWinRate = RandomWinRate(start, moveBudget, playouts, seed, moves);

            BoardLayout layout = start.Layout;
            var colors = new bool[4];
            for (int i = 0; i < layout.CellCount; i++)
            {
                if (layout.GetTile(i) == TileType.Wall) metrics.WallCount++;
                if (layout.GetTile(i) == TileType.Mold) colors[(int)layout.GetMoldColor(i)] = true;
                if (start.GetJelly(i) != JellyColor.None) metrics.JellyCount++;
            }
            for (int c = 1; c < 4; c++) if (colors[c]) metrics.ColorCount++;
            return metrics;
        }

        private static double RandomWinRate(BoardState start, int budget, int playouts, int seed, List<PressMove> moves)
        {
            var rng = new Random(seed);
            var valid = new List<BoardState>(moves.Count);
            int wins = 0;
            for (int p = 0; p < playouts; p++)
            {
                BoardState s = start;
                for (int step = 0; step < budget && !s.IsSolved; step++)
                {
                    valid.Clear();
                    foreach (PressMove m in moves)
                    {
                        BoardState next = MoveResolver.TryApply(s, m);
                        if (next != null) valid.Add(next);
                    }
                    if (valid.Count == 0) break;
                    s = valid[rng.Next(valid.Count)];
                }
                if (s.IsSolved) wins++;
            }
            return playouts == 0 ? 0 : (double)wins / playouts;
        }
    }
}
