using System;
using System.Collections;
using JellyMold.Core;
using JellyMold.Game;
using UnityEngine;

namespace JellyMold.PlayTests
{
    /// <summary>Starts the real game in PlayMode and drives it through GameController's test hooks.</summary>
    internal sealed class GameHarness
    {
        private const string SaveKey = "jellymold.progress";
        private string savedProgress;
        private bool hadSave;

        public App App { get; private set; }
        public GameController Controller { get; private set; }
        public GameFlow Flow => Controller.Flow;
        public BoardView Board => Controller.Board;

        /// <summary>Backs up the player's real progress, then boots the game (App may already exist via its RuntimeInitializeOnLoad hook).</summary>
        public IEnumerator Start()
        {
            hadSave = PlayerPrefs.HasKey(SaveKey);
            savedProgress = PlayerPrefs.GetString(SaveKey, "");
            PlayerPrefs.DeleteKey(SaveKey);

            App = UnityEngine.Object.FindFirstObjectByType<App>();
            if (App == null) App = new GameObject("JellyMold").AddComponent<App>();
            yield return null;
            Controller = App.GetComponent<GameController>();
            if (Controller == null) throw new InvalidOperationException("GameController was not created by App.");
            Flow.Progress.HighestUnlocked = Flow.Levels.Count;
        }

        public void Stop()
        {
            if (App != null)
            {
                Camera cam = Camera.main;
                if (cam != null)
                {
                    CameraRig rig = cam.GetComponent<CameraRig>();
                    if (rig != null) UnityEngine.Object.Destroy(rig);
                }
                UnityEngine.Object.Destroy(App.gameObject);
            }
            if (hadSave) PlayerPrefs.SetString(SaveKey, savedProgress);
            else PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
        }

        public IEnumerator StartLevel(int index)
        {
            Controller.SimulateStartLevel(index);
            yield return WaitSeconds(0.6f);
        }

        /// <summary>Swipes along the pressed line (any cell of the line selects it).</summary>
        public void Swipe(PressMove move)
        {
            bool horizontal = move.Direction.IsHorizontal();
            Controller.SimulateSwipe(horizontal ? 0 : move.Line, horizontal ? move.Line : 0, move.Direction);
        }

        public IEnumerator WaitIdle(float timeout = 5f)
        {
            float end = Time.realtimeSinceStartup + timeout;
            yield return null;
            while (!Controller.IsIdle)
            {
                if (Time.realtimeSinceStartup > end) throw new TimeoutException("Move animation did not finish.");
                yield return null;
            }
            yield return null;
        }

        public IEnumerator PlaySolution(int movesToSkipAtEnd = 0)
        {
            SolveResult solve = PuzzleSolver.Solve(Flow.Session.Current);
            for (int i = 0; i < solve.Solution.Count - movesToSkipAtEnd; i++)
            {
                Swipe(solve.Solution[i]);
                yield return WaitIdle();
            }
        }

        public static IEnumerator WaitSeconds(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        /// <summary>Describes the first cell where the board view disagrees with the logical state, or null.</summary>
        public string FindViewMismatch()
        {
            BoardState state = Flow.Session.Current;
            for (int cell = 0; cell < state.Layout.CellCount; cell++)
            {
                JellyView j = Board.JellyAt(cell);
                JellyColor shown = j != null && j.gameObject.activeSelf ? j.ColorId : JellyColor.None;
                if (shown != state.GetJelly(cell)) return "cell " + cell + ": view " + shown + ", state " + state.GetJelly(cell);
                if (j != null && j.gameObject.activeSelf && (j.transform.localPosition - Board.CellToLocal(cell)).sqrMagnitude > 1e-4f)
                    return "cell " + cell + ": jelly not at its cell";
                MoldView m = Board.MoldAt(cell);
                if (m != null && m.IsFilled != state.IsCellMoldFilled(cell)) return "cell " + cell + ": mold filled " + m.IsFilled;
            }
            return null;
        }
    }
}
