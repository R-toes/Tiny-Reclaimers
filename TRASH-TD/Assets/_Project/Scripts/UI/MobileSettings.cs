using System;
using UnityEngine;

namespace TrashTD.UI
{
    /// <summary>How big the touch-sized panels and buttons are drawn (Settings > Display, phones only).</summary>
    public enum UiSize
    {
        Small = 0,
        Normal = 1,
        Large = 2
    }

    /// <summary>How sharp the game picture is drawn on a phone: a smaller picture is stretched to the screen and costs less battery.</summary>
    public enum PictureQuality
    {
        Full = 0,
        Medium = 1,
        Low = 2
    }

    /// <summary>
    /// Options that only exist on a phone or tablet (Settings > Display), saved in PlayerPrefs: UI size, frame rate and
    /// picture quality. They do nothing on desktop. <see cref="Changed"/> fires after any change so the game applies it
    /// at once (MobileBootstrap sets the frame rate; CrtDisplay resizes the picture; the HUD reads the sizes each frame).
    /// </summary>
    public static class MobileSettings
    {
        private const string UiSizeKey = "mobile.uiSize";
        private const string FrameRateKey = "mobile.frameRate";
        private const string PictureKey = "mobile.pictureQuality";

        public const int DefaultFrameRate = 60;
        public static readonly int[] FrameRates = { 30, 60, 90 };

        private static bool loaded;
        private static UiSize uiSize = UiSize.Normal;
        private static int frameRate = DefaultFrameRate;
        private static PictureQuality picture = PictureQuality.Full;

        /// <summary>Raised after any option changes.</summary>
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlayMode()
        {
            loaded = false;
            Changed = null;
        }

        // ---- pure rules (tested) ----

        /// <summary>Multiplier on the touch sizes (MobileUi): Small 0.8, Normal 1, Large 1.3.</summary>
        public static float UiSizeMultiplier(UiSize size)
        {
            switch (size)
            {
                case UiSize.Small: return 0.8f;
                case UiSize.Large: return 1.3f;
                default: return 1f;
            }
        }

        /// <summary>Share of the screen size the game picture is drawn at: Full 1, Medium 0.75, Low 0.5.</summary>
        public static float PictureScale(PictureQuality quality)
        {
            switch (quality)
            {
                case PictureQuality.Medium: return 0.75f;
                case PictureQuality.Low: return 0.5f;
                default: return 1f;
            }
        }

        /// <summary>The allowed frame rate nearest to <paramref name="value"/> (a saved value from an older version may not be listed).</summary>
        public static int ClampFrameRate(int value)
        {
            int best = DefaultFrameRate;
            int bestDistance = int.MaxValue;
            foreach (int allowed in FrameRates)
            {
                int distance = Math.Abs(allowed - value);
                if (distance < bestDistance)
                {
                    best = allowed;
                    bestDistance = distance;
                }
            }
            return best;
        }

        // ---- saved options ----

        public static UiSize UiSizeSetting
        {
            get { Load(); return uiSize; }
            set
            {
                Load();
                if (uiSize == value) return;
                uiSize = value;
                PlayerPrefs.SetInt(UiSizeKey, (int)value);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        public static int FrameRate
        {
            get { Load(); return frameRate; }
            set
            {
                Load();
                int clamped = ClampFrameRate(value);
                if (frameRate == clamped) return;
                frameRate = clamped;
                PlayerPrefs.SetInt(FrameRateKey, clamped);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        public static PictureQuality Picture
        {
            get { Load(); return picture; }
            set
            {
                Load();
                if (picture == value) return;
                picture = value;
                PlayerPrefs.SetInt(PictureKey, (int)value);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        /// <summary>Multiplier on the touch sizes right now (1 off a phone).</summary>
        public static float CurrentUiSizeMultiplier => Application.isMobilePlatform ? UiSizeMultiplier(UiSizeSetting) : 1f;

        /// <summary>Scale of the game picture right now (1 off a phone).</summary>
        public static float CurrentPictureScale => Application.isMobilePlatform ? PictureScale(Picture) : 1f;

        private static void Load()
        {
            if (loaded) return;
            loaded = true;

            int size = PlayerPrefs.GetInt(UiSizeKey, (int)UiSize.Normal);
            uiSize = Enum.IsDefined(typeof(UiSize), size) ? (UiSize)size : UiSize.Normal;
            frameRate = ClampFrameRate(PlayerPrefs.GetInt(FrameRateKey, DefaultFrameRate));
            int quality = PlayerPrefs.GetInt(PictureKey, (int)PictureQuality.Full);
            picture = Enum.IsDefined(typeof(PictureQuality), quality) ? (PictureQuality)quality : PictureQuality.Full;
        }
    }
}
