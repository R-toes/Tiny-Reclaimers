using System.Collections.Generic;
using UnityEngine;
using TrashTD.Audio;
using TrashTD.Combat;
using TrashTD.Core.GameLoop;
using TrashTD.Core.Grid;
using TrashTD.Data;
using TrashTD.Enemies;

namespace TrashTD.Operators
{
    /// <summary>
    /// Abstract base class for all operators (GDD 1.3).
    /// Implements shared lifecycle, stats, deployment, attack timing, blocking, and damage handling.
    /// Subclasses override targeting, attack behavior, and blocking rules per class archetype.
    /// 
    /// Demonstrates software reuse: shared behavior in base class, class-specific behavior in subclasses,
    /// skill behavior via ISkill composition.
    /// </summary>
    public abstract partial class OperatorBase : MonoBehaviour, IDeployable, IAttacker, IBlocker
    {
        [Header("Operator Data")]
        [SerializeField] protected OperatorData data;

        // --- Runtime Stats (scaled by rarity) ---
        protected int currentHP;
        protected int maxHP;
        protected int currentATK;
        protected int currentDEF;
        protected int currentRES;

        // --- State ---
        protected OperatorRarity currentRarity;
        protected OperatorFacing facing = OperatorFacing.Right;
        protected GridCell deployedCell;
        protected bool isDeployed;
        protected float attackTimer;
        private float trapDamageTimer;
        protected List<EnemyBase> blockedEnemies = new List<EnemyBase>();
        protected EnemyBase currentTarget;
        protected float stunTimer;
        protected bool isStunned;
        protected float bindTimer;
        protected bool isBound;
        protected SpriteRenderer spriteRenderer;

        // --- Skill (composition) ---
        protected ISkill equippedSkill;

        // --- Properties ---
        public OperatorData Data => data;
        public bool IsDeployed => isDeployed;
        public GridCell DeployedCell => deployedCell;
        public int CurrentHP => currentHP;
        public int MaxHP => maxHP;
        public virtual int CurrentATK => currentATK;
        public virtual int CurrentDEF
        {
            get
            {
                int def = currentDEF;
                if (blockedEnemies != null && blockedEnemies.Count > 0)
                {
                    for (int i = 0; i < blockedEnemies.Count; i++)
                    {
                        var enemy = blockedEnemies[i];
                        if (enemy != null && enemy.IsBlocked && enemy is Enemies.GooelistEnemy gooelist)
                        {
                            def = Mathf.RoundToInt(def * (1f - gooelist.OperatorDefReductionPercent));
                            break;
                        }
                    }
                }
                return Mathf.Max(0, def);
            }
        }
        public virtual int CurrentRES => currentRES;
        public OperatorRarity CurrentRarity => currentRarity;
        public OperatorFacing Facing => facing;
        public OperatorClass OperatorClass => data.operatorClass;
        public virtual bool CanReceiveHealing => data == null || !data.cannotBeHealed;
        public virtual bool CanHitAir => data != null && (data.position == OperatorPosition.Ranged || data.position == OperatorPosition.Any || data.operatorClass == OperatorClass.Caster);
        public virtual bool IgnoresMissMechanic => false;
        public virtual bool IsCamouflaged => false;
        public virtual bool IsTargetable => !IsCamouflaged;

        public virtual bool CanBeTargetedByEnemy(EnemyBase attacker) => IsTargetable;
        public virtual bool HasTaunt => false;

        // --- Deployment & Combat Phase Tracking ---
        public bool HasFoughtInWave { get; set; } = false;
        public bool DeployedDuringCurrentPreparation { get; set; } = true;
        public bool IsCopiedFromFramiliar { get; set; } = false;
        public OperatorData OriginalFramiliarData { get; set; } = null;
        public OperatorRarity OriginalFramiliarRarity { get; set; } = OperatorRarity.Star2;

        /// <summary>
        /// Checks whether this operator taunts the specified attacker position.
        /// Ranged enemies prioritize this operator if the attacker is in a surrounding tile.
        /// </summary>
        public virtual bool ShouldTauntAttacker(Vector3 attackerWorldPosition, GridManager grid = null)
        {
            if (!HasTaunt || !isDeployed || currentHP <= 0 || !IsTargetable)
                return false;

            if (grid == null)
            {
                grid = FindAnyObjectByType<GridManager>();
            }

            if (grid != null)
            {
                Vector2Int opGrid = deployedCell != null
                    ? deployedCell.GridPosition
                    : grid.WorldToGridPosition(transform.position);
                Vector2Int attackerGrid = grid.WorldToGridPosition(attackerWorldPosition);

                int dx = Mathf.Abs(attackerGrid.x - opGrid.x);
                int dy = Mathf.Abs(attackerGrid.y - opGrid.y);
                return dx <= 1 && dy <= 1;
            }

            // Fallback for scenes/tests without GridManager
            float cellSize = 1.0f;
            return Mathf.Abs(attackerWorldPosition.x - transform.position.x) <= cellSize * 1.5f &&
                   Mathf.Abs(attackerWorldPosition.y - transform.position.y) <= cellSize * 1.5f;
        }

        public bool IsStunned => isStunned;
        public float StunTimer => stunTimer;
        public bool IsBound => isBound;
        public float BindTimer => bindTimer;
        public IReadOnlyList<EnemyBase> BlockedEnemies => blockedEnemies;
        public int HeatStacks { get; set; }
        private bool isBurning;

        /// <summary>On fire (Stage 4 heat): burns for damage until retreated. Setting it starts or stops the burning visuals.</summary>
        public bool IsBurning
        {
            get => isBurning;
            set
            {
                if (isBurning == value) return;
                isBurning = value;
                Combat.BurningVisual.SetBurning(gameObject, value);
            }
        }
        // Operators are frozen whenever no wave is running (Card Pick and Preparation): no damage (from enemies,
        // traps or weather), no healing, no shields and no abilities. Sandbox stays in Preparation for its whole
        // session (so cards can be deployed mid-wave), so the lock must not apply there.
        public bool IsCombatPaused =>
            GameManager.Instance != null && !GameManager.Instance.IsSandbox &&
            GameManager.Instance.CurrentPhase != StagePhase.WaveActive;

        /// <summary>
        /// Initialize this operator with data and rarity.
        /// Call this after instantiation, before deployment.
        /// </summary>
        public virtual void Initialize(OperatorData operatorData, OperatorRarity rarity, OperatorFacing operatorFacing = OperatorFacing.Right)
        {
            data = operatorData;
            currentRarity = rarity;
            facing = operatorFacing;

            // Scale stats by rarity
            maxHP = data.GetScaledHP(rarity);
            currentHP = maxHP;
            temporaryHP = 0;
            currentATK = data.GetScaledATK(rarity);
            currentDEF = data.GetScaledDEF(rarity);
            currentRES = data.GetScaledRES(rarity);

            attackTimer = 0f;
            trapDamageTimer = 0f;
            isDeployed = false;

            ApplyFacingVisuals();

            // Safety net: ensure sprite is visible even if prefab asset reference was broken
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null && (sr.sprite == null || sr.sprite.texture == null) && data != null && data.portrait != null)
            {
                sr.sprite = data.portrait;
            }

            var anim = GetComponent<OperatorSpriteAnimation>();
            if (anim != null)
            {
                anim.RefreshFromOperatorDataOrRenderer(data?.portrait);
            }
        }

        /// <summary>
        /// Sets HP to the value saved on the deck card when this operator last retreated (call after Initialize).
        /// This is not a heal: it bypasses IsCombatPaused and raises no heal effects.
        /// </summary>
        public void RestoreSavedHP(int hp)
        {
            currentHP = Mathf.Clamp(hp, 1, maxHP);
            if (currentHP < maxHP)
            {
                TrashTD.Combat.WorldHealthBar.UpdateFor(gameObject, currentHP, maxHP);
            }
        }

        public void SetFacing(OperatorFacing newFacing)
        {
            facing = newFacing;
            ApplyFacingVisuals();
        }

        /// <summary>
        /// Where this operator's shots leave its sprite: <see cref="OperatorData.projectileOrigin"/> from the sprite
        /// centre, mirrored like the sprite (the art faces left and is flipped only when facing right).
        /// </summary>
        public Vector3 ProjectileOrigin
        {
            get
            {
                Vector2 offset = data != null ? data.projectileOrigin : Vector2.zero;
                if (facing == OperatorFacing.Right) offset.x = -offset.x;
                Vector3 scale = transform.lossyScale;
                return transform.position + new Vector3(offset.x * scale.x, offset.y * scale.y, 0f);
            }
        }

        /// <summary>
        /// World-space center and half-extents of the operator's actual 32x32 body (1-unit tile footprint).
        /// Used by overhead health bars, stun stars, Harmony notes, and badges so large attack animation
        /// frames (e.g. Scrapper's 64x64 sledgehammer swing) do not displace or inflate standard status effects.
        /// </summary>
        public virtual Vector3 VisualCenter => transform.position;
        public virtual Vector2 BodyExtents => new Vector2(0.5f, 0.5f);

        public virtual void ApplyFacingVisuals()
        {
            bool mirrorHorizontally = facing == OperatorFacing.Right;
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.flipX = mirrorHorizontally;
            }
            else
            {
                SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>();
                for (int i = 0; i < renderers.Length; i++)
                {
                    renderers[i].flipX = mirrorHorizontally;
                }
            }
        }

        // ========================
        // IDeployable Implementation
        // ========================

        public bool Deploy(GridCell cell)
        {
            if (isDeployed) return false;
            if (!cell.CanDeploy(data.position)) return false;

            deployedCell = cell;
            isDeployed = true;
            trapDamageTimer = 0f;
            cell.Deploy(gameObject);
            transform.position = cell.WorldPosition;

            ApplyFacingVisuals();

            OnDeployed();
            return true;
        }

        public void Retreat()
        {
            if (!isDeployed) return;

            // Release all blocked enemies
            for (int i = blockedEnemies.Count - 1; i >= 0; i--)
            {
                ReleaseBlock(blockedEnemies[i]);
            }

            var slipperyEffects = GetComponents<Combat.SlipperyEffect>();
            for (int i = 0; i < slipperyEffects.Length; i++)
            {
                if (slipperyEffects[i] != null)
                {
                    slipperyEffects[i].ClearWithoutRestoring();
                }
            }
            blockCountModifier = 0;
            externalStats.RemoveAll(e => e.Stat == OperatorStat.Block);
            ClearBoundStatusText();

            deployedCell?.Vacate();
            deployedCell = null;
            isDeployed = false;
            IsBurning = false;
            trapDamageTimer = 0f;
            currentTarget = null;

            OnRetreated();
        }

        // ========================
        // IAttacker Implementation
        // ========================

        public virtual void Attack(EnemyBase target)
        {
            if (target == null || !isDeployed || isStunned || isBound) return;

            HasFoughtInWave = true;
            DeployedDuringCurrentPreparation = false;

            PlayAttackSound();
            CombatProjectileVisual.Fire(
                ProjectileOrigin,
                target,
                new Color(1f, 0.9f, 0.35f),
                12f,
                0.1f,
                0.05f,
                false,
                () => ApplyBasicAttackDamage(target));
        }

        private void ApplyBasicAttackDamage(EnemyBase target)
        {
            if (target == null || target.IsDead) return;

            int damage = Combat.DamageCalculator.CalculateDamage(CurrentATK, GetTargetMitigation(target));
            if (target.TryTakeAttackDamage(damage, data.damageType, this)) PlayImpactSound();
        }

        /// <summary>Attack sound: the operator's own Attack clip, else the shared one for its damage type.</summary>
        protected void PlayAttackSound()
        {
            if (data == null) return;

            SfxId fallback = data.damageType == DamageType.Arts ? SfxId.GameplayMagicAttack
                : data.position == OperatorPosition.Ranged ? SfxId.GameplayRangedPhysicalAttack
                : SfxId.GameplayMeleeAttack;
            PlayAttackSound(fallback);
        }

        /// <summary>Attack sound: the operator's own Attack clip, else the given shared sound.</summary>
        protected void PlayAttackSound(SfxId fallback)
        {
            if (data == null || AudioManager.Instance == null) return;

            AudioSource voice;
            if (!UnitSfx.Play(data, UnitSfxEvent.Attack, out voice))
                voice = AudioManager.Instance.PlaySfx(fallback);
            TrackAttackSound(voice);
        }

        /// <summary>Attack sound: the operator's named attack variant (e.g. "Charge1" -> Attack_Charge1), falling back to Attack, then fallback.</summary>
        protected void PlayAttackSound(string variantName, SfxId fallback, float pitch = 1f)
        {
            if (data == null || AudioManager.Instance == null) return;

            AudioSource voice;
            if (!UnitSfx.PlayAttack(data, variantName, out voice))
                voice = AudioManager.Instance.PlaySfx(fallback, 1f, pitch);
            TrackAttackSound(voice);
        }

        /// <summary>
        /// The operator's own Impact clip (projectile landing, explosion), if it has one. Called whenever one of its
        /// hits lands; repeats within a moment are throttled, so an AoE hit plays it once. When the impact plays, the
        /// attack sound still running (a long launch or charge-up) is cut so the two don't overlap.
        /// </summary>
        public void PlayImpactSound()
        {
            if (UnitSfx.Play(data, UnitSfxEvent.Impact, out AudioSource impactVoice) && impactVoice != null)
                CutAttackSound();
        }

        // Melee swings land in the same moment they start; an attack sound younger than this is left alone, or the
        // swing would be silenced.
        private const float MinAttackSoundBeforeCut = 0.1f;

        private AudioSource attackVoice;
        private AudioClip attackVoiceClip;
        private float attackVoiceStartTime;

        protected void TrackAttackSound(AudioSource voice)
        {
            attackVoice = voice;
            attackVoiceClip = voice != null ? voice.clip : null;
            attackVoiceStartTime = Time.unscaledTime;
        }

        private void CutAttackSound()
        {
            if (attackVoice == null) return;

            if (Time.unscaledTime - attackVoiceStartTime >= MinAttackSoundBeforeCut)
                AudioManager.Instance?.StopSfx(attackVoice, attackVoiceClip);
            attackVoice = null;
            attackVoiceClip = null;
        }

        /// <summary>The operator's own Ability clip, else the given shared sound (if any).</summary>
        protected void PlayAbilitySound(SfxId? fallback = null)
        {
            if (data == null || UnitSfx.Play(data, UnitSfxEvent.Ability)) return;
            if (fallback.HasValue) AudioManager.Instance?.PlaySfx(fallback.Value);
        }

        /// <summary>
        /// A named ability clip (abilityName "Dodge" -> Ability_Dodge) for operators with more than one trigger,
        /// else the operator's plain Ability clip, else the given shared sound (if any).
        /// </summary>
        protected void PlayAbilitySound(string abilityName, SfxId? fallback = null)
        {
            if (data == null || UnitSfx.PlayAbility(data, abilityName)) return;
            if (fallback.HasValue) AudioManager.Instance?.PlaySfx(fallback.Value);
        }
    }
}
