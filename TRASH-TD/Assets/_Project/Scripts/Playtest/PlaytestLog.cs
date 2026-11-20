using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using TrashTD.UI;

namespace TrashTD.Playtest
{
    /// <summary>
    /// Records playtest events and sends them to the Google Apps Script web app from <see cref="PlaytestConfig"/>. It exists only when
    /// the config is complete, records only after the player allowed it (<see cref="PlaytestConsent"/>) and never blocks
    /// the game: events wait in a small queue that is saved to disk, one is posted every few seconds, and a failed post
    /// is retried later (a phone that is offline just delivers them when it is back).
    /// Everything is anonymous: a random install id, device model, OS and graphics info, and what happened in the game.
    /// </summary>
    public sealed class PlaytestLog : MonoBehaviour
    {
        private const string InstallKey = "playtest.installId";
        private const string QueueFile = "playtest_queue.json";
        private const float SendInterval = 3f;
        private const float MaxBackoff = 120f;
        private const int MaxErrorsPerSession = 8;

        public static PlaytestLog Instance { get; private set; }

        /// <summary>True when events are being recorded right now (configured, allowed, not an editor run).</summary>
        public static bool Recording => Instance != null && Instance.IsRecording;

        /// <summary>A plain-words report of why events are or are not being sent (TRASH TD > Playtest > Show Status).</summary>
        public static string Status => Instance == null
            ? "The playtest system is not running: this play session has no PlaytestConfig.json with a web app address."
            : Instance.BuildStatus();

        private string BuildStatus()
        {
            string editorLine = Application.isEditor
                ? "Editor sending (\"sendFromEditor\"): " + (config.sendFromEditor ? "ON" : "OFF - set it to true in PlaytestConfig.json, then stop and start Play mode")
                : "Build: yes";
            return "Config: OK\n" +
                   "Player's answer: " + PlaytestConsent.Choice + (PlaytestConsent.Choice == PlaytestChoice.Allowed
                       ? string.Empty
                       : " - press ALLOW on the main menu, or Settings > Audio > PLAYTEST > SHARE DATA ON") + "\n" +
                   editorLine + "\n" +
                   "Recording now: " + (IsRecording ? "YES" : "NO") + "\n" +
                   "Waiting to send: " + queue.Count + "\n" +
                   "Sent so far: " + sentCount + "\n" +
                   "Last result: " + lastResult;
        }

        private PlaytestConfig config;
        private PlaytestQueue queue = new PlaytestQueue();
        private string installId;
        private string sessionId;
        private bool sessionStarted;
        private float sessionStartTime;
        private float backoff;
        private float nextSendTime;
        private bool sending;
        private int errorsRecorded;
        private string lastError;
        private float consentPromptTime = -1f;
        private bool loggedWaiting;
        private int sentCount;
        private string lastResult = "nothing sent yet";
        private bool loggedFirstSuccess;
        private int loggedFailures;

        private bool IsRecording => config != null && config.IsComplete && PlaytestConsent.Allowed && (!Application.isEditor || config.sendFromEditor);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            PlaytestConfig config = PlaytestConfig.Current;
            if (!config.IsComplete) return; // no config in this build: the playtest system does not exist

            var go = new GameObject("PlaytestLog");
            DontDestroyOnLoad(go);
            go.AddComponent<PlaytestLog>();
        }

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            config = PlaytestConfig.Current;

            installId = PlayerPrefs.GetString(InstallKey, string.Empty);
            if (string.IsNullOrEmpty(installId))
            {
                installId = Guid.NewGuid().ToString("N").Substring(0, 8);
                PlayerPrefs.SetString(InstallKey, installId);
                PlayerPrefs.Save();
            }
            sessionId = installId + "-" + Guid.NewGuid().ToString("N").Substring(0, 4);
            sessionStartTime = Time.realtimeSinceStartup;

            queue = PlaytestQueue.FromJson(ReadQueueFile());
            Application.logMessageReceived += HandleLog;

            Debug.Log("[Playtest] ready. Answer: " + PlaytestConsent.Choice + ". " +
                      (Application.isEditor && !config.sendFromEditor
                          ? "Editor runs do NOT send (set \"sendFromEditor\": true in PlaytestConfig.json to test here)."
                          : "Events are sent to the form."));
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= HandleLog;
            if (Instance == this) Instance = null;
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) SaveQueue();
        }

        private void OnApplicationQuit()
        {
            SaveQueue();
        }

        private void Update()
        {
            if (!IsRecording)
            {
                MaybeAskConsent();
                if (!loggedWaiting && PlaytestConsent.Choice == PlaytestChoice.Unknown && !PlaytestUi.IsShowing &&
                    Time.realtimeSinceStartup > 5f)
                {
                    loggedWaiting = true;
                    Debug.Log("[Playtest] waiting for the player's ALLOW. The question shows on the main menu; or use Settings > Audio > PLAYTEST > SHARE DATA > ON.");
                }
                return;
            }

            if (!sessionStarted)
            {
                sessionStarted = true;
                RecordSessionStart();
            }

            if (!sending && queue.Count > 0 && Time.realtimeSinceStartup >= nextSendTime &&
                Application.internetReachability != NetworkReachability.NotReachable)
            {
                StartCoroutine(SendNext());
            }
        }

        // ---------------------------------------------------------------- recording

        /// <summary>
        /// Adds an event (the "event" column) with its stage id and a JSON object of details. Does nothing unless recording.
        /// </summary>
        public static void Record(string eventName, string stage, PlaytestJson data)
        {
            if (Instance == null || !Instance.IsRecording) return;
            Instance.Enqueue(eventName, stage, data);
        }

        private void Enqueue(string eventName, string stage, PlaytestJson data)
        {
            data = data ?? new PlaytestJson();
            data.Add("t", Mathf.RoundToInt(Time.realtimeSinceStartup - sessionStartTime));
            queue.Enqueue(new PlaytestEntry
            {
                session = sessionId,
                platform = Application.platform.ToString(),
                build = Application.version + (Debug.isDebugBuild ? "-dev" : string.Empty),
                evt = eventName,
                stage = stage,
                data = data.ToString()
            });
            SaveQueue();
        }

        private void RecordSessionStart()
        {
            var data = new PlaytestJson()
                .Add("device", SystemInfo.deviceModel)
                .Add("os", SystemInfo.operatingSystem)
                .Add("ramMb", SystemInfo.systemMemorySize)
                .Add("gpu", SystemInfo.graphicsDeviceName)
                .Add("screen", Screen.width + "x" + Screen.height)
                .Add("dpi", Mathf.RoundToInt(Screen.dpi))
                .Add("refreshHz", Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value))
                .Add("controls", ControlSettings.Scheme.ToString())
                .Add("crt", GameGraphicsSettings.CrtStyle.ToString())
                .Add("touch", PointerInput.IsMobileDevice);
            if (PointerInput.IsMobileDevice)
            {
                data.Add("uiSize", MobileSettings.UiSizeSetting.ToString())
                    .Add("fpsCap", MobileSettings.FrameRate)
                    .Add("picture", MobileSettings.Picture.ToString());
            }
            Enqueue("session_start", string.Empty, data);
        }

        private void HandleLog(string message, string stackTrace, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error) return;
            if (!IsRecording || errorsRecorded >= MaxErrorsPerSession || message == lastError) return;

            lastError = message;
            errorsRecorded++;
            string firstStackLine = string.Empty;
            if (!string.IsNullOrEmpty(stackTrace))
            {
                int end = stackTrace.IndexOf('\n');
                firstStackLine = end > 0 ? stackTrace.Substring(0, end) : stackTrace;
            }
            Enqueue("error", PlaytestTracker.ActiveStageId, new PlaytestJson()
                .Add("type", type.ToString())
                .Add("message", message)
                .Add("where", firstStackLine));
        }

        // ---------------------------------------------------------------- sending

        private IEnumerator SendNext()
        {
            PlaytestEntry entry = queue.Peek();
            if (entry == null) yield break;

            sending = true;
            var form = new WWWForm();
            foreach (KeyValuePair<string, string> field in PlaytestQueue.BuildFields(entry))
                form.AddField(field.Key, field.Value);

            using (UnityWebRequest request = UnityWebRequest.Post(config.endpointUrl, form))
            {
                request.timeout = 15;
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    if (!loggedFirstSuccess)
                    {
                        loggedFirstSuccess = true;
                        Debug.Log("[Playtest] first event delivered (HTTP " + request.responseCode + ").");
                    }
                    sentCount++;
                    lastResult = "delivered (HTTP " + request.responseCode + ")";
                    queue.Complete();
                    backoff = 0f;
                    nextSendTime = Time.realtimeSinceStartup + SendInterval;
                }
                else
                {
                    lastResult = "failed: HTTP " + request.responseCode + " " + request.error;
                    if (loggedFailures++ < 3)
                        Debug.LogWarning("[Playtest] could not send an event: HTTP " + request.responseCode + " " + request.error +
                                         ". Check the web app is deployed with access for Anyone, and the address in PlaytestConfig.json.");
                    bool dropped = queue.Fail();
                    backoff = Mathf.Min(MaxBackoff, backoff <= 0f ? SendInterval * 2f : backoff * 2f);
                    nextSendTime = Time.realtimeSinceStartup + backoff;
                    if (dropped) Debug.LogWarning("Playtest: gave up on one event (" + request.error + ")");
                }
            }

            SaveQueue();
            sending = false;
        }

        // ---------------------------------------------------------------- saving

        private static string QueuePath => Path.Combine(Application.persistentDataPath, QueueFile);

        private static string ReadQueueFile()
        {
            try
            {
                return File.Exists(QueuePath) ? File.ReadAllText(QueuePath) : string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private void SaveQueue()
        {
            try
            {
                File.WriteAllText(QueuePath, queue.ToJson());
            }
            catch (Exception)
            {
                // a full or read-only disk only means the queue is not kept across a restart
            }
        }

        // ---------------------------------------------------------------- the first-launch question

        /// <summary>On the main menu, with a config and no answer yet, asks once (a moment after the menu appears).</summary>
        private void MaybeAskConsent()
        {
            if (!config.IsComplete || PlaytestConsent.Choice != PlaytestChoice.Unknown) return;
            if (SceneManager.GetActiveScene().name != TrashTD.Core.SceneNames.MainMenu || PlaytestUi.IsShowing)
            {
                consentPromptTime = -1f;
                return;
            }

            if (consentPromptTime < 0f) consentPromptTime = Time.unscaledTime + 2f;
            if (Time.unscaledTime >= consentPromptTime) PlaytestUi.ShowConsent();
        }
    }
}
