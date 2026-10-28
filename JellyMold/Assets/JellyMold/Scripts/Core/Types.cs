namespace JellyMold.Core
{
    /// <summary>Jelly and mold colours. <see cref="None"/> marks an empty cell.</summary>
    public enum JellyColor : byte
    {
        None = 0,
        Coral = 1,
        Turquoise = 2,
        Yellow = 3,
    }

    /// <summary>Static ground type of a cell. Jellies live in a separate layer on top.</summary>
    public enum TileType : byte
    {
        Floor = 0,
        Wall = 1,
        Mold = 2,
    }

    /// <summary>Press direction. Grid y grows downwards (row 0 is the top row).</summary>
    public enum Direction : byte
    {
        Up = 0,
        Down = 1,
        Left = 2,
        Right = 3,
    }

    public static class DirectionExtensions
    {
        public static int Dx(this Direction d)
        {
            return d == Direction.Left ? -1 : d == Direction.Right ? 1 : 0;
        }

        public static int Dy(this Direction d)
        {
            return d == Direction.Up ? -1 : d == Direction.Down ? 1 : 0;
        }

        /// <summary>Horizontal presses act on a row, vertical presses on a column.</summary>
        public static bool IsHorizontal(this Direction d)
        {
            return d == Direction.Left || d == Direction.Right;
        }

        public static Direction Opposite(this Direction d)
        {
            switch (d)
            {
                case Direction.Up: return Direction.Down;
                case Direction.Down: return Direction.Up;
                case Direction.Left: return Direction.Right;
                default: return Direction.Left;
            }
        }
    }
}
