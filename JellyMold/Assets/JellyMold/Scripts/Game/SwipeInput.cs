using System;
using JellyMold.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace JellyMold.Game
{
    /// <summary>
    /// One-finger swipe detection for touch and mouse (Editor). A drag that starts on a cell
    /// and travels past a threshold becomes exactly one press; short taps become pokes.
    /// </summary>
    public sealed class SwipeInput : MonoBehaviour
    {
        private CameraRig rig;
        private BoardView board;
        private bool tracking;
        private bool fired;
        private Vector2 startScreen;
        private int startX, startY;

        /// <summary>Pointer went down on a cell (x, y).</summary>
        public event Action<int, int> Down;

        /// <summary>Drag direction resolved before release (x, y, direction) — used for the line preview.</summary>
        public event Action<int, int, Direction> Swipe;

        /// <summary>Released without enough movement.</summary>
        public event Action<int, int> Tap;

        /// <summary>Pointer released or cancelled after a swipe or a down.</summary>
        public event Action Up;

        public bool Enabled { get; set; } = true;

        public static SwipeInput Create(GameObject host, CameraRig rig, BoardView board)
        {
            var input = host.AddComponent<SwipeInput>();
            input.rig = rig;
            input.board = board;
            return input;
        }

        private void Update()
        {
            if (!Enabled || board.Layout == null)
            {
                if (tracking) EndGesture();
                return;
            }

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.touchCount > 0)
            {
                Touch t = Input.GetTouch(0);
                switch (t.phase)
                {
                    case TouchPhase.Began: Begin(t.position, t.fingerId); break;
                    case TouchPhase.Moved:
                    case TouchPhase.Stationary: Drag(t.position); break;
                    default: Release(t.position); break;
                }
                return;
            }

            if (Input.GetMouseButtonDown(0)) Begin(Input.mousePosition, -1);
            else if (Input.GetMouseButton(0)) Drag(Input.mousePosition);
            else if (Input.GetMouseButtonUp(0)) Release(Input.mousePosition);
#endif
        }

        private void Begin(Vector2 screen, int pointerId)
        {
            EventSystem es = EventSystem.current;
            if (es != null && es.IsPointerOverGameObject(pointerId)) return;
            if (!board.WorldToCell(rig.ScreenToWorld(screen), out startX, out startY)) return;
            tracking = true;
            fired = false;
            startScreen = screen;
            Down?.Invoke(startX, startY);
        }

        private void Drag(Vector2 screen)
        {
            if (!tracking || fired) return;
            Vector2 delta = screen - startScreen;
            float threshold = Mathf.Max(18f, rig.PixelsPerUnit * 0.28f);
            if (delta.magnitude < threshold) return;

            Direction dir = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)
                ? (delta.x > 0 ? Direction.Right : Direction.Left)
                : (delta.y > 0 ? Direction.Up : Direction.Down);
            fired = true;
            Swipe?.Invoke(startX, startY, dir);
        }

        private void Release(Vector2 screen)
        {
            if (!tracking) return;
            if (!fired)
            {
                Drag(screen);
                if (!fired) Tap?.Invoke(startX, startY);
            }
            EndGesture();
        }

        private void EndGesture()
        {
            tracking = false;
            Up?.Invoke();
        }
    }
}
