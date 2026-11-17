using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TrashTD.Audio;
using TrashTD.Data;

namespace TrashTD.UI
{
    public enum TutorialDialogueLayout
    {
        Gameplay, // Big, leaning in from the bottom-right corner; the task strip sits on the HUD's bottom-right panel
        Compact,  // Bottom-left and small, under the draft screen's card column; stays on screen
        StageMap  // As Gameplay; the task strip sits above the stage map's QA SANDBOX button
    }

    /// <summary>
    /// Coalesce's side of the tutorial: a big pixel-scaled Coalesce leaning in from the bottom-right corner, close
    /// to the camera (feet and shell past the screen edges), with a terminal-style speech bubble beside the head.
    /// Lines type out with a voice blip per character, and Coalesce's sheet animates only while text is typing.
    ///
    /// Coalesce is only on screen while talking: it slides in from the right for a line and back out when the
    /// bubble hides. Each line starts with a small pose (<see cref="TutorialEmote"/>). A task line types out,
    /// holds a moment, then Coalesce leaves and a pinned ▶ TASK strip keeps the instruction up. The compact (draft)
    /// layout and lines with choices keep Coalesce on screen.
    ///
    /// Talk lines dim the screen and catch every click: a click (or Space) finishes the line, the next one
    /// advances (<see cref="Advanced"/>). Task lines leave the game clickable and wait for the player. With the
    /// spotlight on (stage map), the dim is darker and leaves a circle of normal brightness around whatever is
    /// being pointed at. A pulsing hazard-yellow frame can point at any UI element on any canvas. Unscaled time
    /// throughout.
    /// </summary>
    public sealed partial class TutorialDialogueUI : MonoBehaviour
    {
        private const int CanvasSortingOrder = 250; // Above the HUD (100) and the draft (200), under results (300)
        private const int AboveResultsSortingOrder = 310; // Talking over the defeat screen
        private const float CharsPerSecond = 42f;
        private const int BlipEveryChars = 2;
        private const float BlipVolume = 0.35f;
        private const float DimAlpha = 0.28f;
        private const float SpotlightDimAlpha = 0.62f;
        private const float DimFadeSpeed = 2.5f; // Alpha per second
        private const float SpeakingFrameSpeedup = 1.25f; // Talking frames run a little faster than the idle sheet

        private static readonly Color Phosphor = new Color(0.22f, 0.91f, 0.50f, 1f);
        private static readonly Color PhosphorDim = new Color(0.22f, 0.91f, 0.50f, 0.45f);
        private static readonly Color PanelColor = new Color(0.012f, 0.034f, 0.022f, 0.96f);
        private static readonly Color HeaderColor = new Color(0.030f, 0.090f, 0.055f, 1f);
        private static readonly Color BodyColor = new Color(0.80f, 0.95f, 0.85f, 1f);
        private static readonly Color HazardYellow = new Color(0.98f, 0.80f, 0.18f, 1f);
        private static readonly Color HazardInk = new Color(0.07f, 0.06f, 0.03f, 1f);

        // Default (gameplay and stage map): the 32 px sheet at 20x, its bottom-right corner pushed past the screen's
        // so the feet and shell are cut off. Coalesce faces left, into the screen; the bubble sits left of the head
        // with its tail at the snout. Coalesce + bubble cover ~950x430 of 1920x1080 (~20% of the screen).
        private const float SpriteSize = 640f;
        private const float SpriteSunk = 150f;     // Below the bottom edge
        private const float SpriteOverhang = 150f; // Past the right edge
        private const float FeetPivotY = 0.15f;    // Emotes rotate and squash about the feet
        private const float BubbleWidth = 540f;
        private const float BubbleHeight = 230f;
        private const float BubbleRight = 410f;    // From the right edge: just left of the snout
        private const float BubbleBottom = 190f;
        private const float TailY = 80f;           // Up the bubble's right edge: level with the snout
        // Compact: bottom-left, under the draft screen's card column
        private const float CompactSpriteSize = 160f;
        private const float CompactBubbleWidth = 660f;
        private const float CompactBubbleHeight = 170f;
        // The task strip stands where Coalesce used to: on the HUD's bottom-right panel (~331 px tall). At 16:9 the
        // tutorial grid's empty right columns start at x ~1421, so it stays off the lane.
        private const float TaskStripWidth = 470f;
        private const float TaskStripHeight = 96f;
        private const float GameplayRight = 24f;
        private const float GameplayTaskBottom = 345f;
        // Stage map: QA SANDBOX and the ESC hint fill the bottom-right corner up to y ~118; the stage progress
        // readout sits top-right down to y ~-102
        private const float StageMapRight = 60f;
        private const float StageMapTaskBottom = 130f;
        // SKIP TUTORIAL: a hazard-yellow button parked where nothing else lives in each layout (top-right corner offsets)
        private const float SkipWidth = 240f;
        private const float SkipHeight = 48f;
        // Gameplay: under the surveillance feed's date and mission clock (y ~108-168), above the operator details
        // panel (y ~278 at 16:9), over the tutorial map's empty right-hand columns
        private const float GameplaySkipRight = 40f;
        private const float GameplaySkipTop = 182f;
        // Stage map: level with the ACT title plate, just left of the MAP OVERVIEW column (60 + 256 wide + 24 gap),
        // below the stage progress readout and above every stage marker
        private const float StageMapSkipRight = 340f;
        private const float StageMapSkipTop = 115f;
        // Draft: under the DECK count (y 32-80); the checkboxes and draft odds there are hidden in the tutorial draft,
        // and the cards and details panel start at y 230
        private const float CompactSkipRight = 28f;
        private const float CompactSkipTop = 96f;

        private const float SlideSeconds = 0.32f;
        private const float TaskHoldBase = 1.4f;     // A typed task line stays this long (plus per character)...
        private const float TaskHoldPerChar = 0.01f; // ...before Coalesce leaves and the task strip takes over
        private const float StripFadeSeconds = 0.2f;
        private const float SpotPadding = 46f;
        private const float SpotMinRadius = 80f;
        private const float SpotIrisStartRadius = 1300f; // A new spotlight closes in from this radius
        private const float SpotFollowSharpness = 9f;

        private Canvas canvas;
        private RectTransform root;
        private Button catcher;
        private Image dim;
        private RectTransform spotlight;
        private readonly List<Image> spotParts = new List<Image>(); // Hole, left, right, top, bottom
        private RectTransform presenter;
        private RectTransform speakerRig;
        private RectTransform speaker;
        private Image speakerImage;
        private RectTransform bubble;
        private Text nameText;
        private Text tagText;
        private Image tagPlate;
        private Text bodyText;
        private Text promptText;
        private RectTransform tail;
        private CanvasGroup taskStrip;
        private RectTransform taskStripRect;
        private Text taskText;
        private Vector2 taskStripHome;
        private Button skipButton;
        private RectTransform choiceRow;
        private Button primaryChoice;
        private Button secondaryChoice;
        private Action primaryAction;
        private Action secondaryAction;
        private RectTransform pointer;
        private readonly List<Image> pointerParts = new List<Image>();
        private GameObject skipConfirm;

        private Sprite[] frames;
        private float frameDuration = 0.15f;
        private float frameTimer;
        private int frameIndex;

        private string fullText = string.Empty;
        private float typedChars;
        private int lastBlipChar;
        private bool isTask;
        private bool bubbleVisible;
        private TutorialDialogueLayout layout = TutorialDialogueLayout.Gameplay;
        private RectTransform pointTarget;

        private float slide = 1f; // 0 = Coalesce and the bubble on screen, 1 = off it
        private float slideDistance;
        private float slideSign = 1f; // Off to the right (+1) or, in the compact layout, the left (-1)
        private float taskHold;
        private TutorialEmote emote;
        private bool emotePending;
        private float emoteTime = -1f;
        private float dimAlpha;
        private bool spotlightEnabled;
        private bool spotPlaced;
        private Vector2 spotCenter;
        private float spotRadius;

        private static AudioClip synthBlip;
        private static Sprite spotHoleSprite;

        /// <summary>A click (or Space) on a finished talk line.</summary>
        public event Action Advanced;
        /// <summary>The player confirmed SKIP TUTORIAL.</summary>
        public event Action SkipConfirmed;

        public bool IsTyping => bubbleVisible && typedChars < fullText.Length;
        /// <summary>A talk line or the skip prompt is up: the director keeps the game frozen.</summary>
        public bool IsBlockingGame => (bubbleVisible && !isTask) || IsSkipConfirmOpen;
        public bool IsSkipConfirmOpen => skipConfirm != null && skipConfirm.activeSelf;

        private bool HasChoices => primaryAction != null || secondaryAction != null;
        /// <summary>Coalesce can hand a task line over to the task strip and leave (not in the draft or with choices).</summary>
        private bool CanLeaveOnTask => layout != TutorialDialogueLayout.Compact && !HasChoices;
        private bool IsHandingOverTask => bubbleVisible && isTask && CanLeaveOnTask && !IsTyping &&
                                          taskHold >= TaskHoldBase + fullText.Length * TaskHoldPerChar;
        private bool IsPresenterIn => slide <= 0f;

        public static TutorialDialogueUI Create(string speakerName, Sprite[] speakerFrames, float speakerFrameDuration)
        {
            var go = new GameObject("TutorialDialogueCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var ui = go.AddComponent<TutorialDialogueUI>();
            ui.Build(speakerName, speakerFrames, speakerFrameDuration);
            return ui;
        }

        // ─────────────────────────────────────────────────────
        //  Public control
        // ─────────────────────────────────────────────────────

        /// <summary>
        /// Starts a line: Coalesce slides in if needed, strikes the pose, then types it. A talk line dims the screen
        /// and waits for a click; a task line doesn't, and hands over to the task strip once read.
        /// </summary>
        public void Say(string text, bool task, TutorialEmote pose = TutorialEmote.Nod)
        {
            fullText = string.IsNullOrEmpty(text) ? string.Empty : text.Trim();
            typedChars = 0f;
            lastBlipChar = 0;
            isTask = task;
            bubbleVisible = !string.IsNullOrEmpty(fullText);
            taskHold = 0f;
            emote = pose;
            emotePending = bubbleVisible;

            bubble.gameObject.SetActive(bubbleVisible);
            HideChoices();
            bodyText.text = string.Empty;
            promptText.text = string.Empty;
            tagText.text = task ? "▶ TASK" : "// COMMS";
            tagText.color = task ? HazardInk : PhosphorDim;
            tagPlate.color = task ? HazardYellow : Color.clear;
            if (task) taskText.text = fullText;
            ApplyBlocking();
        }

        /// <summary>
        /// Two buttons under the current line (e.g. TRY AGAIN / SKIP TUTORIAL), shown once it has typed out. The
        /// SKIP TUTORIAL tag hides meanwhile, and Coalesce stays on screen; the next Say clears them.
        /// </summary>
        public void ShowChoices(string primaryLabel, Action onPrimary, string secondaryLabel, Action onSecondary)
        {
            primaryAction = onPrimary;
            secondaryAction = onSecondary;
            primaryChoice.GetComponentInChildren<Text>().text = primaryLabel;
            secondaryChoice.GetComponentInChildren<Text>().text = secondaryLabel;
            skipButton.gameObject.SetActive(false);
            bodyText.rectTransform.offsetMin = new Vector2(18f, 66f);
        }

        private void HideChoices()
        {
            primaryAction = null;
            secondaryAction = null;
            choiceRow.gameObject.SetActive(false);
            skipButton.gameObject.SetActive(true);
            bodyText.rectTransform.offsetMin = new Vector2(18f, 30f);
        }

        /// <summary>Draws Coalesce above the results screen (the tutorial's defeat).</summary>
        public void BringAboveResults()
        {
            canvas.sortingOrder = AboveResultsSortingOrder;
        }

        /// <summary>Ends the line: Coalesce slides off screen with the bubble (e.g. while a wave runs).</summary>
        public void HideBubble()
        {
            bubbleVisible = false;
            fullText = string.Empty;
            emotePending = false;
            ApplyBlocking();
        }

        /// <summary>A new layout snaps Coalesce off screen; the next line slides in from that layout's side.</summary>
        public void SetLayout(TutorialDialogueLayout value)
        {
            if (layout == value) return;
            layout = value;
            slide = 1f;
            ApplyLayout();
        }

        /// <summary>Darker dim on talk lines, with a circle of normal brightness around the pointed-at element.</summary>
        public void SetSpotlight(bool enabled)
        {
            spotlightEnabled = enabled;
        }

        /// <summary>Frames this UI element (on any canvas) with pulsing brackets; null clears.</summary>
        public void PointAt(RectTransform target)
        {
            pointTarget = target;
            pointer.gameObject.SetActive(target != null);
        }

        /// <summary>Hides everything (e.g. under the pause menu) without losing the current line.</summary>
        public void SetShown(bool shown)
        {
            if (root.gameObject.activeSelf != shown) root.gameObject.SetActive(shown);
        }

        // ─────────────────────────────────────────────────────
        //  Update: sliding, typing, animation, input, pointer, dim
        // ─────────────────────────────────────────────────────
    }
}
