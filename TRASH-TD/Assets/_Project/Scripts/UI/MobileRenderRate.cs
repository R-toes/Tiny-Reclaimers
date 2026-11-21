using TrashTD.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace TrashTD.UI
{
    /// <summary>
    /// Phone only. The title screen and the stage map hold still most of the time, but a phone with the CRT effect
    /// on was drawing them at the full frame rate anyway. While the main menu scene is up and nobody has touched
    /// the screen for a moment, this draws every second frame (<see cref="OnDemandRendering"/>): the game itself
    /// still updates at the chosen frame rate, only the drawing is skipped, so half the GPU work and battery are
    /// saved on a still picture. The first touch (and the second after it) switches back to full rate at once, so
    /// dragging, pinching and the menu transitions stay smooth. Gameplay is never throttled.
    /// </summary>
    public sealed class MobileRenderRate : MonoBehaviour
    {
        /// <summary>Seconds after the last touch before the menu starts drawing at the reduced rate.</summary>
        public const float IdleSeconds = 1.5f;

        /// <summary>The draw rate the idle menu aims for.</summary>
        public const int IdleRenderFps = 30;

        private float lastActiveTime;

        /// <summary>Creates the (single) controller. Called by MobileBootstrap on phones.</summary>
        public static void Create()
        {
            var go = new GameObject("MobileRenderRate");
            DontDestroyOnLoad(go);
            go.AddComponent<MobileRenderRate>();
        }

        /// <summary>
        /// How many updates pass per drawn frame. 1 = every frame. Only the idle main menu draws less often, aiming
        /// for <see cref="IdleRenderFps"/> (a 60 fps game draws every 2nd frame, 90 every 3rd, 30 every frame).
        /// </summary>
        public static int CalculateInterval(bool inMainMenu, float secondsSinceTouch, int updateFrameRate)
        {
            if (!inMainMenu || secondsSinceTouch < IdleSeconds) return 1;
            return Mathf.Max(1, Mathf.RoundToInt(updateFrameRate / (float)IdleRenderFps));
        }

        private void OnEnable()
        {
            lastActiveTime = Time.unscaledTime;
            SceneManager.activeSceneChanged += HandleSceneChanged;
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= HandleSceneChanged;
            OnDemandRendering.renderFrameInterval = 1;
        }

        private void HandleSceneChanged(Scene previous, Scene next)
        {
            // A new scene starts at full rate (its entrance animation, the first taps)
            lastActiveTime = Time.unscaledTime;
            OnDemandRendering.renderFrameInterval = 1;
        }

        private void Update()
        {
            if (PointerInput.IsPressed || PointerInput.PressedThisFrame || PointerInput.ReleasedThisFrame)
                lastActiveTime = Time.unscaledTime;

            bool inMainMenu = SceneManager.GetActiveScene().name == SceneNames.MainMenu;
            int interval = CalculateInterval(inMainMenu, Time.unscaledTime - lastActiveTime, MobileSettings.FrameRate);
            if (OnDemandRendering.renderFrameInterval != interval) OnDemandRendering.renderFrameInterval = interval;
        }
    }
}
