using UnityEngine;

namespace TrashTD.UI
{
    /// <summary>
    /// Player gameplay options (Settings → Gameplay), saved in PlayerPrefs.
    /// </summary>
    public static class GameplaySettings
    {
        private const string OpeningRerollNoticeKey = "gameplay.openingRerollNotice";
        private const string HideHudKey = "gameplay.hideHud";
        private const string PauseOnFocusLossKey = "gameplay.pauseOnFocusLoss";

        /// <summary>
        /// Pause a running stage when the game window loses focus (alt-tab, another window clicked), so enemies don't
        /// walk through while the player is away. On in builds. Off by default in the editor, where the Game view loses
        /// focus every time another editor window is clicked: TRASH TD > Pause On Focus Loss turns it on there.
        /// </summary>
        public static bool PauseOnFocusLoss
        {
            get => PlayerPrefs.GetInt(PauseOnFocusLossKey, Application.isEditor ? 0 : 1) == 1;
            set
            {
                PlayerPrefs.SetInt(PauseOnFocusLossKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// Hide the gameplay HUD for a clean view of the map (Settings → Gameplay, or H in game). Off by default.
        /// GameplayHUDUI hides the HUD only while a wave or preparation is being played; it shows it again on pause,
        /// in the draft, in the tutorial and on the results screen, and hides it again afterwards while this is on.
        /// </summary>
        public static bool HideHud
        {
            get => PlayerPrefs.GetInt(HideHudKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(HideHudKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// Warn before rerolling the opening draft, which only deals 1★ operators (CardDraftOverlayUI). The
        /// notice's "don't show again" box turns it off; Settings → Gameplay turns it back on.
        /// </summary>
        public static bool ShowOpeningRerollNotice
        {
            get => PlayerPrefs.GetInt(OpeningRerollNoticeKey, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(OpeningRerollNoticeKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }
    }
}
