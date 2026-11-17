using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TrashTD.UI;
using TrashTD.Core.GameLoop;
using static TrashTD.UI.UiKit;

namespace TrashTD.Audio
{
    /// <summary>
    /// Settings panel shared by menu and gameplay (its layout is a prefab when one is baked, otherwise built in code; see
    /// <see cref="BuildPanelForPrefab"/>), in four tabs: AUDIO (music, SFX), DISPLAY
    /// (resolution and window mode, CRT style, brightness; see GameGraphicsSettings), CONTROLS (operator
    /// placement scheme with animated diagrams; see ControlSettings) and GAMEPLAY (notices; see
    /// GameplaySettings). The class keeps its original name because several screens create it.
    /// Styled as the green terminal used by the main menu and stage selector.
    /// </summary>
    public sealed partial class AudioSettingsUI : MonoBehaviour
    {
        private enum Tab { Audio, Display, Controls, Gameplay }
        private static readonly string[] TabLabels = { "AUDIO", "DISPLAY", "CONTROLS", "GAMEPLAY" };
        private static Tab lastTab = Tab.Audio;

        // Terminal palette (matches MainMenuController)
        private static readonly Color ButtonColor = new Color(0.10f, 0.13f, 0.18f, 1f);
        private static readonly Color AccentColor = new Color(0.18f, 0.82f, 0.45f, 1f);
        private static readonly Color AccentBright = new Color(0.32f, 1.00f, 0.64f, 1f);
        private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.62f);
        private static readonly Color CardColor = new Color(0.016f, 0.042f, 0.028f, 0.98f);
        private static readonly Color HeaderColor = new Color(0.030f, 0.090f, 0.055f, 1f);
        private static readonly Color TerminalButton = new Color(0.035f, 0.090f, 0.060f, 1f);
        private static readonly Color TrackColor = new Color(0.020f, 0.060f, 0.040f, 1f);
        private static readonly Color LineColor = new Color(0.22f, 0.91f, 0.50f, 0.45f);
        private static readonly Color LabelColor = new Color(0.45f, 0.78f, 0.56f, 1f);
        private static readonly Color HintColor = new Color(0.45f, 0.78f, 0.56f, 0.7f);
        private static readonly Color SelectedLabel = new Color(0.02f, 0.10f, 0.05f, 1f);

        // Layout, in card pixels from the top-left. PressStart2P glyphs are as wide as the font size,
        // so every text box is sized from its longest string (e.g. "BRIGHTNESS" at 14 px = 140 px).
        private const float CardWidth = 900f;
        private const float CardHeight = 620f;
        private const float HeaderHeight = 56f;
        private const float TabY = 72f;
        private const float TabHeight = 40f;
        private const float TabWidth = 190f;
        private const float FooterY = 540f;
        private const float Padding = 40f;
        private const float LabelWidth = 220f;
        private const float ControlX = Padding + LabelWidth + 14f;      // 270
        private const float ValueWidth = 72f;
        private const float ControlWidth = CardWidth - Padding - ValueWidth - 12f - ControlX; // slider width
        private const float SegmentWidth = (CardWidth - Padding - ControlX - 2f * 12f) / 3f;
        private const int LabelSize = 14;
        private const float ShowDuration = 0.18f;

        private GameObject panel;
        private RectTransform card;
        private CanvasGroup panelGroup;
        private float showTime = ShowDuration;
        private Slider musicSlider;
        private Slider sfxSlider;
        private Slider brightnessSlider;
        private Text musicValueText;
        private Text sfxValueText;
        private Text brightnessValueText;
        private Button fullTvButton;
        private Button classicButton;
        private Button offButton;
        private Text crtStyleHint;
        private Text resolutionText;
        private Button resolutionApplyButton;
        private Button resolutionPrevButton;
        private Button resolutionNextButton;
        private Button fullscreenButton;
        private Button windowedButton;
        private readonly List<Vector2Int> resolutions = new List<Vector2Int>();
        private int resolutionIndex;
        private Vector2Int shownScreenSize;
        private Button noticeOnButton;
        private Button noticeOffButton;
        private Button hideHudOnButton;
        private Button hideHudOffButton;
        private Button resetProgressButton;
        private Button closeButton;
        private GameObject editorHint;
        private Text resetProgressHint;
        private float resetConfirmUntil;   // unscaled time until which the RESET button asks "SURE?"
        private int shownResetState = -1;
        private const string MainMenuSceneName = TrashTD.Core.SceneNames.MainMenu;
        private const float ResetConfirmSeconds = 4f;
        private static readonly Color DangerColor = new Color(0.55f, 0.12f, 0.12f, 1f);
        private readonly Button[] tabButtons = new Button[4];
        private readonly GameObject[] pages = new GameObject[4];
        private readonly PlacementSchemeOption[] schemeOptions = new PlacementSchemeOption[3];
        private Transform pageRoot; // rows are built into the page being filled
        public Button SettingsButton { get; private set; }
        public bool IsOpen => panel != null && panel.activeSelf;
        public void Close() => Hide();

        public static AudioSettingsUI Create(
            Transform canvasRoot,
            Transform buttonParent,
            Action<Button> styleButton,
            Action<RectTransform> positionButton)
        {
            if (canvasRoot == null || buttonParent == null) return null;

            GameObject root = new GameObject("AudioSettingsUI", typeof(RectTransform));
            root.transform.SetParent(canvasRoot, false);
            var settings = root.AddComponent<AudioSettingsUI>();
            settings.Build(buttonParent, styleButton, positionButton);
            return settings;
        }

        /// <summary>Where the baked layout lives (Resources, so a build can load it). Missing = the layout is built in code.</summary>
        public const string PrefabAssetPath = "Assets/Resources/UI/Prefabs/SettingsPanel.prefab";
        private const string PrefabResourcePath = "UI/Prefabs/SettingsPanel";

        private void Build(Transform buttonParent, Action<Button> styleButton, Action<RectTransform> positionButton)
        {
            SettingsButton = CreateButton(buttonParent, "AudioSettingsButton", "SETTINGS", new Vector2(200f, 48f));
            styleButton?.Invoke(SettingsButton);
            positionButton?.Invoke(SettingsButton.GetComponent<RectTransform>());
            SettingsButton.onClick.AddListener(Show);

            // The panel's layout comes from the baked prefab when there is one (TRASH TD > UI > Bake Settings Panel
            // Prefab, then edit it in Prefab Mode); otherwise it is built in code. Either way the same hierarchy is
            // found by name, and everything dynamic (listeners, sounds, the placement diagrams) is attached after.
            Transform canvasRoot = transform.parent;
            if (!TryCreatePanelFromPrefab(canvasRoot))
            {
                BuildLayout(canvasRoot);
                ResolveReferences(panel, out _);
            }
            Wire();

            RefreshGraphicsControls();
            RefreshControls();
            RefreshGameplay();
            SyncResolutions();
            SelectTab(lastTab);
            panel.SetActive(false);
        }

        private bool TryCreatePanelFromPrefab(Transform canvasRoot)
        {
            GameObject prefab = Resources.Load<GameObject>(PrefabResourcePath);
            if (prefab == null) return false;

            GameObject instance = Instantiate(prefab, canvasRoot, false);
            instance.name = "AudioSettingsPanel";
            if (ResolveReferences(instance, out string missing)) return true;

            Debug.LogError("AudioSettingsUI: the baked prefab (" + PrefabAssetPath + ") is missing " + missing +
                           ". Building the panel in code instead; rebake it with TRASH TD > UI > Bake Settings Panel Prefab.");
            instance.SetActive(false);
            Destroy(instance);
            return false;
        }
    }
}
