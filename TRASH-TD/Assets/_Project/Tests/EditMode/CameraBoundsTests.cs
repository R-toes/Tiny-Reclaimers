using NUnit.Framework;
using UnityEngine;
using TrashTD.Core.GameLoop;
using TrashTD.Data;

namespace TrashTD.Tests
{
    /// <summary>
    /// The camera must never show void outside a stage's bounds artwork, at any zoom, and must not jump as the view
    /// outgrows the bounds (the Stage 5 and 6 "snapping" bug). These test the pure rules the game uses:
    /// GameplayCameraController.CalculateViewLimits and StageBootstrapper.CalculateHomeOrthoSize.
    /// </summary>
    public class CameraBoundsTests
    {
        private const float Aspect = 16f / 9f;
        private const float Tolerance = 0.001f;

        // ------------------------------------------------------------------ strict limits (stages with bounds art)

        private static void AssertViewInside(Rect bounds, Vector2 home, float homeSize, float size, float aspect)
        {
            GameplayCameraController.CalculateViewLimits(bounds, home, homeSize, size, aspect, true, out Vector2 min, out Vector2 max);
            Vector2 centre = home + GameplayCameraController.CalculateZoomCentreShift(homeSize, size);
            float halfWidth = size * aspect;
            float halfHeight = size;

            foreach (Vector2 offset in new[] { new Vector2(min.x, min.y), new Vector2(max.x, min.y), new Vector2(min.x, max.y), new Vector2(max.x, max.y) })
            {
                Vector2 view = centre + offset;
                Assert.GreaterOrEqual(view.x - halfWidth, bounds.xMin - Tolerance, "left edge at size " + size);
                Assert.LessOrEqual(view.x + halfWidth, bounds.xMax + Tolerance, "right edge at size " + size);
                Assert.GreaterOrEqual(view.y - halfHeight, bounds.yMin - Tolerance, "bottom edge at size " + size);
                Assert.LessOrEqual(view.y + halfHeight, bounds.yMax + Tolerance, "top edge at size " + size);
            }
        }

        [Test]
        public void StrictLimits_AtHomeZoom_LetTheViewTouchTheBoundsAndNoFurther()
        {
            var bounds = Rect.MinMaxRect(0f, 0f, 20f, 12f);
            var home = new Vector2(10f, 6f);

            GameplayCameraController.CalculateViewLimits(bounds, home, 4f, 4f, Aspect, true, out Vector2 min, out Vector2 max);

            float halfWidth = 4f * Aspect;
            Assert.AreEqual(bounds.xMin + halfWidth - home.x, min.x, Tolerance);
            Assert.AreEqual(bounds.xMax - halfWidth - home.x, max.x, Tolerance);
            Assert.AreEqual(bounds.yMin + 4f - home.y, min.y, Tolerance);
            Assert.AreEqual(bounds.yMax - 4f - home.y, max.y, Tolerance);
        }

        [Test]
        public void StrictLimits_KeepTheWholeViewInsideTheBounds_AtEveryZoom()
        {
            var bounds = Rect.MinMaxRect(0f, 0f, 20f, 12f);
            var home = new Vector2(10f, 6f);

            // From zoomed in to zoomed out as far as the view still fits (5.6 * 16/9 * 2 = 19.9 wide)
            foreach (float size in new[] { 1f, 2f, 2.5f, 4f, 5f, 5.5f })
            {
                AssertViewInside(bounds, home, 4f, size, Aspect);
            }
        }

        [Test]
        public void StrictLimits_HoldOnNarrowerScreens()
        {
            var bounds = Rect.MinMaxRect(-3f, -3f, 17f, 11f);
            var home = new Vector2(7f, 4f);

            foreach (float aspect in new[] { 4f / 3f, 16f / 10f, 16f / 9f, 21f / 9f })
            {
                AssertViewInside(bounds, home, 3.5f, 3.5f, aspect);
                AssertViewInside(bounds, home, 3.5f, 2f, aspect);
            }
        }

        [Test]
        public void StrictLimits_WhenTheViewIsTallerThanTheBounds_CentreItInsteadOfPinningAnEdge()
        {
            // The Stage 5/6 bug: once the view outgrew the art's height it used to be pinned under the top bar, which made the
            // picture jump. It must sit centred on the bounds on that axis.
            var bounds = Rect.MinMaxRect(0f, 0f, 20f, 8f);
            var home = new Vector2(10f, 4f);
            const float homeSize = 3.5f;
            const float size = 5f; // 10 tall > 8

            GameplayCameraController.CalculateViewLimits(bounds, home, homeSize, size, Aspect, true, out Vector2 min, out Vector2 max);

            Vector2 centre = home + GameplayCameraController.CalculateZoomCentreShift(homeSize, size);
            Assert.AreEqual(min.y, max.y, Tolerance, "no room to move vertically");
            Assert.AreEqual(bounds.center.y, centre.y + min.y, Tolerance, "centred on the bounds");
        }

        [Test]
        public void StrictLimits_RoomShrinksSmoothlyAsTheViewGrows_ThenStaysCentred()
        {
            var bounds = Rect.MinMaxRect(0f, 0f, 24f, 8f);
            var home = new Vector2(12f, 4f);
            float previousRoom = float.NaN;
            float previousMid = float.NaN;

            for (float size = 2f; size <= 7f; size += 0.05f)
            {
                GameplayCameraController.CalculateViewLimits(bounds, home, 3.5f, size, Aspect, true, out Vector2 min, out Vector2 max);
                Vector2 centre = home + GameplayCameraController.CalculateZoomCentreShift(3.5f, size);

                float room = max.y - min.y;
                Assert.GreaterOrEqual(room, 0f);
                if (!float.IsNaN(previousRoom))
                {
                    Assert.LessOrEqual(room, previousRoom + Tolerance, "the room to move never grows as you zoom out (size " + size + ")");
                    Assert.LessOrEqual(previousRoom - room, 0.1f + Tolerance, "and shrinks in small steps, no jump (size " + size + ")");
                }
                previousRoom = room;

                float mid = centre.y + (min.y + max.y) * 0.5f;
                Assert.AreEqual(bounds.center.y, mid, Tolerance, "the middle of the allowed range stays on the bounds' centre");
                if (!float.IsNaN(previousMid)) Assert.AreEqual(previousMid, mid, Tolerance);
                previousMid = mid;
            }
        }

        // ------------------------------------------------------------------ non-strict limits (no bounds art)

        [Test]
        public void LooseLimits_AreSymmetric_AndWidenWithTheMapSize()
        {
            var bounds = Rect.MinMaxRect(0f, 0f, 20f, 12f);

            GameplayCameraController.CalculateViewLimits(bounds, new Vector2(10f, 6f), 4f, 4f, Aspect, false, out Vector2 min, out Vector2 max);

            Assert.AreEqual(-max.x, min.x, Tolerance);
            Assert.AreEqual(-max.y, min.y, Tolerance);
            // The map is 20 x 12 plus a 0.3 edge margin; the visible play area is size*aspect wide and 76% of the height
            Assert.AreEqual(10.3f - 4f * Aspect, max.x, 0.01f);
            Assert.AreEqual(6.3f - 4f * 0.7593f, max.y, 0.01f);
        }

        [Test]
        public void LooseLimits_NeverNegative_WhenTheViewIsBiggerThanTheMap()
        {
            var bounds = Rect.MinMaxRect(0f, 0f, 10f, 6f);

            GameplayCameraController.CalculateViewLimits(bounds, new Vector2(5f, 3f), 4f, 9f, Aspect, false, out Vector2 min, out Vector2 max);

            Assert.AreEqual(0f, max.x, Tolerance);
            Assert.AreEqual(0f, max.y, Tolerance);
            Assert.AreEqual(0f, min.x, Tolerance);
            Assert.AreEqual(0f, min.y, Tolerance);
        }

        // ------------------------------------------------------------------ zoom centre

        [Test]
        public void ZoomCentreShift_IsZeroAtHome_AndMovesOppositelyWhenZoomingInAndOut()
        {
            Assert.AreEqual(0f, GameplayCameraController.CalculateZoomCentreShift(4f, 4f).y, Tolerance);

            float zoomedIn = GameplayCameraController.CalculateZoomCentreShift(4f, 2f).y;
            float zoomedOut = GameplayCameraController.CalculateZoomCentreShift(4f, 6f).y;
            Assert.Greater(zoomedIn, 0f);
            Assert.AreEqual(-zoomedIn, zoomedOut, Tolerance);
            Assert.AreEqual(0f, GameplayCameraController.CalculateZoomCentreShift(4f, 2f).x, Tolerance, "only vertical");
        }

        // ------------------------------------------------------------------ home view size

        [Test]
        public void HomeOrthoSize_WithoutBounds_ShowsTheWholeMapBetweenTheHudBars()
        {
            // 7 tall + 0.35 padding, in the 75.9% of the screen the bars leave, halved
            float size = StageBootstrapper.CalculateHomeOrthoSize(7f, 7f, false, 0f, 0f, Aspect);

            Assert.AreEqual((7f + 0.35f) / 0.759f * 0.5f, size, Tolerance);
        }

        [Test]
        public void HomeOrthoSize_IsNeverBelowTheMinimum()
        {
            Assert.AreEqual(4.10f, StageBootstrapper.CalculateHomeOrthoSize(2f, 2f, false, 0f, 0f, Aspect), Tolerance);
        }

        [Test]
        public void HomeOrthoSize_WithBounds_IsCappedToFitTheArtInHeight()
        {
            // Natural size would be 6.82; the art is only 12 tall (half 6), and 40 wide
            float size = StageBootstrapper.CalculateHomeOrthoSize(10f, 7f, true, 40f, 12f, Aspect);

            Assert.AreEqual(6f, size, Tolerance);
        }

        [Test]
        public void HomeOrthoSize_WithBounds_IsCappedToFitTheArtInWidth()
        {
            float size = StageBootstrapper.CalculateHomeOrthoSize(10f, 7f, true, 20f, 40f, Aspect);

            Assert.AreEqual(20f / (2f * Aspect), size, Tolerance);
        }

        [Test]
        public void HomeOrthoSize_WithBounds_IsNotCapped_WhenThePlayableGridWouldNotFitBetweenTheBars()
        {
            // The art is 8 tall: its half (4) is smaller than what the 7-tall grid needs (4.84), so capping would hide the grid
            float natural = StageBootstrapper.CalculateHomeOrthoSize(10f, 7f, false, 0f, 0f, Aspect);
            float size = StageBootstrapper.CalculateHomeOrthoSize(10f, 7f, true, 40f, 8f, Aspect);

            Assert.AreEqual(natural, size, Tolerance);
        }

        [Test]
        public void HomeOrthoSize_IgnoresBounds_WhenTheAspectIsUnknown()
        {
            float natural = StageBootstrapper.CalculateHomeOrthoSize(10f, 7f, false, 0f, 0f, Aspect);

            Assert.AreEqual(natural, StageBootstrapper.CalculateHomeOrthoSize(10f, 7f, true, 20f, 12f, 0f), Tolerance);
        }

        // ------------------------------------------------------------------ the real stages

        [Test]
        public void EveryStageWithBoundsArt_HasArtThatCoversThePlayableGrid()
        {
            // A wrong boundsTileOffset (the art not lined up with the map) is the commonest way to get void at the edge
            int checkedStages = 0;
            for (int number = 1; number <= 6; number++)
            {
                StageData stage = Resources.Load<StageData>("Stages/STAGE_" + number.ToString("00"));
                Assert.IsNotNull(stage, "STAGE_" + number.ToString("00"));
                if (stage.boundsVisualSprite == null) continue;
                checkedStages++;

                float pixelsPerTile = stage.visualTilePixelSize > 0 ? stage.visualTilePixelSize : 32f;
                float left = stage.boundsTileOffset.x;
                float bottom = stage.boundsTileOffset.y;
                float right = left + stage.boundsVisualSprite.rect.width / pixelsPerTile;
                float top = bottom + stage.boundsVisualSprite.rect.height / pixelsPerTile;

                string id = stage.stageId;
                Assert.LessOrEqual(left, 0f, id + ": the bounds art starts to the right of the grid");
                Assert.LessOrEqual(bottom, 0f, id + ": the bounds art starts above the grid's bottom edge");
                Assert.GreaterOrEqual(right, stage.gridWidth, id + ": the bounds art ends before the grid's right edge");
                Assert.GreaterOrEqual(top, stage.gridHeight, id + ": the bounds art ends before the grid's top edge");
            }

            Assert.Greater(checkedStages, 0, "no stage has bounds art, is the test still looking in the right place?");
        }

        // ------------------------------------------------------------------ any screen shape (phones)

        [Test]
        public void MaxSizeInsideBounds_FitsTheBoundsWidthOnAWideScreen()
        {
            var bounds = Rect.MinMaxRect(0f, 0f, 20f, 12f);
            float wide = 20f / 9f;
            float size = GameplayCameraController.CalculateMaxSizeInsideBounds(bounds, wide);
            Assert.AreEqual(20f / (2f * wide), size, Tolerance, "the width is the limit on a wide screen");
            Assert.Less(size, 6f, "smaller than the height-limited size");
        }

        [Test]
        public void MaxSizeInsideBounds_FitsTheBoundsHeightOnANarrowScreen()
        {
            var bounds = Rect.MinMaxRect(0f, 0f, 20f, 12f);
            Assert.AreEqual(6f, GameplayCameraController.CalculateMaxSizeInsideBounds(bounds, 4f / 3f), Tolerance);
        }

        [TestCase(4f / 3f)]
        [TestCase(16f / 9f)]
        [TestCase(2f)]
        [TestCase(20f / 9f)]
        [TestCase(21f / 9f)]
        [TestCase(2.6f)]
        public void CappedView_NeverShowsPastTheBounds_OnAnyAspect(float aspect)
        {
            var bounds = Rect.MinMaxRect(-2f, -1f, 18f, 11f);
            Vector2 home = new Vector2(8f, 4f);
            const float homeSize = 5.6f; // framed for 16:9: too big for a wide phone
            foreach (float zoom in new[] { 1f, 1.5f, 2.25f })
            {
                float size = Mathf.Min(homeSize / zoom, GameplayCameraController.CalculateMaxSizeInsideBounds(bounds, aspect));
                AssertViewInside(bounds, home, homeSize, size, aspect);
            }
        }
    }
}
