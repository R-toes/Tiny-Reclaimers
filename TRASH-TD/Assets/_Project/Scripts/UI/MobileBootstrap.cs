using UnityEngine;

namespace TrashTD.UI
{
    /// <summary>
    /// Start-up settings for phones and tablets (Android builds). Does nothing on desktop and in the editor.
    /// The game is a landscape game: portrait is switched off, the screen stays awake while playing, and the frame
    /// rate is capped at 60 so the phone does not run hot (sleeping on a menu is no reason to drain the battery).
    /// </summary>
    public static class MobileBootstrap
    {
        public const int MobileTargetFrameRate = 60;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            if (!Application.isMobilePlatform) return;

            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;

            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = MobileTargetFrameRate;
        }
    }
}
