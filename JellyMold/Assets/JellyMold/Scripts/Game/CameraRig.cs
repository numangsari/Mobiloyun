using JellyMold.Core;
using UnityEngine;

namespace JellyMold.Game
{
    /// <summary>
    /// Orthographic camera that keeps the board as large as possible between the HUD bars,
    /// inside the device safe area, for any aspect ratio. Also stretches the background.
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        private Camera cam;
        private SpriteRenderer background;
        private BoardView board;
        private Vector2 lastScreen;
        private Rect lastSafe;
        private Vector2 lastBoardSize;

        public Camera Camera => cam;

        public static CameraRig Create(Transform parent, BoardView board)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                cam = camGo.AddComponent<Camera>();
            }
            if (FindFirstObjectByType<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = SpriteFactory.ToColor(Procedural.ArtPalette.BackgroundBottom);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 50f;

            var rig = cam.gameObject.AddComponent<CameraRig>();
            rig.cam = cam;
            rig.board = board;

            var bg = new GameObject("Background");
            bg.transform.SetParent(parent, false);
            rig.background = bg.AddComponent<SpriteRenderer>();
            rig.background.sprite = SpriteFactory.Background();
            rig.background.sortingOrder = SortOrder.Background;
            rig.Refit();
            return rig;
        }

        private void LateUpdate()
        {
            var screen = new Vector2(cam.pixelWidth, cam.pixelHeight);
            if (screen != lastScreen || SafeRect() != lastSafe || board.Size != lastBoardSize) Refit();
        }

        /// <summary>Safe area of the current render target in pixels (full target when rendering offscreen).</summary>
        private Rect SafeRect()
        {
            if (cam.targetTexture != null || Screen.width != cam.pixelWidth || Screen.height != cam.pixelHeight)
                return new Rect(0, 0, cam.pixelWidth, cam.pixelHeight);
            return Screen.safeArea;
        }

        public void Refit()
        {
            lastScreen = new Vector2(cam.pixelWidth, cam.pixelHeight);
            lastSafe = SafeRect();
            lastBoardSize = board.Size;
            if (cam.pixelWidth <= 0 || cam.pixelHeight <= 0) return;

            Vector2 size = board.Size;
            BoardFit.Result fit = BoardFit.Compute(cam.pixelWidth, cam.pixelHeight, lastSafe.xMin, lastSafe.yMin, lastSafe.xMax, lastSafe.yMax, size.x, size.y);
            cam.orthographicSize = fit.OrthographicSize;
            Vector3 b = board.transform.position;
            cam.transform.position = new Vector3(b.x, b.y + fit.CameraOffsetY, -10f);

            // Cover the whole view with the background sprite.
            float fullHeight = fit.OrthographicSize * 2f;
            float viewW = fullHeight * cam.pixelWidth / cam.pixelHeight;
            Vector2 spriteSize = background.sprite.bounds.size;
            float scale = Mathf.Max(viewW / spriteSize.x, fullHeight / spriteSize.y) * 1.02f;
            background.transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y, 5f);
            background.transform.localScale = new Vector3(scale, scale, 1f);
        }

        public Vector3 ScreenToWorld(Vector2 screen)
        {
            Vector3 w = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
            w.z = 0f;
            return w;
        }

        /// <summary>Pixels per world unit, used to scale swipe thresholds to cell size.</summary>
        public float PixelsPerUnit => cam.pixelHeight / (cam.orthographicSize * 2f);
    }
}
