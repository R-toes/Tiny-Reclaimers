using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TrashTD.Core.GameLoop;
using TrashTD.Data;

namespace TrashTD.UI
{
    /// <summary>
    /// The act selection screen: one card per act in a horizontal scroll view, shown between the main menu and an act's stage
    /// map once there is more than one act (with a single act PLAY goes straight to its stage map). The cards are always the
    /// same size however many acts there are: they sit centred while they fit and scroll sideways (drag, mouse wheel or the
    /// slider underneath) when they do not. A card shows the act's own stage map art (Act 1's map as the default when the
    /// act has none), its name and zone, and how many stages are cleared; an act that is still a placeholder shows COMING SOON
    /// in place of the art, and a locked act shows what unlocks it. Clicking an open act opens its stage map.
    /// All sizes are for the 1920 x 1080 reference.
    /// </summary>
    public sealed class ActWorldMapScreen
    {
        // ---- The card: one size for every act
        private const float CardWidth = 800f;
        private const float CardScale = CardWidth / 576f;                // the card's inner measurements are written for 576 wide
        private const float CardPadding = 8f * CardScale;
        private const float ArtWidth = CardWidth - 2f * CardPadding;
        private const float ArtHeight = ArtWidth * 9f / 16f;             // the map art is 16:9
        private const float TextBlockHeight = 112f * CardScale;
        public const float CardHeight = CardPadding + ArtHeight + TextBlockHeight + CardPadding;
        public const float CardGap = 64f;

        // ---- The scroll view: its width at the reference size, and the room kept clear of the title block (top) and the footer (bottom)
        public const float ViewportMargin = 70f;
        public const float ViewportWidth = 1920f - 2f * ViewportMargin;
        private const float ViewportTop = 215f;
        private const float ViewportBottom = 205f;
        public const float ViewportHeight = 1080f - ViewportTop - ViewportBottom;
        private const float ContentPadding = 24f; // room beside the first and last card for their brackets and hover glow

        private const string DefaultHint = "CLICK AN ACT TO DEPLOY     [ESC] BACK";
        private const string ScrollHint = "DRAG, SCROLL OR USE THE SLIDER FOR MORE ACTS     [ESC] BACK";

        private static readonly Color BackgroundColor = new Color(0.02f, 0.05f, 0.03f, 1f);
        private static readonly Color AccentColor = new Color(0.18f, 0.82f, 0.45f, 1f);
        private static readonly Color AccentBright = new Color(0.32f, 1f, 0.64f, 1f);
        private static readonly Color PlateColor = new Color(0.012f, 0.035f, 0.024f, 0.97f);
        private static readonly Color ArtFrameColor = new Color(0.005f, 0.02f, 0.012f, 1f);
        private static readonly Color TextColor = new Color(0.62f, 0.86f, 0.70f, 1f);
        private static readonly Color TitleColor = new Color(1f, 0.82f, 0.25f, 1f);
        private static readonly Color LockedColor = new Color(0.55f, 0.58f, 0.56f, 1f);
        private static readonly Color RequirementColor = new Color(0.95f, 0.42f, 0.36f, 1f);
        private static readonly Color LockedArtTint = new Color(0.38f, 0.40f, 0.39f, 1f);
        private static readonly Color RuleColor = new Color(0.18f, 0.82f, 0.45f, 0.45f);
        private static readonly Color TrackColor = new Color(0.18f, 0.82f, 0.45f, 0.12f);
        private static readonly Color HandleColor = new Color(0.18f, 0.82f, 0.45f, 0.55f);

        public GameObject Root { get; private set; }

        private RectTransform viewport;
        private RectTransform content;
        private ScrollRect scroll;
        private Text hintText;
        private Button sandboxButton;
        private Action<ActData> openAct;
        private Action playHover;
        private Action playClick;
        private Action playDenied;

        // ─────────────────────────────────────────────────────
        //  Layout (static and pure, so it can be tested)
        // ─────────────────────────────────────────────────────

        /// <summary>Width of the scrolling content for this many acts: the cards, the gaps and a little room at both ends.</summary>
        public static float GetContentWidth(int count)
        {
            return Mathf.Max(0, count) * CardWidth + Mathf.Max(0, count - 1) * CardGap + 2f * ContentPadding;
        }

        /// <summary>True when this many acts do not fit the scroll view and so scroll.</summary>
        public static bool NeedsScrolling(int count) => GetContentWidth(count) > ViewportWidth;

        /// <summary>
        /// Horizontal centre of card <paramref name="index"/> of <paramref name="count"/>, in pixels from the middle of the
        /// scrolling content (the cards are centred in it; the content itself is centred in the view while it fits).
        /// </summary>
        public static float GetCardCentreX(int index, int count)
        {
            float cardsWidth = count * CardWidth + (count - 1) * CardGap;
            return -cardsWidth * 0.5f + CardWidth * 0.5f + index * (CardWidth + CardGap);
        }

        public static float GetCardWidth() => CardWidth;

        // ─────────────────────────────────────────────────────
        //  Building
        // ─────────────────────────────────────────────────────

        public static ActWorldMapScreen Create(Transform parent, Action<ActData> openAct, Action back, Action openSandbox,
            Action playHover, Action playClick, Action playDenied)
        {
            var screen = new ActWorldMapScreen
            {
                openAct = openAct,
                playHover = playHover,
                playClick = playClick,
                playDenied = playDenied
            };

            var root = new GameObject("ActWorldMapPanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            root.transform.SetParent(parent, false);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            UiKit.Stretch(rootRect);
            Image background = root.GetComponent<Image>();
            background.color = BackgroundColor;
            background.raycastTarget = true;
            screen.Root = root;

            // Terminal frame around the whole screen, like the stage map
            AddBrackets(rootRect, 46f, 4f, new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.55f), 18f);

            // Title block, centred: SELECT ACT, then CHOOSE WHERE TO DEPLOY between two short rules, then a divider
            Text title = UiKit.MakeText(root.transform, "Title", "SELECT ACT", 56, TextAnchor.MiddleCenter, TitleColor, FontStyle.Bold,
                HorizontalWrapMode.Overflow, VerticalWrapMode.Overflow, false);
            AnchorTopCentre(title.rectTransform, new Vector2(0f, -56f), new Vector2(1000f, 76f));
            Shadow titleShadow = title.gameObject.AddComponent<Shadow>();
            titleShadow.effectColor = new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.35f);
            titleShadow.effectDistance = new Vector2(0f, -4f);

            Text subtitle = UiKit.MakeText(root.transform, "Subtitle", "CHOOSE WHERE TO DEPLOY", 16, TextAnchor.MiddleCenter, TextColor, FontStyle.Normal,
                HorizontalWrapMode.Overflow, VerticalWrapMode.Overflow, false);
            AnchorTopCentre(subtitle.rectTransform, new Vector2(0f, -140f), new Vector2(1000f, 26f));
            float ruleOffset = subtitle.preferredWidth * 0.5f + 22f;
            for (int side = -1; side <= 1; side += 2)
            {
                Image rule = UiKit.MakeImage(root.transform, side < 0 ? "RuleLeft" : "RuleRight", RuleColor, null, false);
                RectTransform ruleRect = rule.rectTransform;
                ruleRect.anchorMin = ruleRect.anchorMax = new Vector2(0.5f, 1f);
                ruleRect.pivot = new Vector2(side < 0 ? 1f : 0f, 0.5f);
                ruleRect.anchoredPosition = new Vector2(side * ruleOffset, -153f);
                ruleRect.sizeDelta = new Vector2(150f, 2f);
            }

            Image divider = UiKit.MakeImage(root.transform, "Divider", new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.22f), null, false);
            AnchorTopCentre(divider.rectTransform, new Vector2(0f, -196f), new Vector2(1780f, 2f));

            screen.BuildScrollView(root.transform);

            // Footer: BACK on the left, a hint in the middle
            Button backButton = CreateButton(root.transform, "BackButton", "BACK", new Vector2(240f, 56f), new Vector2(0f, 0f), new Vector2(80f, 56f), 18);
            UiKit.AttachFeedbackCore(backButton, AccentBright, 1.05f, playHover, playClick, false);
            backButton.onClick.AddListener(() => back());

            // QA SANDBOX, bottom right, for developers only (shown by Refresh while dev mode is on)
            screen.sandboxButton = CreateButton(root.transform, "SandboxButton", "QA SANDBOX", new Vector2(280f, 56f), new Vector2(1f, 0f), new Vector2(-360f, 56f), 16);
            UiKit.AttachFeedbackCore(screen.sandboxButton, AccentBright, 1.05f, playHover, playClick, false);
            screen.sandboxButton.onClick.AddListener(() => openSandbox());

            screen.hintText = UiKit.MakeText(root.transform, "Hint", DefaultHint, 12, TextAnchor.MiddleCenter,
                new Color(TextColor.r, TextColor.g, TextColor.b, 0.7f), FontStyle.Normal, HorizontalWrapMode.Overflow, VerticalWrapMode.Overflow, false);
            RectTransform hintRect = screen.hintText.rectTransform;
            hintRect.anchorMin = hintRect.anchorMax = new Vector2(0.5f, 0f);
            hintRect.pivot = new Vector2(0.5f, 0f);
            hintRect.anchoredPosition = new Vector2(0f, 74f);
            hintRect.sizeDelta = new Vector2(1000f, 20f);

            return screen;
        }

        /// <summary>The horizontal scroll view (viewport, content) and the slider under it.</summary>
        private void BuildScrollView(Transform parent)
        {
            var viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewportObject.transform.SetParent(parent, false);
            viewport = (RectTransform)viewportObject.transform;
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(ViewportMargin, ViewportBottom);
            viewport.offsetMax = new Vector2(-ViewportMargin, -ViewportTop);
            // A clear image so dragging on the empty space between the cards scrolls too
            Image viewportImage = viewportObject.GetComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0f);
            viewportImage.raycastTarget = true;

            var contentObject = new GameObject("Cards", typeof(RectTransform));
            contentObject.transform.SetParent(viewport, false);
            content = (RectTransform)contentObject.transform;
            content.anchorMin = new Vector2(0.5f, 0f);
            content.anchorMax = new Vector2(0.5f, 1f);
            content.pivot = new Vector2(0.5f, 0.5f);
            content.sizeDelta = new Vector2(ViewportWidth, 0f);
            content.anchoredPosition = Vector2.zero;

            // The slider under the cards
            var barObject = new GameObject("ActScrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            barObject.transform.SetParent(parent, false);
            RectTransform barRect = (RectTransform)barObject.transform;
            barRect.anchorMin = barRect.anchorMax = new Vector2(0.5f, 0f);
            barRect.pivot = new Vector2(0.5f, 0f);
            barRect.anchoredPosition = new Vector2(0f, 136f);
            barRect.sizeDelta = new Vector2(900f, 6f);
            barObject.GetComponent<Image>().color = TrackColor;

            var slidingObject = new GameObject("Sliding Area", typeof(RectTransform));
            slidingObject.transform.SetParent(barRect, false);
            RectTransform sliding = (RectTransform)slidingObject.transform;
            UiKit.Stretch(sliding);

            Image handleImage = UiKit.MakeImage(sliding, "Handle", HandleColor, null, true);
            // The scrollbar drives the handle's anchors only; any size left on the rect adds to the handle (a new image
            // defaults to 100 x 100, which made the handle a block 100 px bigger than its track)
            RectTransform handleRect = handleImage.rectTransform;
            handleRect.sizeDelta = Vector2.zero;
            handleRect.anchoredPosition = Vector2.zero;
            Scrollbar scrollbar = barObject.GetComponent<Scrollbar>();
            scrollbar.handleRect = handleImage.rectTransform;
            scrollbar.targetGraphic = handleImage;
            scrollbar.direction = Scrollbar.Direction.LeftToRight;
            scrollbar.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = scrollbar.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.3f, 1.3f, 1.3f, 1f);
            colors.pressedColor = new Color(1.5f, 1.5f, 1.5f, 1f);
            scrollbar.colors = colors;

            scroll = viewportObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true;
            scroll.decelerationRate = 0.12f;
            scroll.scrollSensitivity = 60f;
            scroll.horizontalScrollbar = scrollbar;
            scroll.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }

        /// <summary>
        /// Rebuilds the cards, so progress and locks are current each time the screen is shown.
        /// <paramref name="defaultMapArt"/> is Act 1's stage selector map, used by an act that has no art of its own.
        /// </summary>
        public void Refresh(IReadOnlyList<ActData> acts, Sprite defaultMapArt)
        {
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.Destroy(content.GetChild(i).gameObject);
            }

            var shown = new List<ActData>();
            foreach (ActData act in acts)
            {
                if (act != null) shown.Add(act);
            }

            float contentWidth = GetContentWidth(shown.Count);
            content.sizeDelta = new Vector2(contentWidth, 0f);
            for (int i = 0; i < shown.Count; i++)
            {
                CreateCard(shown[i], GetCardCentreX(i, shown.Count), defaultMapArt);
            }

            // Centred while the cards fit; when they scroll, start at the first act
            float viewportWidth = viewport.rect.width > 1f ? viewport.rect.width : ViewportWidth;
            content.anchoredPosition = new Vector2(Mathf.Max(0f, (contentWidth - viewportWidth) * 0.5f), 0f);
            scroll.velocity = Vector2.zero;
            if (hintText != null) hintText.text = NeedsScrolling(shown.Count) ? ScrollHint : DefaultHint;
            if (sandboxButton != null) sandboxButton.gameObject.SetActive(TrashTD.Development.DevMode.IsEnabled);
        }

        private void CreateCard(ActData act, float centreX, Sprite defaultMapArt)
        {
            bool comingSoon = act.IsComingSoon;
            bool unlocked = ActCatalog.IsActUnlocked(act);
            Color glow = unlocked ? AccentBright : LockedColor;

            Button button = CreateButton(content, "Act_" + act.actNumber, string.Empty, new Vector2(CardWidth, CardHeight),
                new Vector2(0.5f, 0.5f), new Vector2(centreX, 0f), 16);
            RectTransform card = (RectTransform)button.transform;
            card.pivot = new Vector2(0.5f, 0.5f);
            button.GetComponent<Image>().color = PlateColor;
            Text emptyLabel = button.GetComponentInChildren<Text>();
            if (emptyLabel != null) emptyLabel.gameObject.SetActive(false);

            // The art area, at the top of the card
            Image frame = UiKit.MakeImage(card, "ArtFrame", ArtFrameColor, null, false);
            RectTransform frameRect = frame.rectTransform;
            frameRect.anchorMin = frameRect.anchorMax = new Vector2(0.5f, 1f);
            frameRect.pivot = new Vector2(0.5f, 1f);
            frameRect.anchoredPosition = new Vector2(0f, -CardPadding);
            frameRect.sizeDelta = new Vector2(ArtWidth, ArtHeight);

            Sprite art = act.stageMapSprite != null ? act.stageMapSprite : defaultMapArt;
            if (comingSoon || art == null)
            {
                string message = comingSoon ? "COMING SOON" : act.DisplayName;
                Text soon = UiKit.MakeText(frameRect, "ComingSoon", message, Mathf.RoundToInt(44f * CardScale), TextAnchor.MiddleCenter, LockedColor, FontStyle.Bold,
                    HorizontalWrapMode.Overflow, VerticalWrapMode.Overflow, false);
                UiKit.Stretch(soon.rectTransform);
                AddBrackets(frameRect, 22f * CardScale, 2f, new Color(LockedColor.r, LockedColor.g, LockedColor.b, 0.5f), 14f * CardScale);
            }
            else
            {
                if (art.texture != null) art.texture.filterMode = FilterMode.Point;
                Image picture = UiKit.MakeImage(frameRect, "MapArt", unlocked ? Color.white : LockedArtTint, art, false);
                UiKit.Stretch(picture.rectTransform);
                picture.preserveAspect = false;
            }

            // The text block under the art: act name, zone and status stacked with even gaps and centred in the block
            var lines = new List<(string name, string text, int size, Color color, float height)>();
            lines.Add(("ActNumber", act.DisplayName, Mathf.RoundToInt(32f * CardScale), unlocked ? TitleColor : LockedColor, 42f * CardScale));
            if (!string.IsNullOrEmpty(act.zoneName))
                lines.Add(("Zone", act.zoneName.ToUpperInvariant(), Mathf.RoundToInt(14f * CardScale), unlocked ? Color.white : LockedColor, 22f * CardScale));

            string status = comingSoon
                ? string.Empty
                : unlocked
                    ? ActCatalog.GetClearedCount(act) + " / " + act.StageCount + " STAGES CLEARED"
                    : "SIGNAL LOCKED  -  " + ActCatalog.GetActUnlockRequirement(act);
            if (status.Length > 0)
                lines.Add(("Status", status, Mathf.RoundToInt(12f * CardScale), unlocked ? TextColor : RequirementColor, 20f * CardScale));

            float gap = 6f * CardScale;
            float stackHeight = (lines.Count - 1) * gap;
            foreach (var line in lines) stackHeight += line.height;

            float left = CardPadding + 18f * CardScale;
            float width = CardWidth - 2f * left;
            float y = -(CardPadding + ArtHeight + (TextBlockHeight - stackHeight) * 0.5f);
            foreach (var line in lines)
            {
                Text text = UiKit.MakeText(card, line.name, line.text, line.size, TextAnchor.MiddleLeft, line.color,
                    line.name == "ActNumber" ? FontStyle.Bold : FontStyle.Normal, HorizontalWrapMode.Overflow, VerticalWrapMode.Overflow, false);
                RectTransform rect = text.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(left, y);
                rect.sizeDelta = new Vector2(width, line.height);
                y -= line.height + gap;
            }

            AddBrackets(card, 16f * CardScale, 2f, new Color(glow.r, glow.g, glow.b, unlocked ? 0.9f : 0.5f), -5f);

            Action clicked = unlocked ? playClick : playDenied;
            UiKit.AttachFeedbackCore(button, glow, 1.02f, playHover, clicked, false);
            button.onClick.AddListener(() => openAct(act));
        }

        // ─────────────────────────────────────────────────────
        //  Small builders
        // ─────────────────────────────────────────────────────

        /// <summary>A flat terminal button. <paramref name="anchor"/> is where it sits in its parent (0-1); the offset moves it from there.</summary>
        private static Button CreateButton(Transform parent, string name, string label, Vector2 size, Vector2 anchor, Vector2 offset, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0f, 0f);
            rect.sizeDelta = size;
            rect.anchoredPosition = offset;

            Image image = go.GetComponent<Image>();
            image.color = new Color(0.035f, 0.090f, 0.060f, 1f);
            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;

            Text text = UiKit.MakeText(rect, "Label", label, fontSize, TextAnchor.MiddleCenter, TextColor, FontStyle.Bold,
                HorizontalWrapMode.Overflow, VerticalWrapMode.Overflow, false);
            UiKit.Stretch(text.rectTransform);
            return button;
        }

        private static void AnchorTopCentre(RectTransform rect, Vector2 offset, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
        }

        /// <summary>Four L-shaped corner brackets; a negative inset pushes them outside the rect.</summary>
        private static void AddBrackets(RectTransform target, float length, float thickness, Color color, float inset)
        {
            for (int i = 0; i < 4; i++)
            {
                var corner = new Vector2(i % 2, i / 2);
                var inward = new Vector2(corner.x < 0.5f ? 1f : -1f, corner.y < 0.5f ? 1f : -1f);
                AddBracketBar(target, corner, inward * inset, new Vector2(length, thickness), color);
                AddBracketBar(target, corner, inward * inset, new Vector2(thickness, length), color);
            }
        }

        private static void AddBracketBar(RectTransform parent, Vector2 corner, Vector2 offset, Vector2 size, Color color)
        {
            Image bar = UiKit.MakeImage(parent, "Bracket", color, null, false);
            RectTransform rect = bar.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = corner;
            rect.sizeDelta = size;
            rect.anchoredPosition = offset;
        }
    }
}
