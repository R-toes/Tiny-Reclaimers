using NUnit.Framework;
using UnityEngine;
using TrashTD.Core.GameLoop;
using TrashTD.UI;

namespace TrashTD.Tests
{
    /// <summary>
    /// A running stage pauses when the game window loses focus (alt-tab), so enemies do not walk through while the player is
    /// away. GameplayHUDUI.ShouldPauseOnFocusLoss is the rule; the same state the Escape key pauses from.
    /// </summary>
    public class FocusPauseTests
    {
        private const string Key = "gameplay.pauseOnFocusLoss";
        private bool hadKey;
        private int previous;

        [SetUp]
        public void SetUp()
        {
            hadKey = PlayerPrefs.HasKey(Key);
            previous = PlayerPrefs.GetInt(Key, 0);
        }

        [TearDown]
        public void TearDown()
        {
            if (hadKey) PlayerPrefs.SetInt(Key, previous);
            else PlayerPrefs.DeleteKey(Key);
        }

        [Test]
        public void LosingFocus_WhileAStageIsRunning_Pauses()
        {
            Assert.IsTrue(GameplayHUDUI.ShouldPauseOnFocusLoss(false, true, GamePlayState.Playing));
        }

        [Test]
        public void GainingFocus_NeverPauses()
        {
            Assert.IsFalse(GameplayHUDUI.ShouldPauseOnFocusLoss(true, true, GamePlayState.Playing));
        }

        [Test]
        public void WhenTheSettingIsOff_NeverPauses()
        {
            Assert.IsFalse(GameplayHUDUI.ShouldPauseOnFocusLoss(false, false, GamePlayState.Playing));
        }

        [TestCase(GamePlayState.NotStarted)]
        [TestCase(GamePlayState.Paused)]
        [TestCase(GamePlayState.Victory)]
        [TestCase(GamePlayState.Defeat)]
        public void OnlyARunningStage_CanBePaused(GamePlayState state)
        {
            Assert.IsFalse(GameplayHUDUI.ShouldPauseOnFocusLoss(false, true, state), state.ToString());
        }

        [Test]
        public void TheSetting_IsOffByDefaultInTheEditor_AndCanBeSwitched()
        {
            PlayerPrefs.DeleteKey(Key);
            // In the editor the Game view loses focus whenever another editor window is clicked, so it starts off there
            Assert.IsFalse(GameplaySettings.PauseOnFocusLoss);

            GameplaySettings.PauseOnFocusLoss = true;
            Assert.IsTrue(GameplaySettings.PauseOnFocusLoss);
        }
    }
}
