using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using TrashTD.Data;
using TrashTD.Editor;

namespace TrashTD.Tests
{
    /// <summary>The size rules for stage art and bounds art (StageArtRules), PNG size reading, and copying a layout.</summary>
    public class StageArtRulesTests
    {
        // ---------- stage (map) art ----------

        [Test]
        public void MapArt_ExactSize_IsAccepted()
        {
            ArtCheck check = StageArtRules.CheckMapArt(384, 192, 32, 12, 6, Vector2.zero, false);
            Assert.IsTrue(check.ok, check.message);
        }

        [Test]
        public void MapArt_WrongSize_IsRefusedByDefault_AndTheMessageSaysWhatIsNeeded()
        {
            ArtCheck check = StageArtRules.CheckMapArt(384, 224, 32, 12, 6, Vector2.zero, false);
            Assert.IsFalse(check.ok);
            StringAssert.Contains("384x192", check.message);
            StringAssert.Contains("384x224", check.message);
        }

        [Test]
        public void MapArt_NotWholeTiles_IsAlwaysRefused()
        {
            Assert.IsFalse(StageArtRules.CheckMapArt(390, 192, 32, 12, 6, Vector2.zero, false).ok);
            Assert.IsFalse(StageArtRules.CheckMapArt(390, 192, 32, 12, 6, Vector2.zero, true).ok, "Allowing larger art does not allow half tiles.");
        }

        [Test]
        public void MapArt_Larger_IsAcceptedOnlyWhenAllowed_AndOnlyIfItCoversTheGrid()
        {
            // 12x7 tiles of art for a 12x6 grid: one extra row on top at offset 0.
            Assert.IsFalse(StageArtRules.CheckMapArt(384, 224, 32, 12, 6, Vector2.zero, false).ok);
            Assert.IsTrue(StageArtRules.CheckMapArt(384, 224, 32, 12, 6, Vector2.zero, true).ok);

            // Shifted up by one tile it no longer covers the bottom row.
            Assert.IsFalse(StageArtRules.CheckMapArt(384, 224, 32, 12, 6, new Vector2(0f, 1f), true).ok);
            // Shifted down by one tile it still covers every tile (extra row above and below is fine).
            Assert.IsTrue(StageArtRules.CheckMapArt(384, 224, 32, 12, 6, new Vector2(0f, -1f), true).ok);
        }

        [Test]
        public void MapArt_SmallerThanTheGrid_IsRefusedEvenWhenLargerIsAllowed()
        {
            Assert.IsFalse(StageArtRules.CheckMapArt(352, 192, 32, 12, 6, Vector2.zero, true).ok);
            Assert.IsFalse(StageArtRules.CheckMapArt(384, 192, 32, 0, 0, Vector2.zero, false).ok, "No grid yet.");
        }

        // ---------- bounds art ----------

        [Test]
        public void BoundsArt_MustBeWholeTiles_AndAtLeastAsBigAsTheGrid()
        {
            Vector2 centred = StageArtRules.CentredBoundsOffset(576, 416, 32, 12, 6);
            Assert.IsTrue(StageArtRules.CheckBoundsArt(576, 416, 32, 12, 6, centred).ok);
            Assert.IsFalse(StageArtRules.CheckBoundsArt(580, 416, 32, 12, 6, centred).ok, "Not whole tiles.");
            Assert.IsFalse(StageArtRules.CheckBoundsArt(320, 416, 32, 12, 6, Vector2.zero).ok, "Narrower than the grid.");
            Assert.IsFalse(StageArtRules.CheckBoundsArt(576, 160, 32, 12, 6, Vector2.zero).ok, "Shorter than the grid.");
        }

        [Test]
        public void BoundsArt_MustContainTheGridAtItsOffset()
        {
            Assert.IsFalse(StageArtRules.CheckBoundsArt(576, 416, 32, 12, 6, new Vector2(1f, 0f)).ok, "The grid would start left of the art.");
            Assert.IsFalse(StageArtRules.CheckBoundsArt(576, 416, 32, 12, 6, new Vector2(-8f, 0f)).ok, "The grid would run past the right edge.");
            Assert.IsTrue(StageArtRules.CheckBoundsArt(576, 416, 32, 12, 6, new Vector2(-3f, -3f)).ok);
        }

        [Test]
        public void CentredBoundsOffset_MatchesWhatEveryExistingStageUses()
        {
            // art pixels, grid, offset stored in the stage asset
            Assert.AreEqual(new Vector2(-3f, -3f), StageArtRules.CentredBoundsOffset(576, 416, 32, 12, 6), "Stage 1");
            Assert.AreEqual(new Vector2(-4f, -2f), StageArtRules.CentredBoundsOffset(608, 384, 32, 11, 7), "Stage 2");
            Assert.AreEqual(new Vector2(-3f, -3f), StageArtRules.CentredBoundsOffset(640, 416, 32, 14, 7), "Stage 3");
            Assert.AreEqual(new Vector2(-3f, -4f), StageArtRules.CentredBoundsOffset(640, 480, 32, 14, 7), "Stage 4");
            Assert.AreEqual(new Vector2(-7f, -3f), StageArtRules.CentredBoundsOffset(864, 448, 32, 13, 8), "Stage 5");
            Assert.AreEqual(new Vector2(-3f, -2f), StageArtRules.CentredBoundsOffset(576, 288, 32, 12, 5), "Stage 6");
        }

        [Test]
        public void ExistingStages_HaveNoArtWarnings()
        {
            int withArt = 0;
            foreach (StageData stage in Resources.LoadAll<StageData>("Stages"))
            {
                if (!StageMakerLogic.HasGrid(stage)) continue;
                if (stage.mapVisualSprite != null || stage.boundsVisualSprite != null) withArt++;
                var artIssues = StageMakerLogic.Validate(stage, StageMakerRouting.ComputeRoutes(stage))
                    .Where(i => i.message.StartsWith("Map art") || i.message.StartsWith("Bounds art")).ToList();
                Assert.IsEmpty(artIssues, stage.name + ": " + string.Join(" | ", artIssues.Select(i => i.message)));
            }
            Assert.Greater(withArt, 0);
        }

        // ---------- reading a PNG's size ----------

        [Test]
        public void TryReadPngSize_ReadsARealPng()
        {
            string path = Path.Combine(Application.dataPath, "_Project/Art/Tiles/tex_tile_blocked.png");
            Assert.IsTrue(StageMakerWindow.TryReadPngSize(path, out int width, out int height));
            Assert.AreEqual(64, width);
            Assert.AreEqual(64, height);
        }

        [Test]
        public void TryReadPngSize_RefusesAnythingThatIsNotAPng()
        {
            string notPng = Path.Combine(Application.dataPath, "_Project/Scripts/Editor/StageArtRules.cs");
            Assert.IsFalse(StageMakerWindow.TryReadPngSize(notPng, out _, out _));
            Assert.IsFalse(StageMakerWindow.TryReadPngSize(Path.Combine(Application.dataPath, "does-not-exist.png"), out _, out _));
        }

        // ---------- copying a layout ----------

        [Test]
        public void CopyLayout_CopiesTheMapAsIndependentData_AndKeepsArtAndWaves()
        {
            var source = ScriptableObject.CreateInstance<StageData>();
            var target = ScriptableObject.CreateInstance<StageData>();
            try
            {
                StageMakerLogic.CreateStarter(source, 8, 4);
                StageMakerLogic.SetCustomWaypoints(source, 0, Enumerable.Range(0, 8).Select(x => new Vector2Int(x, 2)).ToList());
                target.wavesNormal = new[] { new WaveData { waveName = "keep me" } };
                target.visualTileOffset = new Vector2(0f, -1f);

                StageMakerLogic.CopyLayout(target, source);

                Assert.AreEqual(8, target.gridWidth);
                Assert.AreEqual(4, target.gridHeight);
                CollectionAssert.AreEqual(source.tileLayout, target.tileLayout);
                CollectionAssert.AreEqual(source.spawnPoints, target.spawnPoints);
                CollectionAssert.AreEqual(source.exitPoints, target.exitPoints);
                Assert.AreEqual(1, target.enemyPaths.Length);
                Assert.AreEqual("keep me", target.wavesNormal[0].waveName);
                Assert.AreEqual(new Vector2(0f, -1f), target.visualTileOffset);

                // Changing the copy must not change the original.
                StageMakerLogic.SetTile(target, new Vector2Int(3, 0), TileType.Blocked);
                target.enemyPaths[0].waypoints[0] = new Vector2Int(5, 5);
                Assert.AreEqual(TileType.HighGround, source.GetTile(3, 0));
                Assert.AreEqual(new Vector2Int(0, 2), source.enemyPaths[0].waypoints[0]);
            }
            finally
            {
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(target);
            }
        }
    }
}
