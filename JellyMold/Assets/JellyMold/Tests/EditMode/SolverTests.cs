using JellyMold.Core;
using NUnit.Framework;

namespace JellyMold.Tests
{
    public class SolverTests
    {
        [Test]
        public void FindsOptimalSolution()
        {
            BoardState s = TestBoards.State(
                "r...",
                "....",
                "...R");
            SolveResult r = PuzzleSolver.Solve(s);
            Assert.IsTrue(r.Solvable);
            Assert.AreEqual(5, r.Length);
            AssertSolutionWorks(s, r);
        }

        [Test]
        public void SolutionWithChainedFillIsShortest()
        {
            BoardState s = TestBoards.State("r.Rt.T");
            SolveResult r = PuzzleSolver.Solve(s);
            Assert.AreEqual(2, r.Length);
            AssertSolutionWorks(s, r);
            MoveResult last = MoveResolver.Apply(MoveResolver.Apply(s, r.Solution[0]).After, r.Solution[1]);
            Assert.AreEqual(2, last.MoldsFilled, "both molds fill in the same press");
        }

        [Test]
        public void FilledMoldBlocksTheWayToAFartherMold()
        {
            // Filling the near mold first walls off the far one.
            Assert.IsTrue(PuzzleSolver.Solve(TestBoards.State("rr.RR")).ProvenUnsolvable);
        }

        [Test]
        public void DetectsUnsolvableLevel()
        {
            // The jelly can never get past the wrong-colour mold that walls in the coral one.
            BoardState s = TestBoards.State(
                "r#.",
                "T#R",
                "t#.");
            SolveResult r = PuzzleSolver.Solve(s);
            Assert.IsFalse(r.Solvable);
            Assert.IsTrue(r.ProvenUnsolvable);
        }

        [Test]
        public void MissingJellyColourIsUnsolvable()
        {
            SolveResult r = PuzzleSolver.Solve(TestBoards.State("r..T"));
            Assert.IsTrue(r.ProvenUnsolvable);
        }

        [Test]
        public void SearchTerminatesDespiteCycles()
        {
            // Lots of reversible moves but no yellow jelly: BFS must exhaust the finite space.
            BoardState s = TestBoards.State(
                "r.....",
                "......",
                "..##..",
                "......",
                ".....t",
                "#####Y");
            SolveResult r = PuzzleSolver.Solve(s);
            Assert.IsTrue(r.ProvenUnsolvable);
            Assert.Greater(r.StatesExplored, 100);
        }

        [Test]
        public void DepthLimitIsReported()
        {
            BoardState s = TestBoards.State("r....R");
            SolveResult r = PuzzleSolver.Solve(s, 3);
            Assert.IsFalse(r.Solvable);
            Assert.IsTrue(r.LimitReached);
            Assert.IsFalse(r.ProvenUnsolvable);
        }

        [Test]
        public void ValidatorReportsBudgetAndParProblems()
        {
            var level = new LevelData { Id = 12, Moves = 2, Par = 4, Grid = new[] { "r....R" } };
            ValidationReport rep = LevelValidator.Validate(level);
            Assert.IsFalse(rep.IsValid);
            Assert.AreEqual(2, rep.Errors.Count);

            level.Moves = 6;
            level.Par = 5;
            rep = LevelValidator.Validate(level);
            Assert.IsTrue(rep.IsValid, rep.ToString());
            Assert.AreEqual(0, rep.Warnings.Count, rep.ToString());
        }

        [Test]
        public void ValidatorRejectsMissingJellies()
        {
            var level = new LevelData { Id = 1, Moves = 5, Grid = new[] { "r.RR" } };
            ValidationReport rep = LevelValidator.Validate(level);
            Assert.IsFalse(rep.IsValid);
            StringAssert.Contains("Coral", rep.Errors[0]);
        }

        private static void AssertSolutionWorks(BoardState start, SolveResult r)
        {
            BoardState s = start;
            foreach (PressMove m in r.Solution)
            {
                MoveResult step = MoveResolver.Apply(s, m);
                Assert.IsTrue(step.IsValid, "solution move " + m + " must be valid");
                s = step.After;
            }
            Assert.IsTrue(s.IsSolved);
        }
    }
}
