namespace TrashTD.Core
{
    /// <summary>
    /// The two scenes the game switches between. Both are in Build Settings (SceneNamesTests checks that). Use these instead
    /// of typing the names: the gameplay scene is still called "GameplayTest" from when it was a test scene, and renaming
    /// it later is now one edit here (plus the scene file and its Build Settings entry).
    /// </summary>
    public static class SceneNames
    {
        public const string MainMenu = "MainMenu";
        public const string Gameplay = "GameplayTest";

        /// <summary>The gameplay scene's asset path (editor tools).</summary>
        public const string GameplayScenePath = "Assets/_Project/Scenes/" + Gameplay + ".unity";
    }
}
