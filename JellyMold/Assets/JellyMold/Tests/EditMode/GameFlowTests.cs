using System.Collections.Generic;
using JellyMold.Core;
using NUnit.Framework;

namespace JellyMold.Tests
{
    public class GameFlowTests
    {
        private static GameFlow NewFlow(ProgressData progress = null)
        {
            return new GameFlow(LevelFileTests.LoadLevels(), progress ?? new ProgressData());
        }

        private static void PlaySolution(GameFlow flow)
        {
            SolveResult solve = PuzzleSolver.Solve(flow.Session.Current);
            Assert.IsTrue(solve.Solvable, "level " + flow.Level.Id);
            foreach (PressMove m in solve.Solution) Assert.IsTrue(flow.Press(m).IsValid);
        }

        [Test]
        public void PlayingEveryLevelUnlocksTheNextInOrder()
        {
            GameFlow flow = NewFlow();
            int saves = 0;
            flow.ProgressChanged += () => saves++;

            for (int i = 0; i < flow.Levels.Count; i++)
            {
                Assert.AreEqual(i, flow.ContinueIndex(), "Play opens the first unfinished level");
                Assert.IsTrue(flow.StartLevel(i));
                PlaySolution(flow);
                Assert.AreEqual(GameStatus.Won, flow.Session.Status);
                Assert.AreEqual(3, flow.Stars, "optimal play earns three stars");
                Assert.AreEqual(i + 1 < flow.Levels.Count, flow.HasNext);
            }

            Assert.AreEqual(flow.Levels.Count, saves, "every win is persisted");
            Assert.AreEqual(flow.Levels[flow.Levels.Count - 1].Id, flow.Progress.HighestUnlocked);
            Assert.AreEqual(flow.Levels.Count, flow.Progress.BestMoves.Count);
            Assert.IsTrue(flow.Progress.TutorialSeen);
        }

        [Test]
        public void LockedLevelsCannotBeStarted()
        {
            GameFlow flow = NewFlow();
            Assert.IsTrue(flow.StartLevel(0));
            Assert.IsFalse(flow.StartLevel(1));
            Assert.IsFalse(flow.StartLevel(-1));
            Assert.IsFalse(flow.StartLevel(flow.Levels.Count));
        }

        [Test]
        public void BestMovesKeepTheBetterResult()
        {
            var progress = new ProgressData();
            GameFlow flow = NewFlow(progress);
            flow.StartLevel(0);

            // Level 1: a detour (left, right) before the two winning presses.
            var row2Right = new PressMove(Direction.Right, 2);
            flow.Press(new PressMove(Direction.Left, 2));
            flow.Press(row2Right);
            flow.Press(row2Right);
            flow.Press(row2Right);
            Assert.AreEqual(GameStatus.Won, flow.Session.Status);
            Assert.AreEqual(4, progress.BestMoves[1]);
            Assert.AreEqual(1, flow.Stars);

            flow.StartLevel(0);
            PlaySolution(flow);
            Assert.AreEqual(2, progress.BestMoves[1]);

            flow.StartLevel(0);
            flow.Press(new PressMove(Direction.Left, 2));
            flow.Press(row2Right);
            flow.Press(row2Right);
            flow.Press(row2Right);
            Assert.AreEqual(2, progress.BestMoves[1], "a worse replay does not overwrite the best");
        }

        [Test]
        public void UndoIsAllowedAfterLosingButNotAfterWinning()
        {
            var progress = new ProgressData { HighestUnlocked = 20 };
            GameFlow flow = NewFlow(progress);
            flow.StartLevel(0);
            var away = new PressMove(Direction.Left, 2);
            var back = new PressMove(Direction.Right, 2);
            flow.Press(away);
            flow.Press(back);
            flow.Press(away);
            flow.Press(back);
            flow.Press(away);
            Assert.AreEqual(GameStatus.Lost, flow.Session.Status);
            Assert.IsFalse(flow.CanPlay);
            Assert.IsTrue(flow.CanUndo);

            Assert.IsTrue(flow.TryUndo(out MoveResult undone));
            Assert.IsNotNull(undone);
            Assert.AreEqual(away, undone.Move);
            Assert.AreEqual(GameStatus.Playing, flow.Session.Status);

            flow.Restart();
            PlaySolution(flow);
            Assert.AreEqual(GameStatus.Won, flow.Session.Status);
            Assert.IsFalse(flow.CanUndo);
            Assert.IsFalse(flow.TryUndo(out _));
        }

        [Test]
        public void InvalidPressIsNotRecordedForUndo()
        {
            GameFlow flow = NewFlow();
            flow.StartLevel(0);
            Assert.IsFalse(flow.Press(new PressMove(Direction.Up, 0)).IsValid);
            Assert.IsFalse(flow.CanUndo);
        }

        [Test]
        public void HintsGuideEveryTutorialLevelToTheEnd()
        {
            var progress = new ProgressData { HighestUnlocked = 20 };
            GameFlow flow = NewFlow(progress);
            int tutorialLevels = 0;
            for (int i = 0; i < flow.Levels.Count; i++)
            {
                flow.StartLevel(i);
                if (!flow.Level.Hint)
                {
                    Assert.IsNull(flow.NextHint(), "no hand on regular levels");
                    continue;
                }
                tutorialLevels++;
                int guard = 0;
                while (flow.Session.Status == GameStatus.Playing && guard++ < 20)
                {
                    Hint? hint = flow.NextHint();
                    Assert.IsTrue(hint.HasValue);
                    int x = flow.Session.Layout.X(hint.Value.StartCell), y = flow.Session.Layout.Y(hint.Value.StartCell);
                    // The hand starts on a jelly that is on the pressed line.
                    Assert.AreEqual(hint.Value.Move, PressMove.FromSwipe(x, y, hint.Value.Move.Direction));
                    Assert.AreNotEqual(JellyColor.None, flow.Session.Current.GetJelly(hint.Value.StartCell));
                    Assert.IsTrue(flow.Press(hint.Value.Move).IsValid);
                }
                Assert.AreEqual(GameStatus.Won, flow.Session.Status, "following hints wins level " + flow.Level.Id);
                Assert.AreEqual(flow.Level.Par, flow.Session.MovesUsed, "hints are optimal");
            }
            Assert.Greater(tutorialLevels, 0);
        }

        [Test]
        public void StuckBoardIsDetected()
        {
            // Row 0: the coral jelly can move right once, then sits between the edge and a
            // turquoise mold with no other jelly able to move: the player must undo.
            var level = new LevelData { Id = 1, Moves = 5, Par = 0, Grid = new[] { ".rTR" } };
            var flow = new GameFlow(new List<LevelData> { level }, new ProgressData());
            flow.StartLevel(0);
            Assert.IsFalse(flow.IsStuck);
            flow.Press(new PressMove(Direction.Left, 0));
            Assert.IsFalse(flow.IsStuck, "it can still move back right");

            var blocked = new LevelData { Id = 1, Moves = 5, Par = 0, Grid = new[] { "rTR" } };
            flow = new GameFlow(new List<LevelData> { blocked }, new ProgressData());
            flow.StartLevel(0);
            Assert.IsTrue(flow.IsStuck, "edge on one side, wrong-colour mold on the other");
            Assert.IsNull(flow.NextHint());
        }

        [Test]
        public void SoundToggleIsPersisted()
        {
            GameFlow flow = NewFlow();
            int saves = 0;
            flow.ProgressChanged += () => saves++;
            flow.ToggleSound();
            Assert.IsFalse(flow.Progress.SoundOn);
            Assert.AreEqual(1, saves);
        }
    }
}
