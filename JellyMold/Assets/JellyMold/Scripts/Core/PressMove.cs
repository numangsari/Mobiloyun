using System;
using System.Collections.Generic;

namespace JellyMold.Core
{
    /// <summary>
    /// One player action: press a row (Left/Right) or a column (Up/Down) towards a direction.
    /// <see cref="Line"/> is the row index for horizontal presses and the column index otherwise.
    /// </summary>
    public readonly struct PressMove : IEquatable<PressMove>
    {
        public readonly Direction Direction;
        public readonly int Line;

        public PressMove(Direction direction, int line)
        {
            Direction = direction;
            Line = line;
        }

        /// <summary>Builds the press a swipe starting on cell (x, y) produces.</summary>
        public static PressMove FromSwipe(int x, int y, Direction direction)
        {
            return new PressMove(direction, direction.IsHorizontal() ? y : x);
        }

        /// <summary>Every press that exists on the given layout (4 directions × lines).</summary>
        public static List<PressMove> AllMoves(BoardLayout layout)
        {
            var moves = new List<PressMove>(2 * (layout.Width + layout.Height));
            for (int y = 0; y < layout.Height; y++)
            {
                moves.Add(new PressMove(Direction.Left, y));
                moves.Add(new PressMove(Direction.Right, y));
            }
            for (int x = 0; x < layout.Width; x++)
            {
                moves.Add(new PressMove(Direction.Up, x));
                moves.Add(new PressMove(Direction.Down, x));
            }
            return moves;
        }

        public bool Equals(PressMove other) => Direction == other.Direction && Line == other.Line;
        public override bool Equals(object obj) => obj is PressMove other && Equals(other);
        public override int GetHashCode() => ((int)Direction * 397) ^ Line;

        public override string ToString()
        {
            string axis = Direction.IsHorizontal() ? "row" : "col";
            return axis + " " + Line + " " + Direction;
        }
    }
}
