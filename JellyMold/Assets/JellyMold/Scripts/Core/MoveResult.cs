using System.Collections.Generic;

namespace JellyMold.Core
{
    public enum StepKind : byte
    {
        Moved,
        EnteredMold,
        Blocked,
    }

    public enum BlockReason : byte
    {
        None,
        Edge,
        Wall,
        Jelly,
        FilledMold,
        WrongMold,
    }

    /// <summary>What happened to one jelly during a press. Used only for presentation.</summary>
    public readonly struct JellyStep
    {
        public readonly int From;
        public readonly int To;
        public readonly JellyColor Color;
        public readonly StepKind Kind;
        public readonly BlockReason Reason;

        /// <summary>Resolution order, 0 = the jelly closest to the press target (used for stagger).</summary>
        public readonly int Order;

        public JellyStep(int from, int to, JellyColor color, StepKind kind, BlockReason reason, int order)
        {
            From = from;
            To = to;
            Color = color;
            Kind = kind;
            Reason = reason;
            Order = order;
        }
    }

    /// <summary>Outcome of applying a press. The logical result is final before any animation runs.</summary>
    public sealed class MoveResult
    {
        public PressMove Move { get; }
        public BoardState Before { get; }
        public BoardState After { get; }
        public IReadOnlyList<JellyStep> Steps { get; }
        public int MovedCount { get; }
        public int MoldsFilled { get; }

        /// <summary>False when no jelly moved (or the session no longer accepts moves).</summary>
        public bool IsValid { get; }

        public MoveResult(PressMove move, BoardState before, BoardState after, IReadOnlyList<JellyStep> steps, bool accepted)
        {
            Move = move;
            Before = before;
            Steps = steps;
            int moved = 0, fills = 0;
            foreach (JellyStep s in steps)
            {
                if (s.Kind != StepKind.Blocked) moved++;
                if (s.Kind == StepKind.EnteredMold) fills++;
            }
            MovedCount = moved;
            MoldsFilled = fills;
            IsValid = accepted && moved > 0;
            After = IsValid ? after : before;
        }
    }
}
