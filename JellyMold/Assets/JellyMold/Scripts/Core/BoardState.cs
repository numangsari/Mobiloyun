using System;

namespace JellyMold.Core
{
    /// <summary>
    /// Immutable dynamic part of a level: where the jellies are and which molds are filled.
    /// Every move produces a new instance, which makes undo and search trivial.
    /// </summary>
    public sealed class BoardState
    {
        private readonly JellyColor[] jellies;
        private readonly bool[] filled;
        private StateKey? key;

        public BoardLayout Layout { get; }

        internal BoardState(BoardLayout layout, JellyColor[] jellies, bool[] filled)
        {
            Layout = layout;
            this.jellies = jellies;
            this.filled = filled;
        }

        /// <summary>Creates a state with all molds empty. Jellies may only sit on floor cells.</summary>
        public static BoardState Create(BoardLayout layout, JellyColor[] jellies)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (jellies == null || jellies.Length != layout.CellCount) throw new ArgumentException("Jelly array size mismatch.");
            for (int i = 0; i < jellies.Length; i++)
            {
                if (jellies[i] != JellyColor.None && layout.GetTile(i) != TileType.Floor)
                    throw new ArgumentException("Jelly at cell " + i + " is not on a floor cell.");
            }
            return new BoardState(layout, (JellyColor[])jellies.Clone(), new bool[layout.MoldCount]);
        }

        public JellyColor GetJelly(int cell) => jellies[cell];
        public JellyColor GetJelly(int x, int y) => jellies[Layout.Index(x, y)];
        public bool IsMoldFilled(int moldIndex) => filled[moldIndex];

        public bool IsCellMoldFilled(int cell)
        {
            int m = Layout.GetMoldIndex(cell);
            return m >= 0 && filled[m];
        }

        public int FilledCount
        {
            get
            {
                int n = 0;
                foreach (bool f in filled) if (f) n++;
                return n;
            }
        }

        public bool IsSolved => FilledCount == Layout.MoldCount;

        public int JellyCount
        {
            get
            {
                int n = 0;
                foreach (JellyColor c in jellies) if (c != JellyColor.None) n++;
                return n;
            }
        }

        /// <summary>Compact value identity used by the solver to detect repeated states.</summary>
        public StateKey Key
        {
            get
            {
                if (!key.HasValue) key = StateKey.From(jellies, filled);
                return key.Value;
            }
        }

        internal JellyColor[] CopyJellies() => (JellyColor[])jellies.Clone();
        internal bool[] CopyFilled() => (bool[])filled.Clone();

        public bool SameAs(BoardState other)
        {
            return other != null && ReferenceEquals(Layout, other.Layout) && Key.Equals(other.Key);
        }
    }

    /// <summary>128 bits of jelly colours (2 bits per cell) plus 64 bits of filled-mold flags.</summary>
    public readonly struct StateKey : IEquatable<StateKey>
    {
        public readonly ulong JelliesLow;
        public readonly ulong JelliesHigh;
        public readonly ulong Filled;

        public StateKey(ulong jelliesLow, ulong jelliesHigh, ulong filled)
        {
            JelliesLow = jelliesLow;
            JelliesHigh = jelliesHigh;
            Filled = filled;
        }

        internal static StateKey From(JellyColor[] jellies, bool[] filled)
        {
            ulong lo = 0, hi = 0, f = 0;
            for (int i = 0; i < jellies.Length; i++)
            {
                ulong v = (ulong)jellies[i] & 3UL;
                if (i < 32) lo |= v << (i * 2);
                else hi |= v << ((i - 32) * 2);
            }
            for (int m = 0; m < filled.Length; m++)
            {
                if (filled[m]) f |= 1UL << m;
            }
            return new StateKey(lo, hi, f);
        }

        public bool Equals(StateKey other)
        {
            return JelliesLow == other.JelliesLow && JelliesHigh == other.JelliesHigh && Filled == other.Filled;
        }

        public override bool Equals(object obj) => obj is StateKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                ulong h = JelliesLow * 0x9E3779B97F4A7C15UL;
                h ^= JelliesHigh + 0x632BE59BD9B4E019UL + (h << 6) + (h >> 2);
                h ^= Filled + 0x85EBCA77C2B2AE63UL + (h << 6) + (h >> 2);
                return (int)(h ^ (h >> 32));
            }
        }
    }
}
