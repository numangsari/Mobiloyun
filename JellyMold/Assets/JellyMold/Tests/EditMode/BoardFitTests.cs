using JellyMold.Core;
using NUnit.Framework;

namespace JellyMold.Tests
{
    /// <summary>Board stays fully visible inside the play band on common phone and tablet screens.</summary>
    public class BoardFitTests
    {
        // width, height, safe xMin, yMin, xMax, yMax (pixels, origin bottom-left)
        private static readonly object[] Screens =
        {
            new object[] { "1080x1920 (16:9)", 1080f, 1920f, 0f, 0f, 1080f, 1920f },
            new object[] { "1080x2400 (20:9)", 1080f, 2400f, 0f, 0f, 1080f, 2400f },
            new object[] { "1170x2532 notch", 1170f, 2532f, 0f, 102f, 1170f, 2391f },
            new object[] { "720x1280 small", 720f, 1280f, 0f, 0f, 720f, 1280f },
            new object[] { "1536x2048 tablet", 1536f, 2048f, 0f, 0f, 1536f, 2048f },
            new object[] { "1080x2340 punch-hole", 1080f, 2340f, 0f, 0f, 1080f, 2260f },
        };

        [TestCaseSource(nameof(Screens))]
        public void BoardFitsInsidePlayBandAndScreen(string name, float w, float h, float sx0, float sy0, float sx1, float sy1)
        {
            const float BoardW = 6.9f, BoardH = 6.9f;
            BoardFit.Result fit = BoardFit.Compute(w, h, sx0, sy0, sx1, sy1, BoardW, BoardH);

            float viewH = fit.OrthographicSize * 2f;
            float viewW = viewH * w / h;
            // Board centre in viewport space: the camera sits CameraOffsetY above the board centre.
            float centerY = 0.5f - fit.CameraOffsetY / viewH;
            float halfH = BoardH * 0.5f / viewH;

            Assert.LessOrEqual(BoardW, viewW * (sx1 - sx0) / w + 1e-3f, name + ": board wider than safe width");
            Assert.GreaterOrEqual(centerY - halfH, fit.PlayBottom - 1e-3f, name + ": overlaps bottom HUD");
            Assert.LessOrEqual(centerY + halfH, fit.PlayTop + 1e-3f, name + ": overlaps top HUD");
            Assert.Greater(sy0 / h, -1e-6f);
            Assert.GreaterOrEqual(fit.PlayBottom, sy0 / h, name + ": play band starts inside the safe area");
            Assert.LessOrEqual(fit.PlayTop, sy1 / h, name + ": play band ends inside the safe area");

            // As large as possible: the board fills whichever dimension limits it.
            float widthUse = (BoardW + BoardFit.SideMargin) / (viewW * (sx1 - sx0) / w);
            float heightUse = BoardH / (viewH * (fit.PlayTop - fit.PlayBottom));
            Assert.AreEqual(1f, System.Math.Max(widthUse, heightUse), 1e-3f, name + ": board not maximised");
        }
    }
}
