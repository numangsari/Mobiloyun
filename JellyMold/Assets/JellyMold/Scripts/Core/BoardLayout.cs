using System;
using System.Collections.Generic;

namespace JellyMold.Core
{
    /// <summary>
    /// Immutable static part of a level: grid size, walls and molds (with their colours).
    /// Shared by every <see cref="BoardState"/> of the same level.
    /// </summary>
    public sealed class BoardLayout
    {
        /// <summary>State keys pack two bits per cell into 128 bits, so boards are capped at 64 cells.</summary>
        public const int MaxCells = 64;

        private readonly TileType[] tiles;
        private readonly JellyColor[] moldColors;
        private readonly int[] moldIndexByCell;
        private readonly int[] moldCells;

        public int Width { get; }
        public int Height { get; }
        public int CellCount => Width * Height;
        public int MoldCount => moldCells.Length;

        public BoardLayout(int width, int height, TileType[] tiles, JellyColor[] moldColors)
        {
            if (width <= 0 || height <= 0) throw new ArgumentException("Board size must be positive.");
            if (width * height > MaxCells) throw new ArgumentException("Board has more than " + MaxCells + " cells.");
            if (tiles == null || tiles.Length != width * height) throw new ArgumentException("Tile array size mismatch.");
            if (moldColors == null || moldColors.Length != width * height) throw new ArgumentException("Mold colour array size mismatch.");

            Width = width;
            Height = height;
            this.tiles = (TileType[])tiles.Clone();
            this.moldColors = (JellyColor[])moldColors.Clone();
            moldIndexByCell = new int[width * height];

            var molds = new List<int>();
            for (int i = 0; i < this.tiles.Length; i++)
            {
                moldIndexByCell[i] = -1;
                if (this.tiles[i] != TileType.Mold)
                {
                    this.moldColors[i] = JellyColor.None;
                    continue;
                }
                if (this.moldColors[i] == JellyColor.None) throw new ArgumentException("Mold at cell " + i + " has no colour.");
                moldIndexByCell[i] = molds.Count;
                molds.Add(i);
            }
            moldCells = molds.ToArray();
        }

        public int Index(int x, int y) => y * Width + x;
        public int X(int cell) => cell % Width;
        public int Y(int cell) => cell / Width;
        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        public TileType GetTile(int cell) => tiles[cell];
        public TileType GetTile(int x, int y) => tiles[Index(x, y)];
        public JellyColor GetMoldColor(int cell) => moldColors[cell];

        /// <summary>Mold index (0..MoldCount-1) for a mold cell, -1 otherwise.</summary>
        public int GetMoldIndex(int cell) => moldIndexByCell[cell];

        public int GetMoldCell(int moldIndex) => moldCells[moldIndex];

        /// <summary>Number of molds per colour, indexed by (int)JellyColor.</summary>
        public int[] CountMoldsByColor()
        {
            var counts = new int[4];
            foreach (int cell in moldCells) counts[(int)moldColors[cell]]++;
            return counts;
        }

        /// <summary>Number of cells along a press line (row length for horizontal presses).</summary>
        public int LineLength(Direction d) => d.IsHorizontal() ? Width : Height;

        /// <summary>Number of press lines for a direction (rows for horizontal presses).</summary>
        public int LineCount(Direction d) => d.IsHorizontal() ? Height : Width;
    }
}
