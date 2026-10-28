using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace JellyMold.Core
{
    /// <summary>
    /// Player progress kept only on the device. Serialised to a short text string so the
    /// Unity layer can store it in PlayerPrefs and the format stays testable here.
    /// </summary>
    public sealed class ProgressData
    {
        private const string Version = "v1";

        /// <summary>Highest level number the player may open (1-based).</summary>
        public int HighestUnlocked = 1;
        public bool SoundOn = true;
        public bool TutorialSeen;

        /// <summary>Fewest moves used to win each level, by level id.</summary>
        public readonly Dictionary<int, int> BestMoves = new Dictionary<int, int>();

        public bool IsCompleted(int levelId) => BestMoves.ContainsKey(levelId);

        public bool IsUnlocked(int levelId) => levelId <= HighestUnlocked;

        public void RecordWin(int levelId, int movesUsed, int totalLevels)
        {
            if (!BestMoves.TryGetValue(levelId, out int best) || movesUsed < best) BestMoves[levelId] = movesUsed;
            int next = levelId + 1;
            if (next <= totalLevels && next > HighestUnlocked) HighestUnlocked = next;
        }

        public string Serialize()
        {
            var sb = new StringBuilder(Version);
            sb.Append("|u=").Append(HighestUnlocked.ToString(CultureInfo.InvariantCulture));
            sb.Append("|s=").Append(SoundOn ? '1' : '0');
            sb.Append("|t=").Append(TutorialSeen ? '1' : '0');
            sb.Append("|b=");
            bool first = true;
            var ids = new List<int>(BestMoves.Keys);
            ids.Sort();
            foreach (int id in ids)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append(id.ToString(CultureInfo.InvariantCulture)).Append(':').Append(BestMoves[id].ToString(CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        /// <summary>Parses saved progress. Corrupt or unknown data falls back to a fresh profile.</summary>
        public static ProgressData Deserialize(string text)
        {
            var data = new ProgressData();
            if (string.IsNullOrEmpty(text)) return data;
            string[] parts = text.Split('|');
            if (parts.Length == 0 || parts[0] != Version) return data;

            for (int i = 1; i < parts.Length; i++)
            {
                string part = parts[i];
                int eq = part.IndexOf('=');
                if (eq <= 0) continue;
                string key = part.Substring(0, eq);
                string value = part.Substring(eq + 1);
                switch (key)
                {
                    case "u":
                        if (TryInt(value, out int u) && u >= 1) data.HighestUnlocked = u;
                        break;
                    case "s":
                        data.SoundOn = value != "0";
                        break;
                    case "t":
                        data.TutorialSeen = value == "1";
                        break;
                    case "b":
                        foreach (string entry in value.Split(','))
                        {
                            int colon = entry.IndexOf(':');
                            if (colon <= 0) continue;
                            if (TryInt(entry.Substring(0, colon), out int id) && TryInt(entry.Substring(colon + 1), out int moves) && moves > 0)
                                data.BestMoves[id] = moves;
                        }
                        break;
                }
            }
            return data;
        }

        private static bool TryInt(string s, out int value)
        {
            return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }
    }
}
