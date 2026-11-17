using System.Collections.Generic;

namespace TrashTD.Audio
{
    /// <summary>
    /// The volume groups a sound effect belongs to. Every effect has a category (UI, Enemies, Ambience, ...) and most
    /// also have a specific group (one stage, one operator, one enemy). Its volume is
    /// category level x group level x its own file level (SfxVolumeSettings) x the player's SFX volume.
    /// The groups follow the file's folder under Resources/SFX, so a new unit or stage gets its group for free.
    /// Levels are stored in SfxVolumeSettings by these keys; a key without a stored level uses <see cref="GetDefault"/>.
    /// </summary>
    public static class SoundVolumeGroups
    {
        public const string UI = "category:UI";
        public const string Gameplay = "category:Gameplay";
        public const string Combat = "category:Combat";
        public const string Tiles = "category:Tiles";
        public const string StageHazards = "category:StageHazards";
        public const string Ambience = "category:Ambience";
        public const string Operators = "category:Operators";
        public const string Enemies = "category:Enemies";
        public const string EnemyDeaths = "category:EnemyDeaths";

        private const string StagePrefix = "stage:";
        private const string OperatorPrefix = "operator:";
        private const string EnemyPrefix = "enemy:";
        private const string BouncePrefix = "bounce:";

        /// <summary>The categories in the order the Sound Manager lists them, with a readable name.</summary>
        public static readonly IReadOnlyList<(string key, string label)> Categories = new[]
        {
            (UI, "UI"),
            (Gameplay, "Gameplay"),
            (Combat, "Combat"),
            (Tiles, "Tiles"),
            (StageHazards, "Stage hazards"),
            (Ambience, "Stage ambience"),
            (Operators, "Operators"),
            (Enemies, "Enemies (attacks, impacts, abilities)"),
            (EnemyDeaths, "Enemy deaths"),
        };

        // These levels reproduce the volumes that used to be fixed in code, so moving them here changed nothing:
        // enemy sounds 70%, enemy deaths 80%, stage ambience 45%, and each bounce of a bouncing attack half as loud.
        private static readonly Dictionary<string, float> Defaults = new Dictionary<string, float>
        {
            { Enemies, 0.7f },
            { EnemyDeaths, 0.8f },
            { Ambience, 0.45f },
        };

        public const float DefaultBounceFalloff = 0.5f;

        public static string StageKey(string stageFolder) => StagePrefix + stageFolder;
        public static string OperatorKey(string unitFolderName) => OperatorPrefix + unitFolderName;
        public static string EnemyKey(string unitFolderName) => EnemyPrefix + unitFolderName;
        /// <summary>How loud each bounce of a bouncing attack is next to the one before (Progeny, Resicochet).</summary>
        public static string BounceKey(string unitFolderName) => BouncePrefix + unitFolderName;

        public static float GetDefault(string key)
        {
            if (string.IsNullOrEmpty(key)) return 1f;
            if (key.StartsWith(BouncePrefix)) return DefaultBounceFalloff;
            return Defaults.TryGetValue(key, out float level) ? level : 1f;
        }

        /// <summary>
        /// The category and group of a sound, from its Resources folder ("SFX/Operators/Guard/Qihou") and clip name
        /// ("Attack_2"). Either key is null when it doesn't apply (music, or a clip that didn't come from SFX/).
        /// </summary>
        public static void Resolve(string folder, string clipName, out string categoryKey, out string groupKey)
        {
            categoryKey = null;
            groupKey = null;
            if (string.IsNullOrEmpty(folder)) return;

            string[] parts = folder.Split('/');
            if (parts.Length < 2 || parts[0] != "SFX") return;

            string clipEvent = UnitSfx.StripVariant(clipName ?? string.Empty);
            switch (parts[1])
            {
                case "UI":
                    categoryKey = UI;
                    break;
                case "Gameplay":
                    categoryKey = string.Equals(clipEvent, "EnemyDeath", System.StringComparison.OrdinalIgnoreCase)
                        ? EnemyDeaths
                        : Gameplay;
                    break;
                case "Combat":
                    categoryKey = Combat;
                    break;
                case "Tiles":
                    categoryKey = Tiles;
                    break;
                case "Stages":
                    categoryKey = string.Equals(clipEvent, "Ambience", System.StringComparison.OrdinalIgnoreCase)
                        ? Ambience
                        : StageHazards;
                    if (parts.Length >= 3) groupKey = StageKey(parts[2]);
                    break;
                case "Operators":
                    categoryKey = Operators;
                    if (parts.Length >= 4) groupKey = OperatorKey(parts[3]);
                    break;
                case "Enemies":
                    categoryKey = string.Equals(clipEvent, nameof(UnitSfxEvent.Death), System.StringComparison.OrdinalIgnoreCase)
                        ? EnemyDeaths
                        : Enemies;
                    if (parts.Length >= 4) groupKey = EnemyKey(parts[3]);
                    break;
            }
        }
    }
}
