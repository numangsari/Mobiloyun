using UnityEngine;

namespace JellyMold.Game
{
    /// <summary>
    /// Orthographic camera that keeps the board as large as possible between the HUD bars,
    /// inside the device safe area, for any aspect ratio. Also stretches the background.
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        /// <summary>Share of the safe-area height reserved for the top HUD and bottom buttons.</summary>
        public const float TopReserve = 0.14f;
        public const float BottomReserve = 0.22f;

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
            var screen = new Vector2(Screen.width, Screen.height);
            if (screen != lastScreen || Screen.safeArea != lastSafe || board.Size != lastBoardSize) Refit();
        }

        public void Refit()
        {
            lastScreen = new Vector2(Screen.width, Screen.height);
            lastSafe = Screen.safeArea;
            lastBoardSize = board.Size;
            if (Screen.width <= 0 || Screen.height <= 0) return;

            float aspect = (float)Screen.width / Screen.height;
            Rect safe = Screen.safeArea;
            float safeBottom = safe.yMin / Screen.height, safeTop = safe.yMax / Screen.height;
            float safeWidth = safe.width / Screen.width;

            // Fractions of the full screen height available for the board.
            float playBottom = safeBottom + (safeTop - safeBottom) * BottomReserve;
            float playTop = safeTop - (safeTop - safeBottom) * TopReserve;
            float playHeight = Mathf.Max(0.1f, playTop - playBottom);

            Vector2 size = board.Size;
            float sizeForHeight = size.y / (2f * playHeight);
            float sizeForWidth = (size.x + 0.3f) / (2f * aspect * safeWidth);
            cam.orthographicSize = Mathf.Max(sizeForHeight, sizeForWidth);

            // Shift the camera so the board sits in the middle of the play band.
            float fullHeight = cam.orthographicSize * 2f;
            float bandCenter = (playBottom + playTop) * 0.5f;
            Vector3 b = board.transform.position;
            cam.transform.position = new Vector3(b.x, b.y + (0.5f - bandCenter) * fullHeight, -10f);

            // Cover the whole view with the background sprite.
            Vector2 spriteSize = background.sprite.bounds.size;
            float viewW = fullHeight * aspect;
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
        public float PixelsPerUnit => Screen.height / (cam.orthographicSize * 2f);
    }
}
