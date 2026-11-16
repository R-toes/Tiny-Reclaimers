using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TrashTD.Audio;

namespace TrashTD.Tests
{
    /// <summary>
    /// The settings panel binds to its layout by element name (AudioSettingsUI.ResolveReferences). These tests keep the
    /// code layout and any baked prefab in step with that code: rename an element in one place and they fail.
    /// </summary>
    public class SettingsPanelPrefabTests
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
        public void LayoutBuiltInCode_HasEveryElementTheCodeBindsTo()
        {
            GameObject panel = AudioSettingsUI.BuildPanelForPrefab(canvas.transform);

            bool ok = AudioSettingsUI.ValidatePanel(panel, out string missing);

            Assert.IsTrue(ok, "missing: " + missing);
        }

        [Test]
        public void LayoutBuiltInCode_HasNoListenersOrFeedbackComponents()
        {
            // The prefab holds layout only: nothing dynamic may be baked into it
            GameObject panel = AudioSettingsUI.BuildPanelForPrefab(canvas.transform);

            foreach (Button button in panel.GetComponentsInChildren<Button>(true))
            {
                Assert.AreEqual(0, button.onClick.GetPersistentEventCount(), button.name);
                Assert.IsNull(button.GetComponent<TrashTD.UI.MenuButtonFeedback>(), button.name + " has hover feedback baked in");
            }
        }

        [Test]
        public void AnObjectWithoutTheLayout_IsRejected_WithTheMissingNames()
        {
            var empty = new GameObject("NotThePanel", typeof(CanvasGroup));
            empty.transform.SetParent(canvas.transform, false);

            bool ok = AudioSettingsUI.ValidatePanel(empty, out string missing);

            Assert.IsFalse(ok);
            StringAssert.Contains("SettingsCard", missing);
            StringAssert.Contains("MUSICSlider", missing);
        }

        [Test]
        public void BakedPrefab_WhenPresent_HasEveryElementTheCodeBindsTo()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AudioSettingsUI.PrefabAssetPath);
            if (prefab == null) Assert.Ignore("No baked settings panel (TRASH TD > UI > Bake Settings Panel Prefab): the code layout is used.");

            bool ok = AudioSettingsUI.ValidatePanel(prefab, out string missing);

            Assert.IsTrue(ok, "The baked prefab is out of date, bake it again. Missing: " + missing);
        }
    }
}
