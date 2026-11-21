using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using TrashTD.Core;
using TrashTD.UI;

namespace TrashTD.Tests
{
    /// <summary>
    /// Plays the real gameplay scene for a moment in each graphics style and checks the picture the CRT shows is not
    /// black. Needs a GPU, so it is skipped when the editor runs without graphics (-nographics).
    /// </summary>
    public class CrtRenderSmokeTests
    {
        [UnityTest]
        public IEnumerator GameplayScene_PictureIsNotBlack_InEveryStyle(
            [Values(CrtStyle.FullTv, CrtStyle.Classic, CrtStyle.Off)] CrtStyle style)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("No graphics device (-nographics).");

            CrtStyle previousStyle = GameGraphicsSettings.CrtStyle;
            yield return new EnterPlayMode(false);

            GameGraphicsSettings.CrtStyle = style;
            SceneManager.LoadScene(SceneNames.Gameplay);
            for (int i = 0; i < 90; i++) yield return null;
            yield return new WaitForEndOfFrame();

            float litShare = CrtDisplay.SampleNonBlackFraction();
            GameGraphicsSettings.CrtStyle = previousStyle;
            yield return new ExitPlayMode();

            Assert.Greater(litShare, 0.15f, style + ": the game picture is almost all black (lit share " + litShare + ")");
        }
    }
}
