using System;
using UnityEngine;
using UnityEngine.UI;

namespace TrashTD.UI
{
    /// <summary>
    /// A small "pick one" dialog on its own overlay canvas: a title, a message and a column of buttons. Used for leaving
    /// a stage (SAVE &amp; EXIT, EXIT WITHOUT SAVING, CANCEL) and for continuing a saved one (CONTINUE, START AGAIN, CANCEL).
    /// It lays itself out (a vertical layout group), so text wraps to the card and the card grows to fit; it works while the
    /// game is paused (nothing here uses scaled time). Only one is shown at a time. Any button closes it, then runs its action.
    /// </summary>
    public static class ChoiceDialog
    {
        /// <summary>One button of the dialog.</summary>
        public readonly struct Option
        {
            public readonly string Label;
            public readonly Color Fill;
            public readonly Action OnPick;

            public Option(string label, Color fill, Action onPick)
            {
                Label = label;
                Fill = fill;
                OnPick = onPick;
            }
        }

        public static readonly Color Positive = new Color(0.10f, 0.46f, 0.24f, 1f);
        public static readonly Color Negative = new Color(0.55f, 0.14f, 0.14f, 1f);
        public static readonly Color Neutral = new Color(0.14f, 0.19f, 0.18f, 1f);

        private const float CardWidth = 760f;
        private const float Padding = 40f;

        private static readonly Color Dim = new Color(0f, 0f, 0f, 0.80f);
        private static readonly Color CardColor = new Color(0.015f, 0.040f, 0.032f, 0.99f);
        private static readonly Color Line = new Color(0.22f, 0.91f, 0.50f, 0.55f);
        private static readonly Color Accent = new Color(0.32f, 1.00f, 0.64f, 1f);
        private static readonly Color TextColor = new Color(0.82f, 0.92f, 0.86f, 1f);

        private static GameObject current;

        /// <summary>True while a dialog is up.</summary>
        public static bool IsShowing => current != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlayMode() => current = null;

        /// <summary>Shows the dialog. Does nothing when one is already up.</summary>
        public static void Show(string title, string message, params Option[] options)
        {
            if (IsShowing) return;
            UiKit.EnsureEventSystem();

            var root = new GameObject("ChoiceDialog", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            UnityEngine.Object.DontDestroyOnLoad(root);
            current = root;

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 700; // above the HUD, the pause menu and the playtest screens
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

            Image bar = UiKit.CreateImage(card.transform, "TopBar", Accent);
            bar.raycastTarget = false;
            bar.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            bar.rectTransform.anchorMin = new Vector2(0f, 1f);
            bar.rectTransform.anchorMax = new Vector2(1f, 1f);
            bar.rectTransform.pivot = new Vector2(0.5f, 1f);
            bar.rectTransform.sizeDelta = new Vector2(0f, 5f);
            bar.rectTransform.anchoredPosition = Vector2.zero;

            AddText(card.transform, title, 26, TextAnchor.MiddleCenter, Accent).gameObject.AddComponent<LayoutElement>().minHeight = 40f;

            Image divider = UiKit.CreateImage(card.transform, "Divider", Line);
            divider.raycastTarget = false;
            var dividerElement = divider.gameObject.AddComponent<LayoutElement>();
            dividerElement.minHeight = 2f;
            dividerElement.preferredHeight = 2f;

            if (!string.IsNullOrEmpty(message)) AddText(card.transform, message, 17, TextAnchor.MiddleCenter, TextColor);

            foreach (Option option in options) AddButton(card.transform, root, option);
        }

        /// <summary>Closes the dialog without running anything.</summary>
        public static void Close()
        {
            if (current != null) UnityEngine.Object.Destroy(current);
            current = null;
        }

        private static Text AddText(Transform parent, string content, int size, TextAnchor anchor, Color color)
        {
            Text text = UiKit.CreateText(parent, content, size, anchor, color);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.lineSpacing = 1.25f;
            text.supportRichText = false;
            return text;
        }

        private static void AddButton(Transform parent, GameObject root, Option option)
        {
            var go = new GameObject(option.Label + "Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = option.Fill;
            var element = go.GetComponent<LayoutElement>();
            element.minHeight = 64f;
            element.preferredHeight = 64f;

            Text label = UiKit.CreateText(go.transform, option.Label, 21, TextAnchor.MiddleCenter, Color.white);
            UiKit.Stretch(label.rectTransform);

            Button button = go.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            button.colors = colors;

            Action pick = option.OnPick;
            button.onClick.AddListener(() =>
            {
                if (current == root) Close();
                pick?.Invoke();
            });
        }
    }
}
