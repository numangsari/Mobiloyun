using System;
using System.Collections;
using System.Collections.Generic;
using JellyMold.Core;
using JellyMold.Procedural;
using UnityEngine;

namespace JellyMold.Game
{
    /// <summary>
    /// Presents a <see cref="BoardState"/>: tiles, molds, jellies, line highlight and move playback.
    /// Never decides game rules; it only animates <see cref="MoveResult"/>s produced by the core.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        private readonly List<JellyView> jellyPool = new List<JellyView>();
        private readonly List<GameObject> staticObjects = new List<GameObject>();
        private JellyView[] jellies = new JellyView[0];
        private MoldView[] molds = new MoldView[0];
        private BoardLayout layout;
        private Transform content;
        private SpriteRenderer rowStrip;
        private SpriteRenderer columnStrip;
        private SpriteRenderer activeStrip;
        private Coroutine stripFade;

        public AudioManager Audio { get; set; }
        public FxPool Fx { get; set; }

        /// <summary>Raised when one press fills two or more molds (count, world position of the last fill).</summary>
        public event Action<int, Vector3> Combo;

        public BoardLayout Layout => layout;

        /// <summary>Board size in world units including the panel margin.</summary>
        public Vector2 Size => layout == null ? new Vector2(6f, 6f) : new Vector2(layout.Width + 0.9f, layout.Height + 0.9f);

        public static BoardView Create(Transform parent)
        {
            var go = new GameObject("Board");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<BoardView>();
            view.content = new GameObject("Content").transform;
            view.content.SetParent(go.transform, false);
            return view;
        }

        // ------------------------------------------------------------------ build / sync

        public void Build(BoardState state)
        {
            StopAllCoroutines();
            foreach (GameObject go in staticObjects) Destroy(go);
            staticObjects.Clear();
            foreach (JellyView j in jellyPool) j.gameObject.SetActive(false);

            layout = state.Layout;
            jellies = new JellyView[layout.CellCount];
            molds = new MoldView[layout.CellCount];

            AddSprite("Panel", SpriteFactory.BoardPanel(layout.Width, layout.Height), Vector3.zero, SortOrder.Panel);
            for (int i = 0; i < layout.CellCount; i++)
            {
                Vector3 p = CellToLocal(i);
                switch (layout.GetTile(i))
                {
                    case TileType.Wall:
                        AddSprite("Wall", SpriteFactory.Wall(), p, SortOrder.Tile);
                        break;
                    case TileType.Mold:
                        molds[i] = MoldView.Create(content, layout.GetMoldColor(i), p);
                        molds[i].SetFilled(state.IsCellMoldFilled(i));
                        staticObjects.Add(molds[i].gameObject);
                        break;
                    default:
                        AddSprite("Floor", SpriteFactory.Floor(), p, SortOrder.Tile);
                        break;
                }
            }

            rowStrip = AddSprite("RowStrip", SpriteFactory.Strip(layout.Width, 1), Vector3.zero, SortOrder.Highlight);
            columnStrip = AddSprite("ColumnStrip", SpriteFactory.Strip(1, layout.Height), Vector3.zero, SortOrder.Highlight);
            rowStrip.enabled = false;
            columnStrip.enabled = false;
            activeStrip = null;

            int order = 0;
            for (int i = 0; i < layout.CellCount; i++)
            {
                JellyColor c = state.GetJelly(i);
                if (c == JellyColor.None) continue;
                JellyView j = AcquireJelly(c, CellToLocal(i));
                jellies[i] = j;
                StartCoroutine(j.Appear(0.12f + order++ * 0.05f));
            }
            StartCoroutine(IntroRoutine());
        }

        /// <summary>
        /// Makes the view match <paramref name="state"/>. Unchanged jellies stay put, others pop in or out.
        /// Used for restart and as a safety net after undo.
        /// </summary>
        public void Sync(BoardState state, bool animate)
        {
            for (int i = 0; i < layout.CellCount; i++)
            {
                if (molds[i] != null && molds[i].IsFilled != state.IsCellMoldFilled(i)) molds[i].SetFilled(state.IsCellMoldFilled(i));

                JellyColor want = state.GetJelly(i);
                JellyView have = jellies[i];
                if (have != null && have.gameObject.activeSelf && have.ColorId == want)
                {
                    have.transform.localPosition = CellToLocal(i);
                    continue;
                }
                if (have != null) have.gameObject.SetActive(false);
                jellies[i] = null;
                if (want == JellyColor.None) continue;
                JellyView j = AcquireJelly(want, CellToLocal(i));
                jellies[i] = j;
                if (animate) StartCoroutine(j.Appear(UnityEngine.Random.Range(0f, 0.12f)));
            }
        }

        private SpriteRenderer AddSprite(string name, Sprite sprite, Vector3 localPos, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(content, false);
            go.transform.localPosition = localPos;
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sortingOrder = order;
            staticObjects.Add(go);
            return r;
        }

        private JellyView AcquireJelly(JellyColor color, Vector3 localPos)
        {
            JellyView j = null;
            foreach (JellyView candidate in jellyPool)
            {
                if (!candidate.gameObject.activeSelf)
                {
                    j = candidate;
                    break;
                }
            }
            if (j == null)
            {
                j = JellyView.Create(content);
                jellyPool.Add(j);
            }
            j.Setup(color, localPos);
            return j;
        }

        private IEnumerator IntroRoutine()
        {
            float t = 0f;
            while (t < 1f)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / Timing.BoardIntro);
                float s = Mathf.LerpUnclamped(0.92f, 1f, Ease.OutBack(t));
                content.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            content.localScale = Vector3.one;
        }

        // ------------------------------------------------------------------ coordinates

        public Vector3 CellToLocal(int cell)
        {
            return new Vector3(layout.X(cell) - (layout.Width - 1) * 0.5f, (layout.Height - 1) * 0.5f - layout.Y(cell), 0f);
        }

        public Vector3 CellToWorld(int cell) => content.TransformPoint(CellToLocal(cell));

        public bool WorldToCell(Vector3 world, out int x, out int y)
        {
            Vector3 local = content.InverseTransformPoint(world);
            float fx = local.x + (layout.Width - 1) * 0.5f;
            float fy = (layout.Height - 1) * 0.5f - local.y;
            x = Mathf.RoundToInt(fx);
            y = Mathf.RoundToInt(fy);
            return layout != null && layout.InBounds(x, y) && Mathf.Abs(fx - x) <= 0.5f && Mathf.Abs(fy - y) <= 0.5f;
        }

        public JellyView JellyAt(int cell) => cell >= 0 && cell < jellies.Length ? jellies[cell] : null;

        // ------------------------------------------------------------------ line highlight

        public void ShowLine(Direction dir, int line)
        {
            SpriteRenderer strip = dir.IsHorizontal() ? rowStrip : columnStrip;
            if (activeStrip != null && activeStrip != strip) activeStrip.enabled = false;
            activeStrip = strip;
            strip.transform.localPosition = dir.IsHorizontal()
                ? new Vector3(0f, (layout.Height - 1) * 0.5f - line, 0f)
                : new Vector3(line - (layout.Width - 1) * 0.5f, 0f, 0f);
            strip.enabled = true;
            if (stripFade != null) StopCoroutine(stripFade);
            stripFade = StartCoroutine(FadeStrip(strip, 1f, 0.1f));
        }

        public void HideLine()
        {
            if (activeStrip == null) return;
            if (stripFade != null) StopCoroutine(stripFade);
            stripFade = StartCoroutine(FadeStrip(activeStrip, 0f, 0.25f));
        }

        private IEnumerator FadeStrip(SpriteRenderer strip, float target, float duration)
        {
            float start = strip.color.a;
            float t = 0f;
            while (t < 1f)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / duration);
                strip.color = new Color(1f, 1f, 1f, Mathf.Lerp(start, target, t));
                yield return null;
            }
            if (target <= 0f) strip.enabled = false;
        }

        // ------------------------------------------------------------------ playback

        /// <summary>Animates a press. <paramref name="done"/> fires once every jelly has settled.</summary>
        public void PlayResult(MoveResult result, Action done)
        {
            StartCoroutine(PlayRoutine(result, done));
        }

        private IEnumerator PlayRoutine(MoveResult result, Action done)
        {
            Direction dir = result.Move.Direction;
            var next = (JellyView[])jellies.Clone();
            float longest = 0f;

            if (!result.IsValid)
            {
                Audio?.Play(SoundId.Bump, 1f, 0.7f);
                foreach (JellyStep s in result.Steps)
                {
                    JellyView j = jellies[s.From];
                    if (j == null) continue;
                    float delay = s.Order * Timing.StepStagger * 0.6f;
                    StartCoroutine(j.Bump(dir, delay));
                    longest = Mathf.Max(longest, delay + Timing.Bump);
                }
                yield return new WaitForSeconds(longest);
                done?.Invoke();
                yield break;
            }

            Audio?.Play(SoundId.Press, 1f, 0.6f);
            foreach (JellyStep s in result.Steps)
            {
                if (s.Kind != StepKind.Blocked) next[s.From] = null;
            }

            int chain = 0;
            Vector3 lastFill = Vector3.zero;
            foreach (JellyStep s in result.Steps)
            {
                JellyView j = jellies[s.From];
                if (j == null) continue;
                float delay = s.Order * Timing.StepStagger;
                j.SetSortBoost(layout.LineLength(dir) - s.Order);

                if (s.Kind == StepKind.Blocked)
                {
                    StartCoroutine(j.Bump(dir, delay + Timing.Move * 0.35f));
                    longest = Mathf.Max(longest, delay + Timing.Move * 0.35f + Timing.Bump);
                    continue;
                }

                bool intoMold = s.Kind == StepKind.EnteredMold;
                if (!intoMold) next[s.To] = j;
                int target = s.To;
                JellyColor color = s.Color;
                StartCoroutine(j.Move(CellToLocal(target), dir, delay, intoMold, () =>
                {
                    if (!intoMold)
                    {
                        Audio?.PlaySquish();
                        return;
                    }
                    MoldView mold = molds[target];
                    if (mold != null) mold.PlayFill();
                    Vector3 world = CellToWorld(target);
                    Fx?.Burst(world, SpriteFactory.ToColor(ArtPalette.Base(color)), 12, 1f + chain * 0.25f);
                    Audio?.PlayFill(chain);
                    chain++;
                    lastFill = world;
                }));
                longest = Mathf.Max(longest, delay + Timing.Move + (intoMold ? Timing.EnterMold : Timing.Settle * 0.5f));
            }

            jellies = next;
            yield return new WaitForSeconds(longest);

            if (chain >= 2)
            {
                Audio?.Play(SoundId.Combo, 1f, 0.8f);
                Combo?.Invoke(chain, lastFill);
            }
            done?.Invoke();
        }

        /// <summary>Plays a press backwards (undo), then snaps to <paramref name="target"/> as a safety net.</summary>
        public void PlayUndo(MoveResult undone, BoardState target, Action done)
        {
            StartCoroutine(UndoRoutine(undone, target, done));
        }

        private IEnumerator UndoRoutine(MoveResult undone, BoardState target, Action done)
        {
            Audio?.Play(SoundId.Undo, 1f, 0.8f);
            var next = (JellyView[])jellies.Clone();
            Direction back = undone.Move.Direction.Opposite();
            const float Duration = 0.22f;

            foreach (JellyStep s in undone.Steps)
            {
                if (s.Kind == StepKind.Moved) next[s.To] = null;
            }
            foreach (JellyStep s in undone.Steps)
            {
                if (s.Kind == StepKind.Blocked) continue;
                JellyView j;
                if (s.Kind == StepKind.EnteredMold)
                {
                    if (molds[s.To] != null) molds[s.To].SetFilled(false);
                    j = AcquireJelly(s.Color, CellToLocal(s.To));
                }
                else
                {
                    j = jellies[s.To];
                    if (j == null) continue;
                }
                next[s.From] = j;
                StartCoroutine(j.Move(CellToLocal(s.From), back, 0f, false, null));
            }
            jellies = next;
            yield return new WaitForSeconds(Duration + Timing.Move * 0.5f);
            Sync(target, false);
            done?.Invoke();
        }

        /// <summary>Tapped jelly wobbles (no move).</summary>
        public void Poke(int cell)
        {
            JellyView j = JellyAt(cell);
            if (j == null || !j.gameObject.activeSelf) return;
            StartCoroutine(j.Poke());
            Audio?.Play(SoundId.Tap, UnityEngine.Random.Range(0.95f, 1.1f), 0.7f);
        }

        public void PlayWin()
        {
            int k = 0;
            for (int i = 0; i < molds.Length; i++)
            {
                if (molds[i] != null) molds[i].PlayCheer(k++ * 0.07f);
            }
            if (Fx != null)
            {
                Color[] colors =
                {
                    SpriteFactory.ToColor(ArtPalette.Base(JellyColor.Coral)),
                    SpriteFactory.ToColor(ArtPalette.Base(JellyColor.Turquoise)),
                    SpriteFactory.ToColor(ArtPalette.Base(JellyColor.Yellow)),
                    Color.white,
                };
                Fx.Confetti(transform.position + new Vector3(0f, -Size.y * 0.5f, 0f), Size.x, colors, 60);
            }
            Audio?.Play(SoundId.Win);
        }
    }
}
