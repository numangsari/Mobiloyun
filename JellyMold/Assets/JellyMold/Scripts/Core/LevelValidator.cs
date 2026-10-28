using System.Collections.Generic;

namespace JellyMold.Core
{
    public sealed class ValidationReport
    {
        public LevelData Level { get; }
        public List<string> Errors { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();
        public SolveResult Solve { get; internal set; }
        public bool IsValid => Errors.Count == 0;

        public ValidationReport(LevelData level)
        {
            Level = level;
        }

        public override string ToString()
        {
            string head = "Level " + Level.Id + " '" + Level.Name + "': " + (IsValid ? "OK" : "INVALID");
            if (Solve != null && Solve.Solvable) head += " (optimal " + Solve.Length + ", moves " + Level.Moves + ", states " + Solve.StatesExplored + ")";
            foreach (string e in Errors) head += "\n  error: " + e;
            foreach (string w in Warnings) head += "\n  warning: " + w;
            return head;
        }
    }

    /// <summary>Checks a level's format, material balance, solvability and move budget.</summary>
    public static class LevelValidator
    {
        public static ValidationReport Validate(LevelData level, int maxStates = GameConfig.DefaultSolverStateLimit)
        {
            var report = new ValidationReport(level);
            BoardLayout layout;
            BoardState state;
            try
            {
                level.BuildBoard(out layout, out state);
            }
            catch (System.Exception e)
            {
                report.Errors.Add(e.Message);
                return report;
            }

            if (layout.MoldCount == 0) report.Errors.Add("Level has no molds.");
            if (level.Moves <= 0) report.Errors.Add("Move budget must be positive.");

            int[] molds = layout.CountMoldsByColor();
            var jellies = new int[4];
            for (int i = 0; i < layout.CellCount; i++) jellies[(int)state.GetJelly(i)]++;
            for (int c = 1; c < 4; c++)
            {
                if (jellies[c] < molds[c])
                    report.Errors.Add(((JellyColor)c) + ": " + molds[c] + " molds but only " + jellies[c] + " jellies.");
            }
            if (!report.IsValid) return report;

            SolveResult solve = PuzzleSolver.Solve(state, int.MaxValue, maxStates);
            report.Solve = solve;
            if (!solve.Solvable)
            {
                report.Errors.Add(solve.LimitReached
                    ? "No solution found within " + maxStates + " states."
                    : "Level is unsolvable.");
                return report;
            }

            if (level.Moves < solve.Length)
                report.Errors.Add("Move budget " + level.Moves + " is below the optimal solution length " + solve.Length + ".");
            if (level.Par > 0 && level.Par != solve.Length)
                report.Errors.Add("Par " + level.Par + " does not match optimal solution length " + solve.Length + ".");

            int recommended = GameConfig.RecommendedMoves(level.Id, solve.Length);
            if (level.Moves != recommended)
                report.Warnings.Add("Move budget " + level.Moves + " differs from recommended " + recommended + ".");
            return report;
        }
    }
}
