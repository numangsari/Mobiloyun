using System;
using System.Collections;
using System.Collections.Generic;
using JellyMold.Core;
using JellyMold.Procedural;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace JellyMold.Game.UI
{
    /// <summary>
    /// Builds and drives every screen: title, level select, in-game HUD and the win, lose and
    /// settings overlays. Exposes plain C# events; the GameController decides what they do.
    /// </summary>
    public sealed class UIManager : MonoBehaviour
    {
        private static readonly Vector2 Reference = new Vector2(1080f, 1920f);

        private AudioManager audioManager;
        private RectTransform safe;

        private RectTransform title;
        private Text titleContinue;
        private RectTransform levelSelect;
        private RectTransform levelGrid;
        private readonly List<GameObject> levelButtons = new List<GameObject>();

        private RectTransform hud;
        private Text levelLabel;
        private Text movesLabel;
        private Text movesCaption;
        private RectTransform movesCard;
        private Text tutorialLabel;
        private Button undoButton;
        private Text comboLabel;

        private RectTransform winOverlay;
        private RectTransform winCard;
        private Image[] winStars;
        private Text winTitle;
        private Text winInfo;
        private Text winNextLabel;

        private RectTransform loseOverlay;
        private RectTransform loseCard;
        private Text loseInfo;
        private Button loseUndo;

        private RectTransform settingsOverlay;
        private RectTransform settingsCard;
        private Image soundIcon;
        private Text soundLabel;

        public event Action PlayPressed;
        public event Action LevelsPressed;
        public event Action<int> LevelChosen;
        public event Action MenuPressed;
        public event Action UndoPressed;
        public event Action RestartPressed;
        public event Action NextPressed;
        public event Action SoundToggled;
        public event Action LoseUndoPressed;

        /// <summary>True while a modal overlay is visible (gameplay input should pause).</summary>
        public bool OverlayOpen => winOverlay.gameObject.activeSelf || loseOverlay.gameObject.activeSelf || settingsOverlay.gameObject.activeSelf;

        public static UIManager Create(Transform parent, AudioManager audio)
        {
            if (EventSystem.current == null && FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                es.transform.SetParent(parent, false);
            }

            var go = new GameObject("UI", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = Reference;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();

            var ui = go.AddComponent<UIManager>();
            ui.audioManager = audio;
            ui.safe = UiKit.Rect(go.transform, "SafeArea");
            ui.safe.Stretch();
            ui.safe.gameObject.AddComponent<SafeArea>();
            ui.BuildTitle();
            ui.BuildLevelSelect();
            ui.BuildHud();
            ui.BuildWin();
            ui.BuildLose();
            ui.BuildSettings();
            return ui;
        }

        // ================================================================== screens

        public void ShowTitle(int continueLevel)
        {
            HideAll();
            titleContinue.text = continueLevel > 1 ? "Seviye " + continueLevel : "Başla";
            Show(title);
        }

        public void ShowLevelSelect(IReadOnlyList<LevelData> levels, ProgressData progress)
        {
            HideAll();
            foreach (GameObject b in levelButtons) Destroy(b);
            levelButtons.Clear();

            const int Columns = 4;
            const float Cell = 200f, Gap = 30f;
            int rows = (levels.Count + Columns - 1) / Columns;
            levelGrid.sizeDelta = new Vector2(Columns * Cell + (Columns - 1) * Gap, rows * Cell + (rows - 1) * Gap);
            for (int i = 0; i < levels.Count; i++)
            {
                LevelData level = levels[i];
                bool unlocked = progress.IsUnlocked(level.Id);
                bool done = progress.IsCompleted(level.Id);
                int col = i % Columns, row = i / Columns;
                Vector2 pos = new Vector2(-levelGrid.sizeDelta.x / 2 + Cell / 2 + col * (Cell + Gap), levelGrid.sizeDelta.y / 2 - Cell / 2 - row * (Cell + Gap));

                Color fill = unlocked ? Color.white : UiKit.C(ArtPalette.UiLocked);
                int id = level.Id;
                Button b = UiKit.TextButton(levelGrid, "Level" + id, unlocked ? id.ToString() : "", fill, UiKit.C(ArtPalette.UiText), new Vector2(Cell, Cell), () => LevelChosen?.Invoke(id), audioManager);
                b.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0.5f), new Vector2(Cell, Cell), pos);
                b.interactable = unlocked;
                Text label = b.GetComponentInChildren<Text>();
                label.fontSize = 76;
                label.rectTransform.offsetMin = new Vector2(0f, done ? 46f : 10f);

                if (!unlocked)
                {
                    Image lockIcon = UiKit.Image(b.transform, "Lock", SpriteFactory.Icon(IconKind.Lock), UiKit.C(ArtPalette.UiTextSoft));
                    lockIcon.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(90f, 90f), new Vector2(0f, 6f));
                }
                else if (done)
                {
                    int stars = GameConfig.StarsFor(progress.BestMoves[id], level.Par);
                    for (int s = 0; s < 3; s++)
                    {
                        Image star = UiKit.Image(b.transform, "Star", SpriteFactory.Icon(IconKind.Star), s < stars ? UiKit.C(ArtPalette.Star) : new Color(0.85f, 0.82f, 0.92f));
                        star.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(46f, 46f), new Vector2((s - 1) * 46f, 44f));
                    }
                }
                levelButtons.Add(b.gameObject);
            }
            Show(levelSelect);
        }

        public void ShowHud(LevelData level)
        {
            HideAll();
            levelLabel.text = "Seviye " + level.Id;
            SetTutorial(level.Tutorial);
            Show(hud);
        }

        public void SetMoves(int left, bool punch)
        {
            movesLabel.text = left.ToString();
            movesLabel.color = left <= 2 ? UiKit.C(ArtPalette.UiPrimary) : UiKit.C(ArtPalette.UiText);
            movesCaption.text = "hamle";
            if (punch) StartCoroutine(Punch(movesCard, 0.12f));
        }

        public void SetUndoInteractable(bool value) => undoButton.interactable = value;

        public void SetTutorial(string text)
        {
            tutorialLabel.text = text ?? "";
            tutorialLabel.gameObject.SetActive(!string.IsNullOrEmpty(text));
            if (!string.IsNullOrEmpty(text)) StartCoroutine(FadeText(tutorialLabel));
        }

        public void ShowCombo(int count)
        {
            comboLabel.text = (count >= 3 ? "Muhteşem!" : "Süper!") + "  ×" + count;
            StopCoroutine(nameof(ComboRoutine));
            StartCoroutine(nameof(ComboRoutine));
        }

        public void ShowWin(int stars, int movesUsed, bool hasNext)
        {
            winTitle.text = stars == 3 ? "Kusursuz!" : stars == 2 ? "Harika!" : "Tamamlandı!";
            winInfo.text = movesUsed + " hamlede çözdün";
            winNextLabel.text = hasNext ? "Sonraki" : "Seviyeler";
            for (int i = 0; i < winStars.Length; i++) winStars[i].color = new Color(0.88f, 0.85f, 0.94f);
            OpenOverlay(winOverlay, winCard);
            StartCoroutine(StarRoutine(stars));
        }

        public void ShowLose(int filled, int total, bool canUndo)
        {
            loseInfo.text = filled > 0 ? "Az kaldı! " + filled + "/" + total + " kalıp doldu." : "Bir de farklı bir sırayla dene.";
            loseUndo.interactable = canUndo;
            OpenOverlay(loseOverlay, loseCard);
        }

        public void ShowSettings(bool soundOn)
        {
            SetSoundState(soundOn);
            OpenOverlay(settingsOverlay, settingsCard);
        }

        public void SetSoundState(bool soundOn)
        {
            soundIcon.sprite = SpriteFactory.Icon(soundOn ? IconKind.SoundOn : IconKind.SoundOff);
            soundLabel.text = soundOn ? "Ses: Açık" : "Ses: Kapalı";
        }

        public void HideOverlays()
        {
            winOverlay.gameObject.SetActive(false);
            loseOverlay.gameObject.SetActive(false);
            settingsOverlay.gameObject.SetActive(false);
        }

        private void HideAll()
        {
            HideOverlays();
            title.gameObject.SetActive(false);
            levelSelect.gameObject.SetActive(false);
            hud.gameObject.SetActive(false);
        }

        // ================================================================== builders

        private void BuildTitle()
        {
            title = UiKit.Rect(safe, "Title");
            title.Stretch();
            title.gameObject.AddComponent<CanvasGroup>();

            // Three jellies bouncing above the logo.
            JellyColor[] colors = { JellyColor.Coral, JellyColor.Turquoise, JellyColor.Yellow };
            for (int i = 0; i < 3; i++)
            {
                Image j = UiKit.Image(title, "Jelly" + i, SpriteFactory.Jelly(colors[i]), Color.white);
                j.rectTransform.Place(new Vector2(0.5f, 0.68f), new Vector2(220f, 220f), new Vector2((i - 1) * 240f, 0f));
                j.gameObject.AddComponent<IdleBounce>().Phase = i * 0.7f;
            }

            Text logo = UiKit.Label(title, "Logo", "Jelly Mold", 150, UiKit.C(ArtPalette.UiText));
            logo.rectTransform.Place(new Vector2(0.5f, 0.52f), new Vector2(1000f, 200f));
            logo.gameObject.AddComponent<Shadow>().effectColor = new Color(1f, 1f, 1f, 0.8f);
            Text sub = UiKit.Label(title, "Sub", "Jöleleri kalıplarına bastır!", 48, UiKit.C(ArtPalette.UiTextSoft), TextAnchor.MiddleCenter, false);
            sub.rectTransform.Place(new Vector2(0.5f, 0.45f), new Vector2(900f, 80f));

            Button play = UiKit.TextButton(title, "Play", "Oyna", UiKit.C(ArtPalette.UiPrimary), Color.white, new Vector2(560f, 170f), () => PlayPressed?.Invoke(), audioManager);
            play.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0.3f), new Vector2(560f, 170f));
            titleContinue = UiKit.Label(title, "Continue", "", 40, UiKit.C(ArtPalette.UiTextSoft), TextAnchor.MiddleCenter, false);
            titleContinue.rectTransform.Place(new Vector2(0.5f, 0.3f), new Vector2(560f, 60f), new Vector2(0f, -125f));

            Button levels = UiKit.IconButton(title, "Levels", IconKind.Grid, Color.white, UiKit.C(ArtPalette.UiText), 140f, () => LevelsPressed?.Invoke(), audioManager);
            levels.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0.15f), new Vector2(140f, 140f), new Vector2(-100f, 0f));
            Button settings = UiKit.IconButton(title, "Settings", IconKind.Gear, Color.white, UiKit.C(ArtPalette.UiText), 140f, OpenSettings, audioManager);
            settings.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0.15f), new Vector2(140f, 140f), new Vector2(100f, 0f));
        }

        private void BuildLevelSelect()
        {
            levelSelect = UiKit.Rect(safe, "LevelSelect");
            levelSelect.Stretch();
            levelSelect.gameObject.AddComponent<CanvasGroup>();
            Text header = UiKit.Label(levelSelect, "Header", "Seviyeler", 84, UiKit.C(ArtPalette.UiText));
            header.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(700f, 140f), new Vector2(0f, -120f));
            Button back = UiKit.IconButton(levelSelect, "Back", IconKind.Home, Color.white, UiKit.C(ArtPalette.UiText), 130f, () => MenuPressed?.Invoke(), audioManager);
            back.GetComponent<RectTransform>().Place(new Vector2(0f, 1f), new Vector2(130f, 130f), new Vector2(110f, -120f));
            levelGrid = UiKit.Rect(levelSelect, "Grid").Place(new Vector2(0.5f, 0.47f), new Vector2(900f, 1200f));
        }

        private void BuildHud()
        {
            hud = UiKit.Rect(safe, "Hud");
            hud.Stretch();
            hud.gameObject.AddComponent<CanvasGroup>();

            // Top bar: level pill, moves card, settings.
            Image pill = UiKit.Image(hud, "LevelPill", SpriteFactory.UiRoundRect(), new Color(1f, 1f, 1f, 0.85f), true);
            pill.rectTransform.Place(new Vector2(0f, 1f), new Vector2(300f, 110f), new Vector2(200f, -110f));
            levelLabel = UiKit.Label(pill.transform, "Text", "Seviye 1", 46, UiKit.C(ArtPalette.UiText));
            levelLabel.rectTransform.Stretch();

            movesCard = UiKit.Card(hud, "Moves", new Vector2(230f, 190f));
            movesCard.anchorMin = movesCard.anchorMax = new Vector2(0.5f, 1f);
            movesCard.anchoredPosition = new Vector2(0f, -135f);
            movesLabel = UiKit.Label(movesCard, "Count", "0", 96, UiKit.C(ArtPalette.UiText));
            movesLabel.rectTransform.Stretch();
            movesLabel.rectTransform.offsetMin = new Vector2(0f, 40f);
            movesCaption = UiKit.Label(movesCard, "Caption", "hamle", 36, UiKit.C(ArtPalette.UiTextSoft), TextAnchor.MiddleCenter, false);
            movesCaption.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(220f, 50f), new Vector2(0f, 38f));

            Button settings = UiKit.IconButton(hud, "Settings", IconKind.Gear, Color.white, UiKit.C(ArtPalette.UiText), 120f, OpenSettings, audioManager);
            settings.GetComponent<RectTransform>().Place(new Vector2(1f, 1f), new Vector2(120f, 120f), new Vector2(-110f, -110f));

            // Bottom: tutorial line above undo and restart.
            tutorialLabel = UiKit.Label(hud, "Tutorial", "", 46, UiKit.C(ArtPalette.UiText), TextAnchor.MiddleCenter, false);
            tutorialLabel.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(940f, 130f), new Vector2(0f, 330f));

            undoButton = UiKit.IconButton(hud, "Undo", IconKind.Undo, Color.white, UiKit.C(ArtPalette.UiText), 170f, () => UndoPressed?.Invoke(), audioManager);
            undoButton.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(170f, 170f), new Vector2(-130f, 160f));
            Button restart = UiKit.IconButton(hud, "Restart", IconKind.Restart, Color.white, UiKit.C(ArtPalette.UiText), 170f, () => RestartPressed?.Invoke(), audioManager);
            restart.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(170f, 170f), new Vector2(130f, 160f));

            comboLabel = UiKit.Label(hud, "Combo", "", 90, Color.white);
            comboLabel.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(1000f, 160f), new Vector2(0f, 120f));
            var outline = comboLabel.gameObject.AddComponent<Outline>();
            outline.effectColor = UiKit.C(ArtPalette.UiPrimary);
            outline.effectDistance = new Vector2(5f, -5f);
            comboLabel.gameObject.SetActive(false);
        }

        private RectTransform Overlay(string name, Vector2 cardSize, out RectTransform card)
        {
            RectTransform overlay = UiKit.Rect(transform, name);
            overlay.Stretch();
            overlay.gameObject.AddComponent<CanvasGroup>();
            Image dim = UiKit.Image(overlay, "Dim", null, UiKit.C(ArtPalette.UiDim));
            dim.rectTransform.Stretch(-10f);
            dim.raycastTarget = true;
            RectTransform inner = UiKit.Rect(overlay, "Safe");
            inner.Stretch();
            inner.gameObject.AddComponent<SafeArea>();
            card = UiKit.Card(inner, "Card", cardSize);
            overlay.gameObject.SetActive(false);
            return overlay;
        }

        private void BuildWin()
        {
            winOverlay = Overlay("Win", new Vector2(860f, 900f), out winCard);
            winTitle = UiKit.Label(winCard, "Title", "Harika!", 92, UiKit.C(ArtPalette.UiText));
            winTitle.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(800f, 140f), new Vector2(0f, -120f));
            winStars = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                winStars[i] = UiKit.Image(winCard, "Star" + i, SpriteFactory.Icon(IconKind.Star), Color.white);
                float size = i == 1 ? 200f : 160f;
                winStars[i].rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(size, size), new Vector2((i - 1) * 190f, i == 1 ? -330f : -310f));
            }
            winInfo = UiKit.Label(winCard, "Info", "", 46, UiKit.C(ArtPalette.UiTextSoft), TextAnchor.MiddleCenter, false);
            winInfo.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(800f, 70f), new Vector2(0f, -490f));

            Button next = UiKit.TextButton(winCard, "Next", "Sonraki", UiKit.C(ArtPalette.UiSecondary), Color.white, new Vector2(520f, 160f), () => NextPressed?.Invoke(), audioManager);
            next.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(520f, 160f), new Vector2(60f, 150f));
            winNextLabel = next.GetComponentInChildren<Text>();
            Button replay = UiKit.IconButton(winCard, "Replay", IconKind.Restart, Color.white, UiKit.C(ArtPalette.UiText), 140f, () => RestartPressed?.Invoke(), audioManager);
            replay.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(140f, 140f), new Vector2(-280f, 160f));
            replay.GetComponent<Image>().color = new Color(0.95f, 0.93f, 1f);
        }

        private void BuildLose()
        {
            loseOverlay = Overlay("Lose", new Vector2(860f, 760f), out loseCard);
            Text t = UiKit.Label(loseCard, "Title", "Hamleler bitti", 84, UiKit.C(ArtPalette.UiText));
            t.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(800f, 140f), new Vector2(0f, -120f));
            loseInfo = UiKit.Label(loseCard, "Info", "", 46, UiKit.C(ArtPalette.UiTextSoft), TextAnchor.MiddleCenter, false);
            loseInfo.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(760f, 120f), new Vector2(0f, -250f));

            Button retry = UiKit.TextButton(loseCard, "Retry", "Tekrar Dene", UiKit.C(ArtPalette.UiPrimary), Color.white, new Vector2(600f, 160f), () => RestartPressed?.Invoke(), audioManager);
            retry.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(600f, 160f), new Vector2(0f, 300f));
            loseUndo = UiKit.TextButton(loseCard, "Undo", "Son hamleyi geri al", new Color(0.95f, 0.93f, 1f), UiKit.C(ArtPalette.UiText), new Vector2(600f, 120f), () => LoseUndoPressed?.Invoke(), audioManager);
            loseUndo.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0f), new Vector2(600f, 120f), new Vector2(0f, 130f));
        }

        private void BuildSettings()
        {
            settingsOverlay = Overlay("Settings", new Vector2(860f, 1000f), out settingsCard);
            Text t = UiKit.Label(settingsCard, "Title", "Ayarlar", 84, UiKit.C(ArtPalette.UiText));
            t.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(700f, 140f), new Vector2(0f, -110f));
            Button close = UiKit.IconButton(settingsCard, "Close", IconKind.Close, new Color(0.95f, 0.93f, 1f), UiKit.C(ArtPalette.UiText), 110f, HideOverlays, audioManager);
            close.GetComponent<RectTransform>().Place(new Vector2(1f, 1f), new Vector2(110f, 110f), new Vector2(-90f, -90f));

            Button sound = UiKit.TextButton(settingsCard, "Sound", "", new Color(0.95f, 0.93f, 1f), UiKit.C(ArtPalette.UiText), new Vector2(640f, 140f), () => SoundToggled?.Invoke(), audioManager);
            sound.GetComponent<RectTransform>().Place(new Vector2(0.5f, 1f), new Vector2(640f, 140f), new Vector2(0f, -280f));
            soundLabel = sound.GetComponentInChildren<Text>();
            soundLabel.rectTransform.offsetMin = new Vector2(110f, 8f);
            soundIcon = UiKit.Image(sound.transform, "Icon", SpriteFactory.Icon(IconKind.SoundOn), UiKit.C(ArtPalette.UiText));
            soundIcon.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(80f, 80f), new Vector2(90f, 4f));

            Button levels = UiKit.TextButton(settingsCard, "Levels", "Seviyeler", new Color(0.95f, 0.93f, 1f), UiKit.C(ArtPalette.UiText), new Vector2(640f, 140f), () => LevelsPressed?.Invoke(), audioManager);
            levels.GetComponent<RectTransform>().Place(new Vector2(0.5f, 1f), new Vector2(640f, 140f), new Vector2(0f, -450f));
            Button menu = UiKit.TextButton(settingsCard, "Menu", "Ana Menü", new Color(0.95f, 0.93f, 1f), UiKit.C(ArtPalette.UiText), new Vector2(640f, 140f), () => MenuPressed?.Invoke(), audioManager);
            menu.GetComponent<RectTransform>().Place(new Vector2(0.5f, 1f), new Vector2(640f, 140f), new Vector2(0f, -620f));

            Text privacy = UiKit.Label(settingsCard, "Privacy", "Reklam yok, hesap yok, veri toplama yok.\nİlerlemen yalnızca bu cihazda saklanır.", 34, UiKit.C(ArtPalette.UiTextSoft), TextAnchor.MiddleCenter, false);
            privacy.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(760f, 120f), new Vector2(0f, 110f));
        }

        private void OpenSettings() => SettingsRequested?.Invoke();

        /// <summary>Raised by the gear buttons; the controller answers with <see cref="ShowSettings"/>.</summary>
        public event Action SettingsRequested;

        // ================================================================== animation

        private void Show(RectTransform screen)
        {
            screen.gameObject.SetActive(true);
            StartCoroutine(FadeIn(screen.GetComponent<CanvasGroup>(), screen, 0.25f));
        }

        private void OpenOverlay(RectTransform overlay, RectTransform card)
        {
            overlay.SetAsLastSibling();
            overlay.gameObject.SetActive(true);
            StartCoroutine(FadeIn(overlay.GetComponent<CanvasGroup>(), null, 0.2f));
            StartCoroutine(PopIn(card));
        }

        private IEnumerator FadeIn(CanvasGroup group, RectTransform slide, float duration)
        {
            Vector2 basePos = Vector2.zero;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / duration)
            {
                group.alpha = Ease.OutCubic(t);
                if (slide != null) slide.anchoredPosition = basePos + new Vector2(0f, -30f * (1f - Ease.OutCubic(t)));
                yield return null;
            }
            group.alpha = 1f;
            if (slide != null) slide.anchoredPosition = basePos;
        }

        private IEnumerator PopIn(RectTransform card)
        {
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.35f)
            {
                float s = Mathf.LerpUnclamped(0.8f, 1f, Ease.OutBack(t, 2f));
                card.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            card.localScale = Vector3.one;
        }

        private IEnumerator Punch(RectTransform rt, float amount)
        {
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.3f)
            {
                float w = Ease.Wobble(t) * amount;
                rt.localScale = new Vector3(1f + w, 1f - w, 1f);
                yield return null;
            }
            rt.localScale = Vector3.one;
        }

        private IEnumerator FadeText(Text text)
        {
            Color c = text.color;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.4f)
            {
                text.color = new Color(c.r, c.g, c.b, t);
                yield return null;
            }
            text.color = new Color(c.r, c.g, c.b, 1f);
        }

        private IEnumerator ComboRoutine()
        {
            comboLabel.gameObject.SetActive(true);
            RectTransform rt = comboLabel.rectTransform;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.9f)
            {
                float s = t < 0.3f ? Mathf.LerpUnclamped(0.3f, 1f, Ease.OutBack(t / 0.3f, 2.5f)) : 1f;
                rt.localScale = new Vector3(s, s, 1f);
                rt.anchoredPosition = new Vector2(0f, 120f + 60f * t);
                comboLabel.color = new Color(1f, 1f, 1f, t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f);
                yield return null;
            }
            comboLabel.gameObject.SetActive(false);
        }

        private IEnumerator StarRoutine(int stars)
        {
            yield return new WaitForSecondsRealtime(0.25f);
            for (int i = 0; i < stars && i < winStars.Length; i++)
            {
                winStars[i].color = UiKit.C(ArtPalette.Star);
                audioManager?.Play(SoundId.Fill0 + Mathf.Min(4, i * 2), 1f, 0.7f);
                RectTransform rt = winStars[i].rectTransform;
                for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.3f)
                {
                    float s = Mathf.LerpUnclamped(1.6f, 1f, Ease.OutBack(t, 2.5f));
                    rt.localScale = new Vector3(s, s, 1f);
                    yield return null;
                }
                rt.localScale = Vector3.one;
            }
        }
    }

    /// <summary>Decorative bounce for the title jellies.</summary>
    public sealed class IdleBounce : MonoBehaviour
    {
        public float Phase;
        private Vector2 basePos;
        private RectTransform rt;

        private void Start()
        {
            rt = (RectTransform)transform;
            basePos = rt.anchoredPosition;
        }

        private void Update()
        {
            float t = Time.time * 2.4f + Phase;
            float jump = Mathf.Abs(Mathf.Sin(t));
            float squash = 1f - Mathf.Pow(1f - jump, 6f);
            rt.anchoredPosition = basePos + new Vector2(0f, jump * 40f);
            rt.localScale = new Vector3(1.12f - 0.12f * squash, 0.88f + 0.12f * squash, 1f);
        }
    }
}
