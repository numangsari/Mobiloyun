using System.Collections;
using UnityEngine;

namespace JellyMold.Game
{
    /// <summary>Looping hand that demonstrates a swipe from one point to another.</summary>
    public sealed class TutorialHand : MonoBehaviour
    {
        private SpriteRenderer hand;
        private SpriteRenderer touch;
        private Coroutine loop;

        public static TutorialHand Create(Transform parent)
        {
            var go = new GameObject("TutorialHand");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<TutorialHand>();

            var touchGo = new GameObject("Touch");
            touchGo.transform.SetParent(go.transform, false);
            view.touch = touchGo.AddComponent<SpriteRenderer>();
            view.touch.sprite = SpriteFactory.Ring();
            view.touch.sortingOrder = SortOrder.Hand - 1;

            var handGo = new GameObject("Hand");
            handGo.transform.SetParent(go.transform, false);
            view.hand = handGo.AddComponent<SpriteRenderer>();
            view.hand.sprite = SpriteFactory.Hand();
            view.hand.sortingOrder = SortOrder.Hand;
            handGo.transform.localScale = Vector3.one * 1.1f;
            go.SetActive(false);
            return view;
        }

        public void Show(Vector3 from, Vector3 to)
        {
            gameObject.SetActive(true);
            if (loop != null) StopCoroutine(loop);
            loop = StartCoroutine(Loop(from, to));
        }

        public void Hide()
        {
            if (loop != null) StopCoroutine(loop);
            loop = null;
            gameObject.SetActive(false);
        }

        private IEnumerator Loop(Vector3 from, Vector3 to)
        {
            Vector3 offset = new Vector3(0.08f, -0.06f, 0f);
            while (true)
            {
                // Appear above the start cell.
                for (float t = 0f; t < 1f; t += Time.deltaTime / 0.25f)
                {
                    Place(from + offset + Vector3.up * 0.25f * (1f - t), Mathf.Clamp01(t), 1f, 0f);
                    yield return null;
                }
                // Press down.
                for (float t = 0f; t < 1f; t += Time.deltaTime / 0.15f)
                {
                    Place(from + offset, 1f, Mathf.Lerp(1f, 0.9f, t), t);
                    yield return null;
                }
                // Drag along the line.
                for (float t = 0f; t < 1f; t += Time.deltaTime / 0.55f)
                {
                    Place(Vector3.Lerp(from, to, Ease.InOutCubic(t)) + offset, 1f, 0.9f, 1f - t * 0.5f);
                    yield return null;
                }
                // Lift and fade.
                for (float t = 0f; t < 1f; t += Time.deltaTime / 0.25f)
                {
                    Place(to + offset + Vector3.up * 0.2f * t, 1f - t, Mathf.Lerp(0.9f, 1f, t), 0f);
                    yield return null;
                }
                Place(to, 0f, 1f, 0f);
                yield return new WaitForSeconds(0.45f);
            }
        }

        private void Place(Vector3 position, float alpha, float scale, float touchAlpha)
        {
            transform.position = position;
            hand.color = new Color(1f, 1f, 1f, alpha);
            hand.transform.localScale = Vector3.one * 1.1f * scale;
            touch.color = new Color(1f, 1f, 1f, touchAlpha * 0.8f);
            touch.transform.localScale = Vector3.one * Mathf.Lerp(0.8f, 0.45f, touchAlpha);
        }
    }
}
