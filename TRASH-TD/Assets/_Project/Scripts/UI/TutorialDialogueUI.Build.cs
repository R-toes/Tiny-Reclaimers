using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TrashTD.Audio;
using TrashTD.Data;

namespace TrashTD.UI
{
    // Building the dialogue UI: bubble, spotlight, task strip, skip button and pointer.
    public partial class TutorialDialogueUI
    {
        private void Build(string speakerName, Sprite[] speakerFrames, float speakerFrameDuration)
        {
            canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CanvasSortingOrder;
            var scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            root = CreateRect("Root", transform);
            Stretch(root);

            dim = CreateImage(root, "Dim", Color.clear);
            Stretch(dim.rectTransform);
            dim.raycastTarget = false;
            BuildSpotlight();

            // Talk lines catch every click on the screen
            Image catcherImage = CreateImage(root, "ClickCatcher", Color.clear);
            Stretch(catcherImage.rectTransform);
            catcher = catcherImage.gameObject.AddComponent<Button>();
            catcher.transition = Selectable.Transition.None;
            catcher.navigation = new Navigation { mode = Navigation.Mode.None };
            catcher.onClick.AddListener(HandleClick);

            // Coalesce and the bubble slide in and out together
            presenter = CreateRect("Presenter", root);
            Stretch(presenter);

            frames = speakerFrames != null && speakerFrames.Length > 0 ? speakerFrames : null;
            frameDuration = Mathf.Max(0.06f, speakerFrameDuration);
            speakerRig = CreateRect("CoalesceRig", presenter);
            speakerImage = CreateImage(speakerRig, "Coalesce", frames != null ? Color.white : Color.clear);
            speakerImage.sprite = frames != null ? frames[0] : null;
            speakerImage.preserveAspect = true;
            speakerImage.raycastTarget = false;
            speaker = speakerImage.rectTransform;
            Stretch(speaker);
            speaker.pivot = new Vector2(0.5f, FeetPivotY);

            BuildBubble(speakerName);
            BuildTaskStrip();
            pointer = BuildPointer(root); // Above Coalesce, so nothing hides it
            BuildSkip();

            ApplyLayout();
            HideBubble();
            HideChoices();
            PointAt(null);
            presenter.anchoredPosition = new Vector2(slideSign * slideDistance, 0f);
            bubble.gameObject.SetActive(false);
        }

        private void BuildSpotlight()
        {
            spotlight = CreateRect("Spotlight", root);
            Stretch(spotlight);

            Image hole = CreateImage(spotlight, "Hole", Color.clear);
            hole.sprite = GetSpotHoleSprite();
            spotParts.Add(hole);
            spotParts.Add(CreateImage(spotlight, "Left", Color.clear));
            spotParts.Add(CreateImage(spotlight, "Right", Color.clear));
            spotParts.Add(CreateImage(spotlight, "Top", Color.clear));
            spotParts.Add(CreateImage(spotlight, "Bottom", Color.clear));
            foreach (Image part in spotParts)
            {
                part.raycastTarget = false;
                RectTransform rect = part.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
            }
            spotlight.gameObject.SetActive(false);
        }

        private void BuildBubble(string speakerName)
        {
            Image panel = CreateImage(presenter, "SpeechBubble", PanelColor);
            panel.raycastTarget = false;
            bubble = panel.rectTransform;
            var outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = PhosphorDim;
            outline.effectDistance = new Vector2(2f, -2f);
            AddCornerBrackets(bubble, 16f, 3f, Phosphor, -5f);

            // Tail: a small rotated square on the edge facing Coalesce
            Image tailImage = CreateImage(bubble, "Tail", PanelColor);
            tailImage.raycastTarget = false;
            tail = tailImage.rectTransform;
            tail.sizeDelta = new Vector2(22f, 22f);
            tail.localRotation = Quaternion.Euler(0f, 0f, 45f);

            Image header = CreateImage(bubble, "Header", HeaderColor);
            header.raycastTarget = false;
            RectTransform headerRect = header.rectTransform;
            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.anchorMax = new Vector2(1f, 1f);
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.sizeDelta = new Vector2(0f, 34f);
            headerRect.anchoredPosition = Vector2.zero;

            nameText = CreateText(headerRect, "Name", "◆ " + (string.IsNullOrEmpty(speakerName) ? "COALESCE" : speakerName.ToUpperInvariant()), 14, TextAnchor.MiddleLeft, Phosphor);
            Stretch(nameText.rectTransform, 16f, 140f, 0f, 0f);

            tagPlate = CreateImage(headerRect, "TagPlate", Color.clear);
            tagPlate.raycastTarget = false;
            RectTransform plate = tagPlate.rectTransform;
            plate.anchorMin = plate.anchorMax = new Vector2(1f, 0.5f);
            plate.pivot = new Vector2(1f, 0.5f);
            plate.sizeDelta = new Vector2(118f, 24f);
            plate.anchoredPosition = new Vector2(-10f, 0f);
            tagText = CreateText(plate, "Tag", string.Empty, 11, TextAnchor.MiddleCenter, PhosphorDim);
            Stretch(tagText.rectTransform);

            bodyText = CreateText(bubble, "Body", string.Empty, 17, TextAnchor.UpperLeft, BodyColor);
            bodyText.lineSpacing = 1.4f;
            bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bodyText.verticalOverflow = VerticalWrapMode.Truncate;
            bodyText.resizeTextForBestFit = true;
            bodyText.resizeTextMinSize = 10;
            bodyText.resizeTextMaxSize = 17;
            Stretch(bodyText.rectTransform, 18f, 18f, 48f, 30f);

            promptText = CreateText(bubble, "Prompt", string.Empty, 13, TextAnchor.LowerRight, Phosphor);
            Stretch(promptText.rectTransform, 18f, 12f, 18f, 8f);

            // Choice row along the bubble's bottom edge
            choiceRow = CreateRect("Choices", bubble);
            choiceRow.anchorMin = new Vector2(0f, 0f);
            choiceRow.anchorMax = new Vector2(1f, 0f);
            choiceRow.pivot = new Vector2(0.5f, 0f);
            choiceRow.offsetMin = new Vector2(18f, 14f);
            choiceRow.offsetMax = new Vector2(-18f, 56f);

            primaryChoice = CreateButton(choiceRow, "PrimaryChoice", string.Empty, HazardInk, HazardYellow);
            RectTransform primaryRect = primaryChoice.GetComponent<RectTransform>();
            primaryRect.anchorMin = new Vector2(0f, 0f);
            primaryRect.anchorMax = new Vector2(0.5f, 1f);
            primaryRect.offsetMin = Vector2.zero;
            primaryRect.offsetMax = new Vector2(-6f, 0f);
            primaryChoice.onClick.AddListener(() => Choose(primaryAction));

            secondaryChoice = CreateButton(choiceRow, "SecondaryChoice", string.Empty, Phosphor, HeaderColor);
            RectTransform secondaryRect = secondaryChoice.GetComponent<RectTransform>();
            secondaryRect.anchorMin = new Vector2(0.5f, 0f);
            secondaryRect.anchorMax = new Vector2(1f, 1f);
            secondaryRect.offsetMin = new Vector2(6f, 0f);
            secondaryRect.offsetMax = Vector2.zero;
            secondaryChoice.onClick.AddListener(() => Choose(secondaryAction));
        }

        private void Choose(Action action)
        {
            if (action == null) return;
            AudioManager.Instance?.PlaySfx(SfxId.UiConfirm);
            Action chosen = action;
            HideChoices();
            chosen();
        }

        private void BuildTaskStrip()
        {
            Image panel = CreateImage(root, "TaskStrip", PanelColor);
            panel.raycastTarget = false;
            taskStripRect = panel.rectTransform;
            taskStrip = panel.gameObject.AddComponent<CanvasGroup>();
            taskStrip.blocksRaycasts = false;
            taskStrip.interactable = false;
            taskStrip.alpha = 0f;
            var outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = PhosphorDim;
            outline.effectDistance = new Vector2(2f, -2f);
            AddCornerBrackets(taskStripRect, 12f, 3f, HazardYellow, -4f);

            // Hazard stripe down the left edge
            Image stripe = CreateImage(taskStripRect, "Stripe", HazardYellow);
            stripe.raycastTarget = false;
            RectTransform stripeRect = stripe.rectTransform;
            stripeRect.anchorMin = new Vector2(0f, 0f);
            stripeRect.anchorMax = new Vector2(0f, 1f);
            stripeRect.pivot = new Vector2(0f, 0.5f);
            stripeRect.sizeDelta = new Vector2(6f, 0f);
            stripeRect.anchoredPosition = Vector2.zero;

            Text tag = CreateText(taskStripRect, "Tag", "▶ TASK", 11, TextAnchor.UpperLeft, HazardYellow);
            Stretch(tag.rectTransform, 20f, 16f, 10f, TaskStripHeight - 28f);

            taskText = CreateText(taskStripRect, "Body", string.Empty, 14, TextAnchor.UpperLeft, BodyColor);
            taskText.lineSpacing = 1.3f;
            taskText.horizontalOverflow = HorizontalWrapMode.Wrap;
            taskText.verticalOverflow = VerticalWrapMode.Truncate;
            taskText.resizeTextForBestFit = true;
            taskText.resizeTextMinSize = 10;
            taskText.resizeTextMaxSize = 14;
            Stretch(taskText.rectTransform, 20f, 16f, 30f, 10f);

            taskStrip.gameObject.SetActive(false);
        }

        private void BuildSkip()
        {
            // Easy to find: a hazard-yellow rim, corner brackets and a bold yellow label that turns white on hover
            Image plate = CreateImage(root, "SkipTutorialButton", PanelColor);
            skipButton = plate.gameObject.AddComponent<Button>();
            skipButton.navigation = new Navigation { mode = Navigation.Mode.None };
            skipButton.onClick.AddListener(OpenSkipConfirm);
            var outline = plate.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(HazardYellow.r, HazardYellow.g, HazardYellow.b, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);
            AddCornerBrackets(plate.rectTransform, 12f, 3f, HazardYellow, -5f);
            Text label = CreateText(plate.rectTransform, "Label", "SKIP TUTORIAL  ▶▶", 16, TextAnchor.MiddleCenter, Color.white);
            label.fontStyle = FontStyle.Bold;
            Stretch(label.rectTransform);
            skipButton.targetGraphic = label; // The tint below colours the label (white text x tint)
            ColorBlock colors = skipButton.colors;
            colors.normalColor = HazardYellow;
            colors.highlightedColor = Color.white;
            colors.selectedColor = HazardYellow;
            colors.pressedColor = new Color(0.78f, 0.62f, 0.12f, 1f);
            colors.fadeDuration = 0.08f;
            skipButton.colors = colors;
            plate.rectTransform.sizeDelta = new Vector2(SkipWidth, SkipHeight);

            // Confirm prompt, centred, above everything else on this canvas
            Image shade = CreateImage(root, "SkipConfirm", new Color(0f, 0f, 0f, 0.6f));
            Stretch(shade.rectTransform);
            skipConfirm = shade.gameObject;

            Image card = CreateImage(shade.rectTransform, "Card", PanelColor);
            RectTransform cardRect = card.rectTransform;
            cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(620f, 250f);
            var cardOutline = card.gameObject.AddComponent<Outline>();
            cardOutline.effectColor = PhosphorDim;
            cardOutline.effectDistance = new Vector2(2f, -2f);
            AddCornerBrackets(cardRect, 18f, 3f, HazardYellow, -6f);

            Text title = CreateText(cardRect, "Title", "SKIP TUTORIAL?", 18, TextAnchor.MiddleCenter, HazardYellow);
            Stretch(title.rectTransform, 20f, 20f, 26f, 190f);
            Text body = CreateText(cardRect, "Body", "You can replay it any time from STAGE 00 on the map.", 12, TextAnchor.MiddleCenter, BodyColor);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.lineSpacing = 1.4f;
            Stretch(body.rectTransform, 40f, 40f, 76f, 104f);

            Button stay = CreateButton(cardRect, "StayButton", "KEEP LEARNING", Phosphor, HeaderColor);
            Place(stay.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(-140f, 28f), new Vector2(240f, 52f));
            stay.onClick.AddListener(CloseSkipConfirm);

            Button skip = CreateButton(cardRect, "ConfirmSkipButton", "SKIP", HazardInk, HazardYellow);
            Place(skip.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(140f, 28f), new Vector2(240f, 52f));
            skip.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlaySfx(SfxId.UiConfirm);
                skipConfirm.SetActive(false);
                SkipConfirmed?.Invoke();
            });

            skipConfirm.SetActive(false);
        }

        private RectTransform BuildPointer(RectTransform parent)
        {
            RectTransform frame = CreateRect("Pointer", parent);
            frame.anchorMin = frame.anchorMax = new Vector2(0.5f, 0.5f);
            frame.pivot = new Vector2(0.5f, 0.5f);
            const float length = 26f;
            const float thickness = 5f;
            for (int i = 0; i < 4; i++)
            {
                var corner = new Vector2(i % 2, i / 2);
                pointerParts.Add(CreateBar(frame, corner, new Vector2(length, thickness)));
                pointerParts.Add(CreateBar(frame, corner, new Vector2(thickness, length)));
            }
            return frame;

            Image CreateBar(RectTransform owner, Vector2 corner, Vector2 size)
            {
                Image bar = CreateImage(owner, "PointerBracket", HazardYellow);
                bar.raycastTarget = false;
                Place(bar.rectTransform, corner, Vector2.zero, size);
                bar.rectTransform.pivot = corner;
                return bar;
            }
        }
    }
}
