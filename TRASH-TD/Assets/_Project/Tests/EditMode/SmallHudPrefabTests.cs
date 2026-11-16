using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TrashTD.UI;

namespace TrashTD.Tests
{
    /// <summary>
    /// The operator death notice, the discard confirmation and the cooldown toast bind to their layout by element name.
    /// These tests keep each code layout and any baked prefab in step with that code (see UiPrefabBaker).
    /// </summary>
    public class SmallHudPrefabTests
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
        public void DeathNoticePlate_BuiltInCode_HasEveryNamedPart()
        {
            GameObject plate = OperatorDeathNoticeUI.BuildPlateForPrefab(canvas.transform);

            Assert.IsTrue(OperatorDeathNoticeUI.ValidatePlate(plate, out string missing), "missing: " + missing);
        }

        [Test]
        public void DiscardConfirmation_BuiltInCode_HasEveryNamedPart_AndNoFeedbackBakedIn()
        {
            GameObject panel = GameplayHUDUI.BuildDiscardPanelForPrefab(canvas.transform);

            Assert.IsTrue(GameplayHUDUI.ValidateDiscardPanel(panel, out string missing), "missing: " + missing);
            // The message is found by the path "DiscardCard/Message" when the confirmation is shown
            Assert.IsNotNull(panel.transform.Find("DiscardCard/Message"));
            foreach (Button button in panel.GetComponentsInChildren<Button>(true))
            {
                Assert.IsNull(button.GetComponent<MenuButtonFeedback>(), button.name + " has hover feedback baked in");
            }
        }

        [Test]
        public void CooldownToast_BuiltInCode_HasEveryNamedPart()
        {
            GameObject toast = GameplayHUDUI.BuildCooldownToastForPrefab(canvas.transform);

            Assert.IsTrue(GameplayHUDUI.ValidateCooldownToast(toast, out string missing), "missing: " + missing);
        }

        [Test]
        public void AnObjectWithoutTheLayout_IsRejectedByEachPanel()
        {
            var empty = new GameObject("NotThePanel", typeof(RectTransform), typeof(CanvasGroup));
            empty.transform.SetParent(canvas.transform, false);

            Assert.IsFalse(OperatorDeathNoticeUI.ValidatePlate(empty, out string plateMissing));
            StringAssert.Contains("Portrait", plateMissing);
            Assert.IsFalse(GameplayHUDUI.ValidateDiscardPanel(empty, out string discardMissing));
            StringAssert.Contains("ConfirmDiscardButton", discardMissing);
            Assert.IsFalse(GameplayHUDUI.ValidateCooldownToast(empty, out string toastMissing));
            StringAssert.Contains("Message", toastMissing);
        }

        [Test]
        public void BakedPrefabs_WhenPresent_HaveEveryNamedPart()
        {
            var plate = AssetDatabase.LoadAssetAtPath<GameObject>(OperatorDeathNoticeUI.PrefabAssetPath);
            if (plate != null)
                Assert.IsTrue(OperatorDeathNoticeUI.ValidatePlate(plate, out string m1), "Death notice prefab is out of date, bake it again. Missing: " + m1);

            var discard = AssetDatabase.LoadAssetAtPath<GameObject>(GameplayHUDUI.DiscardPanelPrefabAssetPath);
            if (discard != null)
                Assert.IsTrue(GameplayHUDUI.ValidateDiscardPanel(discard, out string m2), "Discard confirmation prefab is out of date, bake it again. Missing: " + m2);

            var toast = AssetDatabase.LoadAssetAtPath<GameObject>(GameplayHUDUI.CooldownToastPrefabAssetPath);
            if (toast != null)
                Assert.IsTrue(GameplayHUDUI.ValidateCooldownToast(toast, out string m3), "Cooldown toast prefab is out of date, bake it again. Missing: " + m3);
        }
    }
}
