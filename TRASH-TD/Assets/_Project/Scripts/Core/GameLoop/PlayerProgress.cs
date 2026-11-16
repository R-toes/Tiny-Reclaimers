using TrashTD.Systems;
using TrashTD.UI;

namespace TrashTD.Core.GameLoop
{
    /// <summary>
    /// Everything about the player's saved progress that is more than one number: erasing it all.
    /// Used by Settings -> Gameplay -> SAVE DATA and by TRASH TD -> Reset Player Progress in the editor.
    /// </summary>
    public static class PlayerProgress
    {
        /// <summary>
        /// Makes this install a first-time player again: stage clears and stars, the tutorial, the Intel files. Deletes the
        /// save file and its backup, and clears what the session holds on to. Settings (audio, graphics, controls) are kept.
        /// </summary>
        public static void ResetAll()
        {
            SaveSystem.DeleteSave();
            EnemyIntelProgress.ClearSession();
            MapTutorialGuide.ResetSession();
        }
    }
}
