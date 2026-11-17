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
        private const float OffscreenMargin = 30f; // how far past its own width a notice starts to the left

        /// <summary>The baked notice plate (a template instantiated once per death). Missing = the plate is built in code.</summary>
        public const string PrefabAssetPath = "Assets/Resources/UI/Prefabs/OperatorDeathNotice.prefab";
        private const string PrefabResourcePath = "UI/Prefabs/OperatorDeathNotice";

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
        private GameObject plateTemplate;
        // The plate's own size, so a resized plate in the prefab keeps its spacing and slide distance
        private float noticeWidth = NoticeWidth;
        private float noticeHeight = NoticeHeight;

        /// <summary>The named parts of a notice plate (see TryResolvePlate).</summary>
        private sealed class PlateRefs
        {
            public RectTransform rect;
            public CanvasGroup group;
            public Image portrait;
            public Text nameText;
        }

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
            notice.plateTemplate = Resources.Load<GameObject>(PrefabResourcePath);
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
            // The plate's look comes from the baked prefab when there is one (TRASH TD > UI > Bake Operator Death Notice
            // Prefab, then edit it in Prefab Mode); otherwise it is built in code. The name and portrait are filled in here.
            PlateRefs refs = null;
            if (plateTemplate != null)
            {
                GameObject instance = Instantiate(plateTemplate, container, false);
                if (!TryResolvePlate(instance, out refs, out string missing))
                {
                    Debug.LogError("OperatorDeathNoticeUI: the baked plate (" + PrefabAssetPath + ") is missing " + missing +
                                   ". Building it in code instead; rebake it with TRASH TD > UI > Bake Operator Death Notice Prefab.");
                    instance.SetActive(false);
                    Destroy(instance);
                    refs = null;
                    plateTemplate = null; // do not try again
                }
            }
            if (refs == null) TryResolvePlate(BuildPlateLayout(container), out refs, out _);

            // Notices are positioned as offsets from the bottom-left corner (ApplyPosition), whatever the prefab says
            refs.rect.anchorMin = refs.rect.anchorMax = refs.rect.pivot = Vector2.zero;
            noticeWidth = refs.rect.sizeDelta.x;
            noticeHeight = refs.rect.sizeDelta.y;

            refs.nameText.text = displayName;
            refs.portrait.gameObject.SetActive(portrait != null);
            if (portrait != null)
            {
                refs.portrait.sprite = portrait;
                refs.portrait.preserveAspect = true;
            }

            refs.group.alpha = 0f;
            refs.group.interactable = false;
            refs.group.blocksRaycasts = false;
            return new Notice { Rect = refs.rect, Group = refs.group };
        }

        /// <summary>Builds the plate's layout in code under <paramref name="parent"/>, for the bake command and the tests.</summary>
        public static GameObject BuildPlateForPrefab(Transform parent) => BuildPlateLayout(parent);

        /// <summary>True when <paramref name="candidate"/> has every named part the notice binds to; otherwise lists what is missing.</summary>
        public static bool ValidatePlate(GameObject candidate, out string missing) => TryResolvePlate(candidate, out _, out missing);

        private static bool TryResolvePlate(GameObject root, out PlateRefs refs, out string missing)
        {
            var names = new UiPrefabLookup(root);
            refs = new PlateRefs
            {
                rect = root.GetComponent<RectTransform>(),
                group = names.FindOnRoot<CanvasGroup>(),
                portrait = names.Find<Image>("Portrait"),
                nameText = names.Find<Text>("Name")
            };
            missing = names.Missing;
            return names.IsComplete && refs.rect != null;
        }

        private static GameObject BuildPlateLayout(Transform parent)
        {
            var plateObject = new GameObject("DeathNotice", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            plateObject.transform.SetParent(parent, false);
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

            // Red edge bar on the left
            Image bar = CreateImage(rect, "Accent", AccentRed);
            bar.rectTransform.anchorMin = new Vector2(0f, 0f);
            bar.rectTransform.anchorMax = new Vector2(0f, 1f);
            bar.rectTransform.pivot = new Vector2(0f, 0.5f);
            bar.rectTransform.sizeDelta = new Vector2(5f, 0f);
            bar.rectTransform.anchoredPosition = Vector2.zero;

            // Portrait in a square frame (the sprite is set per notice; the notice hides it when there is none)
            Image frame = CreateImage(rect, "PortraitFrame", new Color(1f, 1f, 1f, 0.08f));
            frame.rectTransform.anchorMin = frame.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            frame.rectTransform.pivot = new Vector2(0f, 0.5f);
            frame.rectTransform.sizeDelta = new Vector2(34f, 34f);
            frame.rectTransform.anchoredPosition = new Vector2(12f, 0f);
            Image icon = CreateImage(frame.rectTransform, "Portrait", Color.white);
            icon.preserveAspect = true;
            icon.rectTransform.anchorMin = Vector2.zero;
            icon.rectTransform.anchorMax = Vector2.one;
            icon.rectTransform.offsetMin = new Vector2(2f, 2f);
            icon.rectTransform.offsetMax = new Vector2(-2f, -2f);

            Text header = CreateText(rect, "Header", "OPERATOR DOWN", 11, AccentRed);
            header.rectTransform.anchorMin = new Vector2(0f, 1f);
            header.rectTransform.anchorMax = new Vector2(1f, 1f);
            header.rectTransform.pivot = new Vector2(0f, 1f);
            header.rectTransform.offsetMin = new Vector2(56f, -22f);
            header.rectTransform.offsetMax = new Vector2(-10f, -6f);

            Text nameText = CreateText(rect, "Name", "OPERATOR", 16, NameColor);
            nameText.resizeTextForBestFit = true;
            nameText.resizeTextMinSize = 10;
            nameText.resizeTextMaxSize = 16;
            nameText.horizontalOverflow = HorizontalWrapMode.Wrap;
            nameText.rectTransform.anchorMin = new Vector2(0f, 0f);
            nameText.rectTransform.anchorMax = new Vector2(1f, 0f);
            nameText.rectTransform.pivot = new Vector2(0f, 0f);
            nameText.rectTransform.offsetMin = new Vector2(56f, 6f);
            nameText.rectTransform.offsetMax = new Vector2(-10f, 28f);

            return plateObject;
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

        private float GetTargetY(int index)
        {
            return BottomOffset + index * (noticeHeight + NoticeGap);
        }

        private void ApplyPosition(Notice notice, float age)
        {
            float slide = Mathf.Clamp01(age / SlideInTime);
            float eased = 1f - (1f - slide) * (1f - slide) * (1f - slide);
            float x = Mathf.Lerp(-noticeWidth - OffscreenMargin, LeftMargin, eased);

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
            return UiKit.MakeText(parent, name, content, size, TextAnchor.MiddleLeft, color, FontStyle.Normal, HorizontalWrapMode.Overflow, VerticalWrapMode.Overflow, false);
        }
    }
}
