using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using TrashTD.Data;
using TrashTD.Enemies;
using TrashTD.Operators;
using TrashTD.Core.GameLoop;

namespace TrashTD.Editor
{
    /// <summary>
    /// Editor utility to generate sample ScriptableObject data assets and prefabs for TRASH TD (GDD 1.3, 1.5, 1.11).
    /// Run from Unity menu: TRASH TD -> Generate Sample Data Assets
    /// </summary>
    public static partial class InitialAssetGenerator
    {
        private const string ArtFolder = "Assets/_Project/Art";
        private static readonly string[] ArtSearchFolders =
        {
            ArtFolder + "/Operators",
            ArtFolder + "/Enemies",
            ArtFolder + "/Tiles",
            ArtFolder + "/Placeholders",
            ArtFolder,
        };
        private const string OperatorDataFolder = "Assets/_Project/Data/Operators";
        private const string IronhowlSkillDescription = "<color=#FFD700>Base (1★):</color> When blocking 3 or more enemies, increases ATK and DEF by 20%.\n<color=#FFD700>1st upgrade (2★):</color> Every 8 enemy hits, releases a gray shockwave that stuns all enemies IRONHOWL MK-IX is currently blocking for 1.5 seconds.\n<color=#FFD700>2nd upgrade (3★):</color> Block count increases to 6.";
        private const string NekhrostSkillDescription = "<color=#FFD700>Base (1★):</color> Applies chill to slow and freeze enemies. Freezing an enemy grants 25% faster attacks for 3.5 seconds. Every 4th attack fires 1 extra shard at another enemy in range, or the same enemy if alone.\n<color=#FFD700>1st upgrade (2★):</color> While Nekhrost is deployed, all operators deal 50% more damage to frozen enemies.\n<color=#FFD700>2nd upgrade (3★):</color> Fires 3 shards per attack at different enemies when possible, repeating targets if fewer than 3 are in range. The every-4th-attack bonus adds 1 more shard, for 4 total. Her freeze haste remains.";
        private const string DeadeyeSkillDescription = "<color=#FFD700>Base (1★):</color> Deals 20% more damage to flying enemies, but his attack interval is 20% longer while an aerial target is in range.\n<color=#FFD700>1st upgrade (2★):</color> Removes the attack interval penalty while keeping the flying-enemy damage bonus. Each landed hit against the same target adds 2% of ATK as damage, starting on the first hit and capped at +100%; switching targets resets the bonus.\n<color=#FFD700>2nd upgrade (3★):</color> After killing an enemy, his next shot deals an additional 100% damage.";
        private const string PompothSkillDescription = "<color=#FFD700>Base (1★):</color> Rapid-fire sniper with lower per-shot damage but equal total DPS. Gains +10% attack speed per enemy in range, capped at +100%.\n<color=#FFD700>1st upgrade (2★):</color> Each landed hit against the same enemy ignores 1% of its DEF, stacking to 100%; switching targets resets this bonus.\n<color=#FFD700>2nd upgrade (3★):</color> Increases the attack-speed passive to +30% per enemy, capped at +300%.";
        private const string BubbletsSkillDescription = "<color=#FFD700>Base (3★):</color> Grants an allied operator in range a bubble shield that negates 1 instance of damage. When popped, the bubble heals the operator and deals Arts damage equal to 30% of Bubblets' ATK to enemies on that tile and surrounding tiles.\n<color=#FFD700>1st upgrade (4★):</color> Arts damage increases to 50%. Allies in range gain +10 RES and +40 DEF.\n<color=#FFD700>2nd upgrade (5★):</color> Gives bubble shield to an additional operator. Bubblets can now attack enemies like a caster.";
        private const string DrawgooSkillDescription = "<color=#FFD700>Base (1★):</color> When Drawgoo dies, draw 2 random operator cards from the pool. Cannot be healed by other operators.\n<color=#FFD700>1st upgrade (2★):</color> Gives 2 1x Upgraded cards.\n<color=#FFD700>2nd upgrade (3★):</color> Gives 2 2x Upgraded cards.";
        private const string ResicochetSkillDescription = "<color=#FFD700>Base (3★):</color> Stores a charge every 5 seconds (up to 3). Attacks fire 1 projectile per charge that bounces to up to 4 targets, dealing less damage each bounce.\n<color=#FFD700>1st upgrade (4★):</color> Charging is updated to 3 seconds. Additional 1 bounce.\n<color=#FFD700>2nd upgrade (5★):</color> Bounce doesn't get reduced damage anymore. Operators in range act as an extension of Resicochet's range, but doesn't attack on the extension range if not fully charged.";
        private const string FlurmonicaSkillDescription = "<color=#FFD700>Base (2★):</color> Does not heal. Every 2s, gives 1 Harmony stack to a random ally in range with the fewest stacks (max 3, each lasts 8s). Each stack: Guard +4% RES, Defender +4% DEF, Sniper and Medic +4% attack speed, Caster +4% Arts damage, Specialist +4% max HP.\n<color=#FFD700>1st upgrade (3★):</color> Talent improvements become 6%, stack cap increased to 4.\n<color=#FFD700>2nd upgrade (4★):</color> Talent improvements become 8%, grants 2 stacks by default on each cast.";
        private const string ThornchinSkillDescription = "<color=#FFD700>Base (2★):</color> Whenever Thornchin is attacked, it retaliates against the attacker with sharp urchin spines, dealing Physical damage equal to Thornchin's ATK.\n<color=#FFD700>1st upgrade (3★):</color> Retaliate spikes deal an additional DOT of 5 Arts damage for 5 seconds (stacks up to 4 times) and deal additional AoE damage in adjacent tiles.\n<color=#FFD700>2nd upgrade (4★):</color> AoE damage expands to all surrounding tiles.";
        private const string PanaceaSkillDescription = "<color=#FFD700>Base (3★):</color> Global range. Every 5s, heals the operator with the lowest HP% anywhere on the field.\n<color=#FFD700>1st upgrade (4★):</color> Cooldown is changed to 3s. Heals up to 2 operators.\n<color=#FFD700>2nd upgrade (5★):</color> Restores HP of all operators on the map by a fixed +5 HP per second (excluding Drawgoo).";
        private const string EnemyDataFolder = "Assets/_Project/Data/Enemies";
        private const string StageDataFolder = "Assets/Resources/Stages";
        private const string OperatorPrefabFolder = "Assets/_Project/Prefabs/Operators";
        private const string EnemyPrefabFolder = "Assets/_Project/Prefabs/Enemies";

        // The project's assets are the source of truth, not this generator. Operator, enemy and stage balance is tuned by hand
        // (the inspector, the Wave Planner, code), and the numbers built in here are an older first draft: several operators
        // already differ (IRONHOWL MK-IX HP, Pompoth ATK, ...), and some operators and Stage 4 are not generated at all. So nothing
        // here may reset an existing asset without a per-run confirmation, and never in batch mode. It is a bootstrap tool for
        // an empty project, and it creates missing assets freely (AutoEnsureStages).
        private static bool overwriteApproved;

        private static bool ConfirmOverwrite(string title, List<string> existing)
        {
            if (overwriteApproved || existing.Count == 0) return true;
            if (Application.isBatchMode)
            {
                Debug.LogWarning("[Generator] " + title + ": " + existing.Count + " asset(s) already exist; not resetting them in batch mode.");
                return false;
            }

            string sample = string.Join(", ", existing.GetRange(0, Mathf.Min(6, existing.Count)));
            return EditorUtility.DisplayDialog(title,
                existing.Count + " of the assets this would generate already exist (" + sample + (existing.Count > 6 ? ", ..." : "") + ").\n\n" +
                "Generating RESETS their stats, skill text and, for stages, map layout to the generator's built-in numbers. Those are older " +
                "than the balance in the project, so hand-tuned changes are lost (git can restore them).\n\n" +
                "Only continue if you really mean to start those assets over.",
                "Reset them", "Cancel");
        }

        private static List<string> FindExistingGeneratedAssets()
        {
            var existing = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:OperatorData", new[] { OperatorDataFolder }))
                existing.Add(System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid)));
            foreach (string guid in AssetDatabase.FindAssets("t:EnemyData", new[] { EnemyDataFolder }))
                existing.Add(System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid)));
            foreach (string stage in new[] { "STAGE_01", "STAGE_02", "STAGE_03", "STAGE_05", "STAGE_06" })
            {
                if (AssetDatabase.LoadAssetAtPath<StageData>($"{StageDataFolder}/{stage}.asset") != null) existing.Add(stage);
            }
            return existing;
        }

        [MenuItem("TRASH TD/Generate Sample Data Assets")]
        public static void GenerateAllAssets()
        {
            if (!ConfirmOverwrite("Generate Sample Data Assets", FindExistingGeneratedAssets()))
            {
                Debug.Log("[Generator] Cancelled: nothing was changed.");
                return;
            }

            overwriteApproved = true;
            try
            {
                GenerateAllAssetsConfirmed();
            }
            finally
            {
                overwriteApproved = false;
            }
        }

        private static void GenerateAllAssetsConfirmed()
        {
            EnsureFolder(OperatorDataFolder);
            EnsureFolder(EnemyDataFolder);
            EnsureFolder(StageDataFolder);
            EnsureFolder(OperatorPrefabFolder);
            EnsureFolder(EnemyPrefabFolder);

            AssetDatabase.Refresh();

            GenerateOperators();
            ConfigureNekhrostAttackAnimation();
            ConfigureVespierceAttackAnimation();
            GenerateEnemies();
            GenerateStage();
            GenerateStage2();
            GenerateStage3();
            GenerateStage6();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=green>TRASH TD: All sample Operator, Enemy, Stage assets, and Prefabs successfully generated with placeholder sprites!</color>");
        }

        [MenuItem("TRASH TD/Setup Test Gameplay Scene")]
        public static void SetupTestScene()
        {
            GenerateAllAssets();

            string scenePath = TrashTD.Core.SceneNames.GameplayScenePath;
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.DefaultGameObjects,
                UnityEditor.SceneManagement.NewSceneMode.Single);

            var bootstrapperObj = new GameObject("StageBootstrapper");
            var bootstrapper = bootstrapperObj.AddComponent<StageBootstrapper>();

            // Assign Stage Data
            bootstrapper.stageData = AssetDatabase.LoadAssetAtPath<StageData>($"{StageDataFolder}/STAGE_01.asset");
            bootstrapper.difficulty = StageDifficulty.Normal;

            // Assign Operators Pool
            bootstrapper.operatorPool = new System.Collections.Generic.List<OperatorData>
            {
                AssetDatabase.LoadAssetAtPath<OperatorData>($"{OperatorDataFolder}/OP_Guard_Scrapper.asset"),
                AssetDatabase.LoadAssetAtPath<OperatorData>($"{OperatorDataFolder}/OP_Guard_Qihou.asset"),
                AssetDatabase.LoadAssetAtPath<OperatorData>($"{OperatorDataFolder}/OP_Guard_Echosquire.asset"),
                AssetDatabase.LoadAssetAtPath<OperatorData>($"{OperatorDataFolder}/OP_Specialist_Drawgoo.asset"),
                AssetDatabase.LoadAssetAtPath<OperatorData>($"{OperatorDataFolder}/OP_Guard_Vespierce.asset"),
                AssetDatabase.LoadAssetAtPath<OperatorData>($"{OperatorDataFolder}/OP_Defender_Ironhowl.asset"),
                AssetDatabase.LoadAssetAtPath<OperatorData>($"{OperatorDataFolder}/OP_Defender_Mossmo.asset"),
                AssetDatabase.LoadAssetAtPath<OperatorData>($"{OperatorDataFolder}/OP_Sniper_Deadeye.asset"),
                AssetDatabase.LoadAssetAtPath<OperatorData>($"{OperatorDataFolder}/OP_Sniper_Pompoth.asset"),
                AssetDatabase.LoadAssetAtPath<OperatorData>($"{OperatorDataFolder}/OP_Sniper_Oquloct.asset"),
                AssetDatabase.LoadAssetAtPath<OperatorData>($"{OperatorDataFolder}/OP_Sniper_Basurocket.asset"),
                AssetDatabase.LoadAssetAtPath<OperatorData>($"{OperatorDataFolder}/OP_Caster_Mechonk.asset"),
                AssetDatabase.LoadAssetAtPath<OperatorData>($"{OperatorDataFolder}/OP_Caster_Nekhrost.asset"),
                AssetDatabase.LoadAssetAtPath<OperatorData>($"{OperatorDataFolder}/OP_Medic_Coalesce.asset"),
                AssetDatabase.LoadAssetAtPath<OperatorData>($"{OperatorDataFolder}/OP_Medic_Bubblets.asset"),
                AssetDatabase.LoadAssetAtPath<OperatorData>($"{OperatorDataFolder}/OP_Medic_Progeny.asset"),
                AssetDatabase.LoadAssetAtPath<OperatorData>($"{OperatorDataFolder}/OP_Guard_Stagger.asset"),
                AssetDatabase.LoadAssetAtPath<OperatorData>($"{OperatorDataFolder}/OP_Defender_Thornchin.asset"),
                AssetDatabase.LoadAssetAtPath<OperatorData>($"{OperatorDataFolder}/OP_Guard_Lockburn.asset"),
                AssetDatabase.LoadAssetAtPath<OperatorData>($"{OperatorDataFolder}/OP_Caster_Resicochet.asset"),
                AssetDatabase.LoadAssetAtPath<OperatorData>($"{OperatorDataFolder}/OP_Specialist_Framiliar.asset")
            };

            // Assign Tile Sprites
            bootstrapper.lowGroundSprite = LoadSprite("tex_tile_lowground.png");
            bootstrapper.highGroundSprite = LoadSprite("tex_tile_highground.png");
            bootstrapper.blockedSprite = LoadSprite("tex_tile_blocked.png");
            bootstrapper.enemyPathSprite = LoadSprite("tex_tile_enemypath.png");
            bootstrapper.spawnPointSprite = LoadSprite("tex_tile_spawn.png");
            bootstrapper.exitPointSprite = LoadSprite("tex_tile_exit.png");

            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log($"<color=green>TRASH TD: Test Gameplay Scene successfully created and saved at: {scenePath}</color>");
        }

        /// <summary>
        /// Waves are authored on the stage asset (see Wave Planner). Generated waves are only a first
        /// draft, so existing waves are kept unless the user explicitly confirms overwriting them.
        /// </summary>
        private static bool ShouldGenerateWaves(StageData stage)
        {
            bool hasWaves = (stage.wavesEasy != null && stage.wavesEasy.Length > 0) ||
                            (stage.wavesNormal != null && stage.wavesNormal.Length > 0) ||
                            (stage.wavesHard != null && stage.wavesHard.Length > 0);
            if (!hasWaves) return true;
            if (Application.isBatchMode) return false;

            return !EditorUtility.DisplayDialog(
                "Keep existing waves?",
                $"{stage.name} already has waves. Regenerating replaces ALL of its Easy/Normal/Hard waves " +
                "with generated defaults and discards any hand-tuned changes.\n\n" +
                "The map layout and paths are regenerated either way.",
                "Keep existing waves",
                "Overwrite waves");
        }

        private static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string[] parts = path.Split('/');
                string current = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string next = current + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                    {
                        AssetDatabase.CreateFolder(current, parts[i]);
                    }
                    current = next;
                }
            }
        }

        // Resolves a bare art filename to its path in the Art folder or one of its category subfolders. Operator and
        // enemy art lives in a folder per unit (Art/Operators/Nekhrost/...), so if the direct lookups miss, the Art
        // folder is searched recursively for the file name.
        private static string ResolveArtPath(string filename)
        {
            foreach (string folder in ArtSearchFolders)
            {
                string path = $"{folder}/{filename}";
                if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path, AssetPathToGUIDOptions.OnlyExistingAssets)))
                {
                    return path;
                }
            }

            string nameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(filename);
            foreach (string guid in AssetDatabase.FindAssets(nameWithoutExtension, new[] { ArtFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("/" + filename, System.StringComparison.OrdinalIgnoreCase))
                {
                    return path;
                }
            }

            return $"{ArtFolder}/{filename}";
        }

        private static Sprite LoadSprite(string filename)
        {
            string path = ResolveArtPath(filename);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Sprite LoadSprite(string filename, string spriteName)
        {
            string path = ResolveArtPath(filename);
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite sprite && sprite.name == spriteName)
                {
                    return sprite;
                }
            }

            return null;
        }

        private static Sprite[] LoadSpriteFrames(string filename, string spriteNamePrefix, int frameCount)
        {
            Sprite[] frames = new Sprite[frameCount];
            for (int i = 0; i < frameCount; i++)
            {
                frames[i] = LoadSprite(filename, $"{spriteNamePrefix}_{i}");
                if (frames[i] == null)
                    Debug.LogError($"InitialAssetGenerator: Missing animation frame '{spriteNamePrefix}_{i}' in {filename}.");
            }

            return frames;
        }

        private static void ConfigureNekhrostAttackAnimation()
        {
            Sprite[] idleFrames = LoadSpriteFrames("Nekhrost-Sheet.png", "Nekhrost-Sheet", 8);
            Sprite[] attackFrames = LoadSpriteFrames("Nekhrost Attack Sheet.png", "Nekhrost Attack Sheet", NekhrostSpriteAnimation.AttackFrameCount);
            if (System.Array.Exists(idleFrames, frame => frame == null) ||
                System.Array.Exists(attackFrames, frame => frame == null))
                return;

            string path = $"{OperatorPrefabFolder}/Prefab_OP_Nekhrost.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                Debug.LogError($"InitialAssetGenerator: Missing Nekhrost prefab at {path}.");
                return;
            }

            GameObject prefabContents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                SpriteRenderer spriteRenderer = prefabContents.GetComponent<SpriteRenderer>();
                if (spriteRenderer == null)
                {
                    Debug.LogError($"InitialAssetGenerator: Nekhrost prefab at {path} requires a SpriteRenderer.");
                    return;
                }

                OperatorSpriteAnimation oldAnimation = prefabContents.GetComponent<OperatorSpriteAnimation>();
                if (oldAnimation != null)
                    Object.DestroyImmediate(oldAnimation);

                NekhrostSpriteAnimation animation = prefabContents.GetComponent<NekhrostSpriteAnimation>();
                if (animation == null)
                    animation = prefabContents.AddComponent<NekhrostSpriteAnimation>();

                Transform muzzle = prefabContents.transform.Find("Muzzle");
                if (muzzle == null)
                {
                    var muzzleObject = new GameObject("Muzzle");
                    muzzle = muzzleObject.transform;
                    muzzle.SetParent(prefabContents.transform, false);
                }

                muzzle.localPosition = new Vector3(-1.5f / 32f, 14.5f / 32f, 0f);
                animation.Configure(idleFrames, attackFrames);
                spriteRenderer.sprite = idleFrames[0];
                PrefabUtility.SaveAsPrefabAsset(prefabContents, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabContents);
            }
        }

        private static void ConfigureVespierceAttackAnimation()
        {
            Sprite[] idleFrames = LoadSpriteFrames("Vespierce-Sheet.png", "Vespierce-Sheet", 8);
            Sprite[] attackFrames = LoadSpriteFrames(
                "Vespierce Attack Sheet.png",
                "Vespierce Attack Sheet",
                VespierceSpriteAnimation.AttackFrameCount);
            if (System.Array.Exists(idleFrames, frame => frame == null) ||
                System.Array.Exists(attackFrames, frame => frame == null))
                return;

            string path = $"{OperatorPrefabFolder}/Prefab_OP_Vespierce.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                Debug.LogError($"InitialAssetGenerator: Missing Vespierce prefab at {path}.");
                return;
            }

            GameObject prefabContents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                SpriteRenderer spriteRenderer = prefabContents.GetComponent<SpriteRenderer>();
                if (spriteRenderer == null)
                {
                    Debug.LogError($"InitialAssetGenerator: Vespierce prefab at {path} requires a SpriteRenderer.");
                    return;
                }

                OperatorSpriteAnimation oldAnimation = prefabContents.GetComponent<OperatorSpriteAnimation>();
                if (oldAnimation != null)
                    Object.DestroyImmediate(oldAnimation);

                VespierceSpriteAnimation animation = prefabContents.GetComponent<VespierceSpriteAnimation>();
                if (animation == null)
                    animation = prefabContents.AddComponent<VespierceSpriteAnimation>();

                animation.Configure(idleFrames, attackFrames);
                spriteRenderer.sprite = idleFrames[0];
                PrefabUtility.SaveAsPrefabAsset(prefabContents, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabContents);
            }
        }

        private static GameObject CreateOrGetOperatorPrefab(string prefabName, Sprite sprite, OperatorClass opClass)
        {
            string path = $"{OperatorPrefabFolder}/{prefabName}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var go = new GameObject(prefabName);
            if (prefabName == "Prefab_OP_Pompoth" || prefabName == "Prefab_OP_Deadeye")
            {
                go.transform.localScale = Vector3.one * 0.75f;
            }
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 5;

            // Add corresponding class component
            if (prefabName == "Prefab_OP_Ironhowl") go.AddComponent<IronhowlOperator>();
            else if (prefabName == "Prefab_OP_Deadeye") go.AddComponent<DeadeyeOperator>();
            else if (prefabName == "Prefab_OP_Pompoth") go.AddComponent<PompothOperator>();
            else if (prefabName == "Prefab_OP_Mechonk") go.AddComponent<MechonkOperator>();
            else if (prefabName == "Prefab_OP_Nekhrost") go.AddComponent<NekhrostOperator>();
            else if (prefabName == "Prefab_OP_Coalesce") go.AddComponent<CoalesceOperator>();
            else if (prefabName == "Prefab_OP_Scrapper") go.AddComponent<ScrapperOperator>();
            else if (prefabName == "Prefab_OP_Oquloct") go.AddComponent<OquloctOperator>();
            else
            {
                switch (opClass)
                {
                    case OperatorClass.Guard: go.AddComponent<GuardOperator>(); break;
                    case OperatorClass.Defender: go.AddComponent<DefenderOperator>(); break;
                    case OperatorClass.Sniper: go.AddComponent<SniperOperator>(); break;
                    case OperatorClass.Caster: go.AddComponent<CasterOperator>(); break;
                    case OperatorClass.Medic: go.AddComponent<MedicOperator>(); break;
                    case OperatorClass.Specialist: go.AddComponent<SpecialistOperator>(); break;
                }
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject CreateOrGetEnemyPrefab(string prefabName, Sprite sprite, EnemyArchetype archetype)
        {
            string path = $"{EnemyPrefabFolder}/{prefabName}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var go = new GameObject(prefabName);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 4;

            // Add corresponding archetype component
            switch (archetype)
            {
                case EnemyArchetype.Grunt: go.AddComponent<GruntEnemy>(); break;
                case EnemyArchetype.Rusher: go.AddComponent<RusherEnemy>(); break;
                case EnemyArchetype.Tank: go.AddComponent<TankEnemy>(); break;
                case EnemyArchetype.Caster: go.AddComponent<EnemyCaster>(); break;
                case EnemyArchetype.Flyer: go.AddComponent<FlyerEnemy>(); break;
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject CreateOrGetRobozombiePrefab(string prefabName, Sprite[] frames)
        {
            string path = $"{EnemyPrefabFolder}/{prefabName}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var go = new GameObject(prefabName);
            go.transform.localScale = new Vector3(1.2f, 1.2f, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = frames != null && frames.Length > 0 ? frames[0] : null;
            sr.sortingOrder = 4;

            go.AddComponent<RobozombieEnemy>();
            var anim = go.AddComponent<RobozombieSpriteAnimation>();
            if (frames != null && frames.Length > 0)
            {
                var framesField = typeof(RobozombieSpriteAnimation).GetField("frames", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (framesField != null)
                {
                    framesField.SetValue(anim, frames);
                }
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject CreateOrGetGooelistPrefab(string prefabName, Sprite[] frames)
        {
            string path = $"{EnemyPrefabFolder}/{prefabName}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var go = new GameObject(prefabName);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = frames != null && frames.Length > 0 ? frames[0] : null;
            sr.sortingOrder = 4;

            var gooelistComp = go.AddComponent<TrashTD.Enemies.GooelistEnemy>();
            gooelistComp.SelfDefReductionPercent = 0.75f;
            gooelistComp.OperatorDefReductionPercent = 0.50f;
            var anim = go.AddComponent<TrashTD.Enemies.GooelistSpriteAnimation>();
            if (frames != null && frames.Length > 0)
            {
                var framesField = typeof(TrashTD.Enemies.GooelistSpriteAnimation).GetField("frames", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (framesField != null)
                {
                    framesField.SetValue(anim, frames);
                }
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject CreateOrGetSpoilcupinePrefab(string prefabName, Sprite[] frames)
        {
            string path = $"{EnemyPrefabFolder}/{prefabName}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var go = new GameObject(prefabName);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = frames != null && frames.Length > 0 ? frames[0] : null;
            sr.sortingOrder = 4;

            go.AddComponent<TrashTD.Enemies.SpoilcupineEnemy>();
            var anim = go.AddComponent<TrashTD.Enemies.SpoilcupineSpriteAnimation>();
            if (frames != null && frames.Length > 0)
            {
                var framesField = typeof(TrashTD.Enemies.SpoilcupineSpriteAnimation).GetField("frames", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (framesField != null)
                {
                    framesField.SetValue(anim, frames);
                }
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }
    }
}
