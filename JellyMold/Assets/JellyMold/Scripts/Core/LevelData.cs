using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace JellyMold.Core
{
    public sealed class LevelFormatException : Exception
    {
        public LevelFormatException(string message) : base(message) { }
    }

    /// <summary>
    /// Data-driven level definition. The grid uses one character per cell:
    /// '.' floor, '#' wall, 'R' 'T' 'Y' coral/turquoise/yellow molds,
    /// 'r' 't' 'y' jellies of those colours standing on floor.
    /// </summary>
    public sealed class LevelData
    {
        public int Id;
        public string Name = "";
        /// <summary>Move budget shown to the player.</summary>
        public int Moves;
        /// <summary>Optimal solution length as computed by the solver (0 = unknown).</summary>
        public int Par;
        /// <summary>Short instruction shown under the board (optional).</summary>
        public string Tutorial = "";
        /// <summary>Show the animated hand that demonstrates the next solving press.</summary>
        public bool Hint;
        public string[] Grid = Array.Empty<string>();

        public int Width => Grid.Length > 0 ? Grid[0].Length : 0;
        public int Height => Grid.Length;

        public BoardLayout CreateLayout()
        {
            BuildBoard(out BoardLayout layout, out _);
            return layout;
        }

        public BoardState CreateInitialState()
        {
            BuildBoard(out _, out BoardState state);
            return state;
        }

        public void BuildBoard(out BoardLayout layout, out BoardState state)
        {
            int h = Grid.Length;
            if (h == 0) throw new LevelFormatException("Level " + Id + ": grid is empty.");
            int w = Grid[0].Length;
            if (w == 0) throw new LevelFormatException("Level " + Id + ": grid rows are empty.");
            if (w * h > BoardLayout.MaxCells) throw new LevelFormatException("Level " + Id + ": grid exceeds " + BoardLayout.MaxCells + " cells.");

            var tiles = new TileType[w * h];
            var moldColors = new JellyColor[w * h];
            var jellies = new JellyColor[w * h];
            for (int y = 0; y < h; y++)
            {
                string row = Grid[y] ?? "";
                if (row.Length != w) throw new LevelFormatException("Level " + Id + ": row " + y + " has length " + row.Length + ", expected " + w + ".");
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    char c = row[x];
                    switch (c)
                    {
                        case '.': tiles[i] = TileType.Floor; break;
                        case '#': tiles[i] = TileType.Wall; break;
                        case 'R': tiles[i] = TileType.Mold; moldColors[i] = JellyColor.Coral; break;
                        case 'T': tiles[i] = TileType.Mold; moldColors[i] = JellyColor.Turquoise; break;
                        case 'Y': tiles[i] = TileType.Mold; moldColors[i] = JellyColor.Yellow; break;
                        case 'r': jellies[i] = JellyColor.Coral; break;
                        case 't': jellies[i] = JellyColor.Turquoise; break;
                        case 'y': jellies[i] = JellyColor.Yellow; break;
                        default: throw new LevelFormatException("Level " + Id + ": unknown cell '" + c + "' at (" + x + "," + y + ").");
                    }
                }
            }
            layout = new BoardLayout(w, h, tiles, moldColors);
            state = BoardState.Create(layout, jellies);
        }

        /// <summary>Inverse of <see cref="BuildBoard"/>: renders a state back to grid rows.</summary>
        public static string[] ToGrid(BoardState state)
        {
            BoardLayout layout = state.Layout;
            var rows = new string[layout.Height];
            var sb = new StringBuilder(layout.Width);
            for (int y = 0; y < layout.Height; y++)
            {
                sb.Length = 0;
                for (int x = 0; x < layout.Width; x++)
                {
                    int i = layout.Index(x, y);
                    switch (layout.GetTile(i))
                    {
                        case TileType.Wall: sb.Append('#'); break;
                        case TileType.Mold:
                            char m = ColorChar(layout.GetMoldColor(i));
                            sb.Append(state.IsCellMoldFilled(i) ? '*' : char.ToUpperInvariant(m));
                            break;
                        default:
                            JellyColor j = state.GetJelly(i);
                            sb.Append(j == JellyColor.None ? '.' : ColorChar(j));
                            break;
                    }
                }
                rows[y] = sb.ToString();
            }
            return rows;
        }

        private static char ColorChar(JellyColor c)
        {
            switch (c)
            {
                case JellyColor.Coral: return 'r';
                case JellyColor.Turquoise: return 't';
                case JellyColor.Yellow: return 'y';
                default: return '.';
            }
        }

        public static LevelData FromJson(string json)
        {
            object root;
            try
            {
                root = MiniJson.Parse(json);
            }
            catch (FormatException e)
            {
                throw new LevelFormatException("Invalid level JSON: " + e.Message);
            }

            if (!(root is Dictionary<string, object> obj)) throw new LevelFormatException("Level JSON must be an object.");

            var level = new LevelData
            {
                Id = GetInt(obj, "id", 0),
                Name = GetString(obj, "name"),
                Moves = GetInt(obj, "moves", 0),
                Par = GetInt(obj, "par", 0),
                Tutorial = GetString(obj, "tutorial"),
                Hint = obj.TryGetValue("hint", out object hint) && hint is bool b && b,
            };

            if (!obj.TryGetValue("grid", out object gridObj) || !(gridObj is List<object> gridList))
                throw new LevelFormatException("Level " + level.Id + ": missing 'grid' array.");
            level.Grid = new string[gridList.Count];
            for (int i = 0; i < gridList.Count; i++)
            {
                if (!(gridList[i] is string row)) throw new LevelFormatException("Level " + level.Id + ": grid rows must be strings.");
                level.Grid[i] = row;
            }
            return level;
        }

        public string ToJson()
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"id\": ").Append(Id.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            sb.Append("  \"name\": ").Append(MiniJson.Quote(Name ?? "")).Append(",\n");
            sb.Append("  \"moves\": ").Append(Moves.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            sb.Append("  \"par\": ").Append(Par.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            sb.Append("  \"tutorial\": ").Append(MiniJson.Quote(Tutorial ?? "")).Append(",\n");
            sb.Append("  \"hint\": ").Append(Hint ? "true" : "false").Append(",\n");
            sb.Append("  \"grid\": [\n");
            for (int i = 0; i < Grid.Length; i++)
            {
                sb.Append("    ").Append(MiniJson.Quote(Grid[i]));
                sb.Append(i < Grid.Length - 1 ? ",\n" : "\n");
            }
            sb.Append("  ]\n}\n");
            return sb.ToString();
        }

        private static int GetInt(Dictionary<string, object> obj, string key, int fallback)
        {
            return obj.TryGetValue(key, out object v) && v is double d ? (int)d : fallback;
        }

        private static string GetString(Dictionary<string, object> obj, string key)
        {
            return obj.TryGetValue(key, out object v) && v is string s ? s : "";
        }
    }
}
