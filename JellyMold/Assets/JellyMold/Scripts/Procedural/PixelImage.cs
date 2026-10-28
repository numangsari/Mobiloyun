using System;

namespace JellyMold.Procedural
{
    /// <summary>Straight-alpha colour with float channels in 0..1.</summary>
    public struct Rgba
    {
        public float R, G, B, A;

        public Rgba(float r, float g, float b, float a = 1f)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public static readonly Rgba Clear = new Rgba(0, 0, 0, 0);
        public static readonly Rgba White = new Rgba(1, 1, 1, 1);
        public static readonly Rgba Black = new Rgba(0, 0, 0, 1);

        public static Rgba Hex(int rgb, float a = 1f)
        {
            return new Rgba(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, a);
        }

        public static Rgba Lerp(Rgba a, Rgba b, float t)
        {
            t = Sdf.Clamp01(t);
            return new Rgba(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t, a.A + (b.A - a.A) * t);
        }

        public Rgba WithAlpha(float a) => new Rgba(R, G, B, a);
        public Rgba Scaled(float k) => new Rgba(R * k, G * k, B * k, A);

        /// <summary>Source-over compositing of <paramref name="src"/> onto this colour.</summary>
        public Rgba Over(Rgba src)
        {
            float a = src.A + A * (1f - src.A);
            if (a <= 1e-6f) return Clear;
            float k = A * (1f - src.A);
            return new Rgba((src.R * src.A + R * k) / a, (src.G * src.A + G * k) / a, (src.B * src.A + B * k) / a, a);
        }

        public static Rgba FromHsv(float h, float s, float v, float a = 1f)
        {
            h = ((h % 1f) + 1f) % 1f * 6f;
            int i = (int)Math.Floor(h);
            float f = h - i, p = v * (1 - s), q = v * (1 - s * f), t = v * (1 - s * (1 - f));
            switch (i % 6)
            {
                case 0: return new Rgba(v, t, p, a);
                case 1: return new Rgba(q, v, p, a);
                case 2: return new Rgba(p, v, t, a);
                case 3: return new Rgba(p, q, v, a);
                case 4: return new Rgba(t, p, v, a);
                default: return new Rgba(v, p, q, a);
            }
        }

        public void ToHsv(out float h, out float s, out float v)
        {
            float max = Math.Max(R, Math.Max(G, B)), min = Math.Min(R, Math.Min(G, B));
            v = max;
            float d = max - min;
            s = max <= 0 ? 0 : d / max;
            if (d <= 1e-6f) { h = 0; return; }
            if (max == R) h = (G - B) / d + (G < B ? 6 : 0);
            else if (max == G) h = (B - R) / d + 2;
            else h = (R - G) / d + 4;
            h /= 6f;
        }
    }

    /// <summary>
    /// CPU image in straight alpha, row 0 at the bottom (Unity texture convention).
    /// Shapes are described in "units" centred on the image; <see cref="PixelsPerUnit"/> converts.
    /// </summary>
    public sealed class PixelImage
    {
        public readonly int Width;
        public readonly int Height;
        public readonly float PixelsPerUnit;
        public readonly Rgba[] Pixels;

        public PixelImage(int width, int height, float pixelsPerUnit)
        {
            Width = width;
            Height = height;
            PixelsPerUnit = pixelsPerUnit;
            Pixels = new Rgba[width * height];
        }

        /// <summary>Size of one pixel in units, used as the anti-aliasing width.</summary>
        public float PixelSize => 1f / PixelsPerUnit;

        public float WidthUnits => Width / PixelsPerUnit;
        public float HeightUnits => Height / PixelsPerUnit;

        /// <summary>Evaluates <paramref name="shader"/> at every pixel centre and composites it over the image.</summary>
        public PixelImage Paint(Func<float, float, Rgba> shader)
        {
            float halfW = WidthUnits * 0.5f, halfH = HeightUnits * 0.5f;
            for (int y = 0; y < Height; y++)
            {
                float uy = (y + 0.5f) / PixelsPerUnit - halfH;
                for (int x = 0; x < Width; x++)
                {
                    float ux = (x + 0.5f) / PixelsPerUnit - halfW;
                    Rgba c = shader(ux, uy);
                    if (c.A <= 0f) continue;
                    int i = y * Width + x;
                    Pixels[i] = Pixels[i].Over(c);
                }
            }
            return this;
        }

        public Rgba Get(int x, int y) => Pixels[y * Width + x];

        /// <summary>
        /// Copies colour into fully transparent pixels from their visible neighbours so bilinear
        /// filtering of straight-alpha textures does not produce dark fringes around shapes.
        /// </summary>
        public PixelImage BleedEdges(int passes = 3)
        {
            var src = new Rgba[Pixels.Length];
            for (int pass = 0; pass < passes; pass++)
            {
                Array.Copy(Pixels, src, Pixels.Length);
                for (int y = 0; y < Height; y++)
                {
                    for (int x = 0; x < Width; x++)
                    {
                        int i = y * Width + x;
                        if (src[i].A > 0f || (src[i].R + src[i].G + src[i].B) > 0f) continue;
                        float r = 0, g = 0, b = 0;
                        int n = 0;
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            int ny = y + dy;
                            if (ny < 0 || ny >= Height) continue;
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int nx = x + dx;
                                if (nx < 0 || nx >= Width) continue;
                                Rgba c = src[ny * Width + nx];
                                if (c.A <= 0f && (c.R + c.G + c.B) <= 0f) continue;
                                r += c.R;
                                g += c.G;
                                b += c.B;
                                n++;
                            }
                        }
                        if (n > 0) Pixels[i] = new Rgba(r / n, g / n, b / n, 0f);
                    }
                }
            }
            return this;
        }

        /// <summary>8-bit RGBA bytes, bottom row first.</summary>
        public byte[] ToRgba32()
        {
            var bytes = new byte[Pixels.Length * 4];
            for (int i = 0; i < Pixels.Length; i++)
            {
                Rgba p = Pixels[i];
                bytes[i * 4] = ToByte(p.R);
                bytes[i * 4 + 1] = ToByte(p.G);
                bytes[i * 4 + 2] = ToByte(p.B);
                bytes[i * 4 + 3] = ToByte(p.A);
            }
            return bytes;
        }

        private static byte ToByte(float v)
        {
            return (byte)(Sdf.Clamp01(v) * 255f + 0.5f);
        }
    }
}
