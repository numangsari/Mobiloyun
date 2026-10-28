using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using JellyMold.Core;

namespace JellyMold.LevelTool
{
    /// <summary>
    /// validate [dir]           Validate every level_*.json (solvable, par, move budget). Exit code 1 on errors.
    /// analyze  [dir]           Validate plus difficulty metrics per level.
    /// fix      [dir]           Rewrite par and moves from the solver (moves = optimal + tolerance).
    /// solve    file.json       Print the optimal solution step by step.
    /// generate [options]       Random search for level candidates, printed as JSON grids.
    /// </summary>
    public static class Program
    {
        private const string DefaultDir = "JellyMold/Assets/JellyMold/Resources/Levels";

        public static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("usage: LevelTool validate|analyze [dir] | solve <file> | generate [--key value ...]");
                return 2;
            }
            var opts = ParseOptions(args, 1, out List<string> positional);
            switch (args[0])
            {
                case "validate": return Validate(positional.Count > 0 ? positional[0] : FindDefaultDir(), false);
                case "analyze": return Validate(positional.Count > 0 ? positional[0] : FindDefaultDir(), true);
                case "fix": return Fix(positional.Count > 0 ? positional[0] : FindDefaultDir());
                case "solve": return Solve(positional[0]);
                case "generate": return Generate(opts);
                default:
                    Console.Error.WriteLine("unknown command " + args[0]);
                    return 2;
            }
        }

        private static string FindDefaultDir()
        {
            string dir = Directory.GetCurrentDirectory();
            while (dir != null)
            {
                string candidate = Path.Combine(dir, DefaultDir);
                if (Directory.Exists(candidate)) return candidate;
                dir = Path.GetDirectoryName(dir);
            }
            return DefaultDir;
        }

        private static int Validate(string dir, bool withMetrics)
        {
            string[] files = Directory.GetFiles(dir, "level_*.json").OrderBy(f => f, StringComparer.Ordinal).ToArray();
            if (files.Length == 0)
            {
                Console.Error.WriteLine("no level files in " + dir);
                return 1;
            }

            int failures = 0;
            var ids = new HashSet<int>();
            Console.WriteLine("| # | Name | Size | Colors | Optimal | Moves | States |" + (withMetrics ? " Max fills/move | Random win | Optimal first moves | Fatal first moves |" : ""));
            Console.WriteLine("|---|------|------|--------|---------|-------|--------|" + (withMetrics ? "---|---|---|---|" : ""));
            foreach (string file in files)
            {
                LevelData level;
                try
                {
                    level = LevelData.FromJson(File.ReadAllText(file));
                }
                catch (LevelFormatException e)
                {
                    Console.Error.WriteLine(Path.GetFileName(file) + ": " + e.Message);
                    failures++;
                    continue;
                }

                if (!ids.Add(level.Id))
                {
                    Console.Error.WriteLine(Path.GetFileName(file) + ": duplicate id " + level.Id);
                    failures++;
                }

                ValidationReport report = LevelValidator.Validate(level);
                if (!report.IsValid || report.Warnings.Count > 0) Console.Error.WriteLine(report);
                if (!report.IsValid)
                {
                    failures++;
                    continue;
                }

                BoardState start = level.CreateInitialState();
                int colors = start.Layout.CountMoldsByColor().Count(c => c > 0);
                string row = "| " + level.Id + " | " + level.Name + " | " + level.Width + "×" + level.Height + " | " + colors
                    + " | " + report.Solve.Length + " | " + level.Moves + " | " + report.Solve.StatesExplored + " |";
                if (withMetrics)
                {
                    LevelMetrics m = LevelAnalyzer.Analyze(start, level.Moves, 2000);
                    row += " " + m.MaxFillsInOneMove + " | " + (m.RandomWinRate * 100).ToString("0.0", CultureInfo.InvariantCulture) + "% | " + m.OptimalFirstMoves + " | " + m.FatalFirstMoves + "/" + m.ValidFirstMoves + " |";
                }
                Console.WriteLine(row);
            }

            Console.WriteLine();
            Console.WriteLine(failures == 0 ? "All " + files.Length + " levels valid." : failures + " level(s) failed.");
            return failures == 0 ? 0 : 1;
        }

        private static int Fix(string dir)
        {
            foreach (string file in Directory.GetFiles(dir, "level_*.json").OrderBy(f => f, StringComparer.Ordinal))
            {
                LevelData level = LevelData.FromJson(File.ReadAllText(file));
                SolveResult r = PuzzleSolver.Solve(level.CreateInitialState());
                if (!r.Solvable)
                {
                    Console.Error.WriteLine(Path.GetFileName(file) + ": unsolvable, left unchanged");
                    continue;
                }
                level.Par = r.Length;
                level.Moves = GameConfig.RecommendedMoves(level.Id, r.Length);
                File.WriteAllText(file, level.ToJson());
                Console.WriteLine(Path.GetFileName(file) + ": par " + level.Par + ", moves " + level.Moves);
            }
            return 0;
        }

        private static int Solve(string file)
        {
            LevelData level = LevelData.FromJson(File.ReadAllText(file));
            BoardState s = level.CreateInitialState();
            SolveResult r = PuzzleSolver.Solve(s);
            Console.WriteLine("solvable=" + r.Solvable + " length=" + r.Length + " states=" + r.StatesExplored);
            Print(s);
            foreach (PressMove m in r.Solution)
            {
                MoveResult step = MoveResolver.Apply(s, m);
                Console.WriteLine(m + (step.MoldsFilled > 0 ? "   fills " + step.MoldsFilled : ""));
                s = step.After;
                Print(s);
            }
            return r.Solvable ? 0 : 1;
        }

        private static void Print(BoardState s)
        {
            foreach (string row in LevelData.ToGrid(s)) Console.WriteLine("  " + row);
            Console.WriteLine();
        }

        // ---------------------------------------------------------------- generator

        private static int Generate(Dictionary<string, string> o)
        {
            int size = Int(o, "size", 6);
            int colors = Int(o, "colors", 1);
            int molds = Int(o, "molds", 2);          // molds per colour
            int extra = Int(o, "extra", 0);          // extra jellies (blockers) in total
            int walls = Int(o, "walls", 0);
            int minOpt = Int(o, "minopt", 3);
            int maxOpt = Int(o, "maxopt", 8);
            int minFills = Int(o, "minfills", 1);    // best single-press fill count required
            double maxRandom = Dbl(o, "maxrnd", 1.0);
            double minTrap = Dbl(o, "mintrap", 0.0);
            int tolerance = Int(o, "tol", 2);
            int tries = Int(o, "tries", 3000);
            int keep = Int(o, "keep", 5);
            int seed = Int(o, "seed", 1);
            bool symmetric = Int(o, "sym", 0) == 1;   // mirror walls left/right for a designed look

            var rng = new Random(seed);
            var found = new List<(double score, LevelMetrics m, string[] grid)>();
            var seen = new HashSet<string>();

            for (int t = 0; t < tries; t++)
            {
                char[] cells = Enumerable.Repeat('.', size * size).ToArray();
                var free = Enumerable.Range(0, cells.Length).OrderBy(_ => rng.Next()).ToList();
                int take = 0;
                if (symmetric)
                {
                    for (int w = 0; w < walls / 2; w++)
                    {
                        int c = free.First(i => cells[i] == '.' && cells[MirrorX(i, size)] == '.' && MirrorX(i, size) != i);
                        cells[c] = '#';
                        cells[MirrorX(c, size)] = '#';
                    }
                    free = free.Where(i => cells[i] == '.').ToList();
                }
                else
                {
                    for (int w = 0; w < walls; w++) cells[free[take++]] = '#';
                }
                string moldChars = "RTY", jellyChars = "rty";
                for (int c = 0; c < colors; c++)
                {
                    for (int k = 0; k < molds; k++) cells[free[take++]] = moldChars[c];
                    for (int k = 0; k < molds; k++) cells[free[take++]] = jellyChars[c];
                }
                for (int e = 0; e < extra; e++) cells[free[take++]] = jellyChars[rng.Next(colors)];

                string[] grid = new string[size];
                for (int y = 0; y < size; y++) grid[y] = new string(cells, y * size, size);
                string key = string.Join("/", grid);
                if (!seen.Add(key)) continue;

                BoardState state = new LevelData { Id = 0, Grid = grid }.CreateInitialState();
                SolveResult quick = PuzzleSolver.Solve(state, maxOpt, 150000);
                if (!quick.Solvable || quick.Length < minOpt) continue;

                LevelMetrics m = LevelAnalyzer.Analyze(state, quick.Length + tolerance, 600, seed + t, 150000);
                if (m == null || m.MaxFillsInOneMove < minFills || m.RandomWinRate > maxRandom || m.TrapRate < minTrap) continue;

                // Prefer long, chain-heavy boards that random play rarely solves.
                double score = m.Optimal * 0.6 + m.MaxFillsInOneMove * 1.5 + m.TrapRate * 4 - m.RandomWinRate * 3 - m.OptimalFirstMoves * 0.3;
                found.Add((score, m, grid));
            }

            foreach (var f in found.OrderByDescending(f => f.score).Take(keep))
            {
                Console.WriteLine("// score " + f.score.ToString("0.00", CultureInfo.InvariantCulture) + "  " + f.m);
                Console.WriteLine("\"grid\": [");
                for (int i = 0; i < f.grid.Length; i++) Console.WriteLine("  \"" + f.grid[i] + "\"" + (i < f.grid.Length - 1 ? "," : ""));
                Console.WriteLine("]");
                Console.WriteLine();
            }
            Console.WriteLine("// " + found.Count + " candidates");
            return 0;
        }

        private static int MirrorX(int i, int size) => (i / size) * size + (size - 1 - i % size);

        private static Dictionary<string, string> ParseOptions(string[] args, int start, out List<string> positional)
        {
            var o = new Dictionary<string, string>();
            positional = new List<string>();
            for (int i = start; i < args.Length; i++)
            {
                if (args[i].StartsWith("--", StringComparison.Ordinal) && i + 1 < args.Length) o[args[i].Substring(2)] = args[++i];
                else positional.Add(args[i]);
            }
            return o;
        }

        private static int Int(Dictionary<string, string> o, string k, int d) => o.TryGetValue(k, out string v) ? int.Parse(v, CultureInfo.InvariantCulture) : d;
        private static double Dbl(Dictionary<string, string> o, string k, double d) => o.TryGetValue(k, out string v) ? double.Parse(v, CultureInfo.InvariantCulture) : d;
    }
}
