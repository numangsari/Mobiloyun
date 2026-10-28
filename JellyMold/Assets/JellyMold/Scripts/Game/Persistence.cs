using System;
using System.Collections.Generic;
using JellyMold.Core;
using UnityEngine;

namespace JellyMold.Game
{
    /// <summary>Stores <see cref="ProgressData"/> in PlayerPrefs — on the device only, never sent anywhere.</summary>
    public sealed class SaveManager
    {
        private const string Key = "jellymold.progress";

        public ProgressData Progress { get; private set; }

        public void Load()
        {
            Progress = ProgressData.Deserialize(PlayerPrefs.GetString(Key, ""));
        }

        public void Save()
        {
            PlayerPrefs.SetString(Key, Progress.Serialize());
            PlayerPrefs.Save();
        }

        public void Reset()
        {
            Progress = new ProgressData { SoundOn = Progress != null && Progress.SoundOn };
            Save();
        }
    }

    /// <summary>Loads the level JSON files from Resources/Levels, ordered by id.</summary>
    public static class LevelRepository
    {
        public static List<LevelData> LoadAll()
        {
            var levels = new List<LevelData>();
            foreach (TextAsset asset in Resources.LoadAll<TextAsset>("Levels"))
            {
                try
                {
                    levels.Add(LevelData.FromJson(asset.text));
                }
                catch (LevelFormatException e)
                {
                    Debug.LogError("Level file " + asset.name + " is invalid: " + e.Message);
                }
            }
            levels.Sort((a, b) => a.Id.CompareTo(b.Id));
            return levels;
        }
    }
}
