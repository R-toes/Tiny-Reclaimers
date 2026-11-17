using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using TrashTD.Data;
using TrashTD.UI;

namespace TrashTD.Tests
{
    /// <summary>
    /// The stage map rebuilds its markers when the player switches act. StageMapLayout positions markers from a cached list, so
    /// markers created after the first layout were never placed and all sat at the middle of the map (opening Act 2 and coming
    /// back broke Act 1). RefreshNodes positions the current markers.
    /// </summary>
    public class StageMapLayoutTests
    {
        private GameObject map;
        private StageMapLayout layout;
        private StageData stage;

        [SetUp]
        public void SetUp()
        {
            map = new GameObject("Map", typeof(RectTransform));
            ((RectTransform)map.transform).sizeDelta = new Vector2(1920f, 1080f);
            layout = map.AddComponent<StageMapLayout>();
            stage = ScriptableObject.CreateInstance<StageData>();
            stage.stageId = "STAGE_01";
            stage.mapName = "Test";
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(map);
            Object.DestroyImmediate(stage);
        }

        private StageMapNode MakeNode(Vector2 normalizedPosition)
        {
            var go = new GameObject("StageNode", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(map.transform, false);
            var node = go.AddComponent<StageMapNode>();
            node.Build(stage, 1, normalizedPosition, false, false);
            return node;
        }

        private static void AssertAt(StageMapNode node, Vector2 normalizedPosition)
        {
            var rect = (RectTransform)node.transform;
            // Origin top-left in the data, bottom-left in the anchors
            Assert.AreEqual(normalizedPosition.x, rect.anchorMin.x, 0.0001f);
            Assert.AreEqual(1f - normalizedPosition.y, rect.anchorMin.y, 0.0001f);
            Assert.AreEqual(rect.anchorMin, rect.anchorMax);
        }

        [Test]
        public void RefreshNodes_PositionsTheMarkersThatExist()
        {
            StageMapNode node = MakeNode(new Vector2(0.2f, 0.3f));

            layout.RefreshNodes();

            AssertAt(node, new Vector2(0.2f, 0.3f));
        }

        [Test]
        public void RefreshNodes_PositionsReplacementMarkers_AfterTheFirstLayoutCachedTheOldOnes()
        {
            StageMapNode first = MakeNode(new Vector2(0.2f, 0.3f));
            layout.RefreshNodes(); // the first layout caches this marker

            // Another act: the old markers go and new ones are made
            Object.DestroyImmediate(first.gameObject);
            StageMapNode second = MakeNode(new Vector2(0.8f, 0.6f));
            StageMapNode third = MakeNode(new Vector2(0.1f, 0.9f));
            layout.RefreshNodes();

            AssertAt(second, new Vector2(0.8f, 0.6f));
            AssertAt(third, new Vector2(0.1f, 0.9f));
        }
    }
}
