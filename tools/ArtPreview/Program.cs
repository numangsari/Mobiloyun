using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.IO.Compression;
using JellyMold.Core;
using JellyMold.Procedural;

namespace JellyMold.ArtPreview
{
    /// <summary>
    /// ArtPreview &lt;outDir&gt; [level.json]
    /// Writes every procedural sprite as PNG plus "board.png", a rough composite of a level
    /// laid out like the game scene, and a WAV per sound effect.
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--bench") return Bench();
            string outDir = args.Length > 0 ? args[0] : "art-preview";
            Directory.CreateDirectory(outDir);

            foreach (JellyColor c in new[] { JellyColor.Coral, JellyColor.Turquoise, JellyColor.Yellow })
            {
                Png.Write(Path.Combine(outDir, "jelly_" + c + ".png"), ArtLibrary.Jelly(c));
                Png.Write(Path.Combine(outDir, "mold_" + c + ".png"), ArtLibrary.MoldSocket(c));
                Png.Write(Path.Combine(outDir, "filled_" + c + ".png"), ArtLibrary.MoldFilled(c));
            }
            Png.Write(Path.Combine(outDir, "floor.png"), ArtLibrary.Floor());
            Png.Write(Path.Combine(outDir, "wall.png"), ArtLibrary.Wall());
            Png.Write(Path.Combine(outDir, "hand.png"), ArtLibrary.Hand());
            Png.Write(Path.Combine(outDir, "sparkle.png"), ArtLibrary.Sparkle());
            Png.Write(Path.Combine(outDir, "ring.png"), ArtLibrary.Ring());
            foreach (IconKind k in Enum.GetValues(typeof(IconKind)))
                Png.Write(Path.Combine(outDir, "icon_" + k + ".png"), ArtLibrary.Icon(k));

            if (args.Length > 1)
            {
                LevelData level = LevelData.FromJson(File.ReadAllText(args[1]));
                Png.Write(Path.Combine(outDir, "board.png"), Composite(level.CreateInitialState(), 2));
            }

            foreach (SoundId id in Enum.GetValues(typeof(SoundId)))
                Wav.Write(Path.Combine(outDir, "sfx_" + id + ".wav"), SoundSynth.Generate(id, 22050), 22050);
            Console.WriteLine("written to " + Path.GetFullPath(outDir));
            return 0;
        }

        /// <summary>
        /// Times generating every sprite the game creates at runtime (same set as SpriteFactory.WarmUp)
        /// and every sound, and reports their memory footprint. Desktop .NET numbers: relative only.
        /// </summary>
        private static int Bench()
        {
            var sprites = new List<Func<PixelImage>>();
            foreach (JellyColor c in new[] { JellyColor.Coral, JellyColor.Turquoise, JellyColor.Yellow })
            {
                sprites.Add(() => ArtLibrary.Jelly(c));
                sprites.Add(() => ArtLibrary.MoldSocket(c));
                sprites.Add(() => ArtLibrary.MoldFilled(c));
            }
            sprites.Add(() => ArtLibrary.Floor());
            sprites.Add(() => ArtLibrary.Wall());
            sprites.Add(() => ArtLibrary.BoardPanel(6, 6));
            sprites.Add(() => ArtLibrary.Strip(6, 1));
            sprites.Add(() => ArtLibrary.Strip(1, 6));
            sprites.Add(() => ArtLibrary.Background());
            sprites.Add(() => ArtLibrary.SoftCircle());
            sprites.Add(() => ArtLibrary.Sparkle());
            sprites.Add(() => ArtLibrary.Ring());
            sprites.Add(() => ArtLibrary.Hand());
            sprites.Add(() => ArtLibrary.UiRoundRect(128, 44, false));
            sprites.Add(() => ArtLibrary.UiRoundRect(128, 44, true));
            sprites.Add(() => ArtLibrary.UiShadow());
            sprites.Add(() => ArtLibrary.UiCircle(128, false));
            sprites.Add(() => ArtLibrary.UiCircle(128, true));
            foreach (IconKind k in Enum.GetValues(typeof(IconKind))) sprites.Add(() => ArtLibrary.Icon(k));

            long pixels = 0;
            var artTimes = new List<double>();
            var audioTimes = new List<double>();
            long samples = 0;
            for (int run = 0; run < 6; run++)
            {
                var sw = Stopwatch.StartNew();
                pixels = 0;
                foreach (Func<PixelImage> make in sprites)
                {
                    PixelImage img = make().BleedEdges();
                    img.ToRgba32();
                    pixels += (long)img.Width * img.Height;
                }
                artTimes.Add(sw.Elapsed.TotalMilliseconds);
                sw.Restart();
                samples = 0;
                foreach (SoundId id in Enum.GetValues(typeof(SoundId))) samples += SoundSynth.Generate(id, 22050).Length;
                audioTimes.Add(sw.Elapsed.TotalMilliseconds);
            }
            artTimes.RemoveAt(0); // JIT warm-up
            audioTimes.RemoveAt(0);

            Console.WriteLine("| Measure | Value |");
            Console.WriteLine("|---|---|");
            Console.WriteLine("| Sprites | " + sprites.Count + " |");
            Console.WriteLine("| Sprite pixels | " + pixels.ToString("N0") + " |");
            Console.WriteLine("| Texture memory (RGBA32, no mipmaps) | " + (pixels * 4 / 1048576.0).ToString("0.00") + " MB |");
            Console.WriteLine("| Sprite generation (min / avg of 5) | " + artTimes.Min().ToString("0") + " / " + artTimes.Average().ToString("0") + " ms |");
            Console.WriteLine("| Sound effects | " + Enum.GetValues(typeof(SoundId)).Length + " clips, " + (samples / 22050.0).ToString("0.00") + " s, " + (samples * 4 / 1024.0).ToString("0") + " KB as float |");
            Console.WriteLine("| Sound generation (min / avg of 5) | " + audioTimes.Min().ToString("0.0") + " / " + audioTimes.Average().ToString("0.0") + " ms |");
            Console.WriteLine("| Machine | " + Environment.ProcessorCount + " cores, " + System.Runtime.InteropServices.RuntimeInformation.OSDescription + ", .NET " + Environment.Version + " |");
            return 0;
        }

        /// <summary>Approximates the in-game scene: background, panel, tiles, molds, jellies, one filled mold.</summary>
        private static PixelImage Composite(BoardState s, int fillFirstMolds)
        {
            const int ppu = 96;
            BoardLayout l = s.Layout;
            float worldW = 8f, worldH = worldW * 16f / 9f;
            var canvas = new PixelImage((int)(worldW * ppu), (int)(worldH * ppu), ppu);
            Blit(canvas, ArtLibrary.Background(), 0, 0, worldW, worldH);
            Blit(canvas, ArtLibrary.BoardPanel(l.Width, l.Height), 0, 0.6f, l.Width + 1.44f, l.Height + 1.44f);

            PixelImage floor = ArtLibrary.Floor(), wall = ArtLibrary.Wall(), shadow = ArtLibrary.SoftCircle();
            int filled = 0;
            for (int i = 0; i < l.CellCount; i++)
            {
                float cx = l.X(i) - (l.Width - 1) * 0.5f, cy = 0.6f + (l.Height - 1) * 0.5f - l.Y(i);
                TileType t = l.GetTile(i);
                if (t == TileType.Wall) { Blit(canvas, wall, cx, cy, 1, 1); continue; }
                if (t == TileType.Mold)
                {
                    JellyColor mc = l.GetMoldColor(i);
                    Blit(canvas, ArtLibrary.MoldSocket(mc), cx, cy, 1, 1);
                    if (filled++ < fillFirstMolds) Blit(canvas, ArtLibrary.MoldFilled(mc), cx, cy, 1, 1);
                    continue;
                }
                Blit(canvas, floor, cx, cy, 1, 1);
            }
            for (int i = 0; i < l.CellCount; i++)
            {
                JellyColor j = s.GetJelly(i);
                if (j == JellyColor.None) continue;
                float cx = l.X(i) - (l.Width - 1) * 0.5f, cy = 0.6f + (l.Height - 1) * 0.5f - l.Y(i);
                Blit(canvas, shadow, cx, cy - 0.36f, 0.85f, 0.28f, 0.22f);
                Blit(canvas, ArtLibrary.Jelly(j), cx, cy, 1, 1);
            }
            return canvas;
        }

        /// <summary>Nearest-neighbour-free bilinear blit of a sprite centred at (cx, cy) in canvas units.</summary>
        private static void Blit(PixelImage dst, PixelImage src, float cx, float cy, float w, float h, float tintAlpha = 1f)
        {
            float halfW = dst.WidthUnits * 0.5f, halfH = dst.HeightUnits * 0.5f;
            int x0 = (int)Math.Floor((cx - w / 2 + halfW) * dst.PixelsPerUnit), x1 = (int)Math.Ceiling((cx + w / 2 + halfW) * dst.PixelsPerUnit);
            int y0 = (int)Math.Floor((cy - h / 2 + halfH) * dst.PixelsPerUnit), y1 = (int)Math.Ceiling((cy + h / 2 + halfH) * dst.PixelsPerUnit);
            for (int y = Math.Max(0, y0); y < Math.Min(dst.Height, y1); y++)
            {
                for (int x = Math.Max(0, x0); x < Math.Min(dst.Width, x1); x++)
                {
                    float u = ((x + 0.5f) / dst.PixelsPerUnit - halfW - (cx - w / 2)) / w;
                    float v = ((y + 0.5f) / dst.PixelsPerUnit - halfH - (cy - h / 2)) / h;
                    if (u < 0 || v < 0 || u >= 1 || v >= 1) continue;
                    Rgba c = Sample(src, u, v);
                    c.A *= tintAlpha;
                    if (tintAlpha < 1f) c = new Rgba(0.3f, 0.2f, 0.45f, c.A);
                    int i = y * dst.Width + x;
                    dst.Pixels[i] = dst.Pixels[i].Over(c);
                }
            }
        }

        private static Rgba Sample(PixelImage img, float u, float v)
        {
            float fx = u * img.Width - 0.5f, fy = v * img.Height - 0.5f;
            int ix = (int)Math.Floor(fx), iy = (int)Math.Floor(fy);
            float tx = fx - ix, ty = fy - iy;
            Rgba a = Px(img, ix, iy), b = Px(img, ix + 1, iy), c = Px(img, ix, iy + 1), d = Px(img, ix + 1, iy + 1);
            return Rgba.Lerp(Rgba.Lerp(a, b, tx), Rgba.Lerp(c, d, tx), ty);
        }

        private static Rgba Px(PixelImage img, int x, int y)
        {
            x = Math.Clamp(x, 0, img.Width - 1);
            y = Math.Clamp(y, 0, img.Height - 1);
            return img.Get(x, y);
        }
    }

    internal static class Png
    {
        public static void Write(string path, PixelImage img)
        {
            byte[] rgba = img.ToRgba32();
            using var raw = new MemoryStream();
            for (int y = img.Height - 1; y >= 0; y--)
            {
                raw.WriteByte(0);
                raw.Write(rgba, y * img.Width * 4, img.Width * 4);
            }
            using var z = new MemoryStream();
            using (var zs = new ZLibStream(z, CompressionLevel.Optimal, true)) raw.WriteTo(zs);

            using var f = File.Create(path);
            f.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
            var ihdr = new byte[13];
            BE(ihdr, 0, img.Width);
            BE(ihdr, 4, img.Height);
            ihdr[8] = 8; ihdr[9] = 6;
            Chunk(f, "IHDR", ihdr);
            Chunk(f, "IDAT", z.ToArray());
            Chunk(f, "IEND", new byte[0]);
        }

        private static void Chunk(Stream s, string type, byte[] data)
        {
            var len = new byte[4];
            BE(len, 0, data.Length);
            s.Write(len);
            var td = new byte[4 + data.Length];
            for (int i = 0; i < 4; i++) td[i] = (byte)type[i];
            Array.Copy(data, 0, td, 4, data.Length);
            s.Write(td);
            var crc = new byte[4];
            BE(crc, 0, (int)Crc(td));
            s.Write(crc);
        }

        private static uint Crc(byte[] b)
        {
            uint c = 0xFFFFFFFF;
            foreach (byte x in b)
            {
                c ^= x;
                for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            }
            return c ^ 0xFFFFFFFF;
        }

        private static void BE(byte[] b, int o, int v)
        {
            b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v;
        }
    }

    internal static class Wav
    {
        public static void Write(string path, float[] samples, int rate)
        {
            using var f = new BinaryWriter(File.Create(path));
            f.Write("RIFF".ToCharArray()); f.Write(36 + samples.Length * 2); f.Write("WAVE".ToCharArray());
            f.Write("fmt ".ToCharArray()); f.Write(16); f.Write((short)1); f.Write((short)1); f.Write(rate); f.Write(rate * 2); f.Write((short)2); f.Write((short)16);
            f.Write("data".ToCharArray()); f.Write(samples.Length * 2);
            foreach (float s in samples) f.Write((short)(Math.Clamp(s, -1f, 1f) * 32767));
        }
    }
}
