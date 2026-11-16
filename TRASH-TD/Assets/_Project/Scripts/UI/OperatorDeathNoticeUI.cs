using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TrashTD.Operators;
using TrashTD.Systems;
using static TrashTD.UI.UiKit;

namespace TrashTD.UI
{
    /// <summary>
    /// Small "OPERATOR DOWN" notices on the left side of the gameplay screen, like a kill feed: when an operator dies
    /// (<see cref="OperatorManager.OnOperatorDied"/>; a retreat is not a death) a dark rectangular plate with its portrait,
    /// "OPERATOR DOWN" and its name slides in from the left, stays a few seconds and fades. They sit on the lower left; the
    /// newest notice takes the bottom slot and pushes the older ones up; at most <see cref="MaxNotices"/> show at once. Built by GameplayHUDUI as part of its
    /// canvas. Never takes clicks. Unscaled time.
    /// </summary>
    public sealed class OperatorDeathNoticeUI : MonoBehaviour
    {
        private const int MaxNotices = 4;
        private const float NoticeWidth = 270f;
        private const float NoticeHeight = 46f;
        private const float NoticeGap = 6f;
        private const float LeftMargin = 18f;
        private const float BottomOffset = 290f; // above the deck bar (170) and the surveillance feed's signal / zoom readout
        private const float SlideInTime = 0.22f;
        private const float VisibleTime = 3.6f;
        private const float FadeOutTime = 0.45f;
        private const float OffscreenX = -NoticeWidth - 30f;

        private static readonly Color PlateColor = new Color(0.04f, 0.045f, 0.055f, 0.92f);
        private static readonly Color AccentRed = new Color(1f, 0.3f, 0.28f, 1f);
        private static readonly Color NameColor = new Color(0.95f, 0.95f, 0.95f, 1f);

        private sealed class Notice
        {
            public RectTransform Rect;
            public CanvasGroup Group;
            public float Age;
            public float CurrentY;
        }

        private readonly List<Notice> notices = new List<Notice>();
        private OperatorManager operatorManager;
        private RectTransform container;

        public static OperatorDeathNoticeUI Create(Transform canvasRoot, OperatorManager manager)
        {
            var root = new GameObject("OperatorDeathNotices", typeof(RectTransform));
            root.transform.SetParent(canvasRoot, false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var notice = root.AddComponent<OperatorDeathNoticeUI>();
            notice.container = rect;
            notice.operatorManager = manager != null ? manager : FindAnyObjectByType<OperatorManager>();
            if (notice.operatorManager != null) notice.operatorManager.OnOperatorDied += notice.HandleOperatorDied;
            return notice;
        }

        private void OnDestroy()
        {
            if (operatorManager != null) operatorManager.OnOperatorDied -= HandleOperatorDied;
        }

        private void HandleOperatorDied(OperatorBase op)
        {
            if (op == null || op.Data == null) return;
            Show(op.Data.operatorName, op.Data.portrait);
        }

        /// <summary>Adds a notice for an operator that just died.</summary>
        public void Show(string operatorName, Sprite portrait)
        {
            if (container == null) return;

            // The oldest notices make room for the new one
            while (notices.Count >= MaxNotices) RemoveNotice(notices.Count - 1);

            Notice notice = BuildNotice(string.IsNullOrWhiteSpace(operatorName) ? "OPERATOR" : operatorName.ToUpperInvariant(), portrait);
            notice.CurrentY = GetTargetY(0);
            notices.Insert(0, notice);
            ApplyPosition(notice, 0f);
        }

        private Notice BuildNotice(string displayName, Sprite portrait)
        {
            var plateObject = new GameObject("DeathNotice", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            plateObject.transform.SetParent(container, false);
            var rect = (RectTransform)plateObject.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0f);
            rect.sizeDelta = new Vector2(NoticeWidth, NoticeHeight);

            Image plate = plateObject.GetComponent<Image>();
            plate.color = PlateColor;
            plate.raycastTarget = false;
            var outline = plateObject.AddComponent<Outline>();
            outline.effectColor = new Color(AccentRed.r, AccentRed.g, AccentRed.b, 0.55f);
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = false;

            CanvasGroup group = plateObject.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            // Red edge bar on the left
            Image bar = CreateImage(rect, "Accent", AccentRed);
            bar.rectTransform.anchorMin = new Vector2(0f, 0f);
            bar.rectTransform.anchorMax = new Vector2(0f, 1f);
            bar.rectTransform.pivot = new Vector2(0f, 0.5f);
            bar.rectTransform.sizeDelta = new Vector2(5f, 0f);
            bar.rectTransform.anchoredPosition = Vector2.zero;

            // Portrait in a square frame
            Image frame = CreateImage(rect, "PortraitFrame", new Color(1f, 1f, 1f, 0.08f));
            frame.rectTransform.anchorMin = frame.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            frame.rectTransform.pivot = new Vector2(0f, 0.5f);
            frame.rectTransform.sizeDelta = new Vector2(34f, 34f);
            frame.rectTransform.anchoredPosition = new Vector2(12f, 0f);
            if (portrait != null)
            {
                Image icon = CreateImage(frame.rectTransform, "Portrait", Color.white);
                icon.sprite = portrait;
                icon.preserveAspect = true;
                icon.rectTransform.anchorMin = Vector2.zero;
                icon.rectTransform.anchorMax = Vector2.one;
                icon.rectTransform.offsetMin = new Vector2(2f, 2f);
                icon.rectTransform.offsetMax = new Vector2(-2f, -2f);
            }

            Text header = CreateText(rect, "Header", "OPERATOR DOWN", 11, AccentRed);
            header.rectTransform.anchorMin = new Vector2(0f, 1f);
            header.rectTransform.anchorMax = new Vector2(1f, 1f);
            header.rectTransform.pivot = new Vector2(0f, 1f);
            header.rectTransform.offsetMin = new Vector2(56f, -22f);
            header.rectTransform.offsetMax = new Vector2(-10f, -6f);

            Text nameText = CreateText(rect, "Name", displayName, 16, NameColor);
            nameText.resizeTextForBestFit = true;
            nameText.resizeTextMinSize = 10;
            nameText.resizeTextMaxSize = 16;
            nameText.horizontalOverflow = HorizontalWrapMode.Wrap;
            nameText.rectTransform.anchorMin = new Vector2(0f, 0f);
            nameText.rectTransform.anchorMax = new Vector2(1f, 0f);
            nameText.rectTransform.pivot = new Vector2(0f, 0f);
            nameText.rectTransform.offsetMin = new Vector2(56f, 6f);
            nameText.rectTransform.offsetMax = new Vector2(-10f, 28f);

            return new Notice { Rect = rect, Group = group };
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = notices.Count - 1; i >= 0; i--)
            {
                Notice notice = notices[i];
                notice.Age += dt;
                if (notice.Age >= SlideInTime + VisibleTime + FadeOutTime)
                {
                    RemoveNotice(i);
                    continue;
                }
            }

            // Older notices glide up as new ones arrive
            for (int i = 0; i < notices.Count; i++)
            {
                Notice notice = notices[i];
                notice.CurrentY = Mathf.Lerp(notice.CurrentY, GetTargetY(i), 1f - Mathf.Exp(-14f * dt));
                ApplyPosition(notice, notice.Age);
            }
        }

        private static float GetTargetY(int index)
        {
            return BottomOffset + index * (NoticeHeight + NoticeGap);
        }

        private static void ApplyPosition(Notice notice, float age)
        {
            float slide = Mathf.Clamp01(age / SlideInTime);
            float eased = 1f - (1f - slide) * (1f - slide) * (1f - slide);
            float x = Mathf.Lerp(OffscreenX, LeftMargin, eased);

            float fadeOut = Mathf.Clamp01((age - SlideInTime - VisibleTime) / FadeOutTime);
            notice.Group.alpha = Mathf.Clamp01(slide * 2f) * (1f - fadeOut);
            notice.Rect.anchoredPosition = new Vector2(x, notice.CurrentY);
        }

        private void RemoveNotice(int index)
        {
            if (index < 0 || index >= notices.Count) return;
            if (notices[index].Rect != null) Destroy(notices[index].Rect.gameObject);
            notices.RemoveAt(index);
        }

        private static Text CreateText(Transform parent, string name, string content, int size, Color color)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            Text text = textObject.GetComponent<Text>();
            text.text = content;
            text.font = UIFontHelper.GetPixelFont();
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }
    }
}
