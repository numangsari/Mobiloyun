using System.Collections;
using JellyMold.Core;
using JellyMold.Procedural;
using UnityEngine;

namespace JellyMold.Game
{
    /// <summary>A mold cell: dashed socket while empty, a happy jelly in the mold's shape once filled.</summary>
    public sealed class MoldView : MonoBehaviour
    {
        private SpriteRenderer socket;
        private SpriteRenderer filled;
        private SpriteRenderer glow;
        private Coroutine running;

        public JellyColor ColorId { get; private set; }
        public bool IsFilled { get; private set; }

        public static MoldView Create(Transform parent, JellyColor color, Vector3 position)
        {
            var go = new GameObject("Mold_" + color);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var view = go.AddComponent<MoldView>();
            view.ColorId = color;

            view.socket = go.AddComponent<SpriteRenderer>();
            view.socket.sprite = SpriteFactory.MoldSocket(color);
            view.socket.sortingOrder = SortOrder.Tile;

            var glowGo = new GameObject("Glow");
            glowGo.transform.SetParent(go.transform, false);
            view.glow = glowGo.AddComponent<SpriteRenderer>();
            view.glow.sprite = SpriteFactory.Ring();
            view.glow.sortingOrder = SortOrder.Effects - 1;
            view.glow.enabled = false;

            var filledGo = new GameObject("Filled");
            filledGo.transform.SetParent(go.transform, false);
            view.filled = filledGo.AddComponent<SpriteRenderer>();
            view.filled.sprite = SpriteFactory.MoldFilled(color);
            view.filled.sortingOrder = SortOrder.MoldFilled;
            view.SetFilled(false);
            return view;
        }

        /// <summary>Instant state change (used by undo, restart and level start).</summary>
        public void SetFilled(bool value)
        {
            if (running != null) StopCoroutine(running);
            running = null;
            IsFilled = value;
            filled.enabled = value;
            filled.transform.localScale = Vector3.one;
            glow.enabled = false;
        }

        public void PlayFill()
        {
            IsFilled = true;
            if (running != null) StopCoroutine(running);
            running = StartCoroutine(FillRoutine());
        }

        /// <summary>Happy bounce used in the win celebration.</summary>
        public void PlayCheer(float delay)
        {
            if (!IsFilled) return;
            if (running != null) StopCoroutine(running);
            running = StartCoroutine(CheerRoutine(delay));
        }

        private IEnumerator FillRoutine()
        {
            filled.enabled = true;
            glow.enabled = true;
            Color tint = SpriteFactory.ToColor(ArtPalette.Light(ColorId));
            float t = 0f;
            while (t < 1f)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / Timing.EnterMold);
                float s = Ease.OutBack(Mathf.Clamp01(t * 1.6f), 2.6f);
                float wobble = t > 0.6f ? Ease.Wobble((t - 0.6f) / 0.4f) * 0.08f : 0f;
                filled.transform.localScale = new Vector3(s * (1f + wobble), s * (1f - wobble), 1f);

                float g = Ease.OutCubic(t);
                glow.transform.localScale = Vector3.one * Mathf.Lerp(0.6f, 1.55f, g);
                glow.color = new Color(tint.r, tint.g, tint.b, (1f - g) * 0.9f);
                yield return null;
            }
            filled.transform.localScale = Vector3.one;
            glow.enabled = false;
            running = null;
        }

        private IEnumerator CheerRoutine(float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            float t = 0f;
            while (t < 1f)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / 0.45f);
                float jump = Ease.Bell(Mathf.Clamp01(t * 1.6f));
                float wobble = Ease.Wobble(t) * 0.1f;
                filled.transform.localPosition = new Vector3(0f, jump * 0.22f, 0f);
                filled.transform.localScale = new Vector3(1f + wobble, 1f - wobble + jump * 0.08f, 1f);
                yield return null;
            }
            filled.transform.localPosition = Vector3.zero;
            filled.transform.localScale = Vector3.one;
            running = null;
        }
    }
}
