using System;
using JellyMold.Core;

namespace JellyMold.Procedural
{
    public enum IconKind
    {
        Undo,
        Restart,
        Gear,
        SoundOn,
        SoundOff,
        Star,
        Lock,
        Home,
        Play,
        Close,
        Grid,
        Next,
    }

    /// <summary>
    /// Procedural art: every sprite of the game is generated here from distance fields,
    /// so the project ships no external image assets. Shapes are in units where one board
    /// cell is 1×1 and the image is centred on (0, 0).
    /// </summary>
    public static class ArtLibrary
    {
        public const float JellyHalf = 0.4f;
        public const float JellyRadius = 0.17f;

        // ------------------------------------------------------------------ jellies

        public static PixelImage Jelly(JellyColor color, int size = 192)
        {
            var img = new PixelImage(size, size, size);
            float aa = img.PixelSize;
            Rgba ink = ArtPalette.Ink(color);
            Rgba blush = Rgba.Lerp(ArtPalette.Base(JellyColor.Coral), Rgba.White, 0.35f);

            return img.Paint((x, y) =>
            {
                float d = Sdf.RoundBox(x, y, JellyHalf, JellyHalf, JellyRadius);
                float cover = Sdf.Fill(d, aa);
                if (cover <= 0f) return Rgba.Clear;
                Rgba c = JellyShade(color, x, y, d, JellyHalf, aa);

                // Face: two glossy eyes, a small smile and soft cheeks.
                for (int side = -1; side <= 1; side += 2)
                {
                    float ex = x - side * 0.115f, ey = y - 0.005f;
                    float eye = Sdf.Fill(Sdf.Ellipse(ex, ey, 0.042f, 0.052f), aa);
                    c = c.Over(ink.WithAlpha(eye * 0.92f));
                    float spark = Sdf.Fill(Sdf.Circle(ex - 0.012f, ey - 0.02f, 0.015f), aa);
                    c = c.Over(Rgba.White.WithAlpha(spark * 0.95f));
                    float cheek = Sdf.Soft(Sdf.Ellipse(x - side * 0.215f, y + 0.075f, 0.055f, 0.032f), 0.02f);
                    c = c.Over(blush.WithAlpha(cheek * 0.35f));
                }
                float smile = Sdf.Fill(Sdf.Arc(x, y + 0.045f, 0.05f, (float)(Math.PI * 1.15), (float)(Math.PI * 1.85), 0.011f), aa);
                c = c.Over(ink.WithAlpha(smile * 0.85f));

                return c.WithAlpha(c.A * cover);
            });
        }

        /// <summary>
        /// Translucent jelly material for any shape given its distance <paramref name="d"/>:
        /// vertical gradient, darker rim, inner glow, bottom caustic and a glossy highlight.
        /// </summary>
        private static Rgba JellyShade(JellyColor color, float x, float y, float d, float half, float aa)
        {
            Rgba baseC = ArtPalette.Base(color), light = ArtPalette.Light(color), dark = ArtPalette.Dark(color);
            float g = Sdf.Clamp01((y + half) / (2f * half));
            Rgba c = Rgba.Lerp(dark, baseC, Sdf.SmoothStep(0f, 0.55f, g));
            c = Rgba.Lerp(c, light, Sdf.SmoothStep(0.6f, 1.1f, g) * 0.55f);

            float glow = 1f - Sdf.Clamp01(Sdf.Length(x, y + 0.04f) / (half * 1.05f));
            c = Rgba.Lerp(c, light, glow * glow * 0.35f);

            float inside = Sdf.SmoothStep(0f, 0.075f, -d);
            c = Rgba.Lerp(dark.Scaled(0.94f), c, inside);
            float alpha = Sdf.Lerp(1f, 0.9f, inside);

            // Light refracted through the body collects along the lower inner edge.
            float band = Sdf.SmoothStep(0.012f, 0.03f, -d) * (1f - Sdf.SmoothStep(0.035f, 0.07f, -d));
            c = Rgba.Lerp(c, light, band * Sdf.SmoothStep(-0.05f, -0.3f, y) * 0.7f);

            // Main specular streak (rotated ellipse, upper left) and a small dot.
            float hx = x + 0.165f * half / JellyHalf, hy = y - 0.215f * half / JellyHalf;
            Sdf.Rotate(ref hx, ref hy, -0.62f);
            float spec = Sdf.Soft(Sdf.Ellipse(hx, hy, 0.115f * half / JellyHalf, 0.05f * half / JellyHalf), 0.014f);
            c = c.WithAlpha(alpha).Over(Rgba.White.WithAlpha(spec * 0.82f));
            float dot = Sdf.Fill(Sdf.Circle(x - 0.215f * half / JellyHalf, y - 0.255f * half / JellyHalf, 0.028f * half / JellyHalf), aa);
            c = c.Over(Rgba.White.WithAlpha(dot * 0.9f));

            // A thin rim light on the top edge sells the rounded volume.
            float rim = Sdf.SmoothStep(0.0f, 0.012f, -d) * (1f - Sdf.SmoothStep(0.014f, 0.03f, -d)) * Sdf.SmoothStep(0.1f, 0.35f, y);
            c = c.Over(Rgba.White.WithAlpha(rim * 0.55f));
            return c;
        }

        // ------------------------------------------------------------------ molds

        /// <summary>Distance to the mold silhouette of a colour: coral heart, turquoise drop, yellow star.</summary>
        public static float MoldShape(JellyColor color, float x, float y, float scale = 1f)
        {
            x /= scale;
            y /= scale;
            float d;
            switch (color)
            {
                case JellyColor.Coral:
                    const float k = 0.56f;
                    d = Sdf.Heart(x / k, y / k + 0.53f) * k;
                    break;
                case JellyColor.Turquoise:
                    // Drop: circle blended into a point at the top.
                    float circle = Sdf.Circle(x, y + 0.06f, 0.255f);
                    float tip = Sdf.Triangle(x, y, -0.18f, 0.08f, 0.18f, 0.08f, 0f, 0.33f);
                    d = Sdf.SmoothMin(circle, tip, 0.06f);
                    break;
                default:
                    d = Sdf.Star5(x, y + 0.02f, 0.35f, 0.52f) - 0.035f;
                    break;
            }
            return d * scale;
        }

        /// <summary>Empty mold: recessed tile with the coloured silhouette as a dashed outline.</summary>
        public static PixelImage MoldSocket(JellyColor color, int size = 160)
        {
            var img = new PixelImage(size, size, size);
            float aa = img.PixelSize;
            Rgba baseC = ArtPalette.Base(color), dark = ArtPalette.Dark(color);
            return img.Paint((x, y) =>
            {
                float tile = Sdf.RoundBox(x, y, 0.46f, 0.46f, 0.13f);
                float cover = Sdf.Fill(tile, aa);
                if (cover <= 0f) return Rgba.Clear;
                Rgba c = Rgba.Lerp(ArtPalette.Socket, ArtPalette.Floor, Sdf.SmoothStep(-0.46f, 0.4f, y));
                // Inner shadow along the top edge makes the socket look pressed in.
                float inner = 1f - Sdf.SmoothStep(0f, 0.09f, -Sdf.RoundBox(x, y + 0.03f, 0.46f, 0.46f, 0.13f));
                c = Rgba.Lerp(c, ArtPalette.FloorShade.Scaled(0.93f), inner * Sdf.SmoothStep(-0.1f, 0.4f, y) * 0.8f);

                float d = MoldShape(color, x, y);
                c = c.Over(baseC.WithAlpha(Sdf.Fill(d, aa) * 0.2f));
                float outline = Sdf.Fill(Math.Abs(d + 0.022f) - 0.019f, aa);
                float angle = (float)Math.Atan2(y, x);
                float dash = Sdf.SmoothStep(-0.25f, 0.25f, (float)Math.Cos(angle * 14f));
                c = c.Over(dark.WithAlpha(outline * Sdf.Lerp(0.45f, 0.95f, dash)));
                return c.WithAlpha(c.A * cover);
            });
        }

        /// <summary>Mold after filling: the jelly takes the mold's shape and gets a happy face.</summary>
        public static PixelImage MoldFilled(JellyColor color, int size = 160)
        {
            var img = new PixelImage(size, size, size);
            float aa = img.PixelSize;
            Rgba ink = ArtPalette.Ink(color);
            return img.Paint((x, y) =>
            {
                float d = MoldShape(color, x, y, 1.18f);
                float cover = Sdf.Fill(d, aa);
                if (cover <= 0f) return Rgba.Clear;
                Rgba c = JellyShade(color, x, y, d, 0.36f, aa);
                for (int side = -1; side <= 1; side += 2)
                {
                    // ^ ^ eyes
                    float eye = Sdf.Fill(Sdf.Arc(x - side * 0.09f, y - 0.0f, 0.04f, 0.35f, (float)(Math.PI - 0.35), 0.012f), aa);
                    c = c.Over(ink.WithAlpha(eye * 0.85f));
                }
                float smile = Sdf.Fill(Sdf.Arc(x, y + 0.05f, 0.045f, (float)(Math.PI * 1.1), (float)(Math.PI * 1.9), 0.011f), aa);
                c = c.Over(ink.WithAlpha(smile * 0.8f));
                return c.WithAlpha(c.A * cover);
            });
        }

        // ------------------------------------------------------------------ board

        public static PixelImage Floor(int size = 128)
        {
            var img = new PixelImage(size, size, size);
            float aa = img.PixelSize;
            return img.Paint((x, y) =>
            {
                float d = Sdf.RoundBox(x, y, 0.46f, 0.46f, 0.13f);
                float cover = Sdf.Fill(d, aa);
                if (cover <= 0f) return Rgba.Clear;
                Rgba c = Rgba.Lerp(ArtPalette.FloorShade, ArtPalette.Floor, Sdf.SmoothStep(-0.5f, -0.3f, y));
                float edge = Sdf.SmoothStep(0f, 0.03f, -d);
                c = Rgba.Lerp(ArtPalette.FloorShade, c, 0.6f + 0.4f * edge);
                return c.WithAlpha(cover * 0.92f);
            });
        }

        public static PixelImage Wall(int size = 128)
        {
            var img = new PixelImage(size, size, size);
            float aa = img.PixelSize;
            return img.Paint((x, y) =>
            {
                float d = Sdf.RoundBox(x, y, 0.47f, 0.47f, 0.15f);
                float cover = Sdf.Fill(d, aa);
                if (cover <= 0f) return Rgba.Clear;
                // Side face at the bottom, lit top face above it.
                float top = Sdf.RoundBox(x, y - 0.06f, 0.42f, 0.38f, 0.13f);
                Rgba c = ArtPalette.WallShade;
                float topCover = Sdf.Fill(top, aa);
                Rgba face = Rgba.Lerp(ArtPalette.Wall, ArtPalette.WallTop, Sdf.SmoothStep(-0.3f, 0.45f, y));
                c = Rgba.Lerp(c, face, topCover);
                float shine = Sdf.Soft(Sdf.Ellipse(x + 0.16f, y - 0.26f, 0.13f, 0.045f), 0.02f);
                c = c.Over(Rgba.White.WithAlpha(shine * 0.35f * topCover));
                return c.WithAlpha(cover);
            });
        }

        /// <summary>Rounded translucent panel behind the grid, with its own soft drop shadow.</summary>
        public static PixelImage BoardPanel(int cellsX, int cellsY, int pixelsPerUnit = 48)
        {
            float pad = 0.22f, margin = 0.5f;
            int w = (int)Math.Ceiling((cellsX + 2 * (pad + margin)) * pixelsPerUnit);
            int h = (int)Math.Ceiling((cellsY + 2 * (pad + margin)) * pixelsPerUnit);
            var img = new PixelImage(w, h, pixelsPerUnit);
            float aa = img.PixelSize;
            float hx = cellsX * 0.5f + pad, hy = cellsY * 0.5f + pad;
            return img.Paint((x, y) =>
            {
                float shadow = Sdf.Soft(Sdf.RoundBox(x, y + 0.12f, hx, hy, 0.4f), 0.22f);
                Rgba c = ArtPalette.BoardShadow.WithAlpha(ArtPalette.BoardShadow.A * shadow);
                float d = Sdf.RoundBox(x, y, hx, hy, 0.4f);
                float cover = Sdf.Fill(d, aa);
                Rgba panel = ArtPalette.BoardPanel;
                float rim = Sdf.Fill(Math.Abs(d + 0.02f) - 0.018f, aa) * Sdf.SmoothStep(-hy, hy, y);
                panel = panel.Over(Rgba.White.WithAlpha(rim * 0.8f));
                return c.Over(panel.WithAlpha(panel.A * cover));
            });
        }

        /// <summary>Highlight strip for a pressed row (cells × 1) or column (1 × cells).</summary>
        public static PixelImage Strip(int cellsX, int cellsY, int pixelsPerUnit = 48)
        {
            var img = new PixelImage(cellsX * pixelsPerUnit, cellsY * pixelsPerUnit, pixelsPerUnit);
            float hx = cellsX * 0.5f - 0.02f, hy = cellsY * 0.5f - 0.02f;
            return img.Paint((x, y) =>
            {
                float d = Sdf.RoundBox(x, y, hx, hy, 0.42f);
                float a = Sdf.Soft(d, 0.05f);
                float edge = Sdf.Fill(Math.Abs(d + 0.03f) - 0.02f, img.PixelSize);
                return Rgba.White.WithAlpha(a * 0.5f + edge * 0.45f);
            });
        }

        /// <summary>Pastel gradient with large soft bokeh circles.</summary>
        public static PixelImage Background(int width = 270, int height = 480)
        {
            var img = new PixelImage(width, height, height);
            float halfW = img.WidthUnits * 0.5f;
            var blobs = new[]
            {
                (x: -0.7f * halfW, y: 0.36f, r: 0.2f, c: Rgba.Hex(0xFFD1DF, 0.55f)),
                (x: 0.85f * halfW, y: 0.18f, r: 0.16f, c: Rgba.Hex(0xFFF0C2, 0.6f)),
                (x: -0.9f * halfW, y: -0.22f, r: 0.18f, c: Rgba.Hex(0xC9F1EE, 0.55f)),
                (x: 0.6f * halfW, y: -0.4f, r: 0.22f, c: Rgba.Hex(0xE3D6FF, 0.6f)),
            };
            return img.Paint((x, y) =>
            {
                Rgba c = Rgba.Lerp(ArtPalette.BackgroundBottom, ArtPalette.BackgroundTop, Sdf.SmoothStep(-0.5f, 0.5f, y));
                foreach (var b in blobs)
                {
                    float a = Sdf.Soft(Sdf.Circle(x - b.x, y - b.y, b.r), 0.12f);
                    c = c.Over(b.c.WithAlpha(b.c.A * a));
                }
                return c;
            });
        }

        // ------------------------------------------------------------------ effects

        public static PixelImage SoftCircle(int size = 64)
        {
            var img = new PixelImage(size, size, size);
            return img.Paint((x, y) =>
            {
                float r = Sdf.Clamp01(Sdf.Length(x, y) / 0.5f);
                float a = (1f - r) * (1f - r);
                return Rgba.White.WithAlpha(Sdf.SmoothStep(0f, 1f, a * 1.6f));
            });
        }

        public static PixelImage Sparkle(int size = 64)
        {
            var img = new PixelImage(size, size, size);
            return img.Paint((x, y) =>
            {
                float ax = Math.Abs(x), ay = Math.Abs(y);
                float star = Math.Min(ax * 6f + ay, ay * 6f + ax) / 0.5f;
                float a = Sdf.Clamp01(1f - star);
                float core = Sdf.Clamp01(1f - Sdf.Length(x, y) / 0.12f);
                return Rgba.White.WithAlpha(Sdf.Clamp01(a * 1.4f + core));
            });
        }

        public static PixelImage Ring(int size = 128)
        {
            var img = new PixelImage(size, size, size);
            float aa = img.PixelSize;
            return img.Paint((x, y) =>
            {
                float d = Math.Abs(Sdf.Length(x, y) - 0.42f) - 0.035f;
                return Rgba.White.WithAlpha(Sdf.Fill(d, aa) * 0.9f + Sdf.Soft(d, 0.05f) * 0.3f);
            });
        }

        /// <summary>Pointing hand for the tutorial. The fingertip sits at the image's top centre region.</summary>
        public static PixelImage Hand(int size = 128)
        {
            var img = new PixelImage(size, size, size);
            float aa = img.PixelSize;
            Rgba outline = ArtPalette.UiText;
            return img.Paint((x, y) =>
            {
                float finger = Sdf.Segment(x, y, 0f, 0.02f, 0f, 0.3f, 0.075f);
                float palm = Sdf.RoundBox(x - 0.05f, y + 0.17f, 0.16f, 0.15f, 0.08f);
                float thumb = Sdf.Segment(x, y, -0.1f, -0.1f, -0.2f, 0.0f, 0.055f);
                float knuckle1 = Sdf.Circle(x - 0.11f, y + 0.0f, 0.068f);
                float knuckle2 = Sdf.Circle(x - 0.2f, y + 0.03f, 0.06f);
                float d = Sdf.SmoothMin(finger, palm, 0.05f);
                d = Sdf.SmoothMin(d, thumb, 0.04f);
                d = Sdf.SmoothMin(d, Math.Min(knuckle1, knuckle2), 0.03f);
                float shadow = Sdf.Soft(d + 0.0f - 0.02f, 0.04f) * 0.25f;
                Rgba c = Rgba.Black.WithAlpha(0f);
                c = c.Over(outline.WithAlpha(shadow));
                c = c.Over(outline.WithAlpha(Sdf.Fill(d - 0.022f, aa)));
                Rgba skin = Rgba.Lerp(Rgba.Hex(0xF2ECFF), Rgba.White, Sdf.SmoothStep(-0.3f, 0.3f, y));
                c = c.Over(skin.WithAlpha(Sdf.Fill(d, aa)));
                return c;
            });
        }

        // ------------------------------------------------------------------ UI

        /// <summary>White rounded rectangle meant for 9-slicing; the bottom lip is slightly darker.</summary>
        public static PixelImage UiRoundRect(int size, float radiusPx, bool lip)
        {
            var img = new PixelImage(size, size, size);
            float aa = img.PixelSize, r = radiusPx / size;
            float lipH = lip ? 8f / size : 0f;
            return img.Paint((x, y) =>
            {
                float d = Sdf.RoundBox(x, y, 0.5f - aa, 0.5f - aa, r);
                float cover = Sdf.Fill(d, aa);
                if (cover <= 0f) return Rgba.Clear;
                float face = Sdf.Fill(Sdf.RoundBox(x, y - lipH * 0.5f, 0.5f - aa, 0.5f - aa - lipH * 0.5f, r), aa);
                float shade = lip ? Sdf.Lerp(0.82f, 1f, face) : 1f;
                return new Rgba(shade, shade, shade, cover);
            });
        }

        /// <summary>Soft shadow used behind panels (9-sliced).</summary>
        public static PixelImage UiShadow(int size = 128)
        {
            var img = new PixelImage(size, size, size);
            return img.Paint((x, y) =>
            {
                float d = Sdf.RoundBox(x, y, 0.3f, 0.3f, 0.12f);
                return Rgba.Black.WithAlpha(Sdf.Soft(d, 0.16f));
            });
        }

        public static PixelImage UiCircle(int size, bool lip)
        {
            var img = new PixelImage(size, size, size);
            float aa = img.PixelSize, lipH = lip ? 7f / size : 0f;
            return img.Paint((x, y) =>
            {
                float d = Sdf.Circle(x, y, 0.5f - aa);
                float cover = Sdf.Fill(d, aa);
                if (cover <= 0f) return Rgba.Clear;
                float face = Sdf.Fill(Sdf.Circle(x, y - lipH, 0.5f - aa - lipH), aa);
                float shade = lip ? Sdf.Lerp(0.82f, 1f, face) : 1f;
                return new Rgba(shade, shade, shade, cover);
            });
        }

        public static PixelImage Icon(IconKind kind, int size = 96)
        {
            var img = new PixelImage(size, size, size);
            float aa = img.PixelSize;
            return img.Paint((x, y) => Rgba.White.WithAlpha(Sdf.Fill(IconDistance(kind, x, y), aa)));
        }

        private static float IconDistance(IconKind kind, float x, float y)
        {
            const float W = 0.055f;
            const float Pi = (float)Math.PI;
            switch (kind)
            {
                case IconKind.Undo:
                {
                    float arc = Sdf.Arc(x - 0.03f, y + 0.02f, 0.22f, -Pi * 0.75f, Pi * 0.75f, W);
                    float hx = 0.03f + 0.22f * (float)Math.Cos(Pi * 0.75f), hy = -0.02f + 0.22f * (float)Math.Sin(Pi * 0.75f);
                    float head = Sdf.Triangle(x, y, hx - 0.15f, hy + 0.02f, hx + 0.06f, hy + 0.15f, hx + 0.06f, hy - 0.13f);
                    return Math.Min(arc, head - 0.01f);
                }
                case IconKind.Restart:
                {
                    float arc = Sdf.Arc(x, y, 0.24f, Pi * 0.6f, Pi * 2.3f, W);
                    float ex = 0.24f * (float)Math.Cos(Pi * 0.6f), ey = 0.24f * (float)Math.Sin(Pi * 0.6f);
                    float head = Sdf.Triangle(x, y, ex + 0.14f, ey + 0.02f, ex - 0.06f, ey + 0.14f, ex - 0.06f, ey - 0.12f);
                    return Math.Min(arc, head - 0.01f);
                }
                case IconKind.Gear:
                {
                    float a = (float)Math.Atan2(y, x);
                    float teeth = Sdf.SmoothStep(-0.3f, 0.3f, (float)Math.Cos(a * 8f));
                    float outer = Sdf.Length(x, y) - (0.25f + 0.08f * teeth);
                    return Math.Max(outer, -(Sdf.Length(x, y) - 0.1f));
                }
                case IconKind.SoundOn:
                case IconKind.SoundOff:
                {
                    float box = Sdf.RoundBox(x + 0.2f, y, 0.07f, 0.09f, 0.02f);
                    float cone = Sdf.Triangle(x, y, -0.2f, 0f, 0.04f, 0.24f, 0.04f, -0.24f) - 0.01f;
                    float speaker = Math.Min(box, cone);
                    if (kind == IconKind.SoundOff)
                    {
                        float c1 = Sdf.Segment(x, y, 0.12f, -0.1f, 0.3f, 0.1f, 0.04f);
                        float c2 = Sdf.Segment(x, y, 0.12f, 0.1f, 0.3f, -0.1f, 0.04f);
                        return Math.Min(speaker, Math.Min(c1, c2));
                    }
                    float w1 = Sdf.Arc(x - 0.04f, y, 0.13f, -Pi * 0.27f, Pi * 0.27f, 0.035f);
                    float w2 = Sdf.Arc(x - 0.04f, y, 0.25f, -Pi * 0.27f, Pi * 0.27f, 0.035f);
                    return Math.Min(speaker, Math.Min(w1, w2));
                }
                case IconKind.Star:
                    return Sdf.Star5(x, y + 0.02f, 0.38f, 0.5f) - 0.03f;
                case IconKind.Lock:
                {
                    float body = Sdf.RoundBox(x, y + 0.13f, 0.21f, 0.15f, 0.05f);
                    float shackle = Sdf.Arc(x, y - 0.1f, 0.13f, 0f, Pi, 0.04f);
                    float legs = Math.Min(Sdf.Segment(x, y, -0.13f, -0.02f, -0.13f, 0.1f, 0.04f), Sdf.Segment(x, y, 0.13f, -0.02f, 0.13f, 0.1f, 0.04f));
                    return Math.Min(body, Math.Min(shackle, legs));
                }
                case IconKind.Home:
                {
                    float roof = Sdf.Triangle(x, y, -0.3f, 0.02f, 0.3f, 0.02f, 0f, 0.3f) - 0.02f;
                    float house = Sdf.RoundBox(x, y + 0.13f, 0.19f, 0.16f, 0.03f);
                    float door = Sdf.RoundBox(x, y + 0.2f, 0.06f, 0.1f, 0.02f);
                    return Math.Max(Math.Min(roof, house), -door);
                }
                case IconKind.Play:
                    return Sdf.Triangle(x, y, -0.15f, 0.25f, -0.15f, -0.25f, 0.27f, 0f) - 0.04f;
                case IconKind.Close:
                    return Math.Min(Sdf.Segment(x, y, -0.2f, -0.2f, 0.2f, 0.2f, W), Sdf.Segment(x, y, -0.2f, 0.2f, 0.2f, -0.2f, W));
                case IconKind.Grid:
                {
                    float d = float.MaxValue;
                    for (int i = -1; i <= 1; i += 2)
                        for (int j = -1; j <= 1; j += 2)
                            d = Math.Min(d, Sdf.RoundBox(x - i * 0.13f, y - j * 0.13f, 0.09f, 0.09f, 0.03f));
                    return d;
                }
                case IconKind.Next:
                    return Math.Min(Sdf.Segment(x, y, -0.08f, 0.22f, 0.12f, 0f, W), Sdf.Segment(x, y, -0.08f, -0.22f, 0.12f, 0f, W));
                default:
                    return 1f;
            }
        }
    }
}
