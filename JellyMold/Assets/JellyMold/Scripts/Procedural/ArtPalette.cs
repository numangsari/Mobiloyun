using System;
using JellyMold.Core;

namespace JellyMold.Procedural
{
    /// <summary>All colours of the game in one place: a soft pastel scene with saturated jellies.</summary>
    public static class ArtPalette
    {
        public static readonly Rgba BackgroundTop = Rgba.Hex(0xFFE6EF);
        public static readonly Rgba BackgroundBottom = Rgba.Hex(0xD6E9FF);
        public static readonly Rgba BoardPanel = Rgba.Hex(0xFFFFFF, 0.55f);
        public static readonly Rgba BoardShadow = Rgba.Hex(0x6B5B95, 0.18f);
        public static readonly Rgba Floor = Rgba.Hex(0xF7F2FF);
        public static readonly Rgba FloorShade = Rgba.Hex(0xE6DDF7);
        public static readonly Rgba Socket = Rgba.Hex(0xE9E1F7);
        public static readonly Rgba Wall = Rgba.Hex(0xA597D8);
        public static readonly Rgba WallTop = Rgba.Hex(0xC6BBF0);
        public static readonly Rgba WallShade = Rgba.Hex(0x7C6DB8);

        public static readonly Rgba UiText = Rgba.Hex(0x4A3F6B);
        public static readonly Rgba UiTextSoft = Rgba.Hex(0x8C82AA);
        public static readonly Rgba UiPanel = Rgba.Hex(0xFFFFFF);
        public static readonly Rgba UiPrimary = Rgba.Hex(0xFF7482);
        public static readonly Rgba UiSecondary = Rgba.Hex(0x3CC8BE);
        public static readonly Rgba UiDim = Rgba.Hex(0x2B2140, 0.45f);
        public static readonly Rgba UiLocked = Rgba.Hex(0xD3CCE6);
        public static readonly Rgba Star = Rgba.Hex(0xFFC93C);

        public static Rgba Base(JellyColor c)
        {
            switch (c)
            {
                case JellyColor.Coral: return Rgba.Hex(0xFF6B7A);
                case JellyColor.Turquoise: return Rgba.Hex(0x26C6BE);
                case JellyColor.Yellow: return Rgba.Hex(0xFFBF2E);
                default: return Rgba.White;
            }
        }

        public static Rgba Light(JellyColor c) => Rgba.Lerp(Base(c), Rgba.White, 0.5f);

        public static Rgba Dark(JellyColor c)
        {
            Base(c).ToHsv(out float h, out float s, out float v);
            return Rgba.FromHsv(h + (c == JellyColor.Yellow ? -0.03f : 0.01f), Math.Min(1f, s * 1.08f), v * 0.74f);
        }

        /// <summary>Deep tone for the jelly's eyes and mouth.</summary>
        public static Rgba Ink(JellyColor c)
        {
            Base(c).ToHsv(out float h, out float s, out _);
            return Rgba.FromHsv(h, Math.Min(1f, s * 0.9f), 0.28f);
        }
    }
}
