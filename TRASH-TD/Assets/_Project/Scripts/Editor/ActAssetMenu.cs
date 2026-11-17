using UnityEditor;
using UnityEngine;
using TrashTD.Core.GameLoop;
using TrashTD.Data;

namespace TrashTD.Editor
{
    /// <summary>
    /// TRASH TD > Acts. The game reads its acts from ActData assets in Resources/Acts (ActCatalog). Until Act 1's asset
    /// exists the catalog builds Act 1 in memory from the original six stages, so the game plays the same either way;
    /// creating the asset makes its marker positions, map art and zone text editable in the inspector.
    /// </summary>
    public static class ActAssetMenu
    {
        [MenuItem("TRASH TD/Acts/Check Acts", false, 41)]
        private static void CheckActs()
        {
            ActCatalog.UseActs(null);
            var problems = ActCatalog.FindProblems(ActCatalog.All);
            if (problems.Count == 0)
            {
                Debug.Log("[Acts] " + ActCatalog.All.Count + " act(s), no problems.");
                return;
            }

            foreach (string problem in problems) Debug.LogWarning("[Acts] " + problem);
            Debug.LogError("[Acts] " + problems.Count + " problem(s) with the acts; see the warnings above.");
        }
    }
}
