using NUnit.Framework;
using TrashTD.Core.GameLoop;
using TrashTD.Data;
using TrashTD.UI;
using UnityEngine;

namespace TrashTD.Tests
{
    /// <summary>Which stages darken the scenery outside the grid and fill the preview monitor's height. Both follow StageData.environment, never a stage id.</summary>
    public class StageBorderArtRulesTests
    {
        private static StageData Stage(StageEnvironment environment, bool withBoundsArt)
        {
            var stage = ScriptableObject.CreateInstance<StageData>();
            stage.environment = environment;
            stage.gridWidth = 11;
            stage.gridHeight = 8;
            if (withBoundsArt)
            {
                var texture = new Texture2D(4, 4);
                stage.boundsVisualSprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.zero);
            }
            return stage;
        }

        [TestCase(StageEnvironment.FallingItems)]
        [TestCase(StageEnvironment.Traffic)]
        [TestCase(StageEnvironment.AcidRain)]
        public void BorderDarkening_AppliesToStagesWithSceneryAroundTheMap(StageEnvironment environment)
        {
            Assert.IsTrue(StageBootstrapper.StageHasPlayableBorderDarkening(Stage(environment, true)));
        }

        [Test]
        public void BorderDarkening_NeedsBoundsArt_AndSkipsOtherEnvironments()
        {
            Assert.IsFalse(StageBootstrapper.StageHasPlayableBorderDarkening(Stage(StageEnvironment.FallingItems, false)),
                "No bounds art, nothing to darken.");
            Assert.IsFalse(StageBootstrapper.StageHasPlayableBorderDarkening(Stage(StageEnvironment.FactoryHeat, true)));
            Assert.IsFalse(StageBootstrapper.StageHasPlayableBorderDarkening(null));
        }

        [Test]
        public void PreviewFillsTheMonitorHeight_ForFallingItemsTrafficAndAcidRain()
        {
            Assert.IsTrue(StageMapPreview.ShouldFillPreviewHeight(Stage(StageEnvironment.FallingItems, false)));
            Assert.IsTrue(StageMapPreview.ShouldFillPreviewHeight(Stage(StageEnvironment.AcidRain, false)));
            Assert.IsFalse(StageMapPreview.ShouldFillPreviewHeight(Stage(StageEnvironment.FactoryHeat, false)));
            Assert.IsFalse(StageMapPreview.ShouldFillPreviewHeight(null));
        }
    }
}
