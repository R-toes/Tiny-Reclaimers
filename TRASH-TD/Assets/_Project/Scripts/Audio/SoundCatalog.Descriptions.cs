using System.Collections.Generic;

namespace TrashTD.Audio
{
    // When each sound plays and what plays without it. Keep in step with the code that plays it.
    public static partial class SoundCatalog
    {
        private sealed class SharedEntry
        {
            public string when;
            public SfxId? fallback;
            public string note;
        }

        private sealed class AbilityEntry
        {
            public string name;            // file name, e.g. "Ability_Dodge"
            public string[] aliases;       // other accepted names, e.g. "Dodge"
            public string when;
            public string[] fallbackNames; // files in the same folder tried next, e.g. "Ability"
            public SfxId? sharedFallback;  // then this shared sound
            public string note;            // then this (e.g. "replays its heal sound, quieter")
            public bool optional;
            public bool loop;
        }

        private sealed class OperatorEntry
        {
            public string attack;          // when Attack plays (null = generic)
            public string impact;          // when Impact plays (null = generic)
            public string[] attackAliases;
            public string[] deployAliases;
            public SfxId? attackFallback;  // instead of the damage-type default
            public bool silentAttack;      // no shared fallback for Attack
            public bool noAttack;          // never attacks (no Attack slot)
            public bool noImpact;          // never lands a hit (no Impact slot)
            public AbilityEntry[] abilities;
        }

        private sealed class EnemyEntry
        {
            public string attack;
            public string impact;
            public SfxId? attackFallback;
            public AbilityEntry[] extras;
        }

        private static readonly string[] PlainAbility = { nameof(UnitSfxEvent.Ability) };

        private static AbilityEntry Ability(string when) =>
            new AbilityEntry { name = nameof(UnitSfxEvent.Ability), when = when };

        /// <summary>A named ability (Ability_X, also accepted as X) that falls back to the unit's plain Ability file.</summary>
        private static AbilityEntry Named(string abilityName, string when) => new AbilityEntry
        {
            name = SoundNames.AbilityClip(abilityName), aliases = new[] { abilityName }, when = when, fallbackNames = PlainAbility
        };

        // ── Shared sounds ──

        private static readonly Dictionary<SfxId, SharedEntry> SharedEntries = new Dictionary<SfxId, SharedEntry>
        {
            { SfxId.UiHover, new SharedEntry { when = "The pointer moves onto a button (menus, HUD, draft, Intel, results)." } },
            { SfxId.UiClick, new SharedEntry { when = "A button is pressed." } },
            { SfxId.UiBack, new SharedEntry { when = "A back or close button." } },
            { SfxId.UiDenied, new SharedEntry { when = "Something isn't allowed: a locked stage, a card on cooldown, a full squad, a tile the operator can't stand on." } },
            { SfxId.UiConfirm, new SharedEntry { when = "A draft card is picked; confirming in the tutorial, Intel and results screens." } },
            { SfxId.UiCardShuffle, new SharedEntry { when = "The draft cards riffle-shuffle (each swap), and the draft-odds reels spinning.", fallback = SfxId.UiHover } },
            { SfxId.UiCardDeal, new SharedEntry { when = "Each draft card slides into place.", fallback = SfxId.UiHover } },
            { SfxId.UiCardFlip, new SharedEntry { when = "A draft card flips face-up.", fallback = SfxId.UiHover } },
            { SfxId.UiReroll, new SharedEntry { when = "The draft's REROLL button (or R).", fallback = SfxId.UiClick } },
            { SfxId.UiCardDiscard, new SharedEntry { when = "A deck card is discarded, after confirming." } },
            { SfxId.UiCardPickUp, new SharedEntry { when = "A deck card is picked up to drag it." } },
            { SfxId.UiCardDrop, new SharedEntry { when = "A dragged deck card is dropped on another slot (rearranging the deck)." } },
            { SfxId.TutorialVoice, new SharedEntry { when = "Coalesce's talking blip in the tutorial, once per typed character. Keep it about 40 ms.", note = "a blip generated in code" } },

            { SfxId.GameplayDeploy, new SharedEntry { when = "Every deploy. The operator's own Deploy file plays on top." } },
            { SfxId.GameplayRetreat, new SharedEntry { when = "An operator is retreated to the deck (not on death)." } },
            { SfxId.GameplayWaveStart, new SharedEntry { when = "A wave begins (the INVADING banner)." } },
            { SfxId.GameplayWaveCleared, new SharedEntry { when = "Every enemy of a wave is dead (not the last wave)." } },
            { SfxId.GameplayEnemyDeath, new SharedEntry { when = "An enemy dies and has no Death file of its own." } },
            { SfxId.GameplayOperatorDeath, new SharedEntry { when = "An operator dies and has no Death file of its own." } },
            { SfxId.GameplayLifeLost, new SharedEntry { when = "An enemy reaches the exit." } },
            { SfxId.GameplayVictory, new SharedEntry { when = "The stage is cleared." } },
            { SfxId.GameplayDefeat, new SharedEntry { when = "Lives reach 0." } },
            { SfxId.GameplayPause, new SharedEntry { when = "The pause menu opens (Esc, the pause button, or the game window losing focus)." } },
            { SfxId.GameplayResume, new SharedEntry { when = "The pause menu closes." } },
            { SfxId.GameplayCardUpgrade, new SharedEntry { when = "3 copies merge into a higher-rarity card. The draft's ODDS UP! uses it too (ButtonConfirm until it exists)." } },
            { SfxId.GameplayPreparation, new SharedEntry { when = "The PREPARATION banner, after the draft and before the wave." } },
            { SfxId.GameplayHazardBriefing, new SharedEntry { when = "The stage hazard briefing pops up at the start of a hazard stage." } },

            { SfxId.GameplayRangedPhysicalAttack, new SharedEntry { when = "The attack of a ranged physical operator without its own Attack file." } },
            { SfxId.GameplayMagicAttack, new SharedEntry { when = "The attack of an Arts operator without its own Attack file; also Bubblets' pop and Mossmo's pulse without their own files." } },
            { SfxId.GameplayMeleeAttack, new SharedEntry { when = "The attack of a melee operator without its own Attack file; Talon Rat's claw; Thornchin's spikes and the Stage 2 items landing without their own files." } },
            { SfxId.CombatStun, new SharedEntry { when = "An operator or enemy is stunned: falling tools, a car, THE ERADICATOR, Scrapper, Stag-ger's pull without its own file." } },
            { SfxId.CombatMiss, new SharedEntry { when = "An attack misses: the sandstorm's miss chance, or an enemy that evades (Sand Plastic, Sand Poncho, Sand Hulk)." } },

            { SfxId.TileTrapTick, new SharedEntry { when = "Each damage tick on a trap tile, for operators and enemies." } },
            { SfxId.TileHoleEnter, new SharedEntry { when = "A ground enemy drops into a hole tile." } },
            { SfxId.TileHoleExit, new SharedEntry { when = "It climbs out of the paired hole." } },

            { SfxId.Stage2ItemFall, new SharedEntry { when = "An item starts falling (every 10 s)." } },
            { SfxId.Stage2ItemLandGlass, new SharedEntry { when = "A glass item lands (damage over time).", fallback = SfxId.GameplayMeleeAttack } },
            { SfxId.Stage2ItemLandMetal, new SharedEntry { when = "A metal item lands (burst damage).", fallback = SfxId.GameplayMeleeAttack } },
            { SfxId.Stage2ItemLandTools, new SharedEntry { when = "A tools item lands (damage and stun).", fallback = SfxId.GameplayMeleeAttack } },
            { SfxId.Stage3Gust, new SharedEntry { when = "A strong sandstorm gust starts (every 8-16 s)." } },
            { SfxId.Stage4HeatUp, new SharedEntry { when = "An operator gains a heat stack." } },
            { SfxId.Stage4Ignite, new SharedEntry { when = "An operator reaches 5 heat stacks and starts burning." } },
            { SfxId.Stage4BurnTick, new SharedEntry { when = "Burn damage, once per second while anyone burns." } },
            { SfxId.Stage4Extinguish, new SharedEntry { when = "A burning operator is retreated." } },
            { SfxId.Stage4HeatBlast, new SharedEntry { when = "The reactor blasts heat at the start of a wave (screen flare and shake)." } },
            { SfxId.Stage5CarPass, new SharedEntry { when = "A car enters the boulevard." } },
            { SfxId.Stage5CarHit, new SharedEntry { when = "A car hits an enemy or an operator." } },
            { SfxId.Stage6AcidTick, new SharedEntry { when = "An acid rain damage tick (every 2 s)." } },
        };

        private static SharedEntry GetSharedEntry(SfxId id) =>
            SharedEntries.TryGetValue(id, out SharedEntry entry) ? entry : new SharedEntry { when = "(not described yet)" };

        /// <summary>True if every shared sound has a description (SoundCatalogTests checks it).</summary>
        public static bool HasSharedDescription(SfxId id) => SharedEntries.ContainsKey(id);

        // ── Operators (keyed by operatorName) ──

        private static readonly Dictionary<string, OperatorEntry> OperatorEntries = new Dictionary<string, OperatorEntry>
        {
            { "Qihou", new OperatorEntry { attack = "Chi paw strike.", abilities = new[] { Ability("Knockback: every 4th hit knocks the enemy back.") } } },
            { "Scrapper", new OperatorEntry { attack = "Swing.", abilities = new[] { Ability("Its stun goes off (20% chance per hit).") } } },
            { "Echosquire", new OperatorEntry { attack = "Slash (the hit also drains life).",
                abilities = new[] { Named(SoundNames.Dodge, "Second chance: dodges a hit (\"DODGE!\", once per wave).") } } },
            { "Lockburn", new OperatorEntry { attack = "Claw swipe (melee) and fireball throw (ranged): both use this file.",
                impact = "The swipe hits or the fireball lands." } },
            { "Stag-ger", new OperatorEntry { attack = "Slugger swing.", abilities = new[]
            {
                Named(SoundNames.Dodge, "Dodges an attack (20%)."),
                Named(SoundNames.Empowered, "The +50% ATK strike after a dodge."),
                new AbilityEntry
                {
                    name = SoundNames.AbilityClip(SoundNames.Pull), aliases = new[] { SoundNames.Pull },
                    when = "Pulls an enemy to it.", fallbackNames = PlainAbility, sharedFallback = SfxId.CombatStun
                }
            } } },
            { "Vespierce", new OperatorEntry { attack = "Slash.",
                abilities = new[] { Named(SoundNames.Regen, "Starts regenerating after 5 s without being attacked.") } } },

            { "IRONHOWL MK-IX", new OperatorEntry { attack = "Bash.",
                abilities = new[] { Ability("Releases its shockwave, and its ATK/DEF frenzy switches on.") } } },
            { "Thornchin", new OperatorEntry { attack = "Attack.", abilities = new[]
            {
                new AbilityEntry
                {
                    name = SoundNames.AbilityClip(SoundNames.Retaliate), aliases = new[] { SoundNames.Retaliate },
                    when = "Counter-spikes an operator's attacker.", fallbackNames = PlainAbility,
                    sharedFallback = SfxId.GameplayMeleeAttack
                },
                new AbilityEntry
                {
                    name = SoundNames.AbilityClip(SoundNames.Surrounding),
                    aliases = new[] { SoundNames.Surrounding, "Ability_Adjacent", "Adjacent", "Ability_AoE", "AoE" },
                    when = "Upgraded: the spikes on the tiles around it.",
                    fallbackNames = new[] { SoundNames.AbilityClip(SoundNames.Retaliate), SoundNames.Retaliate, "Ability" },
                    sharedFallback = SfxId.GameplayMeleeAttack
                }
            } } },
            { "Mlem", new OperatorEntry { attack = "Tongue lash.", abilities = new[]
            {
                Named(SoundNames.Swallow, "The gulp when its mouth closes on a catch."),
                new AbilityEntry
                {
                    name = SoundNames.AbilityClip(SoundNames.Digest), aliases = new[] { SoundNames.Digest }, loop = true,
                    when = "Digesting: starts when digestion starts and plays again each time it ends, until digestion " +
                           "ends (4.5-12 s); fades out when it ends, Mlem retreats or dies. A short burbling clip works."
                }
            } } },
            { "Mossmo", new OperatorEntry { noAttack = true, noImpact = true, abilities = new[]
            {
                new AbilityEntry
                {
                    name = nameof(UnitSfxEvent.Ability), aliases = SoundNames.MossmoPulse,
                    when = "Its reactive heal pulse when it takes damage.", sharedFallback = SfxId.GameplayMagicAttack
                },
                new AbilityEntry
                {
                    name = SoundNames.AbilityClip(SoundNames.Enrage), aliases = new[] { SoundNames.Enrage },
                    when = "An enemy it held for 20 s without taking damage turns enraged."
                }
            } } },

            { "Basurocket", new OperatorEntry { attack = "Rocket launch.", impact = "The explosion (and its cluster bombs)." } },
            { "Dead Eye", new OperatorEntry { attack = "Rifle shot.", impact = "The bullet hits." } },
            { "Oquloct", new OperatorEntry { attack = "Shotgun blast.", impact = "The pellets hit.",
                abilities = new[] { Ability("Turns camouflaged when an enemy hits it.") } } },
            { "Pompoth", new OperatorEntry { attack = "Rapid shot." } },

            { "Castiel", new OperatorEntry { attack = "The laser locks onto a new target.", impact = "Each laser tick." } },
            { "Mechonk", new OperatorEntry { attack = "Fire cast.", impact = "The fire bursts." } },
            { "Nekhrost", new OperatorEntry { attack = "Frost cast.", impact = "The frost hits.", abilities = new[]
            {
                Named(SoundNames.Freeze, "Freezes an enemy (and gains haste)."),
                Named(SoundNames.ExtraHit, "Every 4th attack's bonus bolt.")
            } } },
            { "Resicochet", new OperatorEntry
            {
                attack = "The crystal laser fires; each bounce replays it, quieter (see Bounce falloff).",
                impact = "A crystal hit.", attackAliases = SoundNames.ResicochetCharge,
                abilities = new[] { Ability("Stores a charge (\"CHARGE n!\", every 5 s).") }
            } },

            { "Bubblets", new OperatorEntry { attack = "Casts a bubble shield on an ally (and its 5-star attack).",
                abilities = new[]
                {
                    new AbilityEntry
                    {
                        name = nameof(UnitSfxEvent.Ability), when = "A bubble shield pops (blocks a hit, heals and splashes).",
                        sharedFallback = SfxId.GameplayMagicAttack
                    }
                } } },
            { "Coalesce", new OperatorEntry { attack = "Each heal.", silentAttack = true, noImpact = true,
                abilities = new[] { Ability("Bonus heal on a low-HP ally (20%).") } } },
            { "Panacea", new OperatorEntry { attack = "Vial throw.", impact = "The vial shatters and heals.",
                attackFallback = SfxId.GameplayRangedPhysicalAttack } },
            { "Progeny", new OperatorEntry
            {
                attack = "Each heal beam.", attackAliases = new[] { "Heal", "Laser", "Beam" },
                deployAliases = SoundNames.ProgenyDeploy, attackFallback = SfxId.GameplayMagicAttack, noImpact = true,
                abilities = new[]
                {
                    new AbilityEntry
                    {
                        name = SoundNames.AbilityClip(SoundNames.Bounce), aliases = new[] { SoundNames.Bounce, "Ability" },
                        when = "The heal bounces on to the next ally, quieter each bounce (see Bounce falloff).",
                        note = "replays its heal sound, quieter each bounce"
                    }
                }
            } },

            { "Drawgoo", new OperatorEntry { abilities = new[] { Ability("The death card-draw (plays right after its Death).") } } },
            { "Framiliar", new OperatorEntry { abilities = new[]
            {
                new AbilityEntry
                {
                    name = SoundNames.AbilityClip(SoundNames.Transform), aliases = new[] { SoundNames.Transform, "Ability" },
                    when = "Morphs into another operator.", sharedFallback = SfxId.GameplayCardUpgrade
                }
            } } },
            { "Flurmonica", new OperatorEntry { attack = "Plays Harmony on an ally (every 2 s).", silentAttack = true, noImpact = true } },
        };

        private static OperatorEntry GetOperatorEntry(string operatorName) =>
            operatorName != null && OperatorEntries.TryGetValue(operatorName, out OperatorEntry entry) ? entry : new OperatorEntry();

        private static bool HasBouncingAttack(string operatorName) => operatorName == "Progeny" || operatorName == "Resicochet";

        // ── Enemies (keyed by enemyName) ──

        private static EnemyEntry Melee(string attack = "Melee hit on its blocker.") => new EnemyEntry { attack = attack };

        private static readonly Dictionary<string, EnemyEntry> EnemyEntries = new Dictionary<string, EnemyEntry>
        {
            { "Sludge Grunt", Melee() },
            { "Sand Plastic", new EnemyEntry { attack = "Melee hit or sand throw.", impact = "Thrown sand lands." } },
            { "Spoilcupine", Melee("Melee hit, and its quill reflect when hit hard.") },
            { "Tick-Torque", Melee() },
            { "Robozombie", new EnemyEntry { attack = "Melee hit on its blocker.", extras = new[]
            {
                new AbilityEntry
                {
                    name = SoundNames.AbilityClip(SoundNames.Detonate), aliases = new[] { SoundNames.Detonate },
                    when = "Explodes on death (25% chance). Its Death sound plays too.", fallbackNames = PlainAbility
                }
            } } },
            { "Oculite Spawn", Melee() },
            { "Toxic Rusher", Melee() },
            { "Exploding Bug", new EnemyEntry { attack = "Melee hit on its blocker.", extras = new[]
            {
                new AbilityEntry
                {
                    name = SoundNames.AbilityClip(SoundNames.Detonate), aliases = new[] { SoundNames.Detonate },
                    when = "Explodes on death (always). Its Death sound plays too.", fallbackNames = PlainAbility
                }
            } } },
            { "Occulorm", new EnemyEntry { attack = "Leaps onto an operator, and every bite (0.5 s apart: keep it short).",
                impact = "Lands its leap." } },

            { "Pollution Tank", Melee("Heavy slam.") },
            { "Sand Hulk", Melee("Shockwave slam.") },
            { "Gooelist", new EnemyEntry { attack = "Bash.", extras = new[]
            {
                Named(SoundNames.Deflect, "Deflects a hit from an operator that isn't its blocker (\"DUEL\").")
            } } },
            { "Junkernaut", Melee("Bash.") },
            { "Oculoid Carrier", Melee("Bash.") },
            { "THE ERADICATOR", new EnemyEntry { attack = "Melee swing and rocket launch.", impact = "A rocket blast.", extras = new[]
            {
                new AbilityEntry
                {
                    name = SoundNames.AttackClip(SoundNames.Heavy), optional = true,
                    when = "Every 4th rocket: the heavy warhead's launch.", fallbackNames = new[] { nameof(UnitSfxEvent.Attack) }
                },
                new AbilityEntry
                {
                    name = SoundNames.ImpactClip(SoundNames.Heavy), optional = true,
                    when = "The heavy warhead's blast.", fallbackNames = new[] { nameof(UnitSfxEvent.Impact) }
                }
            } } },

            { "Sand Poncho", new EnemyEntry { attack = "Cast.", impact = "The spell lands." } },
            { "Smog Caster", new EnemyEntry { attack = "Cast.", impact = "The spell lands." } },
            { "Askalaser", new EnemyEntry { attack = "Its global shot and its laser.", impact = "The shot or laser hits." } },
            { "Roboflame", new EnemyEntry { attack = "A flame burst starts.", impact = "Each flame tick." } },
            { "Hextension", new EnemyEntry { attack = "Its ranged attack, after its bind.", impact = "The attack lands.", extras = new[]
            {
                Named(SoundNames.Bind, "Casts its wire bind on an operator (a 15 s channel).")
            } } },

            { "Talon Rat", new EnemyEntry { attack = "Claw swipe.", attackFallback = SfxId.GameplayMeleeAttack } },
            { "Skyshard", new EnemyEntry { attack = "Shard shot.", impact = "Shard blast." } },
            { "Acid Flyer", new EnemyEntry { attack = "Acid spit.", impact = "Acid splash." } },
            { "Robodrone", new EnemyEntry
            {
                attack = "Each SMG round, one per attack interval in ~5 s bursts (keep it very short).",
                impact = "Each round hits.",
                extras = new[] { Named(SoundNames.Reload, "Its magazine runs dry and it reloads (2 s).") }
            } },
            { "O.F.O", new EnemyEntry { attack = "Arts shot.", impact = "The shot lands.", extras = new[]
            {
                Named(SoundNames.Summon, "Stops to summon Oculites (every 6 s).")
            } } },
        };

        private static EnemyEntry GetEnemyEntry(string enemyName) =>
            enemyName != null && EnemyEntries.TryGetValue(enemyName, out EnemyEntry entry) ? entry : new EnemyEntry();

        /// <summary>The operator and enemy names that have descriptions (SoundCatalogTests checks every unit has one).</summary>
        public static bool HasOperatorDescription(string operatorName) => OperatorEntries.ContainsKey(operatorName ?? "");
        public static bool HasEnemyDescription(string enemyName) => EnemyEntries.ContainsKey(enemyName ?? "");
    }
}
