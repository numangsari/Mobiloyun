using System;

namespace JellyMold.Core
{
    /// <summary>
    /// Pure layout math for the board camera: largest orthographic size that keeps the board
    /// inside the play band (safe area minus HUD reserves) for any screen shape.
    /// </summary>
    public static class BoardFit
    {
        /// <summary>Share of the safe-area height reserved for the top HUD and the bottom buttons.</summary>
        public const float TopReserve = 0.14f;
        public const float BottomReserve = 0.22f;

        /// <summary>Extra horizontal margin in world units so the panel never touches the screen edge.</summary>
        public const float SideMargin = 0.3f;

        public readonly struct Result
        {
            /// <summary>Camera orthographic size (half the visible height in world units).</summary>
            public readonly float OrthographicSize;

            /// <summary>Camera y offset relative to the board centre (world units).</summary>
            public readonly float CameraOffsetY;

            /// <summary>Play band in viewport coordinates (0 = bottom, 1 = top of the screen).</summary>
            public readonly float PlayBottom, PlayTop;

            public Result(float size, float offset, float bottom, float top)
            {
                OrthographicSize = size;
                CameraOffsetY = offset;
                PlayBottom = bottom;
                PlayTop = top;
            }
        }

        /// <param name="screenWidth">Render target width in pixels.</param>
        /// <param name="screenHeight">Render target height in pixels.</param>
        /// <param name="safeXMin">Safe area rectangle in pixels (use 0, 0, width, height when there is none).</param>
        /// <param name="boardWidth">Board width in world units including the panel margin.</param>
        public static Result Compute(float screenWidth, float screenHeight, float safeXMin, float safeYMin, float safeXMax, float safeYMax, float boardWidth, float boardHeight)
        {
            if (screenWidth <= 0 || screenHeight <= 0) throw new ArgumentException("Screen size must be positive.");
            float aspect = screenWidth / screenHeight;
            float safeBottom = safeYMin / screenHeight, safeTop = safeYMax / screenHeight;
            float safeWidth = Math.Max(0.1f, (safeXMax - safeXMin) / screenWidth);

            float playBottom = safeBottom + (safeTop - safeBottom) * BottomReserve;
            float playTop = safeTop - (safeTop - safeBottom) * TopReserve;
            float playHeight = Math.Max(0.1f, playTop - playBottom);

            float sizeForHeight = boardHeight / (2f * playHeight);
            float sizeForWidth = (boardWidth + SideMargin) / (2f * aspect * safeWidth);
            float size = Math.Max(sizeForHeight, sizeForWidth);

            // Shift the camera so the board centre sits in the middle of the play band.
            float bandCenter = (playBottom + playTop) * 0.5f;
            float offset = (0.5f - bandCenter) * size * 2f;
            return new Result(size, offset, playBottom, playTop);
        }
    }
}
