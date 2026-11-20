using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TrashTD.UI;
using TrashTD.Core.GameLoop;
using static TrashTD.UI.UiKit;

namespace TrashTD.Audio
{
    // The panel layout: building it in code or from the baked prefab, finding its named parts, and wiring its behaviour.
    public partial class AudioSettingsUI
    {
        /// <summary>
        /// Builds the panel's layout in code under <paramref name="canvasRoot"/> and returns it, for the editor's bake command
        /// (UiPrefabBaker) and the tests. Nothing dynamic is attached: no listeners, no feedback components.
        /// </summary>
        public static GameObject BuildPanelForPrefab(Transform canvasRoot)
        {
            AudioSettingsUI host = new GameObject("AudioSettingsUI.Bake", typeof(RectTransform)).AddComponent<AudioSettingsUI>();
            host.enabled = false;
            host.BuildLayout(canvasRoot);
            GameObject result = host.panel;
            DestroyHost(host);
            return result;
        }

        /// <summary>True when <paramref name="candidate"/> has every named object this class binds to; otherwise lists what is missing.</summary>
        public static bool ValidatePanel(GameObject candidate, out string missing)
        {
            AudioSettingsUI host = new GameObject("AudioSettingsUI.Validate", typeof(RectTransform)).AddComponent<AudioSettingsUI>();
            host.enabled = false;
            bool ok = host.ResolveReferences(candidate, out missing);
            DestroyHost(host);
            return ok;
        }

        private static void DestroyHost(AudioSettingsUI host)
        {
            if (Application.isPlaying) Destroy(host.gameObject);
            else DestroyImmediate(host.gameObject);
        }

        /// <summary>
        /// The visual layout only: every element has a stable name that <see cref="ResolveReferences"/> looks up. Text that
        /// changes at runtime starts empty or at a default; <see cref="Wire"/> and the Refresh methods fill it in.
        /// </summary>
        private void BuildLayout(Transform canvasRoot)
        {
            // Full-screen dimmer (blocks clicks to whatever is underneath) holding the card
            panel = new GameObject("AudioSettingsPanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            panel.transform.SetParent(canvasRoot, false);
            SetStretch(panel.GetComponent<RectTransform>());
            panel.GetComponent<Image>().color = DimColor;
            panelGroup = panel.GetComponent<CanvasGroup>();

            Image cardImage = CreateImage(panel.transform, "SettingsCard", CardColor);
            card = cardImage.rectTransform;
            SetPosition(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CardWidth, CardHeight));
            cardImage.raycastTarget = true;
            Outline cardOutline = cardImage.gameObject.AddComponent<Outline>();
            cardOutline.effectColor = LineColor;
            cardOutline.effectDistance = new Vector2(2f, -2f);
            AddCornerBrackets(card, 24f, 3f, AccentBright, -7f);

            // Header bar
            Image header = CreateImage(card, "Header", HeaderColor);
            PlaceTopLeft(header.rectTransform, 0f, 0f, CardWidth, HeaderHeight);
            Image headerLine = CreateImage(card, "HeaderLine", LineColor);
            PlaceTopLeft(headerLine.rectTransform, 0f, HeaderHeight, CardWidth, 2f);

            Text title = CreateText(header.transform, "SETTINGS", 20, TextAnchor.MiddleLeft, AccentBright);
            title.gameObject.name = "Title";
            PlaceTopLeft(title.rectTransform, Padding - 8f, 0f, 400f, HeaderHeight);
            Text escHint = CreateText(header.transform, "[ESC] CLOSE", 11, TextAnchor.MiddleRight, HintColor);
            escHint.gameObject.name = "EscHint";
            PlaceTopLeft(escHint.rectTransform, CardWidth - Padding + 8f - 300f, 0f, 300f, HeaderHeight);

            // ── Tabs ──
            for (int i = 0; i < TabLabels.Length; i++)
            {
                tabButtons[i] = CreateTerminalButton(card, TabLabels[i] + "Tab", TabLabels[i], 13);
                PlaceTopLeft(tabButtons[i].GetComponent<RectTransform>(), Padding + i * (TabWidth + 10f), TabY, TabWidth, TabHeight);
            }
            Image tabLine = CreateImage(card, "TabLine", new Color(LineColor.r, LineColor.g, LineColor.b, 0.35f));
            PlaceTopLeft(tabLine.rectTransform, Padding, TabY + TabHeight + 6f, CardWidth - 2f * Padding, 2f);

            // ── Audio ──
            pageRoot = CreatePage(Tab.Audio);
            CreateSectionHeader("VOLUME", 160f);
            CreateSliderRow("MUSIC", 210f, 0f, 1f, 0.45f, out _);
            CreateSliderRow("SFX", 262f, 0f, 1f, 0.45f, out _);

            // ── Display ──
            pageRoot = CreatePage(Tab.Display);
            CreateSectionHeader("SCREEN", 150f);
            CreateResolutionRow(196f);
            CreateRowLabel("MODE", 244f);
            CreateSegmentButton("FullscreenButton", "FULLSCREEN", 0, 244f);
            CreateSegmentButton("WindowedButton", "WINDOWED", 1, 244f);
#if UNITY_EDITOR
            Text editorHintText = CreateText(pageRoot, "(In the editor, the Game view sets these.)", 9, TextAnchor.MiddleLeft, HintColor);
            editorHintText.gameObject.name = "EditorHint"; // Wire hides it outside the editor (a baked prefab ships in builds too)
            PlaceTopLeft(editorHintText.rectTransform, ControlX, 268f, CardWidth - Padding - ControlX, 18f);
#endif

            CreateSectionHeader("CRT STYLE", 310f);
            CreateRowLabel("STYLE", 356f);
            CreateStyleButton("CrtFullTvButton", "FULL TV", 0, 356f);
            CreateStyleButton("CrtClassicButton", "CLASSIC", 1, 356f);
            CreateStyleButton("CrtOffButton", "OFF", 2, 356f);

            Text crtHint = CreateText(pageRoot, string.Empty, 11, TextAnchor.UpperLeft, HintColor);
            crtHint.gameObject.name = "CrtStyleHint";
            crtHint.horizontalOverflow = HorizontalWrapMode.Wrap;
            crtHint.verticalOverflow = VerticalWrapMode.Overflow;
            crtHint.lineSpacing = 1.4f;
            PlaceTopLeft(crtHint.rectTransform, ControlX, 388f, CardWidth - Padding - ControlX, 40f);

            CreateSectionHeader("PICTURE", 450f);
            CreateSliderRow("BRIGHTNESS", 494f,
                GameGraphicsSettings.MinBrightness, GameGraphicsSettings.MaxBrightness, GameGraphicsSettings.Brightness,
                out _);

            // ── Controls ── (the three placement-scheme options are animated and generated at runtime: see Wire)
            pageRoot = CreatePage(Tab.Controls);
            CreateSectionHeader("OPERATOR PLACEMENT", 150f);
            Text placementHint = CreateText(pageRoot, "After a card is dropped (or clicked, then a tile clicked):", 10, TextAnchor.MiddleLeft, HintColor);
            placementHint.gameObject.name = "PlacementHint";
            PlaceTopLeft(placementHint.rectTransform, Padding, 168f, CardWidth - 2f * Padding, 20f);
            Text keysHint = CreateText(pageRoot, "KEYS: WASD / ARROWS FACE  |  ENTER PLACES  |  ESC / RIGHT-CLICK CANCELS", 9, TextAnchor.MiddleCenter, HintColor);
            keysHint.gameObject.name = "KeysHint";
            PlaceTopLeft(keysHint.rectTransform, Padding, 504f, CardWidth - 2f * Padding, 20f);

            // ── Gameplay ──
            pageRoot = CreatePage(Tab.Gameplay);
            CreateSectionHeader("NOTICES", 160f);
            CreateRowLabel("REROLL NOTICE", 210f);
            CreateSegmentButton("RerollNoticeOnButton", "ON", 0, 210f);
            CreateSegmentButton("RerollNoticeOffButton", "OFF", 1, 210f);
            Text noticeHint = CreateText(pageRoot,
                "Warns before you reroll the first draft of a run, which only deals 1-star operators.",
                11, TextAnchor.UpperLeft, HintColor);
            noticeHint.gameObject.name = "RerollNoticeHint";
            noticeHint.horizontalOverflow = HorizontalWrapMode.Wrap;
            noticeHint.verticalOverflow = VerticalWrapMode.Overflow;
            noticeHint.lineSpacing = 1.4f;
            PlaceTopLeft(noticeHint.rectTransform, ControlX, 242f, CardWidth - Padding - ControlX, 40f);

            CreateSectionHeader("INTERFACE", 310f);
            CreateRowLabel("HIDE UI", 360f);
            CreateSegmentButton("HideHudOnButton", "ON", 0, 360f);
            CreateSegmentButton("HideHudOffButton", "OFF", 1, 360f);
            Text hideHudHint = CreateText(pageRoot,
                "Hides the HUD while you play, for a clean view of the map. Press H in a stage to switch it too.",
                11, TextAnchor.UpperLeft, HintColor);
            hideHudHint.gameObject.name = "HideHudHint";
            hideHudHint.horizontalOverflow = HorizontalWrapMode.Wrap;
            hideHudHint.verticalOverflow = VerticalWrapMode.Overflow;
            hideHudHint.lineSpacing = 1.4f;
            PlaceTopLeft(hideHudHint.rectTransform, ControlX, 392f, CardWidth - Padding - ControlX, 40f);

            CreateSectionHeader("SAVE DATA", 450f);
            CreateRowLabel("PROGRESS", 492f);
            CreateSegmentButton("ResetProgressButton", "RESET", 0, 492f);
            Text resetHint = CreateText(pageRoot, string.Empty, 11, TextAnchor.UpperLeft, HintColor);
            resetHint.gameObject.name = "ResetProgressHint";
            resetHint.horizontalOverflow = HorizontalWrapMode.Wrap;
            resetHint.verticalOverflow = VerticalWrapMode.Overflow;
            resetHint.lineSpacing = 1.4f;
            PlaceTopLeft(resetHint.rectTransform, ControlX + SegmentWidth + 12f, 474f, 2f * SegmentWidth + 12f, 40f);
            pageRoot = card;

            // Footer
            Image footerLine = CreateImage(card, "FooterLine", new Color(LineColor.r, LineColor.g, LineColor.b, 0.25f));
            PlaceTopLeft(footerLine.rectTransform, Padding, FooterY, CardWidth - 2f * Padding, 2f);
            Button close = CreateTerminalButton(card, "CloseButton", "CLOSE", 16);
            PlaceTopLeft(close.GetComponent<RectTransform>(), (CardWidth - 220f) * 0.5f, FooterY + 18f, 220f, 48f);
            AddCornerBrackets(close.GetComponent<RectTransform>(), 10f, 2f, LineColor);
        }

        /// <summary>
        /// Finds the named objects of a panel (built in code or instantiated from the prefab) and fills this class's fields.
        /// False, with the missing names, when any is absent or lacks its component.
        /// </summary>
        private bool ResolveReferences(GameObject root, out string missing)
        {
            var byName = new Dictionary<string, Transform>();
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) byName.TryAdd(child.name, child);

            var missingNames = new List<string>();
            T Find<T>(string objectName) where T : Component
            {
                if (byName.TryGetValue(objectName, out Transform found))
                {
                    T component = found.GetComponent<T>();
                    if (component != null) return component;
                }
                missingNames.Add(objectName + " (" + typeof(T).Name + ")");
                return null;
            }

            panel = root;
            panelGroup = root.GetComponent<CanvasGroup>();
            if (panelGroup == null) missingNames.Add("CanvasGroup on the root");
            card = Find<RectTransform>("SettingsCard");

            for (int i = 0; i < TabLabels.Length; i++)
            {
                tabButtons[i] = Find<Button>(TabLabels[i] + "Tab");
                RectTransform page = Find<RectTransform>(TabLabels[i] + "Page");
                pages[i] = page != null ? page.gameObject : null;
            }

            musicSlider = Find<Slider>("MUSICSlider");
            sfxSlider = Find<Slider>("SFXSlider");
            brightnessSlider = Find<Slider>("BRIGHTNESSSlider");
            musicValueText = Find<Text>("MUSICValue");
            sfxValueText = Find<Text>("SFXValue");
            brightnessValueText = Find<Text>("BRIGHTNESSValue");

            resolutionPrevButton = Find<Button>("ResolutionPrevButton");
            resolutionNextButton = Find<Button>("ResolutionNextButton");
            resolutionApplyButton = Find<Button>("ResolutionApplyButton");
            resolutionText = Find<Text>("ResolutionValue");
            fullscreenButton = Find<Button>("FullscreenButton");
            windowedButton = Find<Button>("WindowedButton");
            fullTvButton = Find<Button>("CrtFullTvButton");
            classicButton = Find<Button>("CrtClassicButton");
            offButton = Find<Button>("CrtOffButton");
            crtStyleHint = Find<Text>("CrtStyleHint");

            noticeOnButton = Find<Button>("RerollNoticeOnButton");
            noticeOffButton = Find<Button>("RerollNoticeOffButton");
            hideHudOnButton = Find<Button>("HideHudOnButton");
            hideHudOffButton = Find<Button>("HideHudOffButton");
            resetProgressButton = Find<Button>("ResetProgressButton");
            resetProgressHint = Find<Text>("ResetProgressHint");
            closeButton = Find<Button>("CloseButton");

            // Optional: only present when the layout was built in the editor
            editorHint = byName.TryGetValue("EditorHint", out Transform hint) ? hint.gameObject : null;

            pageRoot = card;
            missing = string.Join(", ", missingNames);
            return missingNames.Count == 0;
        }

        /// <summary>Everything dynamic: starting values, click listeners and sounds, and the animated placement options.</summary>
        private void Wire()
        {
            musicSlider.SetValueWithoutNotify(AudioManager.Instance != null ? AudioManager.Instance.MusicVolume : 0.45f);
            sfxSlider.SetValueWithoutNotify(AudioManager.Instance != null ? AudioManager.Instance.SfxVolume : 0.45f);
            brightnessSlider.minValue = GameGraphicsSettings.MinBrightness;
            brightnessSlider.maxValue = GameGraphicsSettings.MaxBrightness;
            brightnessSlider.SetValueWithoutNotify(GameGraphicsSettings.Brightness);
            if (editorHint != null) editorHint.SetActive(Application.isEditor);

            for (int i = 0; i < tabButtons.Length; i++)
            {
                Tab tab = (Tab)i;
                Bind(tabButtons[i], () => SelectTab(tab));
            }

            Bind(resolutionPrevButton, () => StepResolution(-1));
            Bind(resolutionNextButton, () => StepResolution(1));
            Bind(resolutionApplyButton, ApplyResolution);
            Bind(fullscreenButton, () => SetWindowMode(true));
            Bind(windowedButton, () => SetWindowMode(false));
            Bind(fullTvButton, () => SetCrtStyle(CrtStyle.FullTv));
            Bind(classicButton, () => SetCrtStyle(CrtStyle.Classic));
            Bind(offButton, () => SetCrtStyle(CrtStyle.Off));

            Bind(noticeOnButton, () => SetRerollNotice(true));
            Bind(noticeOffButton, () => SetRerollNotice(false));
            Bind(hideHudOnButton, () => SetHideHud(true));
            Bind(hideHudOffButton, () => SetHideHud(false));
            Bind(resetProgressButton, OnResetProgressClicked);
            Bind(closeButton, Hide, SfxId.UiBack);

            if (PointerInput.IsMobileDevice) HideDesktopScreenRows();

            // The placement options are animated diagrams made from generated sprites, so they are created here, on the CONTROLS page
            Transform controlsPage = pages[(int)Tab.Controls].transform;
            Vector2 optionSize = new Vector2(250f, 300f);
            float optionGap = (CardWidth - 2f * Padding - 3f * optionSize.x) * 0.5f;
            for (int i = 0; i < schemeOptions.Length; i++)
            {
                PlacementControlScheme scheme = (PlacementControlScheme)i;
                PlacementSchemeOption option = PlacementSchemeOption.Create(controlsPage, scheme, optionSize);
                PlaceTopLeft(option.Button.GetComponent<RectTransform>(), Padding + i * (optionSize.x + optionGap), 194f, optionSize.x, optionSize.y);
                Bind(option.Button, () => SetPlacementScheme(scheme));
                schemeOptions[i] = option;
            }

            musicSlider.onValueChanged.AddListener(value =>
            {
                AudioManager.Instance?.SetMusicVolume(value);
                RefreshValueTexts();
            });
            sfxSlider.onValueChanged.AddListener(value =>
            {
                AudioManager.Instance?.SetSfxVolume(value);
                RefreshValueTexts();
            });
            brightnessSlider.onValueChanged.AddListener(value =>
            {
                GameGraphicsSettings.Brightness = value;
                RefreshGraphicsControls();
            });
        }

        /// <summary>A click listener plus the terminal button feedback (hover, glow, click sound).</summary>
        private static void Bind(Button button, Action onClick, SfxId clickSound = SfxId.UiClick)
        {
            button.onClick.AddListener(() => onClick());
            AttachFeedback(button, clickSound);
        }

        /// <summary>A full-card container for one tab; rows use the card's own coordinates.</summary>
        private Transform CreatePage(Tab tab)
        {
            var page = new GameObject(TabLabels[(int)tab] + "Page", typeof(RectTransform));
            page.transform.SetParent(card, false);
            SetStretch(page.GetComponent<RectTransform>());
            pages[(int)tab] = page;
            return page.transform;
        }
    }
}
