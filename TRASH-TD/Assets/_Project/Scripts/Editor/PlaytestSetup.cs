using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using TrashTD.Playtest;

namespace TrashTD.Editor
{
    /// <summary>
    /// TRASH TD > Playtest: creates <c>Assets/Resources/PlaytestConfig.json</c> from the Apps Script web app address
    /// copied to the clipboard, and sends a test row (the steps are in Docs/PLAYTEST_SETUP.md). The config file is
    /// git-ignored on purpose.
    /// </summary>
    public static class PlaytestSetup
    {
        private const string ConfigPath = "Assets/Resources/PlaytestConfig.json";

        [MenuItem("TRASH TD/Playtest/Create Config From Copied Web App Link")]
        public static void CreateFromClipboard()
        {
            string url = PlaytestConfig.NormalizeEndpointUrl(GUIUtility.systemCopyBuffer);
            if (string.IsNullOrEmpty(url))
            {
                EditorUtility.DisplayDialog("Playtest setup",
                    "The clipboard does not hold a web app address.\n\n" +
                    "In Apps Script choose Deploy > New deployment > Web app, deploy it, and copy the Web app URL " +
                    "(https://script.google.com/macros/s/.../exec). Then run this again. Docs/PLAYTEST_SETUP.md has the steps.", "OK");
                return;
            }

            if (File.Exists(ConfigPath) &&
                !EditorUtility.DisplayDialog("Playtest setup", "Replace the existing playtest config?", "Replace", "Cancel"))
                return;

            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
            var config = new PlaytestConfig { endpointUrl = url };
            File.WriteAllText(ConfigPath, JsonUtility.ToJson(config, true));
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Playtest setup",
                "Saved " + ConfigPath + ".\nBuilds made from now on send playtest data to your sheet (git ignores this file).\n\n" +
                "Next: TRASH TD > Playtest > Send Test Row.", "OK");
        }

        /// <summary>
        /// Posts one row straight to the web app (ignoring the player's answer and the editor switch), so the address and
        /// the script can be checked without playing. The result says what Google answered.
        /// </summary>
        [MenuItem("TRASH TD/Playtest/Send Test Row")]
        public static void SendTestRow()
        {
            if (!File.Exists(ConfigPath))
            {
                EditorUtility.DisplayDialog("Playtest test", "There is no config yet. Use Create Config From Copied Web App Link first.", "OK");
                return;
            }

            PlaytestConfig config = PlaytestConfig.Parse(File.ReadAllText(ConfigPath));
            if (!config.IsComplete)
            {
                EditorUtility.DisplayDialog("Playtest test", "The config has no valid web app address (it must start with https://script.google.com/macros/s/ and end in /exec).", "OK");
                return;
            }

            var entry = new PlaytestEntry
            {
                session = "test-" + System.Guid.NewGuid().ToString("N").Substring(0, 6),
                platform = "UnityMenuTest",
                build = Application.version,
                evt = "test",
                stage = "-",
                data = "{\"note\":\"test row sent from TRASH TD > Playtest > Send Test Row\"}"
            };
            var form = new WWWForm();
            foreach (var field in PlaytestQueue.BuildFields(entry)) form.AddField(field.Key, field.Value);

            UnityWebRequest request = UnityWebRequest.Post(config.endpointUrl, form);
            request.timeout = 30;
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();

            EditorApplication.CallbackFunction poll = null;
            poll = () =>
            {
                if (!operation.isDone) return;
                EditorApplication.update -= poll;

                string reply = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
                string message;
                if (request.result == UnityWebRequest.Result.Success)
                {
                    message = "The web app answered (HTTP " + request.responseCode + ").\n\nOpen the sheet: a row with Event \"test\" should be in the " +
                              "\"Playtest\" tab within a few seconds.\n\nIf there is no row, the reply was:\n" +
                              (reply.Length > 200 ? reply.Substring(0, 200) : reply);
                }
                else
                {
                    message = "It was not accepted: HTTP " + request.responseCode + " " + request.error + "\n\n" +
                              "401 or 403: deploy the web app with \"Who has access: Anyone\".\n" +
                              "404: the address is wrong, or the deployment was deleted.\n" +
                              "No response: no internet connection.";
                }
                request.Dispose();
                EditorUtility.DisplayDialog("Playtest test", message, "OK");
            };
            EditorApplication.update += poll;
        }

        /// <summary>Shows why events are or are not being sent. Use it while the game is running (Play mode).</summary>
        [MenuItem("TRASH TD/Playtest/Show Status")]
        public static void ShowStatus()
        {
            EditorUtility.DisplayDialog("Playtest status" + (Application.isPlaying ? string.Empty : " (not playing)"),
                Application.isPlaying
                    ? PlaytestLog.Status
                    : "Start Play mode first (from the main menu scene), then run this again while the game runs.", "OK");
        }

        [MenuItem("TRASH TD/Playtest/Open Config")]
        public static void OpenConfig()
        {
            Object asset = AssetDatabase.LoadAssetAtPath<TextAsset>(ConfigPath);
            if (asset == null) EditorUtility.DisplayDialog("Playtest setup", "There is no config yet. Use Create Config From Copied Web App Link first.", "OK");
            else AssetDatabase.OpenAsset(asset);
        }
    }
}
