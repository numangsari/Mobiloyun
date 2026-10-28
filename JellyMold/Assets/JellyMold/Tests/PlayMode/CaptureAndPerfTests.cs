using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using JellyMold.Core;
using JellyMold.Game;
using JellyMold.Procedural;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.TestTools;

namespace JellyMold.PlayTests
{
    /// <summary>
    /// Produces review artefacts under JellyMold/Builds/: screenshots at phone resolutions and a
    /// performance report. Needs a graphics device (do not run with -nographics).
    /// </summary>
    public class CaptureAndPerfTests
    {
        private static readonly Vector2Int[] Resolutions = { new Vector2Int(1080, 1920), new Vector2Int(1080, 2400) };

        private GameHarness game;

        private static string OutputDir(string sub)
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", sub));
            Directory.CreateDirectory(dir);
            return dir;
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            game = new GameHarness();
            yield return game.Start();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            game.Stop();
            yield return null;
        }

        [UnityTest, Category("Capture")]
        public IEnumerator CaptureScreenshots()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("No graphics device (-nographics); screenshots skipped.");

            string dir = OutputDir("Screenshots");
            Camera cam = Camera.main;
            Canvas canvas = game.Controller.Ui.GetComponent<Canvas>();

            foreach (Vector2Int res in Resolutions)
            {
                var rt = new RenderTexture(res.x, res.y, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                cam.targetTexture = rt;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 5f;
                string tag = res.x + "x" + res.y;

                game.Controller.SimulateShowTitle();
                yield return GameHarness.WaitSeconds(0.8f);
                Save(cam, rt, Path.Combine(dir, tag + "_title.png"));

                foreach (int index in new[] { 0, 6, 19 })
                {
                    yield return game.StartLevel(index);
                    yield return GameHarness.WaitSeconds(0.6f);
                    Save(cam, rt, Path.Combine(dir, tag + "_level" + (index + 1).ToString("00") + ".png"));
                }

                // Mid-move frame of level 7: the press with the double fill, captured during the animation.
                yield return game.StartLevel(6);
                yield return game.PlaySolution(movesToSkipAtEnd: 1);
                SolveResult solve = PuzzleSolver.Solve(game.Flow.Session.Current);
                game.Swipe(solve.Solution[0]);
                yield return GameHarness.WaitSeconds(Timing.Move * 0.9f);
                Save(cam, rt, Path.Combine(dir, tag + "_level07_fill.png"));
                yield return WaitForWinStars();
                Save(cam, rt, Path.Combine(dir, tag + "_win.png"));

                game.Controller.Ui.HideOverlays();
                cam.targetTexture = null;
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                rt.Release();
                Object.Destroy(rt);
            }
            UnityEngine.Debug.Log("[Jelly Mold] Screenshots written to " + dir);
        }

        private IEnumerator WaitForWinStars()
        {
            float end = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < end)
            {
                var stars = game.Controller.Ui.GetComponentsInChildren<UnityEngine.UI.Image>()
                    .Where(image => image.name == "Star0" || image.name == "Star1" || image.name == "Star2").ToArray();
                if (game.Controller.Ui.OverlayOpen && stars.Length == 3 && stars.All(image =>
                    image.color == SpriteFactory.ToColor(ArtPalette.Star) &&
                    (image.rectTransform.localScale - Vector3.one).sqrMagnitude < 0.0001f))
                    yield break;
                yield return null;
            }
            Assert.Fail("Kazanma görüntüsü için üç yıldızın da tamamlanması bekleniyordu.");
        }

        private static void Save(Camera cam, RenderTexture rt, string path)
        {
            cam.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.Destroy(tex);
        }

        [UnityTest, Category("Perf")]
        public IEnumerator PerformanceReport()
        {
            // 1) Cost of generating all procedural art and audio (pure C#, as at first launch).
            var sw = Stopwatch.StartNew();
            foreach (JellyColor c in new[] { JellyColor.Coral, JellyColor.Turquoise, JellyColor.Yellow })
            {
                ArtLibrary.Jelly(c).BleedEdges();
                ArtLibrary.MoldSocket(c).BleedEdges();
                ArtLibrary.MoldFilled(c).BleedEdges();
            }
            ArtLibrary.Background().BleedEdges();
            ArtLibrary.BoardPanel(6, 6).BleedEdges();
            double artMs = sw.Elapsed.TotalMilliseconds;
            sw.Restart();
            foreach (SoundId id in (SoundId[])System.Enum.GetValues(typeof(SoundId))) SoundSynth.Generate(id, 22050);
            double audioMs = sw.Elapsed.TotalMilliseconds;
            SpriteFactory.WarmUp(6, 6);

            // 2) Frame times while playing every level's optimal solution, uncapped.
            int previousRate = Application.targetFrameRate;
            int previousVSync = QualitySettings.vSyncCount;
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 0;
            var frames = new List<float>();
            bool recording = true;
            IEnumerator Recorder()
            {
                while (recording)
                {
                    frames.Add(Time.unscaledDeltaTime * 1000f);
                    yield return null;
                }
            }
            // Bölüm değişiminde durdurulan oyun işlemleri ölçümü kesmemeli.
            Coroutine recorder = game.App.StartCoroutine(Recorder());

            try
            {
                for (int i = 0; i < game.Flow.Levels.Count; i++)
                {
                    yield return game.StartLevel(i);
                    yield return game.PlaySolution();
                    yield return GameHarness.WaitSeconds(0.3f);
                }
            }
            finally
            {
                recording = false;
                game.App.StopCoroutine(recorder);
                Application.targetFrameRate = previousRate;
                QualitySettings.vSyncCount = previousVSync;
            }

            frames.RemoveRange(0, Mathf.Min(10, frames.Count)); // warm-up frames
            Assert.Greater(frames.Count, 100);
            frames.Sort();
            float avg = frames.Count > 0 ? frames.Average() : 0f;
            float p95 = frames.Count > 0 ? frames[(int)(frames.Count * 0.95f)] : 0f;
            float max = frames.Count > 0 ? frames[frames.Count - 1] : 0f;

            var json = new StringBuilder();
            json.Append("{\n");
            Field(json, "unityVersion", "\"" + Application.unityVersion + "\"");
            Field(json, "platform", "\"" + Application.platform + "\"");
            Field(json, "graphicsDevice", "\"" + SystemInfo.graphicsDeviceName + "\"");
            Field(json, "artGenerationMs", F(artMs));
            Field(json, "audioGenerationMs", F(audioMs));
            Field(json, "sprites", SpriteFactory.SpriteCount.ToString(CultureInfo.InvariantCulture));
            Field(json, "spriteTextureMB", F(SpriteFactory.TextureBytes / 1048576.0));
            Field(json, "frames", frames.Count.ToString(CultureInfo.InvariantCulture));
            Field(json, "frameMsAvg", F(avg));
            Field(json, "frameMsP95", F(p95));
            Field(json, "frameMsMax", F(max));
            Field(json, "monoUsedMB", F(Profiler.GetMonoUsedSizeLong() / 1048576.0));
            Field(json, "totalAllocatedMB", F(Profiler.GetTotalAllocatedMemoryLong() / 1048576.0));
            Field(json, "totalReservedMB", F(Profiler.GetTotalReservedMemoryLong() / 1048576.0));
            Field(json, "textureMemoryMB", F(Texture.currentTextureMemory / 1048576.0), last: true);
            json.Append("}\n");

            string path = Path.Combine(OutputDir(""), "perf.json");
            File.WriteAllText(path, json.ToString());
            UnityEngine.Debug.Log("[Jelly Mold] Performance report:\n" + json);
        }

        private static string F(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);

        private static void Field(StringBuilder sb, string name, string value, bool last = false)
        {
            sb.Append("  \"").Append(name).Append("\": ").Append(value).Append(last ? "\n" : ",\n");
        }
    }
}
