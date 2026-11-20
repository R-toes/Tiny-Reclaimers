using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TrashTD.UI;

namespace TrashTD.Playtest
{
    /// <summary>
    /// The two small screens of the playtest system, built in code on their own overlay canvas: the first-launch
    /// question ("share anonymous data?") and the feedback form (1-5 stars and an optional note). Only one shows at a time.
    /// Each card lays itself out (a vertical layout group): text wraps to the card's width and the card grows to fit it,
    /// so nothing can run past an edge whatever the language, font size or screen.
    /// </summary>
    public static class PlaytestUi
    {
        private const float CardWidth = 920f;
        private const float Padding = 40f;

        private static readonly Color Dim = new Color(0f, 0f, 0f, 0.80f);
        private static readonly Color CardColor = new Color(0.015f, 0.040f, 0.032f, 0.99f);
        private static readonly Color Line = new Color(0.22f, 0.91f, 0.50f, 0.55f);
        private static readonly Color Accent = new Color(0.32f, 1.00f, 0.64f, 1f);
        private static readonly Color TextColor = new Color(0.82f, 0.92f, 0.86f, 1f);
        private static readonly Color Muted = new Color(0.50f, 0.64f, 0.57f, 1f);
        private static readonly Color Green = new Color(0.10f, 0.46f, 0.24f, 1f);
        private static readonly Color Grey = new Color(0.14f, 0.19f, 0.18f, 1f);
        private static readonly Color InputFill = new Color(0.03f, 0.08f, 0.065f, 1f);
        private static readonly Color Gold = new Color(1f, 0.80f, 0.20f, 1f);

        private static GameObject current;

        /// <summary>True while either screen is up.</summary>
        public static bool IsShowing => current != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlayMode() => current = null;

        // ---------------------------------------------------------------- consent

        public static void ShowConsent()
        {
            if (IsShowing) return;
            Transform card = Open("PlaytestConsent", out GameObject root);

            AddTitle(card, "HELP US PLAYTEST");
            AddDivider(card);
            AddParagraph(card, "This is a playtest build. With your OK, the game sends anonymous information to its developers:", 17, TextColor);
            AddParagraph(card, "-  Your device model and how smoothly the game runs", 16, TextColor, indent: 24f);
            AddParagraph(card, "-  Which stages you play, how they end, and which operators you use", 16, TextColor, indent: 24f);
            AddParagraph(card, "-  Error messages, and any feedback you choose to send", 16, TextColor, indent: 24f);
            AddParagraph(card, "No name, email or account is ever collected.", 16, Accent);
            AddParagraph(card, "You can change this any time in Settings > Audio > Playtest.", 14, Muted);

            AddButtonRow(card,
                ("ALLOW", Green, () =>
                {
                    PlaytestConsent.Choice = PlaytestChoice.Allowed;
                    Close(root);
                }),
                ("NO THANKS", Grey, () =>
                {
                    PlaytestConsent.Choice = PlaytestChoice.Declined;
                    Close(root);
                }));
        }

        // ---------------------------------------------------------------- feedback

        public static void ShowFeedback()
        {
            if (IsShowing) return;
            Transform card = Open("PlaytestFeedback", out GameObject root);

            AddTitle(card, "SEND FEEDBACK");
            AddDivider(card);
            string stage = PlaytestTracker.ActiveStageId;
            AddParagraph(card, string.IsNullOrEmpty(stage) ? "How is the game so far?" : "How is it going on " + stage + "?", 17, TextColor,
                TextAnchor.MiddleCenter);

            int rating = 0;
            var stars = new Text[5];
            RectTransform starRow = AddRow(card, 84f, 14f);
            for (int i = 0; i < 5; i++)
            {
                int value = i + 1;
                Button star = AddButton(starRow, "★", Grey, null);
                stars[i] = star.GetComponentInChildren<Text>();
                stars[i].fontSize = 44;
                stars[i].color = Muted;
                star.onClick.AddListener(() =>
                {
                    rating = value;
                    for (int s = 0; s < 5; s++) stars[s].color = s < rating ? Gold : Muted;
                });
            }

            InputField note = AddInput(card, 170f, "What did you like, or what went wrong? (optional)");
            Text message = AddParagraph(card, string.Empty, 15, Gold, TextAnchor.MiddleCenter);

            AddButtonRow(card,
                ("SEND", Green, () =>
                {
                    string text = note.text != null ? note.text.Trim() : string.Empty;
                    if (rating == 0 && text.Length == 0)
                    {
                        message.text = "Pick a star rating or write a note first.";
                        return;
                    }

                    PlaytestLog.Record("feedback", PlaytestTracker.ActiveStageId, new PlaytestJson().Add("rating", rating).Add("comment", text));
                    Close(root);
                }),
                ("CANCEL", Grey, () => Close(root)));
        }

        // ---------------------------------------------------------------- the card

        /// <summary>A dimmed full-screen overlay with a centred card that sizes itself to what is put in it.</summary>
        private static Transform Open(string name, out GameObject root)
        {
            UiKit.EnsureEventSystem();

            root = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Object.DontDestroyOnLoad(root);
            current = root;

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 600; // above the HUD (100), the draft (200) and the tutorial windows (300)
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            Image dim = UiKit.CreateImage(root.transform, "Dim", Dim);
            UiKit.SetStretch(dim.rectTransform);
            dim.raycastTarget = true; // blocks the game underneath

            Image card = UiKit.CreateImage(root.transform, "Card", CardColor);
            RectTransform rect = card.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(CardWidth, 100f); // the height follows the content
            rect.anchoredPosition = Vector2.zero;
            card.raycastTarget = true;

            var outline = card.gameObject.AddComponent<Outline>();
            outline.effectColor = Line;
            outline.effectDistance = new Vector2(2f, -2f);

            var layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset((int)Padding, (int)Padding, 36, 36);
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = card.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // A bright bar along the top edge (outside the layout)
            Image bar = UiKit.CreateImage(card.transform, "TopBar", Accent);
            bar.raycastTarget = false;
            bar.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            bar.rectTransform.anchorMin = new Vector2(0f, 1f);
            bar.rectTransform.anchorMax = new Vector2(1f, 1f);
            bar.rectTransform.pivot = new Vector2(0.5f, 1f);
            bar.rectTransform.sizeDelta = new Vector2(0f, 5f);
            bar.rectTransform.anchoredPosition = Vector2.zero;
            return card.transform;
        }

        private static void Close(GameObject root)
        {
            if (root != null) Object.Destroy(root);
            if (current == root) current = null;
        }

        // ---------------------------------------------------------------- building blocks

        private static void AddTitle(Transform parent, string title)
        {
            Text text = UiKit.CreateText(parent, title, 28, TextAnchor.MiddleCenter, Accent);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.gameObject.AddComponent<LayoutElement>().minHeight = 44f;
        }

        private static void AddDivider(Transform parent)
        {
            Image line = UiKit.CreateImage(parent, "Divider", Line);
            line.raycastTarget = false;
            var element = line.gameObject.AddComponent<LayoutElement>();
            element.minHeight = 2f;
            element.preferredHeight = 2f;
        }

        /// <summary>A paragraph that wraps to the card's width; its height is whatever the text needs.</summary>
        private static Text AddParagraph(Transform parent, string content, int size, Color color, TextAnchor anchor = TextAnchor.UpperLeft, float indent = 0f)
        {
            Text text = UiKit.CreateText(parent, content, size, anchor, color);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.lineSpacing = 1.25f;
            text.supportRichText = false;
            if (indent > 0f)
            {
                // A text inside a layout cannot be moved sideways, so bullets sit in a small padded group
                var holder = new GameObject("Indent", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                holder.transform.SetParent(parent, false);
                var group = holder.GetComponent<HorizontalLayoutGroup>();
                group.padding = new RectOffset((int)indent, 0, 0, 0);
                group.childControlWidth = true;
                group.childControlHeight = true;
                group.childForceExpandWidth = true;
                group.childForceExpandHeight = false;
                text.transform.SetParent(holder.transform, false);
            }
            return text;
        }

        /// <summary>A horizontal row of equal-width cells (the buttons) with a fixed height.</summary>
        private static RectTransform AddRow(Transform parent, float height, float spacing)
        {
            var go = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var group = go.GetComponent<HorizontalLayoutGroup>();
            group.spacing = spacing;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = true;
            var element = go.GetComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
            return (RectTransform)go.transform;
        }

        private static void AddButtonRow(Transform parent, params (string label, Color fill, UnityAction action)[] buttons)
        {
            RectTransform row = AddRow(parent, 68f, 24f);
            foreach (var spec in buttons) AddButton(row, spec.label, spec.fill, spec.action);
        }

        private static Button AddButton(Transform parent, string label, Color fill, UnityAction onClick)
        {
            var go = new GameObject(label + "Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = fill;
            go.GetComponent<LayoutElement>().flexibleWidth = 1f;

            Text text = UiKit.CreateText(go.transform, label, 22, TextAnchor.MiddleCenter, Color.white);
            UiKit.Stretch(text.rectTransform);

            Button button = go.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            button.colors = colors;
            if (onClick != null) button.onClick.AddListener(onClick);
            return button;
        }

        private static InputField AddInput(Transform parent, float height, string placeholder)
        {
            var go = new GameObject("Note", typeof(RectTransform), typeof(Image), typeof(InputField), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = InputFill;
            var element = go.GetComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;

            Text text = UiKit.CreateText(go.transform, string.Empty, 18, TextAnchor.UpperLeft, Color.white);
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            Inset(text.rectTransform, 14f);

            Text hint = UiKit.CreateText(go.transform, placeholder, 18, TextAnchor.UpperLeft, Muted);
            hint.horizontalOverflow = HorizontalWrapMode.Wrap;
            hint.verticalOverflow = VerticalWrapMode.Truncate;
            Inset(hint.rectTransform, 14f);

            InputField field = go.GetComponent<InputField>();
            field.textComponent = text;
            field.placeholder = hint;
            field.lineType = InputField.LineType.MultiLineNewline;
            field.characterLimit = 280;
            return field;
        }

        private static void Inset(RectTransform rect, float amount)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(amount, amount);
            rect.offsetMax = new Vector2(-amount, -amount);
        }
    }
}
