using System;
using UnityEngine;

namespace TrashTD.Playtest
{
    public enum PlaytestChoice
    {
        /// <summary>Not asked yet.</summary>
        Unknown = 0,
        Allowed = 1,
        Declined = 2
    }

    /// <summary>
    /// The player's answer to "share anonymous playtest data?", kept in PlayerPrefs. Nothing is ever recorded or sent
    /// until it is Allowed, and it can be changed any time in Settings.
    /// </summary>
    public static class PlaytestConsent
    {
        private const string Key = "playtest.consent";

        /// <summary>Raised after the answer changes.</summary>
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlayMode() => Changed = null;

        public static PlaytestChoice Choice
        {
            get
            {
                int saved = PlayerPrefs.GetInt(Key, (int)PlaytestChoice.Unknown);
                return Enum.IsDefined(typeof(PlaytestChoice), saved) ? (PlaytestChoice)saved : PlaytestChoice.Unknown;
            }
            set
            {
                if (Choice == value) return;
                PlayerPrefs.SetInt(Key, (int)value);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        public static bool Allowed => Choice == PlaytestChoice.Allowed;
    }
}
