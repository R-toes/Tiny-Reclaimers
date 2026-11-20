using UnityEngine;

namespace TrashTD.UI
{
    /// <summary>
    /// Sizes for the touch version, in one place so they are easy to tune after testing on a phone. Every value
    /// returns the desktop value unchanged when the game is not running on a phone or tablet. Sizes are in canvas
    /// units (the HUD canvas is 1920 x 1080); on a typical phone one unit is about 0.07 mm, so a 44-unit button
    /// is only about 3 mm wide.
    /// </summary>
    public static class MobileUi
    {
        /// <summary>The info tab above a selected operator (name, HP, ATK, SHOW MORE) is drawn this many times bigger.</summary>
        public const float InfoTabScale = 1.6f;

        /// <summary>Side of the square retreat button beside the info tab (44 on desktop).</summary>
        public const float RetreatButtonSize = 92f;

        /// <summary>The SHOW MORE details panel scale (0.82 on desktop; the panel is 500 x 640 before scaling).</summary>
        public const float DetailsPanelScale = 1.3f;

        /// <summary>Every button, toggle and slider gets at least this much room to be tapped (see TouchTargets).</summary>
        public const float MinTouchTarget = 84f;

        /// <summary>Most a tap area grows on each side, so crowded buttons do not start overlapping their neighbours.</summary>
        public const float MaxTouchPadding = 18f;

        /// <summary>True on a phone or tablet build.</summary>
        public static bool IsMobile => PointerInput.IsMobileDevice;

        // The player's UI size option (Settings > Display) multiplies the touch sizes
        public static float InfoTab => Pick(IsMobile, 1f, InfoTabScale * MobileSettings.CurrentUiSizeMultiplier);
        public static float Retreat => Pick(IsMobile, 44f, RetreatButtonSize * MobileSettings.CurrentUiSizeMultiplier);
        public static float Details(float desktopScale) => Pick(IsMobile, desktopScale, DetailsPanelScale * MobileSettings.CurrentUiSizeMultiplier);

        /// <summary>The desktop value, or the touch value on a phone. Pure, so the rule is tested.</summary>
        public static float Pick(bool mobile, float desktop, float touch) => mobile ? touch : desktop;

        /// <summary>
        /// How far (in the element's own units) a tap area must grow on each side so an element of
        /// <paramref name="size"/> canvas units has <see cref="MinTouchTarget"/> to be tapped. Zero when it is already
        /// big enough; capped at <see cref="MaxTouchPadding"/>. <paramref name="unitsPerCanvasUnit"/> converts the
        /// canvas amount into the element's own units (1 when it is not scaled).
        /// </summary>
        public static float TouchPadding(float size, float unitsPerCanvasUnit)
        {
            float missing = Mathf.Max(0f, MinTouchTarget - size) * 0.5f;
            return Mathf.Min(missing, MaxTouchPadding) * Mathf.Max(0.0001f, unitsPerCanvasUnit);
        }
    }
}
