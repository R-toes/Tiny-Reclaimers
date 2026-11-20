using System;
using UnityEngine;

namespace TrashTD.Playtest
{
    /// <summary>
    /// Where playtest data goes: a Google Apps Script "web app" that adds each event as a row to a Google Sheet (the
    /// script is in Docs/PLAYTEST_SETUP.md). Read from the text file <c>Assets/Resources/PlaytestConfig.json</c>, which
    /// is not committed (see .gitignore), so a build only sends data when that file was present at build time and a
    /// teammate's own test runs never reach the shared sheet. With no file, or no address, the whole playtest system
    /// stays off and the game asks nothing.
    /// </summary>
    [Serializable]
    public sealed class PlaytestConfig
    {
        public const string ResourceName = "PlaytestConfig";
        private const string ScriptHost = "https://script.google.com/macros/s/";

        /// <summary>The web app's address, ending in /exec.</summary>
        public string endpointUrl;

        /// <summary>Also send from the Unity editor (off by default, so development runs do not fill the sheet).</summary>
        public bool sendFromEditor;

        private static PlaytestConfig current;

        /// <summary>The config of this build (loaded once). Never null.</summary>
        public static PlaytestConfig Current
        {
            get
            {
                if (current != null) return current;
                TextAsset asset = Resources.Load<TextAsset>(ResourceName);
                current = asset != null ? Parse(asset.text) : new PlaytestConfig();
                return current;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlayMode() => current = null;

        /// <summary>True when there is a usable web app address: the system can send.</summary>
        public bool IsComplete => !string.IsNullOrEmpty(endpointUrl);

        /// <summary>Reads a config from JSON text and tidies the address. Bad text gives an empty config.</summary>
        public static PlaytestConfig Parse(string json)
        {
            PlaytestConfig config = null;
            try
            {
                config = string.IsNullOrWhiteSpace(json) ? null : JsonUtility.FromJson<PlaytestConfig>(json);
            }
            catch (Exception)
            {
                // fall through to an empty config
            }

            if (config == null) config = new PlaytestConfig();
            config.endpointUrl = NormalizeEndpointUrl(config.endpointUrl);
            return config;
        }

        /// <summary>
        /// The web app address as the game posts to it: https://script.google.com/macros/s/.../exec. A query or fragment is
        /// removed, and a test address ending in /dev becomes /exec. Anything else (another host, plain http) becomes empty.
        /// </summary>
        public static string NormalizeEndpointUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return string.Empty;
            url = url.Trim();
            if (!url.StartsWith(ScriptHost, StringComparison.Ordinal)) return string.Empty;

            int cut = url.IndexOfAny(new[] { '?', '#' });
            if (cut >= 0) url = url.Substring(0, cut);
            url = url.TrimEnd('/');

            if (url.EndsWith("/dev", StringComparison.Ordinal)) url = url.Substring(0, url.Length - 4) + "/exec";
            if (!url.EndsWith("/exec", StringComparison.Ordinal)) return string.Empty;
            return url.Length > ScriptHost.Length + "/exec".Length ? url : string.Empty;
        }
    }
}
