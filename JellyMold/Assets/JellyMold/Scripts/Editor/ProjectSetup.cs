using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace JellyMold.EditorTools
{
    /// <summary>
    /// One-click project configuration: portrait mobile player settings, offline/privacy defaults
    /// and the main scene. Runs automatically once when the project is first opened.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        public const string ScenePath = "Assets/JellyMold/Scenes/Main.unity";
        private const string AppliedKey = "JellyMold.ProjectSetup.v1";

        static ProjectSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (SessionState.GetBool(AppliedKey, false)) return;
                SessionState.SetBool(AppliedKey, true);
                if (PlayerSettings.productName != "Jelly Mold") Apply();
            };
        }

        [MenuItem("Jelly Mold/Apply Project Settings")]
        public static void Apply()
        {
            PlayerSettings.companyName = "JellyMold";
            PlayerSettings.productName = "Jelly Mold";
            PlayerSettings.bundleVersion = "0.1.0";
#if UNITY_2021_2_OR_NEWER
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.jellymold.demo");
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.jellymold.demo");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
#else
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.jellymold.demo");
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, "com.jellymold.demo");
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
#endif

            // Portrait only.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            // Offline game: never ask for the INTERNET permission.
            PlayerSettings.Android.forceInternetPermission = false;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;

            DisableServices();
            UseLegacyInputManager();
            EnsureScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[Jelly Mold] Project settings applied (portrait, offline, analytics/crash reporting off).");
        }

        /// <summary>
        /// Turns off Unity's optional data services if they exist in this Unity version.
        /// Done by reflection because these editor APIs move between versions.
        /// </summary>
        private static void DisableServices()
        {
            SetStaticBool("UnityEditor.Analytics.AnalyticsSettings, UnityEditor", "enabled", false);
            SetStaticBool("UnityEditor.Analytics.PerformanceReportingSettings, UnityEditor", "enabled", false);
            SetStaticBool("UnityEditor.CrashReporting.CrashReportingSettings, UnityEditor", "enabled", false);
            SetStaticBool("UnityEditor.PlayerSettings, UnityEditor", "enableCrashReportAPI", false);
            SetStaticBool("UnityEditor.PlayerSettings, UnityEditor", "submitAnalytics", false);
        }

        private static void SetStaticBool(string typeName, string property, bool value)
        {
            Type type = Type.GetType(typeName);
            PropertyInfo p = type?.GetProperty(property, BindingFlags.Public | BindingFlags.Static);
            if (p == null || !p.CanWrite) return;
            try
            {
                p.SetValue(null, value);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Jelly Mold] Could not set " + typeName + "." + property + ": " + e.Message);
            }
        }

        /// <summary>The game reads touches through the built-in Input Manager (no extra package).</summary>
        private static void UseLegacyInputManager()
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0) return;
            var so = new SerializedObject(assets[0]);
            SerializedProperty handler = so.FindProperty("activeInputHandler");
            if (handler == null || handler.intValue == 0 || handler.intValue == 2) return;
            handler.intValue = 2; // "Both": keeps the legacy Input class available.
            so.ApplyModifiedProperties();
            Debug.Log("[Jelly Mold] Active Input Handling set to 'Both'. Restart the editor if prompted.");
        }

        [MenuItem("Jelly Mold/Create Main Scene")]
        public static void EnsureScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/JellyMold/Scenes")) AssetDatabase.CreateFolder("Assets/JellyMold", "Scenes");
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var cam = new GameObject("Main Camera");
                cam.tag = "MainCamera";
                var c = cam.AddComponent<Camera>();
                c.orthographic = true;
                cam.transform.position = new Vector3(0f, 0f, -10f);
                new GameObject("JellyMold").AddComponent<JellyMold.Game.App>();
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }
}
