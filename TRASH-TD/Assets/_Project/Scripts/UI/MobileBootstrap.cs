using UnityEngine;

namespace TrashTD.UI
{
    /// <summary>
    /// Start-up settings for phones and tablets (Android builds). Does nothing on desktop and in the editor.
    /// The game is a landscape game: portrait is switched off, the screen stays awake while playing, and the frame
    /// rate is capped at the player's choice (60 by default, Settings > Display) so the phone does not run hot.
    /// </summary>
    public static class MobileBootstrap
    {
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
            ApplyFrameRate();
            MobileSettings.Changed += ApplyFrameRate;

            TouchTargets.Create();
            MobileRenderRate.Create();
        }

        /// <summary>Caps the frame rate at the player's choice (30, 60 or 90).</summary>
        private static void ApplyFrameRate()
        {
            Application.targetFrameRate = MobileSettings.FrameRate;
        }
    }
}
