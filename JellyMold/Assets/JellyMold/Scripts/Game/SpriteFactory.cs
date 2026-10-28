using System;
using System.Collections.Generic;
using JellyMold.Core;
using JellyMold.Procedural;
using UnityEngine;

namespace JellyMold.Game
{
    /// <summary>Turns <see cref="ArtLibrary"/> images into cached Unity sprites (generated once per run).</summary>
    public static class SpriteFactory
    {
        /// <summary>UI sprites use 100 px per unit so 9-slice borders map 1:1 to canvas units.</summary>
        private const float UiPixelsPerUnit = 100f;

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite Jelly(JellyColor c) => Get("jelly_" + c, () => ArtLibrary.Jelly(c));
        public static Sprite MoldSocket(JellyColor c) => Get("mold_" + c, () => ArtLibrary.MoldSocket(c));
        public static Sprite MoldFilled(JellyColor c) => Get("filled_" + c, () => ArtLibrary.MoldFilled(c));
        public static Sprite Floor() => Get("floor", () => ArtLibrary.Floor());
        public static Sprite Wall() => Get("wall", () => ArtLibrary.Wall());
        public static Sprite BoardPanel(int w, int h) => Get("panel_" + w + "x" + h, () => ArtLibrary.BoardPanel(w, h));
        public static Sprite Strip(int w, int h) => Get("strip_" + w + "x" + h, () => ArtLibrary.Strip(w, h));
        public static Sprite Background() => Get("background", () => ArtLibrary.Background());
        public static Sprite SoftCircle() => Get("soft", () => ArtLibrary.SoftCircle());
        public static Sprite Sparkle() => Get("sparkle", () => ArtLibrary.Sparkle());
        public static Sprite Ring() => Get("ring", () => ArtLibrary.Ring());

        /// <summary>Hand pivot sits on the fingertip so it can be placed exactly on a cell.</summary>
        public static Sprite Hand() => Get("hand", () => ArtLibrary.Hand(), new Vector2(0.5f, 0.86f));

        public static Sprite UiRoundRect() => GetUi("ui_round", () => ArtLibrary.UiRoundRect(128, 44, false), 48);
        public static Sprite UiButton() => GetUi("ui_button", () => ArtLibrary.UiRoundRect(128, 44, true), 48);
        public static Sprite UiShadow() => GetUi("ui_shadow", () => ArtLibrary.UiShadow(), 56);
        public static Sprite UiCircle() => GetUi("ui_circle", () => ArtLibrary.UiCircle(128, false), 0);
        public static Sprite UiCircleButton() => GetUi("ui_circle_btn", () => ArtLibrary.UiCircle(128, true), 0);
        public static Sprite Icon(IconKind k) => GetUi("icon_" + k, () => ArtLibrary.Icon(k), 0);

        /// <summary>Number of generated sprites and their total texture size in bytes (RGBA32, no mipmaps).</summary>
        public static int SpriteCount => Cache.Count;

        public static long TextureBytes
        {
            get
            {
                long total = 0;
                foreach (Sprite s in Cache.Values)
                {
                    if (s != null) total += (long)s.texture.width * s.texture.height * 4;
                }
                return total;
            }
        }

        /// <summary>Generates every board sprite up front (used by the performance report).</summary>
        public static void WarmUp(int boardWidth, int boardHeight)
        {
            foreach (JellyColor c in new[] { JellyColor.Coral, JellyColor.Turquoise, JellyColor.Yellow })
            {
                Jelly(c);
                MoldSocket(c);
                MoldFilled(c);
            }
            Floor();
            Wall();
            BoardPanel(boardWidth, boardHeight);
            Strip(boardWidth, 1);
            Strip(1, boardHeight);
            Background();
            SoftCircle();
            Sparkle();
            Ring();
            Hand();
            UiRoundRect();
            UiButton();
            UiShadow();
            UiCircle();
            UiCircleButton();
            foreach (IconKind k in (IconKind[])Enum.GetValues(typeof(IconKind))) Icon(k);
        }

        private static Sprite Get(string key, Func<PixelImage> make, Vector2? pivot = null)
        {
            if (Cache.TryGetValue(key, out Sprite s) && s != null) return s;
            PixelImage img = make().BleedEdges();
            s = Create(img, pivot ?? new Vector2(0.5f, 0.5f), img.PixelsPerUnit, Vector4.zero, key);
            Cache[key] = s;
            return s;
        }

        private static Sprite GetUi(string key, Func<PixelImage> make, float border)
        {
            if (Cache.TryGetValue(key, out Sprite s) && s != null) return s;
            PixelImage img = make().BleedEdges();
            s = Create(img, new Vector2(0.5f, 0.5f), UiPixelsPerUnit, new Vector4(border, border, border, border), key);
            Cache[key] = s;
            return s;
        }

        private static Sprite Create(PixelImage img, Vector2 pivot, float pixelsPerUnit, Vector4 border, string name)
        {
            var tex = new Texture2D(img.Width, img.Height, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            tex.LoadRawTextureData(img.ToRgba32());
            // Upload to the GPU and drop the CPU copy to save memory.
            tex.Apply(false, true);
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, img.Width, img.Height), pivot, pixelsPerUnit, 0, SpriteMeshType.FullRect, border);
            sprite.name = name;
            return sprite;
        }

        public static Color ToColor(Rgba c) => new Color(c.R, c.G, c.B, c.A);
    }
}
