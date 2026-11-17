using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using TrashTD.Data;
using TrashTD.Enemies;
using TrashTD.Operators;
using TrashTD.Core.GameLoop;

namespace TrashTD.Editor
{
    // Creating the operator prefabs and the OperatorData helper.
    public partial class InitialAssetGenerator
    {
        private static GameObject CreateFramiliarPrefab(Sprite[] frames)
        {
            const string prefabName = "Prefab_OP_Framiliar";
            string path = $"{OperatorPrefabFolder}/{prefabName}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                var existingOp = existing.GetComponent<TrashTD.Operators.FramiliarOperator>();
                if (existingOp != null && frames != null)
                {
                    existingOp.ConfigureAnimationFrames(frames);
                }
                var existingAnim = existing.GetComponent<TrashTD.Operators.OperatorSpriteAnimation>();
                if (existingAnim != null && frames != null && frames.Length > 0)
                {
                    existingAnim.Configure(frames);
                }
                var existingSr = existing.GetComponent<SpriteRenderer>();
                if (existingSr != null && frames != null && frames.Length > 0 && frames[0] != null)
                {
                    existingSr.sprite = frames[0];
                }
                EditorUtility.SetDirty(existing);
                return existing;
            }

            var go = new GameObject(prefabName);
            var spriteRenderer = go.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = frames != null && frames.Length > 0 ? frames[0] : null;
            spriteRenderer.sortingOrder = 5;
            var op = go.AddComponent<TrashTD.Operators.FramiliarOperator>();
            if (frames != null && frames.Length > 0)
            {
                op.ConfigureAnimationFrames(frames);
            }
            var animation = go.AddComponent<TrashTD.Operators.OperatorSpriteAnimation>();
            if (frames != null && frames.Length > 0)
            {
                animation.Configure(frames);
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject CreateResicochetPrefab(Sprite[] c1, Sprite[] c2, Sprite[] c3)
        {
            const string prefabName = "Prefab_OP_Resicochet";
            string path = $"{OperatorPrefabFolder}/{prefabName}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                var existingOp = existing.GetComponent<TrashTD.Operators.ResicochetOperator>();
                if (existingOp != null)
                {
                    existingOp.ConfigureAnimationFrames(c1, c2, c3);
                }
                var existingAnim = existing.GetComponent<TrashTD.Operators.OperatorSpriteAnimation>();
                if (existingAnim != null && c1 != null && c1.Length > 0)
                {
                    existingAnim.Configure(c1);
                }
                var existingSr = existing.GetComponent<SpriteRenderer>();
                if (existingSr != null && c1 != null && c1.Length > 0 && c1[0] != null)
                {
                    existingSr.sprite = c1[0];
                }
                EditorUtility.SetDirty(existing);
                return existing;
            }

            var go = new GameObject(prefabName);
            var spriteRenderer = go.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = c1 != null && c1.Length > 0 ? c1[0] : null;
            spriteRenderer.sortingOrder = 5;
            var op = go.AddComponent<TrashTD.Operators.ResicochetOperator>();
            op.ConfigureAnimationFrames(c1, c2, c3);
            var animation = go.AddComponent<TrashTD.Operators.OperatorSpriteAnimation>();
            if (c1 != null && c1.Length > 0)
            {
                animation.Configure(c1);
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject CreateBubbletsPrefab(Sprite firstFrame, Sprite secondFrame)
        {
            const string prefabName = "Prefab_OP_Bubblets";
            string path = $"{OperatorPrefabFolder}/{prefabName}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var go = new GameObject(prefabName);
            var spriteRenderer = go.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = firstFrame;
            spriteRenderer.sortingOrder = 5;
            go.AddComponent<BubbletsOperator>();
            var animation = go.AddComponent<OperatorSpriteAnimation>();
            animation.Configure(new[] { firstFrame, secondFrame });

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject CreateMossmoPrefab(Sprite firstFrame, Sprite secondFrame)
        {
            const string prefabName = "Prefab_OP_Mossmo";
            string path = $"{OperatorPrefabFolder}/{prefabName}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var go = new GameObject(prefabName);
            var spriteRenderer = go.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = firstFrame;
            spriteRenderer.sortingOrder = 5;
            go.AddComponent<MossmoOperator>();
            var animation = go.AddComponent<OperatorSpriteAnimation>();
            animation.Configure(new[] { firstFrame, secondFrame });

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject CreateEchosquirePrefab(Sprite[] idleFrames, Sprite[] attackFrames)
        {
            const string prefabName = "Prefab_OP_Echosquire";
            string path = $"{OperatorPrefabFolder}/{prefabName}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                var contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    ConfigureEchosquirePrefab(contents, idleFrames, attackFrames);
                    return PrefabUtility.SaveAsPrefabAsset(contents, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }

            var go = new GameObject(prefabName);
            go.AddComponent<EchosquireOperator>();
            ConfigureEchosquirePrefab(go, idleFrames, attackFrames);

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static void ConfigureEchosquirePrefab(GameObject prefab, Sprite[] idleFrames, Sprite[] attackFrames)
        {
            var spriteRenderer = prefab.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null) spriteRenderer = prefab.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = idleFrames[0];
            spriteRenderer.sortingOrder = 5;

            var legacyAnimation = prefab.GetComponent<OperatorSpriteAnimation>();
            if (legacyAnimation != null) Object.DestroyImmediate(legacyAnimation, true);

            var animation = prefab.GetComponent<EchosquireSpriteAnimation>();
            if (animation == null) animation = prefab.AddComponent<EchosquireSpriteAnimation>();
            animation.Configure(idleFrames, attackFrames);
        }

        private static GameObject CreateQihouPrefab(Sprite[] frames, Sprite[] attackFrames)
        {
            const string prefabName = "Prefab_OP_Qihou";
            string path = $"{OperatorPrefabFolder}/{prefabName}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                var existingAnim = existing.GetComponent<OperatorSpriteAnimation>();
                if (existingAnim != null) existingAnim.enabled = false;

                var existingQihouAnim = existing.GetComponent<QihouSpriteAnimation>();
                if (existingQihouAnim == null) existingQihouAnim = existing.AddComponent<QihouSpriteAnimation>();
                existingQihouAnim.Configure(frames, attackFrames);

                var existingSr = existing.GetComponent<SpriteRenderer>();
                if (existingSr != null) existingSr.sprite = frames[0];
                EditorUtility.SetDirty(existing);
                return existing;
            }

            var go = new GameObject(prefabName);
            var spriteRenderer = go.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = frames[0];
            spriteRenderer.sortingOrder = 5;
            go.AddComponent<QihouOperator>();
            var animation = go.AddComponent<QihouSpriteAnimation>();
            animation.Configure(frames, attackFrames);

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject CreateBasurocketPrefab(Sprite[] frames)
        {
            const string prefabName = "Prefab_OP_Sniper_Basurocket";
            string path = $"{OperatorPrefabFolder}/{prefabName}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                var existingAnim = existing.GetComponent<OperatorSpriteAnimation>();
                if (existingAnim == null)
                {
                    existingAnim = existing.AddComponent<OperatorSpriteAnimation>();
                }
                existingAnim.Configure(frames);
                var existingSr = existing.GetComponent<SpriteRenderer>();
                if (existingSr != null && frames != null && frames.Length > 0 && frames[0] != null)
                {
                    existingSr.sprite = frames[0];
                }
                EditorUtility.SetDirty(existing);
                return existing;
            }

            var go = new GameObject(prefabName);
            var spriteRenderer = go.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = frames != null && frames.Length > 0 ? frames[0] : null;
            spriteRenderer.sortingOrder = 5;
            go.AddComponent<BasurocketOperator>();
            var animation = go.AddComponent<OperatorSpriteAnimation>();
            animation.Configure(frames);

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject CreateProgenyPrefab(Sprite[] frames)
        {
            const string prefabName = "Prefab_OP_Progeny";
            string path = $"{OperatorPrefabFolder}/{prefabName}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                var existingAnim = existing.GetComponent<OperatorSpriteAnimation>();
                if (existingAnim == null)
                {
                    existingAnim = existing.AddComponent<OperatorSpriteAnimation>();
                }
                existingAnim.Configure(frames);
                var existingSr = existing.GetComponent<SpriteRenderer>();
                if (existingSr != null && frames != null && frames.Length > 0 && frames[0] != null)
                {
                    existingSr.sprite = frames[0];
                }
                EditorUtility.SetDirty(existing);
                return existing;
            }

            var go = new GameObject(prefabName);
            var spriteRenderer = go.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = frames != null && frames.Length > 0 ? frames[0] : null;
            spriteRenderer.sortingOrder = 5;
            go.AddComponent<ProgenyOperator>();
            var animation = go.AddComponent<OperatorSpriteAnimation>();
            animation.Configure(frames);

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject CreateStaggerPrefab(Sprite[] frames)
        {
            const string prefabName = "Prefab_OP_Stagger";
            string path = $"{OperatorPrefabFolder}/{prefabName}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                var existingAnim = existing.GetComponent<OperatorSpriteAnimation>();
                if (existingAnim == null)
                {
                    existingAnim = existing.AddComponent<OperatorSpriteAnimation>();
                }
                existingAnim.Configure(frames);
                var existingSr = existing.GetComponent<SpriteRenderer>();
                if (existingSr != null && frames != null && frames.Length > 0 && frames[0] != null)
                {
                    existingSr.sprite = frames[0];
                }
                EditorUtility.SetDirty(existing);
                return existing;
            }

            var go = new GameObject(prefabName);
            var spriteRenderer = go.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = frames != null && frames.Length > 0 ? frames[0] : null;
            spriteRenderer.sortingOrder = 5;
            go.AddComponent<StaggerOperator>();
            var animation = go.AddComponent<OperatorSpriteAnimation>();
            animation.Configure(frames);

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject CreateThornchinPrefab(Sprite[] frames)
        {
            const string prefabName = "Prefab_OP_Defender_Thornchin";
            string path = $"{OperatorPrefabFolder}/{prefabName}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                var existingAnim = existing.GetComponent<OperatorSpriteAnimation>();
                if (existingAnim == null)
                {
                    existingAnim = existing.AddComponent<OperatorSpriteAnimation>();
                }
                existingAnim.Configure(frames);
                var existingSr = existing.GetComponent<SpriteRenderer>();
                if (existingSr != null && frames != null && frames.Length > 0 && frames[0] != null)
                {
                    existingSr.sprite = frames[0];
                }
                EditorUtility.SetDirty(existing);
                return existing;
            }

            var go = new GameObject(prefabName);
            var spriteRenderer = go.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = frames != null && frames.Length > 0 ? frames[0] : null;
            spriteRenderer.sortingOrder = 5;
            go.AddComponent<ThornchinOperator>();
            var animation = go.AddComponent<OperatorSpriteAnimation>();
            animation.Configure(frames);

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject CreateLockburnPrefab(Sprite[] frames)
        {
            const string prefabName = "Prefab_OP_Guard_Lockburn";
            string path = $"{OperatorPrefabFolder}/{prefabName}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                // The shipped prefab uses LockburnSpriteAnimation (idle + throw + swipe); only a bare prefab gets the generic idle loop.
                if (existing.GetComponent<LockburnSpriteAnimation>() == null)
                {
                    var existingAnim = existing.GetComponent<OperatorSpriteAnimation>();
                    if (existingAnim == null)
                    {
                        existingAnim = existing.AddComponent<OperatorSpriteAnimation>();
                    }
                    existingAnim.Configure(frames);
                }
                var existingSr = existing.GetComponent<SpriteRenderer>();
                if (existingSr != null && frames != null && frames.Length > 0 && frames[0] != null)
                {
                    existingSr.sprite = frames[0];
                }
                if (existing.GetComponent<LockburnOperator>() == null)
                {
                    existing.AddComponent<LockburnOperator>();
                }
                EditorUtility.SetDirty(existing);
                return existing;
            }

            var go = new GameObject(prefabName);
            var spriteRenderer = go.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = frames != null && frames.Length > 0 ? frames[0] : null;
            spriteRenderer.sortingOrder = 5;
            go.AddComponent<LockburnOperator>();
            var animation = go.AddComponent<OperatorSpriteAnimation>();
            animation.Configure(frames);

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject CreateScrapperPrefab(Sprite[] frames)
        {
            const string prefabName = "Prefab_OP_Scrapper";
            string path = $"{OperatorPrefabFolder}/{prefabName}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                var existingAnim = existing.GetComponent<OperatorSpriteAnimation>();
                if (existingAnim == null)
                {
                    existingAnim = existing.AddComponent<OperatorSpriteAnimation>();
                }
                existingAnim.Configure(frames);
                var existingSr = existing.GetComponent<SpriteRenderer>();
                if (existingSr != null && frames != null && frames.Length > 0 && frames[0] != null)
                {
                    existingSr.sprite = frames[0];
                }
                EditorUtility.SetDirty(existing);
                return existing;
            }

            var go = new GameObject(prefabName);
            var spriteRenderer = go.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = frames != null && frames.Length > 0 ? frames[0] : null;
            spriteRenderer.sortingOrder = 5;
            go.AddComponent<ScrapperOperator>();
            var animation = go.AddComponent<OperatorSpriteAnimation>();
            animation.Configure(frames);

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject CreateCoalescePrefab(Sprite[] frames)
        {
            const string prefabName = "Prefab_OP_Coalesce";
            string path = $"{OperatorPrefabFolder}/{prefabName}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                var existingAnim = existing.GetComponent<OperatorSpriteAnimation>();
                if (existingAnim == null)
                {
                    existingAnim = existing.AddComponent<OperatorSpriteAnimation>();
                }
                existingAnim.Configure(frames);
                var existingSr = existing.GetComponent<SpriteRenderer>();
                if (existingSr != null && frames != null && frames.Length > 0 && frames[0] != null)
                {
                    existingSr.sprite = frames[0];
                }
                EditorUtility.SetDirty(existing);
                return existing;
            }

            var go = new GameObject(prefabName);
            var spriteRenderer = go.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = frames != null && frames.Length > 0 ? frames[0] : null;
            spriteRenderer.sortingOrder = 5;
            go.AddComponent<CoalesceOperator>();
            var animation = go.AddComponent<OperatorSpriteAnimation>();
            animation.Configure(frames);

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static OperatorData CreateOperator(string assetName, string displayName, OperatorClass opClass,
            OperatorPosition pos, OperatorRarity rarity, int hp, int atk, int def, int res, int blockCount,
            int range, float interval, Vector2Int[] rangePattern, Sprite portrait, GameObject prefab)
        {
            string path = $"{OperatorDataFolder}/{assetName}.asset";
            var data = AssetDatabase.LoadAssetAtPath<OperatorData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<OperatorData>();
                AssetDatabase.CreateAsset(data, path);
            }

            data.operatorName = displayName;
            data.operatorClass = opClass;
            data.position = pos;
            data.baseRarity = rarity;
            data.baseHP = hp;
            data.baseATK = atk;
            data.baseDEF = def;
            data.baseRES = res;
            data.blockCount = blockCount;
            data.attackRange = range;
            data.attackInterval = interval;
            data.rangePattern = rangePattern;
            data.portrait = portrait;
            data.operatorPrefab = prefab;

            EditorUtility.SetDirty(data);
            return data;
        }
    }
}
