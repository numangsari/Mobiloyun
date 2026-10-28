using System;
using System.Collections.Generic;

namespace JellyMold.Core
{
    public enum GameStatus
    {
        Playing,
        Won,
        Lost,
    }

    /// <summary>
    /// One play-through of a level: move budget, undo history and win/lose state.
    /// Pure logic; the Unity layer only reads it and plays animations for the results.
    /// </summary>
    public sealed class GameSession
    {
        private readonly Stack<Snapshot> history = new Stack<Snapshot>();

        public BoardLayout Layout { get; }
        public BoardState Initial { get; }
        public BoardState Current { get; private set; }
        public int MaxMoves { get; }
        public int MovesLeft { get; private set; }
        public int MovesUsed => MaxMoves - MovesLeft;
        public GameStatus Status { get; private set; }
        public bool CanUndo => history.Count > 0;

        public GameSession(BoardState initial, int maxMoves)
        {
            if (initial == null) throw new ArgumentNullException(nameof(initial));
            if (maxMoves <= 0) throw new ArgumentException("A level needs at least one move.");
            Layout = initial.Layout;
            Initial = initial;
            MaxMoves = maxMoves;
            Restart();
        }

        public static GameSession FromLevel(LevelData level)
        {
            return new GameSession(level.CreateInitialState(), level.Moves);
        }

        /// <summary>
        /// Applies a press. Invalid presses (nothing moves) and presses after the game ended
        /// leave the state and the move counter untouched; the result still carries Blocked steps.
        /// </summary>
        public MoveResult TryPress(PressMove move)
        {
            if (Status != GameStatus.Playing)
            {
                return new MoveResult(move, Current, Current, Array.Empty<JellyStep>(), false);
            }

            MoveResult result = MoveResolver.Apply(Current, move);
            if (!result.IsValid) return result;

            history.Push(new Snapshot(Current, MovesLeft));
            Current = result.After;
            MovesLeft--;
            UpdateStatus();
            return result;
        }

        /// <summary>Reverts the last valid press, including filled molds and the move counter.</summary>
        public bool Undo()
        {
            if (history.Count == 0) return false;
            Snapshot s = history.Pop();
            Current = s.State;
            MovesLeft = s.MovesLeft;
            UpdateStatus();
            return true;
        }

        public void Restart()
        {
            history.Clear();
            Current = Initial;
            MovesLeft = MaxMoves;
            UpdateStatus();
        }

        /// <summary>True when at least one press would move a jelly from the current state.</summary>
        public bool HasAnyValidMove()
        {
            foreach (PressMove m in PressMove.AllMoves(Layout))
            {
                if (MoveResolver.TryApply(Current, m) != null) return true;
            }
            return false;
        }

        private void UpdateStatus()
        {
            if (Current.IsSolved) Status = GameStatus.Won;
            else if (MovesLeft <= 0) Status = GameStatus.Lost;
            else Status = GameStatus.Playing;
        }

        private readonly struct Snapshot
        {
            public readonly BoardState State;
            public readonly int MovesLeft;

            public Snapshot(BoardState state, int movesLeft)
            {
                State = state;
                MovesLeft = movesLeft;
            }
        }
    }

    /// <summary>
    /// Single-owner lock that rejects new input while a move is being presented.
    /// Kept in Core so the "no input during animation" rule is testable.
    /// </summary>
    public sealed class InputGate
    {
        public bool IsBusy { get; private set; }

        public bool TryAcquire()
        {
            if (IsBusy) return false;
            IsBusy = true;
            return true;
        }

        public void Release()
        {
            IsBusy = false;
        }
    }
}
