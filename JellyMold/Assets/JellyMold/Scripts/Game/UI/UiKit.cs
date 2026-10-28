using System;
using System.Collections;
using JellyMold.Procedural;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace JellyMold.Game.UI
{
    /// <summary>Small helpers to build uGUI hierarchies from code with a consistent look.</summary>
    public static class UiKit
    {
        private static Font font;

        public static Font Font
        {
            get
            {
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        public static Color C(Rgba c) => SpriteFactory.ToColor(c);

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>Anchors to a point (in parent-relative 0..1 coordinates) with a fixed size.</summary>
        public static RectTransform Place(this RectTransform rt, Vector2 anchor, Vector2 size, Vector2 offset = default)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = offset;
            return rt;
        }

        public static RectTransform Stretch(this RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        public static Image Image(Transform parent, string name, Sprite sprite, Color color, bool sliced = false)
        {
            RectTransform rt = Rect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.type = sliced ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            img.preserveAspect = !sliced;
            img.raycastTarget = false;
            return img;
        }

        public static Text Label(Transform parent, string name, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleCenter, bool bold = true)
        {
            RectTransform rt = Rect(parent, name);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>Rounded button with a label, squash feedback and a click sound.</summary>
        public static Button TextButton(Transform parent, string name, string text, Color fill, Color textColor, Vector2 size, Action onClick, AudioManager audio)
        {
            Image bg = Image(parent, name, SpriteFactory.UiButton(), fill, true);
            bg.rectTransform.sizeDelta = size;
            bg.raycastTarget = true;
            Text label = Label(bg.transform, "Label", text, Mathf.RoundToInt(size.y * 0.38f), textColor);
            label.rectTransform.Stretch();
            label.rectTransform.offsetMin = new Vector2(12f, 8f);
            label.rectTransform.offsetMax = new Vector2(-12f, 0f);
            return Wire(bg, onClick, audio);
        }

        /// <summary>Round button with an icon.</summary>
        public static Button IconButton(Transform parent, string name, IconKind icon, Color fill, Color iconColor, float diameter, Action onClick, AudioManager audio)
        {
            Image bg = Image(parent, name, SpriteFactory.UiCircleButton(), fill);
            bg.rectTransform.sizeDelta = new Vector2(diameter, diameter);
            bg.raycastTarget = true;
            Image ic = Image(bg.transform, "Icon", SpriteFactory.Icon(icon), iconColor);
            ic.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(diameter * 0.58f, diameter * 0.58f), new Vector2(0f, diameter * 0.03f));
            return Wire(bg, onClick, audio);
        }

        private static Button Wire(Image bg, Action onClick, AudioManager audio)
        {
            var button = bg.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            bg.gameObject.AddComponent<PressSquash>();
            button.onClick.AddListener(() =>
            {
                audio?.Play(SoundId.Click);
                onClick?.Invoke();
            });
            return button;
        }

        /// <summary>White rounded card with a soft shadow behind it.</summary>
        public static RectTransform Card(Transform parent, string name, Vector2 size)
        {
            RectTransform holder = Rect(parent, name).Place(new Vector2(0.5f, 0.5f), size);
            Image shadow = Image(holder, "Shadow", SpriteFactory.UiShadow(), new Color(0.24f, 0.14f, 0.4f, 0.28f), true);
            shadow.rectTransform.Stretch(-40f);
            shadow.rectTransform.anchoredPosition = new Vector2(0f, -14f);
            Image card = Image(holder, "Card", SpriteFactory.UiRoundRect(), C(ArtPalette.UiPanel), true);
            card.rectTransform.Stretch();
            card.raycastTarget = true;
            return holder;
        }
    }

    /// <summary>Squashes a button while pressed for a tactile, jelly-like feel.</summary>
    public sealed class PressSquash : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private Coroutine anim;

        public void OnPointerDown(PointerEventData eventData) => Animate(new Vector3(1.06f, 0.9f, 1f), 0.08f);
        public void OnPointerUp(PointerEventData eventData) => Release();
        public void OnPointerExit(PointerEventData eventData) => Release();

        private void Release()
        {
            if (anim != null) StopCoroutine(anim);
            anim = null;
            if (!isActiveAndEnabled)
            {
                transform.localScale = Vector3.one;
                return;
            }
            anim = StartCoroutine(Wobble());
        }

        private void Animate(Vector3 target, float duration)
        {
            if (!isActiveAndEnabled) return;
            if (anim != null) StopCoroutine(anim);
            anim = StartCoroutine(To(target, duration));
        }

        private IEnumerator To(Vector3 target, float duration)
        {
            Vector3 start = transform.localScale;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / duration)
            {
                transform.localScale = Vector3.Lerp(start, target, t);
                yield return null;
            }
            transform.localScale = target;
        }

        private IEnumerator Wobble()
        {
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.35f)
            {
                float w = Ease.Wobble(t) * 0.1f;
                transform.localScale = new Vector3(1f - w, 1f + w, 1f);
                yield return null;
            }
            transform.localScale = Vector3.one;
        }

        private void OnDisable()
        {
            transform.localScale = Vector3.one;
        }
    }

    /// <summary>Keeps a RectTransform inside Screen.safeArea (notches, rounded corners, home bar).</summary>
    public sealed class SafeArea : MonoBehaviour
    {
        private Rect applied;
        private Vector2Int screen;

        private void OnEnable() => Apply();

        private void Update()
        {
            if (Screen.safeArea != applied || screen.x != Screen.width || screen.y != Screen.height) Apply();
        }

        private void Apply()
        {
            applied = Screen.safeArea;
            screen = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0) return;
            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(applied.xMin / Screen.width, applied.yMin / Screen.height);
            rt.anchorMax = new Vector2(applied.xMax / Screen.width, applied.yMax / Screen.height);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
