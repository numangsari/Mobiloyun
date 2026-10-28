using System.Collections;
using System.Collections.Generic;
using JellyMold.Core;
using JellyMold.Game.UI;
using JellyMold.Procedural;
using UnityEngine;

namespace JellyMold.Game
{
    /// <summary>
    /// Glue between input, the pure <see cref="GameFlow"/>, the board view and the UI.
    /// Owns the input gate so no press is accepted while a move is still animating.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        private static readonly string[] Cheers = { "Harika!", "Süper!", "Çok iyi!", "Tam isabet!" };

        private readonly InputGate gate = new InputGate();

        private GameFlow flow;
        private SaveManager save;
        private AudioManager audioManager;
        private BoardView board;
        private UIManager ui;
        private TutorialHand hand;
        private GameObject boardRoot;
        private bool firstMoveCheered;

        /// <summary>Test hooks for PlayMode tests (see InternalsVisibleTo in AssemblyInfo.cs).</summary>
        internal GameFlow Flow => flow;
        internal BoardView Board => board;
        internal UIManager Ui => ui;
        internal bool IsIdle => !gate.IsBusy;
        internal void SimulateSwipe(int x, int y, Direction dir) => OnSwipe(x, y, dir);
        internal void SimulateUndo() => Undo();
        internal void SimulateStartLevel(int index) => StartLevel(index);
        internal void SimulateShowTitle() => ShowTitle();

        public void Init(List<LevelData> levels, SaveManager save, AudioManager audio, BoardView board, UIManager ui, SwipeInput input, TutorialHand hand, GameObject boardRoot)
        {
            flow = new GameFlow(levels, save.Progress);
            this.save = save;
            audioManager = audio;
            this.board = board;
            this.ui = ui;
            this.hand = hand;
            this.boardRoot = boardRoot;

            audio.Muted = !flow.Progress.SoundOn;
            flow.ProgressChanged += save.Save;
            board.Combo += (count, _) => ui.ShowCombo(count);

            input.Swipe += OnSwipe;
            input.Tap += (x, y) => { if (CanPlay()) board.Poke(board.Layout.Index(x, y)); };
            input.Up += () => board.HideLine();

            ui.PlayPressed += () => StartLevel(flow.ContinueIndex());
            ui.LevelsPressed += ShowLevelSelect;
            ui.LevelChosen += id => StartLevel(flow.IndexOf(id));
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
            ui.SettingsRequested += () => ui.ShowSettings(flow.Progress.SoundOn);

            ShowTitle();
        }

        private void ShowTitle()
        {
            hand.Hide();
            boardRoot.SetActive(false);
            int index = flow.ContinueIndex();
            ui.ShowTitle(index < 0 ? 1 : flow.Levels[index].Id);
        }

        private void ShowLevelSelect()
        {
            hand.Hide();
            boardRoot.SetActive(false);
            ui.ShowLevelSelect(flow.Levels, flow.Progress);
        }

        private void StartLevel(int index)
        {
            if (!flow.StartLevel(index)) return;
            StopAllCoroutines();
            gate.Release();
            firstMoveCheered = false;

            boardRoot.SetActive(true);
            board.Build(flow.Session.Current);
            ui.ShowHud(flow.Level);
            ui.SetMoves(flow.Session.MovesLeft, false);
            ui.SetUndoInteractable(false);
            ShowHint();
        }

        private bool CanPlay()
        {
            return flow.CanPlay && boardRoot.activeSelf && !ui.OverlayOpen;
        }

        // ------------------------------------------------------------------ moves

        private void OnSwipe(int x, int y, Direction dir)
        {
            if (!CanPlay() || !gate.TryAcquire()) return;

            PressMove move = PressMove.FromSwipe(x, y, dir);
            board.ShowLine(dir, move.Line);
            MoveResult result = flow.Press(move);
            if (result.IsValid)
            {
                hand.Hide();
                ui.SetMoves(flow.Session.MovesLeft, true);
                ui.SetUndoInteractable(flow.CanUndo);
            }
            board.PlayResult(result, () => OnMovePlayed(result));
        }

        private void OnMovePlayed(MoveResult result)
        {
            gate.Release();
            if (!result.IsValid) return;

            switch (flow.Session.Status)
            {
                case GameStatus.Won:
                    StartCoroutine(WinRoutine());
                    break;
                case GameStatus.Lost:
                    StartCoroutine(LoseRoutine());
                    break;
                default:
                    if (flow.Level.Hint && !firstMoveCheered)
                    {
                        firstMoveCheered = true;
                        ui.SetTutorial(Cheers[Random.Range(0, Cheers.Length)] + " Devam et.");
                    }
                    if (flow.IsStuck) ui.SetTutorial("Sıkıştın! Geri al ya da yeniden başla.");
                    ShowHint();
                    break;
            }
        }

        private void Undo()
        {
            if (!flow.CanUndo || !gate.TryAcquire()) return;
            hand.Hide();
            flow.TryUndo(out MoveResult undone);
            ui.SetMoves(flow.Session.MovesLeft, true);
            ui.SetUndoInteractable(flow.CanUndo);
            if (undone == null)
            {
                board.Sync(flow.Session.Current, true);
                gate.Release();
                ShowHint();
                return;
            }
            board.PlayUndo(undone, flow.Session.Current, () =>
            {
                gate.Release();
                ShowHint();
            });
        }

        private void Restart()
        {
            if (flow.Session == null) return;
            if (!ui.OverlayOpen && gate.IsBusy) return;
            ui.HideOverlays();
            StopAllCoroutines();
            flow.Restart();
            gate.Release();
            firstMoveCheered = false;
            board.Build(flow.Session.Current);
            ui.SetMoves(flow.Session.MovesLeft, true);
            ui.SetUndoInteractable(false);
            ui.SetTutorial(flow.Level.Tutorial);
            audioManager.Play(SoundId.Undo, 0.8f, 0.7f);
            ShowHint();
        }

        private void Next()
        {
            ui.HideOverlays();
            if (flow.HasNext) StartLevel(flow.LevelIndex + 1);
            else ShowLevelSelect();
        }

        private void ToggleSound()
        {
            flow.ToggleSound();
            audioManager.Muted = !flow.Progress.SoundOn;
            ui.SetSoundState(flow.Progress.SoundOn);
        }

        private IEnumerator WinRoutine()
        {
            board.PlayWin();
            ui.SetTutorial("");
            yield return new WaitForSeconds(Timing.WinDelay + 0.4f);
            if (flow.Session.Status != GameStatus.Won) yield break;
            ui.ShowWin(flow.Stars, flow.Session.MovesUsed, flow.HasNext);
        }

        private IEnumerator LoseRoutine()
        {
            yield return new WaitForSeconds(0.35f);
            if (flow.Session.Status != GameStatus.Lost) yield break;
            audioManager.Play(SoundId.Lose);
            ui.ShowLose(flow.Session.Current.FilledCount, flow.Session.Layout.MoldCount, flow.CanUndo);
        }

        // ------------------------------------------------------------------ tutorial hand

        /// <summary>On hint levels, demonstrate the next press of an optimal solution from the current state.</summary>
        private void ShowHint()
        {
            hand.Hide();
            Hint? hint = flow.NextHint();
            if (!hint.HasValue) return;
            Vector3 from = board.CellToWorld(hint.Value.StartCell);
            Vector2 dir = JellyView.DirVector(hint.Value.Move.Direction);
            hand.Show(from, from + (Vector3)(dir * 1.3f));
        }
    }
}
