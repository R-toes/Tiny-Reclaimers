using System.Collections.Generic;
using UnityEngine;
using TrashTD.Core.GameLoop;
using TrashTD.Data;
using TrashTD.Operators;
using TrashTD.Systems;
using TrashTD.UI;

namespace TrashTD.Playtest
{
    /// <summary>
    /// Watches one stage run and reports it: "stage_start" when it begins and "stage_end" when it is cleared, failed or
    /// left, with how far the player got, who they used and how smoothly it ran. Added to the StageBootstrapper by
    /// <see cref="Begin"/>; it sits idle (and records nothing) unless <see cref="PlaytestLog"/> is recording. The
    /// sandbox and a stage without data are never reported.
    /// </summary>
    public sealed class PlaytestTracker : MonoBehaviour
    {
        /// <summary>A frame longer than this counts as a hitch.</summary>
        public const float HitchSeconds = 0.05f;

        /// <summary>The stage being played, or empty in the menus (the log tags errors and feedback with it).</summary>
        public static string ActiveStageId { get; private set; } = string.Empty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlayMode() => ActiveStageId = string.Empty;

        private string stageId;
        private string difficulty;
        private bool started;
        private bool ended;
        private float startTime;

        private GameManager gameManager;
        private WaveManager waveManager;
        private OperatorManager operatorManager;
        private CardDraftSystem draftSystem;

        private int wave;
        private int totalWaves;
        private int lives = -1;
        private int livesLost;
        private int rerolls;
        private readonly Dictionary<string, int> deployed = new Dictionary<string, int>();
        private readonly Dictionary<string, int> retreated = new Dictionary<string, int>();
        private readonly Dictionary<string, int> died = new Dictionary<string, int>();
        private readonly List<string> picks = new List<string>();

        private float frameTimeSum;
        private int frames;
        private int hitches;
        private float worstFrame;

        /// <summary>Starts tracking this stage run. Does nothing for the sandbox or a missing stage.</summary>
        public static void Begin(GameObject host, string stage, string stageDifficulty)
        {
            if (host == null || string.IsNullOrEmpty(stage) || stage == "SANDBOX") return;
            PlaytestTracker tracker = host.AddComponent<PlaytestTracker>();
            tracker.stageId = stage;
            tracker.difficulty = stageDifficulty;
        }

        private void Start()
        {
            gameManager = FindAnyObjectByType<GameManager>();
            waveManager = FindAnyObjectByType<WaveManager>();
            operatorManager = FindAnyObjectByType<OperatorManager>();
            draftSystem = FindAnyObjectByType<CardDraftSystem>();

            if (gameManager != null)
            {
                gameManager.OnStageVictory += HandleVictory;
                gameManager.OnStageDefeat += HandleDefeat;
                gameManager.OnLifePointsChanged += HandleLives;
            }
            if (waveManager != null) waveManager.OnWaveStarted += HandleWaveStarted;
            if (operatorManager != null)
            {
                operatorManager.OnOperatorDeployed += HandleDeployed;
                operatorManager.OnOperatorRetreated += HandleRetreated;
                operatorManager.OnOperatorDied += HandleDied;
            }
            if (draftSystem != null)
            {
                draftSystem.OnCardSelected += HandlePick;
                draftSystem.OnRerollCountChanged += HandleReroll;
            }

            started = true;
            startTime = Time.realtimeSinceStartup;
            ActiveStageId = stageId;
            PlaytestLog.Record("stage_start", stageId, new PlaytestJson().Add("difficulty", difficulty));
        }

        private void Update()
        {
            if (!started || ended || gameManager == null || gameManager.CurrentState != GamePlayState.Playing) return;

            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f || dt > 1f) return; // a pause, a scene load or a debugger stop is not a frame of play
            frameTimeSum += dt;
            frames++;
            if (dt > HitchSeconds) hitches++;
            if (dt > worstFrame) worstFrame = dt;
        }

        private void OnDestroy()
        {
            if (gameManager != null)
            {
                gameManager.OnStageVictory -= HandleVictory;
                gameManager.OnStageDefeat -= HandleDefeat;
                gameManager.OnLifePointsChanged -= HandleLives;
            }
            if (waveManager != null) waveManager.OnWaveStarted -= HandleWaveStarted;
            if (operatorManager != null)
            {
                operatorManager.OnOperatorDeployed -= HandleDeployed;
                operatorManager.OnOperatorRetreated -= HandleRetreated;
                operatorManager.OnOperatorDied -= HandleDied;
            }
            if (draftSystem != null)
            {
                draftSystem.OnCardSelected -= HandlePick;
                draftSystem.OnRerollCountChanged -= HandleReroll;
            }

            // Leaving the stage (retry, quit to menu, closing the game) before it ended
            if (started && !ended) End("quit", 0);
            if (ActiveStageId == stageId) ActiveStageId = string.Empty;
        }

        // ---------------------------------------------------------------- events

        private void HandleVictory(int stars) => End("clear", stars);
        private void HandleDefeat() => End("fail", 0);

        private void HandleWaveStarted(int current, int total)
        {
            wave = current;
            totalWaves = total;
        }

        private void HandleLives(int current, int max)
        {
            if (lives >= 0 && current < lives) livesLost += lives - current;
            lives = current;
        }

        private void HandleDeployed(OperatorBase op) => Count(deployed, op);
        private void HandleRetreated(OperatorBase op) => Count(retreated, op);
        private void HandleDied(OperatorBase op) => Count(died, op);

        private void HandlePick(DraftCard card)
        {
            if (card != null && card.operatorData != null) picks.Add(card.operatorData.operatorName);
        }

        private void HandleReroll(int remaining) => rerolls++;

        private static void Count(Dictionary<string, int> counts, OperatorBase op)
        {
            string name = op != null && op.Data != null ? op.Data.operatorName : "?";
            counts.TryGetValue(name, out int value);
            counts[name] = value + 1;
        }

        // ---------------------------------------------------------------- the report

        private void End(string result, int stars)
        {
            if (ended || !started) return;
            ended = true;

            PlaytestLog.Record("stage_end", stageId, BuildEndData(result, stars));
        }

        private PlaytestJson BuildEndData(string result, int stars)
        {
            var data = new PlaytestJson()
                .Add("result", result)
                .Add("stars", stars)
                .Add("difficulty", difficulty)
                .Add("device", SystemInfo.deviceModel)
                .Add("seconds", Mathf.RoundToInt(Time.realtimeSinceStartup - startTime))
                .Add("wave", wave)
                .Add("waves", totalWaves)
                .Add("livesLeft", Mathf.Max(lives, 0))
                .Add("livesLost", livesLost)
                .Add("rerolls", rerolls)
                .Add("avgFps", AverageFps(frames, frameTimeSum))
                .Add("hitches", hitches)
                .Add("worstMs", Mathf.RoundToInt(worstFrame * 1000f))
                // The graphics options in force now (a tester can change them mid-session, so session_start is not
                // enough): this is what lets the sheet compare frame rate per CRT style on each device
                .Add("crt", GameGraphicsSettings.CrtStyle.ToString());
            if (PointerInput.IsMobileDevice)
            {
                data.Add("fpsCap", MobileSettings.FrameRate)
                    .Add("picture", MobileSettings.Picture.ToString());
            }
            data.AddCounts("deployed", deployed);
            data.AddCounts("retreated", retreated);
            data.AddCounts("died", died);
            data.AddList("picks", picks);
            return data;
        }

        /// <summary>Average frames per second over <paramref name="frames"/> frames that took <paramref name="seconds"/> in total (0 with none).</summary>
        public static int AverageFps(int frames, float seconds)
        {
            return frames > 0 && seconds > 0f ? Mathf.RoundToInt(frames / seconds) : 0;
        }
    }
}
