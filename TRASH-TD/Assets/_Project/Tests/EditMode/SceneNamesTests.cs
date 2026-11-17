using NUnit.Framework;
using UnityEditor;
using TrashTD.Core;

namespace TrashTD.Tests
{
    /// <summary>
    /// SceneManager.LoadScene by name only works for scenes in Build Settings. These keep SceneNames in step with the scene
    /// files and the build, so renaming a scene (or dropping it from the build) fails here, not in a player's game.
    /// </summary>
    public class SceneNamesTests
    {
        private static bool InBuild(string sceneName)
        {
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled && System.IO.Path.GetFileNameWithoutExtension(scene.path) == sceneName) return true;
            }
            return false;
        }

        [Test]
        public void TheMainMenuScene_IsInTheBuild()
        {
            Assert.IsTrue(InBuild(SceneNames.MainMenu), SceneNames.MainMenu + " is not an enabled scene in Build Settings");
        }

        [Test]
        public void TheGameplayScene_IsInTheBuild()
        {
            Assert.IsTrue(InBuild(SceneNames.Gameplay), SceneNames.Gameplay + " is not an enabled scene in Build Settings");
        }

        [Test]
        public void TheGameplayScenePath_PointsAtARealScene()
        {
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(SceneNames.GameplayScenePath), SceneNames.GameplayScenePath);
        }
    }
}
