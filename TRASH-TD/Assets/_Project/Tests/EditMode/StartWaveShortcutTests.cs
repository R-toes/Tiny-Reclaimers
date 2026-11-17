using NUnit.Framework;
using TrashTD.Core.GameLoop;
using TrashTD.UI;

namespace TrashTD.Tests
{
    /// <summary>Space starts the wave under the same conditions as the START WAVE button. GameplayHUDUI.ShouldStartWaveOnSpace is the rule.</summary>
    public class StartWaveShortcutTests
    {
        private static bool Rule(
            GamePlayState state = GamePlayState.Playing,
            StagePhase phase = StagePhase.Preparation,
            bool blocked = false,
            bool sandbox = false,
            bool sandboxWaveRunning = false,
            bool tutorialAllows = true) =>
            GameplayHUDUI.ShouldStartWaveOnSpace(state, phase, blocked, sandbox, sandboxWaveRunning, tutorialAllows);

        [Test]
        public void StartsTheWave_InPreparation()
        {
            Assert.IsTrue(Rule());
        }

        [Test]
        public void DoesNothing_OutsideOfPreparation()
        {
            Assert.IsFalse(Rule(phase: StagePhase.CardPick), "A draft is still open.");
            Assert.IsFalse(Rule(phase: StagePhase.WaveActive), "A wave is already running.");
        }

        [Test]
        public void DoesNothing_WhenTheStageIsNotRunning()
        {
            Assert.IsFalse(Rule(state: GamePlayState.Paused));
            Assert.IsFalse(Rule(state: GamePlayState.NotStarted));
            Assert.IsFalse(Rule(state: GamePlayState.Victory));
            Assert.IsFalse(Rule(state: GamePlayState.Defeat));
        }

        [Test]
        public void DoesNothing_WhileSomethingElseOwnsTheKey()
        {
            Assert.IsFalse(Rule(blocked: true), "A text field, menu, briefing, draft or placement is open.");
        }

        [Test]
        public void DoesNothing_WhenTheTutorialHoldsTheWave()
        {
            Assert.IsFalse(Rule(tutorialAllows: false));
        }

        [Test]
        public void InTheSandbox_OnlyStartsAWave_NeverStopsOne()
        {
            Assert.IsTrue(Rule(sandbox: true, phase: StagePhase.CardPick), "The sandbox has no draft; its own wave state decides.");
            Assert.IsFalse(Rule(sandbox: true, sandboxWaveRunning: true), "Space must not stop a running sandbox wave.");
            Assert.IsFalse(Rule(sandbox: true, blocked: true));
            Assert.IsFalse(Rule(sandbox: true, state: GamePlayState.Paused));
        }
    }
}
