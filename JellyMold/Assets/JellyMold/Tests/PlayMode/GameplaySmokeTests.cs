using System.Collections;
using JellyMold.Core;
using JellyMold.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace JellyMold.PlayTests
{
    /// <summary>End-to-end checks of the running game: input → rules → animation → UI → save.</summary>
    [Category("Smoke")]
    public class GameplaySmokeTests
    {
        private GameHarness game;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            game = new GameHarness();
            yield return game.Start();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            game.Stop();
            yield return null;
        }

        [UnityTest]
        public IEnumerator SolvingLevelOneShowsWinPanelAndSavesProgress()
        {
            yield return game.StartLevel(0);
            Assert.AreEqual(1, game.Flow.Level.Id);
            yield return game.PlaySolution();

            Assert.AreEqual(GameStatus.Won, game.Flow.Session.Status);
            yield return GameHarness.WaitSeconds(Timing.WinDelay + 1f);
            Assert.IsTrue(game.Controller.Ui.OverlayOpen, "win panel is visible");
            ProgressData saved = ProgressData.Deserialize(PlayerPrefs.GetString("jellymold.progress", ""));
            Assert.IsTrue(saved.IsCompleted(1), "win written to PlayerPrefs");
        }

        [UnityTest]
        public IEnumerator SecondPressDuringAnimationIsIgnored()
        {
            yield return game.StartLevel(0);
            var right = new PressMove(Direction.Right, 2);
            game.Swipe(right);
            game.Swipe(right);
            game.Swipe(right);
            Assert.IsFalse(game.Controller.IsIdle);
            yield return game.WaitIdle();
            Assert.AreEqual(1, game.Flow.Session.MovesUsed);
            Assert.IsNull(game.FindViewMismatch());
        }

        [UnityTest]
        public IEnumerator UndoRestoresTheBoardViewIncludingFilledMolds()
        {
            // Level 7: play all but the winning press (winning would block undo), then undo twice.
            yield return game.StartLevel(6);
            SolveResult solve = PuzzleSolver.Solve(game.Flow.Session.Current);
            for (int i = 0; i < solve.Solution.Count - 1; i++)
            {
                game.Swipe(solve.Solution[i]);
                yield return game.WaitIdle();
                Assert.IsNull(game.FindViewMismatch(), "after move " + i);
            }
            for (int i = 0; i < 2; i++)
            {
                game.Controller.SimulateUndo();
                yield return game.WaitIdle();
                Assert.IsNull(game.FindViewMismatch(), "after undo " + i);
            }
            Assert.AreEqual(solve.Solution.Count - 3, game.Flow.Session.MovesUsed);
        }

        [UnityTest]
        public IEnumerator LosingShowsLosePanelAndUndoResumes()
        {
            yield return game.StartLevel(0);
            var away = new PressMove(Direction.Left, 2);
            var back = new PressMove(Direction.Right, 2);
            while (game.Flow.Session.Status == GameStatus.Playing)
            {
                game.Swipe(game.Flow.Session.MovesUsed % 2 == 0 ? away : back);
                yield return game.WaitIdle();
            }
            Assert.AreEqual(GameStatus.Lost, game.Flow.Session.Status);
            yield return GameHarness.WaitSeconds(0.8f);
            Assert.IsTrue(game.Controller.Ui.OverlayOpen, "lose panel is visible");

            game.Controller.Ui.HideOverlays();
            game.Controller.SimulateUndo();
            yield return game.WaitIdle();
            Assert.AreEqual(GameStatus.Playing, game.Flow.Session.Status);
            Assert.IsNull(game.FindViewMismatch());
        }

        [UnityTest]
        public IEnumerator EveryLevelLoadsAndFitsInTheCameraView()
        {
            for (int i = 0; i < game.Flow.Levels.Count; i++)
            {
                yield return game.StartLevel(i);
                Assert.IsNull(game.FindViewMismatch(), "level " + (i + 1));
                Camera cam = Camera.main;
                Vector2 half = game.Board.Size * 0.5f;
                Vector3 c = game.Board.transform.position;
                foreach (Vector3 corner in new[] { c + new Vector3(-half.x, -half.y), c + new Vector3(half.x, half.y) })
                {
                    Vector3 v = cam.WorldToViewportPoint(corner);
                    Assert.That(v.x, Is.InRange(-0.001f, 1.001f), "level " + (i + 1) + " x");
                    Assert.That(v.y, Is.InRange(-0.001f, 1.001f), "level " + (i + 1) + " y");
                }
            }
        }
    }
}
