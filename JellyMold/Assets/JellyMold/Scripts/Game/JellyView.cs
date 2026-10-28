using System;
using System.Collections;
using JellyMold.Core;
using UnityEngine;

namespace JellyMold.Game
{
    /// <summary>Sprite sorting orders for the board scene (higher draws on top).</summary>
    public static class SortOrder
    {
        public const int Background = -100;
        public const int Panel = -60;
        public const int Tile = -50;
        public const int Highlight = -40;
        public const int MoldFilled = -30;
        public const int Shadow = -20;
        public const int Jelly = 0;
        public const int Effects = 50;
        public const int Hand = 80;
    }

    /// <summary>
    /// One jelly on the board. All motion is cosmetic: the logical move has already been
    /// resolved by <see cref="MoveResolver"/>; this only plays squash, stretch and wobble.
    /// </summary>
    public sealed class JellyView : MonoBehaviour
    {
        private Transform body;
        private SpriteRenderer bodyRenderer;
        private SpriteRenderer shadowRenderer;
        private float idlePhase;
        private int animating;

        public JellyColor ColorId { get; private set; }

        public static JellyView Create(Transform parent)
        {
            var go = new GameObject("Jelly");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<JellyView>();

            var shadow = new GameObject("Shadow");
            shadow.transform.SetParent(go.transform, false);
            shadow.transform.localPosition = new Vector3(0f, -0.36f, 0f);
            shadow.transform.localScale = new Vector3(0.8f, 0.24f, 1f);
            view.shadowRenderer = shadow.AddComponent<SpriteRenderer>();
            view.shadowRenderer.sprite = SpriteFactory.SoftCircle();
            view.shadowRenderer.color = new Color(0.29f, 0.2f, 0.45f, 0.22f);
            view.shadowRenderer.sortingOrder = SortOrder.Shadow;

            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(go.transform, false);
            view.body = bodyGo.transform;
            view.bodyRenderer = bodyGo.AddComponent<SpriteRenderer>();
            view.bodyRenderer.sortingOrder = SortOrder.Jelly;
            return view;
        }

        public void Setup(JellyColor color, Vector3 position)
        {
            ColorId = color;
            bodyRenderer.sprite = SpriteFactory.Jelly(color);
            bodyRenderer.color = Color.white;
            shadowRenderer.enabled = true;
            transform.localPosition = position;
            body.localScale = Vector3.one;
            body.localPosition = Vector3.zero;
            idlePhase = UnityEngine.Random.value * 10f;
            animating = 0;
            gameObject.SetActive(true);
        }

        private void Update()
        {
            if (animating > 0) return;
            // Gentle breathing keeps the board alive while idle.
            float s = Mathf.Sin(Time.time * 2.2f + idlePhase) * 0.018f;
            SetDeform(1f + s, 1f - s * 0.8f, Vector2.right);
        }

        /// <summary>Front jellies sort above the ones behind them in the press direction.</summary>
        public void SetSortBoost(int boost)
        {
            bodyRenderer.sortingOrder = SortOrder.Jelly + boost;
        }

        /// <summary>Squash, travel with stretch, then land with a damped wobble.</summary>
        public IEnumerator Move(Vector3 to, Direction dir, float delay, bool intoMold, Action onArrive)
        {
            animating++;
            if (delay > 0f) yield return new WaitForSeconds(delay);
            Vector2 axis = DirVector(dir);
            Vector3 from = transform.localPosition;
            float t = 0f;
            while (t < 1f)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / Timing.Move);
                if (t < 0.2f)
                {
                    // Anticipation: compress against the press.
                    float c = Ease.Bell(t / 0.2f);
                    transform.localPosition = from - (Vector3)(axis * 0.05f * c);
                    SetDeform(1f - 0.18f * c, 1f + 0.12f * c, axis);
                }
                else
                {
                    float u = (t - 0.2f) / 0.8f;
                    transform.localPosition = Vector3.LerpUnclamped(from, to, Ease.InOutCubic(u));
                    float s = Ease.Bell(u);
                    SetDeform(1f + 0.24f * s, 1f - 0.15f * s, axis);
                }
                yield return null;
            }
            transform.localPosition = to;
            onArrive?.Invoke();

            if (intoMold)
            {
                // Pour into the mold: shrink and fade while the mold pops the filled shape.
                float k = 0f;
                while (k < 1f)
                {
                    k = Mathf.Min(1f, k + Time.deltaTime / 0.14f);
                    float sc = Mathf.Lerp(1f, 0.55f, k);
                    SetDeform(sc * (1f + 0.15f * Ease.Bell(k)), sc, axis);
                    bodyRenderer.color = new Color(1f, 1f, 1f, 1f - k);
                    shadowRenderer.enabled = false;
                    yield return null;
                }
                animating--;
                gameObject.SetActive(false);
                yield break;
            }

            yield return Settle(axis, 1f);
            animating--;
        }

        /// <summary>Blocked jelly: lean into the obstacle and spring back.</summary>
        public IEnumerator Bump(Direction dir, float delay)
        {
            animating++;
            if (delay > 0f) yield return new WaitForSeconds(delay);
            Vector2 axis = DirVector(dir);
            Vector3 home = transform.localPosition;
            float t = 0f;
            while (t < 1f)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / Timing.Bump);
                float b = Ease.Bell(t);
                transform.localPosition = home + (Vector3)(axis * 0.09f * b);
                SetDeform(1f - 0.2f * b, 1f + 0.14f * b, axis);
                yield return null;
            }
            transform.localPosition = home;
            yield return Settle(axis, 0.6f);
            animating--;
        }

        /// <summary>Poke reaction for taps.</summary>
        public IEnumerator Poke()
        {
            animating++;
            yield return Settle(Vector2.up, 0.8f);
            animating--;
        }

        /// <summary>Drop-in when a level starts or the board is restored.</summary>
        public IEnumerator Appear(float delay)
        {
            animating++;
            body.localScale = Vector3.zero;
            if (delay > 0f) yield return new WaitForSeconds(delay);
            float t = 0f;
            while (t < 1f)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / 0.3f);
                float s = Ease.OutBack(t, 2.2f);
                SetDeform(s, s, Vector2.up);
                yield return null;
            }
            yield return Settle(Vector2.up, 0.5f);
            animating--;
        }

        private IEnumerator Settle(Vector2 axis, float strength)
        {
            float t = 0f;
            while (t < 1f)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / Timing.Settle);
                float w = Ease.Wobble(t) * strength;
                SetDeform(1f - 0.16f * w, 1f + 0.13f * w, axis);
                yield return null;
            }
            SetDeform(1f, 1f, axis);
        }

        /// <summary>Scales the body along the motion axis and across it, keeping it centred.</summary>
        private void SetDeform(float along, float across, Vector2 axis)
        {
            bool horizontal = Mathf.Abs(axis.x) > 0.5f;
            body.localScale = horizontal ? new Vector3(along, across, 1f) : new Vector3(across, along, 1f);
            // Keep the jelly sitting on its "floor" so vertical squash reads as weight.
            float sy = body.localScale.y;
            body.localPosition = new Vector3(0f, (sy - 1f) * 0.12f, 0f);
            shadowRenderer.transform.localScale = new Vector3(0.8f * body.localScale.x, 0.24f, 1f);
        }

        public static Vector2 DirVector(Direction d) => new Vector2(d.Dx(), -d.Dy());
    }
}
