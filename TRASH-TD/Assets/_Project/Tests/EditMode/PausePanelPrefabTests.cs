using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TrashTD.UI;

namespace TrashTD.Tests
{
    /// <summary>
    /// The pause menu binds to its layout by element name (GameplayHUDUI.TryResolvePausePanel). These tests keep the code
    /// layout and any baked prefab in step with that code: rename an element in one place and they fail.
    /// </summary>
    public class PausePanelPrefabTests
    {
        private GameObject canvas;

        [SetUp]
        public void SetUp()
        {
            canvas = new GameObject("TestCanvas", typeof(Canvas), typeof(CanvasScaler));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(canvas);
        }

        [Test]
        public void LayoutBuiltInCode_HasEveryPartTheHudBindsTo()
        {
            GameObject panel = GameplayHUDUI.BuildPausePanelForPrefab(canvas.transform);

            bool ok = GameplayHUDUI.ValidatePausePanel(panel, out string missing);

            Assert.IsTrue(ok, "missing: " + missing);
        }

        [Test]
        public void LayoutBuiltInCode_KeepsTheCardNameOtherScreensFind()
        {
            // AudioSettingsUI and ReclaimersIntelUI add their pause-menu buttons to the transform called "PauseCard"
            GameObject panel = GameplayHUDUI.BuildPausePanelForPrefab(canvas.transform);

            Assert.IsNotNull(panel.transform.Find("PauseCard"));
        }

        [Test]
        public void LayoutBuiltInCode_HasNoListenersOrFeedbackComponents()
        {
            // The prefab holds layout only: nothing dynamic may be baked into it
            GameObject panel = GameplayHUDUI.BuildPausePanelForPrefab(canvas.transform);

            Button[] buttons = panel.GetComponentsInChildren<Button>(true);
            Assert.AreEqual(3, buttons.Length);
            foreach (Button button in buttons)
            {
                Assert.AreEqual(0, button.onClick.GetPersistentEventCount(), button.name);
                Assert.IsNull(button.GetComponent<MenuButtonFeedback>(), button.name + " has hover feedback baked in");
            }
        }

        [Test]
        public void AnObjectWithoutTheLayout_IsRejected_WithTheMissingNames()
        {
            var empty = new GameObject("NotThePanel", typeof(CanvasGroup));
            empty.transform.SetParent(canvas.transform, false);

            bool ok = GameplayHUDUI.ValidatePausePanel(empty, out string missing);

            Assert.IsFalse(ok);
            StringAssert.Contains("PauseCard", missing);
            StringAssert.Contains("ResumeButton", missing);
        }

        [Test]
        public void BakedPrefab_WhenPresent_HasEveryPartTheHudBindsTo()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameplayHUDUI.PausePanelPrefabAssetPath);
            if (prefab == null) Assert.Ignore("No baked pause panel (TRASH TD > UI > Bake Pause Panel Prefab): the code layout is used.");

            bool ok = GameplayHUDUI.ValidatePausePanel(prefab, out string missing);

            Assert.IsTrue(ok, "The baked prefab is out of date, bake it again. Missing: " + missing);
        }
    }
}
