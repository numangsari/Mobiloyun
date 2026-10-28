using System.Collections;
using System.Collections.Generic;
using JellyMold.Core;
using JellyMold.Game.UI;
using JellyMold.Procedural;
using UnityEngine;

namespace JellyMold.Game
{
    /// <summary>
    /// Glue between input, the pure <see cref="GameSession"/>, the board view and the UI.
    /// Owns the input gate so no press is accepted while a move is still animating.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        private static readonly string[] Cheers = { "Harika!", "Süper!", "Çok iyi!", "Tam isabet!" };

        private readonly InputGate gate = new InputGate();
        private readonly Stack<MoveResult> undoResults = new Stack<MoveResult>();

        private List<LevelData> levels;
        private SaveManager save;
        private AudioManager audioManager;
        private BoardView board;
        private UIManager ui;
        private SwipeInput input;
        private TutorialHand hand;
        private GameObject boardRoot;

        private GameSession session;
        private int levelIndex;
        private bool firstMoveCheered;

        public void Init(List<LevelData> levels, SaveManager save, AudioManager audio, BoardView board, UIManager ui, SwipeInput input, TutorialHand hand, GameObject boardRoot)
        {
            this.levels = levels;
            this.save = save;
            audioManager = audio;
            this.board = board;
            this.ui = ui;
            this.input = input;
            this.hand = hand;
            this.boardRoot = boardRoot;

            audio.Muted = !save.Progress.SoundOn;
            board.Combo += (count, _) => ui.ShowCombo(count);

            input.Swipe += OnSwipe;
            input.Tap += (x, y) => { if (CanPlay()) board.Poke(board.Layout.Index(x, y)); };
            input.Up += () => board.HideLine();

            ui.PlayPressed += () => StartLevel(ContinueIndex());
            ui.LevelsPressed += ShowLevelSelect;
            ui.LevelChosen += id => StartLevel(levels.FindIndex(l => l.Id == id));
            ui.MenuPressed += ShowTitle;
            ui.UndoPressed += Undo;
            ui.LoseUndoPressed += () =>
            {
                ui.HideOverlays();
                Undo();
            };
            ui.RestartPressed += Restart;
            ui.NextPressed += Next;
            ui.SoundToggled += ToggleSound;
            ui.SettingsRequested += () => ui.ShowSettings(save.Progress.SoundOn);

            ShowTitle();
        }

        private int ContinueIndex()
        {
            int unlocked = Mathf.Clamp(save.Progress.HighestUnlocked, 1, levels.Count);
            return levels.FindIndex(l => l.Id == unlocked) is int i && i >= 0 ? i : 0;
        }

        private void ShowTitle()
        {
            hand.Hide();
            boardRoot.SetActive(false);
            ui.ShowTitle(levels.Count == 0 ? 1 : levels[ContinueIndex()].Id);
        }

        private void ShowLevelSelect()
        {
            hand.Hide();
            boardRoot.SetActive(false);
            ui.ShowLevelSelect(levels, save.Progress);
        }

        private void StartLevel(int index)
        {
            if (index < 0 || index >= levels.Count) return;
            levelIndex = index;
            LevelData level = levels[index];
            session = GameSession.FromLevel(level);
            undoResults.Clear();
            gate.Release();
            firstMoveCheered = false;

            boardRoot.SetActive(true);
            board.Build(session.Current);
            ui.ShowHud(level);
            ui.SetMoves(session.MovesLeft, false);
            ui.SetUndoInteractable(false);
            ShowHint();
        }

        private bool CanPlay()
        {
            return session != null && boardRoot.activeSelf && !ui.OverlayOpen && session.Status == GameStatus.Playing;
        }

        // ------------------------------------------------------------------ moves

        private void OnSwipe(int x, int y, Direction dir)
        {
            if (!CanPlay() || !gate.TryAcquire()) return;

            PressMove move = PressMove.FromSwipe(x, y, dir);
            board.ShowLine(dir, move.Line);
            MoveResult result = session.TryPress(move);
            if (result.IsValid)
            {
                hand.Hide();
                undoResults.Push(result);
                ui.SetMoves(session.MovesLeft, true);
                ui.SetUndoInteractable(true);
            }
            board.PlayResult(result, () => OnMovePlayed(result));
        }

        private void OnMovePlayed(MoveResult result)
        {
            gate.Release();
            if (!result.IsValid) return;

            switch (session.Status)
            {
                case GameStatus.Won:
                    StartCoroutine(WinRoutine());
                    break;
                case GameStatus.Lost:
                    StartCoroutine(LoseRoutine());
                    break;
                default:
                    if (levels[levelIndex].Hint && !firstMoveCheered)
                    {
                        firstMoveCheered = true;
                        ui.SetTutorial(Cheers[Random.Range(0, Cheers.Length)] + " Devam et.");
                    }
                    if (!session.HasAnyValidMove()) ui.SetTutorial("Sıkıştın! Geri al ya da yeniden başla.");
                    ShowHint();
                    break;
            }
        }

        private void Undo()
        {
            if (session == null || !session.CanUndo || !gate.TryAcquire()) return;
            hand.Hide();
            MoveResult last = undoResults.Count > 0 ? undoResults.Pop() : null;
            session.Undo();
            ui.SetMoves(session.MovesLeft, true);
            ui.SetUndoInteractable(session.CanUndo);
            if (last == null)
            {
                board.Sync(session.Current, true);
                gate.Release();
                ShowHint();
                return;
            }
            board.PlayUndo(last, session.Current, () =>
            {
                gate.Release();
                ShowHint();
            });
        }

        private void Restart()
        {
            if (session == null) return;
            bool fromOverlay = ui.OverlayOpen;
            if (!fromOverlay && gate.IsBusy) return;
            ui.HideOverlays();
            StopAllCoroutines();
            session.Restart();
            undoResults.Clear();
            gate.Release();
            firstMoveCheered = false;
            board.Build(session.Current);
            ui.SetMoves(session.MovesLeft, true);
            ui.SetUndoInteractable(false);
            ui.SetTutorial(levels[levelIndex].Tutorial);
            audioManager.Play(SoundId.Undo, 0.8f, 0.7f);
            ShowHint();
        }

        private void Next()
        {
            ui.HideOverlays();
            if (levelIndex + 1 < levels.Count) StartLevel(levelIndex + 1);
            else ShowLevelSelect();
        }

        private void ToggleSound()
        {
            save.Progress.SoundOn = !save.Progress.SoundOn;
            audioManager.Muted = !save.Progress.SoundOn;
            save.Save();
            ui.SetSoundState(save.Progress.SoundOn);
        }

        private IEnumerator WinRoutine()
        {
            LevelData level = levels[levelIndex];
            int used = session.MovesUsed;
            save.Progress.RecordWin(level.Id, used, levels.Count);
            if (level.Hint) save.Progress.TutorialSeen = true;
            save.Save();

            board.PlayWin();
            ui.SetTutorial("");
            yield return new WaitForSeconds(Timing.WinDelay + 0.4f);
            ui.ShowWin(GameConfig.StarsFor(used, level.Par), used, levelIndex + 1 < levels.Count);
        }

        private IEnumerator LoseRoutine()
        {
            yield return new WaitForSeconds(0.35f);
            audioManager.Play(SoundId.Lose);
            ui.ShowLose(session.Current.FilledCount, session.Layout.MoldCount, session.CanUndo);
        }

        // ------------------------------------------------------------------ tutorial hand

        /// <summary>On hint levels, demonstrate the next press of an optimal solution from the current state.</summary>
        private void ShowHint()
        {
            hand.Hide();
            if (session == null || session.Status != GameStatus.Playing || !levels[levelIndex].Hint) return;
            SolveResult solve = PuzzleSolver.Solve(session.Current, session.MovesLeft, 20000);
            if (!solve.Solvable || solve.Length == 0) return;

            PressMove move = solve.Solution[0];
            MoveResult preview = MoveResolver.Apply(session.Current, move);
            int startCell = -1;
            foreach (JellyStep s in preview.Steps)
            {
                if (s.Kind == StepKind.Blocked) continue;
                startCell = s.From;
                break;
            }
            if (startCell < 0) return;
            Vector3 from = board.CellToWorld(startCell);
            Vector2 dir = JellyView.DirVector(move.Direction);
            hand.Show(from, from + (Vector3)(dir * 1.3f));
        }
    }
}
