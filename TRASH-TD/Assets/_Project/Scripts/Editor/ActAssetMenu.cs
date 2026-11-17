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
        [MenuItem("TRASH TD/Acts/Create Act 1 Asset", false, 40)]
        private static void CreateAct1Asset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<ActData>(ActCatalog.Act1AssetPath);
            if (existing != null)
            {
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                EditorUtility.DisplayDialog("Create Act 1 Asset", "Act 1 already has an asset (" + ActCatalog.Act1AssetPath + ").", "OK");
                return;
            }

            ActData act = ActCatalog.CreateBuiltInAct1();
            if (act.StageCount == 0)
            {
                Object.DestroyImmediate(act);
                Debug.LogError("[Acts] Act 1 not created: none of the STAGE_01 to STAGE_06 assets could be loaded from Resources/Stages.");
                return;
            }

            if (!AssetDatabase.IsValidFolder("Assets/Resources/Acts")) AssetDatabase.CreateFolder("Assets/Resources", "Acts");
            act.name = "ACT_01";
            AssetDatabase.CreateAsset(act, ActCatalog.Act1AssetPath);
            AssetDatabase.SaveAssets();
            ActCatalog.UseActs(null); // read the acts again

            Selection.activeObject = act;
            EditorGUIUtility.PingObject(act);
            Debug.Log("[Acts] Created " + ActCatalog.Act1AssetPath + " from the original six stages and marker positions. Commit it with its .meta.");
        }

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
