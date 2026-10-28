using System;
using System.Collections.Generic;

namespace JellyMold.Core
{
    /// <summary>A suggested press for the tutorial hand: the move and the cell the swipe should start on.</summary>
    public readonly struct Hint
    {
        public readonly PressMove Move;
        public readonly int StartCell;

        public Hint(PressMove move, int startCell)
        {
            Move = move;
            StartCell = startCell;
        }
    }

    /// <summary>
    /// Level-to-level game flow without any Unity dependency: which level to continue, recording
    /// wins, undo/restart permissions and tutorial hints. The Unity GameController only adds
    /// animation, UI and timing on top of this.
    /// </summary>
    public sealed class GameFlow
    {
        /// <summary>Hints only run on small tutorial boards; the cap keeps a frame from stalling.</summary>
        private const int HintStateLimit = 20000;

        private readonly List<LevelData> levels;
        private readonly Stack<MoveResult> undoResults = new Stack<MoveResult>();

        public IReadOnlyList<LevelData> Levels => levels;
        public ProgressData Progress { get; }
        public GameSession Session { get; private set; }
        public int LevelIndex { get; private set; } = -1;
        public LevelData Level => LevelIndex >= 0 ? levels[LevelIndex] : null;
        public bool HasNext => LevelIndex + 1 < levels.Count;

        /// <summary>Raised whenever <see cref="Progress"/> changed and should be persisted.</summary>
        public event Action ProgressChanged;

        public GameFlow(IEnumerable<LevelData> levels, ProgressData progress)
        {
            this.levels = new List<LevelData>(levels);
            this.levels.Sort((a, b) => a.Id.CompareTo(b.Id));
            Progress = progress ?? new ProgressData();
        }

        /// <summary>Index of the level "Play" should open: the highest unlocked one.</summary>
        public int ContinueIndex()
        {
            if (levels.Count == 0) return -1;
            int unlocked = Math.Max(1, Math.Min(Progress.HighestUnlocked, levels[levels.Count - 1].Id));
            int index = levels.FindIndex(l => l.Id == unlocked);
            return index >= 0 ? index : 0;
        }

        public int IndexOf(int levelId) => levels.FindIndex(l => l.Id == levelId);

        /// <summary>Starts a level if it exists and is unlocked.</summary>
        public bool StartLevel(int index)
        {
            if (index < 0 || index >= levels.Count || !Progress.IsUnlocked(levels[index].Id)) return false;
            LevelIndex = index;
            Session = GameSession.FromLevel(levels[index]);
            undoResults.Clear();
            return true;
        }

        public bool CanPlay => Session != null && Session.Status == GameStatus.Playing;

        /// <summary>
        /// Applies a press. A winning press records progress immediately, before any animation,
        /// so closing the app during the celebration never loses the win.
        /// </summary>
        public MoveResult Press(PressMove move)
        {
            if (Session == null) throw new InvalidOperationException("No level started.");
            MoveResult result = Session.TryPress(move);
            if (!result.IsValid) return result;
            undoResults.Push(result);
            if (Session.Status == GameStatus.Won)
            {
                Progress.RecordWin(Level.Id, Session.MovesUsed, levels[levels.Count - 1].Id);
                if (Level.Hint) Progress.TutorialSeen = true;
                ProgressChanged?.Invoke();
            }
            return result;
        }

        /// <summary>Undo is allowed while playing and after losing, but not once the level is won.</summary>
        public bool CanUndo => Session != null && Session.CanUndo && Session.Status != GameStatus.Won;

        /// <summary>Reverts the last press. <paramref name="undone"/> is the press to play backwards.</summary>
        public bool TryUndo(out MoveResult undone)
        {
            undone = null;
            if (!CanUndo) return false;
            undone = undoResults.Count > 0 ? undoResults.Pop() : null;
            return Session.Undo();
        }

        public void Restart()
        {
            if (Session == null) return;
            Session.Restart();
            undoResults.Clear();
        }

        /// <summary>Stars for the current result (only meaningful after winning).</summary>
        public int Stars => Session == null ? 0 : GameConfig.StarsFor(Session.MovesUsed, Level.Par);

        /// <summary>Still playing but no press moves anything: the player must undo or restart.</summary>
        public bool IsStuck => CanPlay && !Session.HasAnyValidMove();

        /// <summary>Next press of an optimal solution on hint levels; null elsewhere or when none fits the budget.</summary>
        public Hint? NextHint()
        {
            if (!CanPlay || !Level.Hint) return null;
            SolveResult solve = PuzzleSolver.Solve(Session.Current, Session.MovesLeft, HintStateLimit);
            if (!solve.Solvable || solve.Length == 0) return null;

            PressMove move = solve.Solution[0];
            foreach (JellyStep s in MoveResolver.Apply(Session.Current, move).Steps)
            {
                if (s.Kind != StepKind.Blocked) return new Hint(move, s.From);
            }
            return null;
        }

        public void ToggleSound()
        {
            Progress.SoundOn = !Progress.SoundOn;
            ProgressChanged?.Invoke();
        }
    }
}
