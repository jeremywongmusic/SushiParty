using SushiParty.Audio;
using System.Collections;
using SushiParty.Board;
using SushiParty.Menu;
using SushiParty.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SushiParty.Core
{
    public sealed class MinigameFlow : MonoBehaviour
    {
        public const string MenuSceneName = "MainMenu";
        public const string BoardSceneName = "Board";
        private const float FadeOutDuration = 0.28f;
        private const float FadeInDuration = 0.34f;
        private ScreenFader fader;
        private bool transitioning;
        private MatchSetup boardSetup;
        private AudioHandle boardMusic;
        private AudioHandle menuMusic;

        public static MinigameFlow Instance { get; private set; }

        public MatchSetup Setup { get; } = new MatchSetup();

        public MinigameOutcome? LastOutcome { get; private set; }

        public MinigameId? LastPlayed { get; private set; }

        public BoardSession BoardSession { get; private set; }

        public bool InBoardGame => BoardSession != null;

        public BoardAudioReporter BoardAudio { get; } = new BoardAudioReporter(new GameAudioGlobals());

        public bool IsTransitioning => transitioning;

        internal static MinigameFlow CreateOn(GameObject host)
        {
            MinigameFlow flow = host.AddComponent<MinigameFlow>();
            flow.fader = ScreenFader.Create(host.transform);

            PauseMenuController.CreateOn(host);

            return flow;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void Launch(MatchSetup setup)
        {
            if (transitioning)
            {
                return;
            }

            MinigameDefinition definition = MinigameLibrary.Get(setup.Minigame);
            StartCoroutine(LoadMinigame(definition, setup));
        }

        public void ReturnToMenu()
        {
            if (transitioning)
            {
                return;
            }

            ClearBoardGame();

            StartCoroutine(LoadScene(MenuSceneName, StartMenuMusic));
        }

        public void StartBoardGame(MatchSetup setup)
        {
            if (transitioning)
            {
                return;
            }

            boardSetup = setup;
            BoardSession = new BoardSession(BoardLayout.CreateDefault(), new RandomDie());

            StartCoroutine(LoadBoard(BoardSession));
        }

        public void AdoptBoardSession(BoardSession session, MatchSetup setup = null)
        {
            if (session == null || BoardSession == session)
            {
                return;
            }

            if (BoardSession != null)
            {
                Debug.LogWarning("A board session was already running; keeping the one that is in play.", this);
                return;
            }

            BoardSession = session;
            boardSetup = setup ?? Setup;
        }

        public bool LaunchMinigameFromBoard(MinigameId id)
        {
            if (transitioning)
            {
                return false;
            }

            if (BoardSession == null)
            {
                Debug.LogError("LaunchMinigameFromBoard was called with no board game running. Ignoring it.");
                return false;
            }

            MatchSetup setup = boardSetup ?? Setup;
            setup.Minigame = id;

            StopBoardMusic();

            StartCoroutine(LoadMinigame(MinigameLibrary.Get(id), setup));
            return true;
        }

        public void ReturnToBoard()
        {
            if (transitioning)
            {
                return;
            }

            if (BoardSession == null)
            {
                Debug.LogWarning("ReturnToBoard was called with no board game running — going to the menu instead.");
                ReturnToMenu();
                return;
            }

            StartCoroutine(LoadBoard(BoardSession));
        }

        public void EndBoardGame()
        {
            ClearBoardGame();
            ReturnToMenu();
        }

        private void ClearBoardGame()
        {
            StopBoardMusic();

            BoardAudio.Reset();

            BoardSession = null;
            boardSetup = null;
        }

        private IEnumerator LoadBoard(BoardSession existing)
        {
            yield return LoadScene(BoardSceneName, () =>
            {
                BoardController controller = FindFirstObjectByType<BoardController>();
                if (controller == null)
                {
                    Debug.LogError(
                        $"Scene '{BoardSceneName}' has no BoardController. " +
                        "Add one to the scene root, or the board cannot start.");
                    return;
                }

                controller.Adopt(existing);
            });

            boardMusic = GameAudio.Loop(Sfx.MusicBoard);
        }

        private void StopBoardMusic()
        {
            GameAudio.Stop(ref boardMusic);
        }

        public void StartMenuMusic()
        {
            if (menuMusic.IsValid)
            {
                return;
            }

            menuMusic = GameAudio.Loop(Sfx.MusicMenu);
        }

        public void StopMenuMusic()
        {
            GameAudio.Stop(ref menuMusic);
        }

        private IEnumerator LoadMinigame(MinigameDefinition definition, MatchSetup setup)
        {
            LastPlayed = definition.Id;
            LastOutcome = null;

            yield return LoadScene(definition.SceneName, () =>
            {
                MinigameController controller = FindFirstObjectByType<MinigameController>();
                if (controller == null)
                {
                    Debug.LogError(
                        $"Scene '{definition.SceneName}' has no MinigameController. " +
                        "Add one to the scene root, or the round cannot start.");
                    return;
                }

                if (controller.Id != definition.Id)
                {
                    Debug.LogWarning(
                        $"Scene '{definition.SceneName}' hosts a controller for {controller.Id} " +
                        $"but was launched as {definition.Id}. Using the scene's own definition.");
                }

                MinigameDefinition actual = MinigameLibrary.Get(controller.Id);
                controller.Initialize(new MinigameContext(actual, setup, isDirectPlay: false));
            });
        }

        private IEnumerator LoadScene(string sceneName, System.Action afterLoad)
        {
            transitioning = true;

            GameAudio.StopAll();

            StopMenuMusic();
            StopBoardMusic();

            yield return fader.FadeTo(1f, FadeOutDuration);

            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (operation == null)
            {
                Debug.LogError($"Could not load scene '{sceneName}'. Is it added to Build Settings?");
                yield return fader.FadeTo(0f, FadeInDuration);
                transitioning = false;
                yield break;
            }

            while (!operation.isDone)
            {
                yield return null;
            }

            // Runs after the new scene's Awake/OnEnable but before its first Start.
            afterLoad?.Invoke();

            yield return fader.FadeTo(0f, FadeInDuration);
            transitioning = false;
        }

        public void RecordOutcome(MinigameId id, MinigameOutcome result)
        {
            LastPlayed = id;
            LastOutcome = result;

            if (!SettlesBoardRound(BoardSession, result))
            {
                return;
            }

            BoardSession.ApplyMinigameOutcome(result.Won);
        }

        public static bool SettlesBoardRound(BoardSession session, MinigameOutcome result)
        {
            return session != null
                   && session.AwaitingMinigame
                   && result.Result != MinigameResult.Abandoned;
        }
    }
}
