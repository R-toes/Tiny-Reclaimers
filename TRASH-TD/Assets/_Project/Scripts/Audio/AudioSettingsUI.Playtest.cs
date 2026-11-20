using UnityEngine;
using UnityEngine.UI;
using TrashTD.Playtest;
using static TrashTD.UI.UiKit;

namespace TrashTD.Audio
{
    // Settings > Audio > PLAYTEST: the data sharing switch and the feedback button. Added in code, and only in a build that
    // has a playtest config (PlaytestConfig), so the baked layout is unchanged and a normal build shows nothing.
    public partial class AudioSettingsUI
    {
        private Button playtestOnButton;
        private Button playtestOffButton;
        private Button playtestFeedbackButton;

        private void BuildPlaytestRows()
        {
            GameObject page = pages[(int)Tab.Audio];
            if (page == null) return;

            Transform previousRoot = pageRoot;
            pageRoot = page.transform;

            CreateSectionHeader("PLAYTEST", 340f);
            CreateRowLabel("SHARE DATA", 386f);
            playtestOnButton = CreateSegmentButton("PlaytestOnButton", "ON", 0, 386f);
            playtestOffButton = CreateSegmentButton("PlaytestOffButton", "OFF", 1, 386f);
            Bind(playtestOnButton, () => SetPlaytestSharing(PlaytestChoice.Allowed));
            Bind(playtestOffButton, () => SetPlaytestSharing(PlaytestChoice.Declined));

            CreateRowLabel("FEEDBACK", 434f);
            playtestFeedbackButton = CreateSegmentButton("PlaytestFeedbackButton", "SEND", 0, 434f);
            Bind(playtestFeedbackButton, PlaytestUi.ShowFeedback);

            // Wraps inside the card (the page is 590 wide at this point; one line would run past its edge)
            Text hint = CreateText(pageRoot, "Anonymous: your device, how stages go and your feedback. No names or accounts.", 11,
                TextAnchor.UpperLeft, HintColor);
            hint.horizontalOverflow = HorizontalWrapMode.Wrap;
            hint.verticalOverflow = VerticalWrapMode.Overflow;
            hint.lineSpacing = 1.3f;
            PlaceTopLeft(hint.rectTransform, ControlX, 462f, CardWidth - Padding - ControlX, 34f);

            pageRoot = previousRoot;
            RefreshPlaytestControls();
        }

        private void SetPlaytestSharing(PlaytestChoice choice)
        {
            PlaytestConsent.Choice = choice;
            RefreshPlaytestControls();
        }

        /// <summary>Lights the current choice; feedback can only be sent while sharing is on.</summary>
        private void RefreshPlaytestControls()
        {
            if (playtestOnButton == null) return;

            bool allowed = PlaytestConsent.Allowed;
            SetToggleVisual(playtestOnButton, allowed);
            SetToggleVisual(playtestOffButton, !allowed);
            playtestFeedbackButton.interactable = allowed;
            SetToggleVisual(playtestFeedbackButton, false);
            Text label = playtestFeedbackButton.GetComponentInChildren<Text>();
            if (label != null && !allowed) label.color = new Color(LabelColor.r, LabelColor.g, LabelColor.b, 0.3f);
        }
    }
}
