using JellyMold.Game.UI;
using UnityEngine;

namespace JellyMold.Game
{
    /// <summary>
    /// Entry point. Builds the whole game from code after the first scene loads, so the project
    /// needs no hand-authored scene or prefab. A scene containing an App also works.
    /// </summary>
    public sealed class App : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (FindFirstObjectByType<App>() != null) return;
            new GameObject("JellyMold").AddComponent<App>();
        }

        private void Awake()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Screen.orientation = ScreenOrientation.Portrait;
            Screen.sleepTimeout = SleepTimeout.SystemSetting;
#if ENABLE_LEGACY_INPUT_MANAGER
            Input.multiTouchEnabled = false;
#endif

            var save = new SaveManager();
            save.Load();

            AudioManager audio = AudioManager.Create(transform);
            var boardRoot = new GameObject("BoardRoot");
            boardRoot.transform.SetParent(transform, false);
            BoardView board = BoardView.Create(boardRoot.transform);
            board.Audio = audio;
            board.Fx = FxPool.Create(transform);
            TutorialHand hand = TutorialHand.Create(boardRoot.transform);

            CameraRig rig = CameraRig.Create(transform, board);
            UIManager ui = UIManager.Create(transform, audio);
            SwipeInput input = SwipeInput.Create(gameObject, rig, board);

            var controller = gameObject.AddComponent<GameController>();
            controller.Init(LevelRepository.LoadAll(), save, audio, board, ui, input, hand, boardRoot);
        }
    }
}
