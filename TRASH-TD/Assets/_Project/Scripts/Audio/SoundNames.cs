namespace TrashTD.Audio
{
    /// <summary>
    /// Every named unit sound the game looks for, beyond the standard events (UnitSfxEvent: Deploy, Attack, Impact,
    /// Ability, Death). Code plays these through the constants below, never a typed string, so the Sound Manager
    /// (TRASH TD > Sound Manager) and <see cref="SoundCatalog"/> always know every file name the game can play.
    /// SoundCatalogTests fails when a sound call uses a string literal instead.
    ///
    /// A named ability "X" is the file Ability_X (or just X), falling back to the unit's plain Ability file unless
    /// noted. A named attack or impact variant "X" is Attack_X / Impact_X, falling back to the plain Attack / Impact.
    /// </summary>
    public static class SoundNames
    {
        // ── Operator abilities ──
        public const string Dodge = "Dodge";         // Echosquire's second chance, Stag-ger's dodge
        public const string Empowered = "Empowered"; // Stag-ger's +50% ATK strike after a dodge
        public const string Pull = "Pull";           // Stag-ger pulls an enemy
        public const string Regen = "Regen";         // Vespierce starts regenerating
        public const string Swallow = "Swallow";     // Mlem's gulp
        public const string Digest = "Digest";       // Mlem digesting (loops while digesting; no Ability fallback)
        public const string Freeze = "Freeze";       // Nekhrost freezes an enemy
        public const string ExtraHit = "ExtraHit";   // Nekhrost's every-4th-attack bonus bolt
        public const string Transform = "Transform"; // Framiliar morphs
        public const string Enrage = "Enrage";       // Mossmo enrages a stalled enemy (no Ability fallback: that is its heal pulse)
        public const string Retaliate = "Retaliate"; // Thornchin's counter-spike
        public const string Surrounding = "Surrounding"; // Thornchin's AoE spikes
        public const string Bounce = "Bounce";       // Progeny's heal bouncing on

        // ── Enemy abilities ──
        public const string Bind = "Bind";           // Hextension casts its wire bind
        public const string Deflect = "Deflect";     // Gooelist deflects a hit from a non-blocker
        public const string Summon = "Summon";       // O.F.O starts summoning
        public const string Detonate = "Detonate";   // Exploding Bug / Robozombie explode
        public const string Reload = "Reload";       // Robodrone's magazine runs dry

        // ── Attack / impact variants ──
        public const string Heavy = "Heavy";         // THE ERADICATOR's heavy warhead: Attack_Heavy, Impact_Heavy

        // ── Alternative file names a unit also accepts (older names, kept so existing files keep working) ──
        public static readonly string[] MossmoPulse = { "Pulse", "HealPulse" };
        public static readonly string[] ProgenyDeploy = { "Cry" };
        public static readonly string[] ProgenyHeal = { "Attack", "Heal", "Laser", "Beam" };
        public static readonly string[] ProgenyBounce = { AbilityClip(Bounce), Bounce, "Ability" };
        public static readonly string[] ThornchinRetaliate = { AbilityClip(Retaliate), Retaliate, "Ability" };
        public static readonly string[] ThornchinSurrounding =
            { AbilityClip(Surrounding), Surrounding, "Ability_Adjacent", "Adjacent", "Ability_AoE", "AoE" };
        public static readonly string[] ResicochetCharge =
            { "Charge 1", "Charge 2", "Charge 3", "Charge1", "Charge2", "Charge3", "Charge" };
        public static readonly string[] MlemDigest = { AbilityClip(Digest), Digest };
        public static readonly string[] FramiliarTransform = { AbilityClip(Transform), Transform, "Ability" };

        /// <summary>"Dodge" -> "Ability_Dodge".</summary>
        public static string AbilityClip(string abilityName) => "Ability_" + abilityName;
        /// <summary>"Heavy" -> "Attack_Heavy".</summary>
        public static string AttackClip(string variant) => "Attack_" + variant;
        /// <summary>"Heavy" -> "Impact_Heavy".</summary>
        public static string ImpactClip(string variant) => "Impact_" + variant;
    }
}
