using System.IO;
using System.Linq;
using System.Text;
using JellyMold.Core;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace JellyMold.EditorTools
{
    /// <summary>Editor menu for level validation and test builds.</summary>
    public static class LevelTools
    {
        private const string LevelFolder = "Assets/JellyMold/Resources/Levels";

        [MenuItem("Jelly Mold/Validate Levels")]
        public static void ValidateLevels()
        {
            var sb = new StringBuilder();
            int failures = 0;
            string[] files = Directory.GetFiles(LevelFolder, "level_*.json").OrderBy(f => f).ToArray();
            foreach (string file in files)
            {
                ValidationReport report;
                try
                {
                    report = LevelValidator.Validate(LevelData.FromJson(File.ReadAllText(file)));
                }
                catch (LevelFormatException e)
                {
                    failures++;
                    sb.AppendLine(Path.GetFileName(file) + ": " + e.Message);
                    continue;
                }
                if (!report.IsValid) failures++;
                sb.AppendLine(report.ToString());
            }
            string summary = failures == 0 ? "All " + files.Length + " levels are solvable and budgeted correctly." : failures + " level(s) failed.";
            if (failures == 0) Debug.Log("[Jelly Mold] " + summary + "\n" + sb);
            else Debug.LogError("[Jelly Mold] " + summary + "\n" + sb);
            EditorUtility.DisplayDialog("Jelly Mold levels", summary, "OK");
        }

        [MenuItem("Jelly Mold/Build Android APK (test)")]
        public static void BuildAndroid()
        {
            ProjectSetup.Apply();
            Directory.CreateDirectory("Builds/Android");
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ProjectSetup.ScenePath },
                locationPathName = "Builds/Android/JellyMold.apk",
                target = BuildTarget.Android,
                options = BuildOptions.None,
            };
            EditorUserBuildSettings.buildAppBundle = false;
            BuildReport report = BuildPipeline.BuildPlayer(options);
            Debug.Log("[Jelly Mold] Android build: " + report.summary.result + ", " + report.summary.totalSize / (1024 * 1024) + " MB, errors: " + report.summary.totalErrors);
        }
    }
}
