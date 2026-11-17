using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TrashTD.Audio;
using TrashTD.Combat;
using TrashTD.Core.Grid;
using TrashTD.Data;
using TrashTD.Enemies;
using TrashTD.Systems;

namespace TrashTD.Operators
{
    /// <summary>
    /// Resicochet — 3-Star Caster.
    /// High physical DEF, low magic RES.
    /// Ability:
    /// - Stores 1 charge every 5 seconds (up to 3 charges, default 1).
    /// - When attacking, fires 1 projectile per stored charge at 0.5s intervals.
    /// - Each projectile bounces to surrounding tiles of the hit target (up to 4 targets total),
    ///   reducing damage by 25% on each bounce.
    /// - Sprite row changes dynamically with charge count (Row 1: 1 charge, Row 2: 2 charges, Row 3: 3 charges).
    /// - Floating crystals have an outline aura effect matching Vespierce's isolation talent,
    ///   scaling, pulsing, and bobbing with the crystals frame-by-frame and matching charge color (Orange -> Green -> Blue).
    /// </summary>
    public partial class ResicochetOperator : CasterOperator, IOperatorSpriteFrames
    {
        public Sprite[] Frames => charge1Frames;
        public float FrameDuration => 0.25f;
        public const float ChargeIntervalSeconds = 5.0f;
        public const float UpgradedChargeIntervalSeconds = 3.0f;
        public const int MaxChargeCount = 3;
        public const float BurstFireInterval = 0.5f;
        public const int MaxBounceTargets = 4;
        public const int UpgradedMaxBounceTargets = 5;
        public const float BounceDamageReductionPerHit = 0.25f;

        public bool HasFirstUpgrade => currentRarity >= OperatorRarity.Star4;
        public bool HasSecondUpgrade => currentRarity >= OperatorRarity.Star5;

        public float CurrentChargeInterval => HasFirstUpgrade ? UpgradedChargeIntervalSeconds : ChargeIntervalSeconds;
        public int CurrentMaxBounceTargets => HasFirstUpgrade ? UpgradedMaxBounceTargets : MaxBounceTargets;

        public void SetRarityForTesting(OperatorRarity rarity)
        {
            currentRarity = rarity;
        }

        public float GetBounceDamageMultiplier(int bounceIndex)
        {
            if (HasSecondUpgrade)
            {
                return 1.0f; // 2x Upgraded: Bounce damage is no longer reduced (100% on all bounces)
            }

            float reductionPerBounce = HasFirstUpgrade ? 0.20f : 0.25f;
            return Mathf.Max(0.1f, 1.0f - (bounceIndex * reductionPerBounce));
        }

        [Header("Sluggish Effect")]
        [SerializeField] private float sluggishDuration = 3.0f;
        [SerializeField] private float sluggishSpeedMultiplier = 0.6f;
        [SerializeField] private float sluggishAttackIntervalMultiplier = 1.5f;

        public float SluggishDuration => sluggishDuration;
        public float SluggishSpeedMultiplier => sluggishSpeedMultiplier;
        public float SluggishAttackIntervalMultiplier => sluggishAttackIntervalMultiplier;

        [Header("Audio")]
        [Tooltip("Custom sound played on attack. If null, loads from SFX/Operators/Caster/Resicochet/Attack (supports Attack, Attack_2, Attack_3, etc.), falling back to MagicAttack.")]
        [SerializeField] private AudioClip attackSound;
        [Tooltip("Optional array of attack sounds to randomize between in Inspector.")]
        [SerializeField] private AudioClip[] attackSounds;
        [Tooltip("Optional custom override sound played on bounce. If null, replays the attack sound quieter and quieter per bounce.")]
        [SerializeField] private AudioClip bounceSound;
        [SerializeField] private AudioClip[] bounceSounds;

        private AudioClip currentVolleyAttackClip;
        private AudioSource currentVolleyVoice;
        private AudioClip currentVolleyVoiceClip;

        public AudioClip CurrentVolleyAttackClip => currentVolleyAttackClip;
        public AudioSource CurrentVolleyVoice => currentVolleyVoice;
        public AudioClip CurrentVolleyVoiceClip => currentVolleyVoiceClip;

        public void SetCurrentVolleyAttackClipForTesting(AudioClip clip) => currentVolleyAttackClip = clip;
        public void SetCurrentVolleyVoiceForTesting(AudioSource voice, AudioClip clip)
        {
            currentVolleyVoice = voice;
            currentVolleyVoiceClip = clip;
        }

        private void StopPreviousVolleyVoice()
        {
            if (currentVolleyVoice != null && currentVolleyVoiceClip != null)
            {
                AudioManager.Instance?.StopSfx(currentVolleyVoice, currentVolleyVoiceClip);
                currentVolleyVoice = null;
                currentVolleyVoiceClip = null;
            }
        }

        public AudioClip AttackSound
        {
            get => attackSound;
            set => attackSound = value;
        }

        public AudioClip[] AttackSounds
        {
            get => attackSounds;
            set => attackSounds = value;
        }

        public AudioClip BounceSound
        {
            get => bounceSound;
            set => bounceSound = value;
        }

        public AudioClip[] BounceSounds
        {
            get => bounceSounds;
            set => bounceSounds = value;
        }

        // Backwards compatibility properties
        public AudioClip Charge1AttackSound { get => attackSound; set => attackSound = value; }
        public AudioClip[] Charge1AttackSounds { get => attackSounds; set => attackSounds = value; }
        public AudioClip Charge2AttackSound { get => attackSound; set => attackSound = value; }
        public AudioClip[] Charge2AttackSounds { get => attackSounds; set => attackSounds = value; }
        public AudioClip Charge3AttackSound { get => attackSound; set => attackSound = value; }
        public AudioClip[] Charge3AttackSounds { get => attackSounds; set => attackSounds = value; }

        public static readonly Color Charge1ProjectileColor = new Color(1f, 0.55f, 0.12f); // Orange (1 charge)
        public static readonly Color Charge2ProjectileColor = new Color(0.2f, 0.95f, 0.35f); // Green (2 charges)
        public static readonly Color Charge3ProjectileColor = new Color(0.2f, 0.65f, 1f);   // Blue (3 charges)

        public static Color GetProjectileColorForCharges(int chargeCount)
        {
            return chargeCount switch
            {
                1 => Charge1ProjectileColor,
                2 => Charge2ProjectileColor,
                3 => Charge3ProjectileColor,
                _ => Charge1ProjectileColor
            };
        }

        public static float GetChargeDamageMultiplier(int chargeCount)
        {
            return chargeCount switch
            {
                2 => 1.10f,
                3 => 1.25f,
                _ => 1.0f
            };
        }

        [Header("Resicochet Charge Animation Frames (4 frames each)")]
        [SerializeField] private Sprite[] charge1Frames;
        [SerializeField] private Sprite[] charge2Frames;
        [SerializeField] private Sprite[] charge3Frames;

        [Header("Resicochet Crystal Outline Frames (4 frames each)")]
        [SerializeField] private Sprite[] crystalCharge1Frames;
        [SerializeField] private Sprite[] crystalCharge2Frames;
        [SerializeField] private Sprite[] crystalCharge3Frames;

        private int charges = 1;
        private float chargeTimer = 0f;
        private bool isFiringBurst = false;
        private Coroutine burstCoroutine;
        private OperatorSpriteAnimation spriteAnim;

        // Vespierce-style crystal aura & outline renderers
        private SpriteRenderer opSpriteRenderer;
        private SpriteRenderer crystalAuraOuter;
        private SpriteRenderer crystalAuraInner;
        private SpriteRenderer crystalOutlineLeft;
        private SpriteRenderer crystalOutlineRight;
        private SpriteRenderer crystalOutlineUp;
        private SpriteRenderer crystalOutlineDown;

        public int CurrentCharges => charges;
        public float ChargeTimer => chargeTimer;
        public int MaxCharges => MaxChargeCount;
        public bool IsFiringBurst => isFiringBurst;

        public Sprite[] Charge1Frames => charge1Frames;
        public Sprite[] Charge2Frames => charge2Frames;
        public Sprite[] Charge3Frames => charge3Frames;

        public Sprite[] CrystalCharge1Frames => crystalCharge1Frames;
        public Sprite[] CrystalCharge2Frames => crystalCharge2Frames;
        public Sprite[] CrystalCharge3Frames => crystalCharge3Frames;

        public SpriteRenderer CrystalAuraOuter => crystalAuraOuter;
        public SpriteRenderer CrystalAuraInner => crystalAuraInner;
        public SpriteRenderer CrystalOutlineLeft => crystalOutlineLeft;
        public SpriteRenderer CrystalOutlineRight => crystalOutlineRight;
        public SpriteRenderer CrystalOutlineUp => crystalOutlineUp;
        public SpriteRenderer CrystalOutlineDown => crystalOutlineDown;

        public void ConfigureAnimationFrames(Sprite[] c1, Sprite[] c2, Sprite[] c3)
        {
            charge1Frames = c1;
            charge2Frames = c2;
            charge3Frames = c3;
            UpdateChargeVisuals();
        }

        public void ConfigureCrystalAnimationFrames(Sprite[] c1, Sprite[] c2, Sprite[] c3)
        {
            crystalCharge1Frames = c1;
            crystalCharge2Frames = c2;
            crystalCharge3Frames = c3;
            if (crystalAuraOuter != null)
            {
                UpdateCrystalAuras();
            }
        }

        public override void Initialize(OperatorData operatorData, OperatorRarity rarity, OperatorFacing operatorFacing = OperatorFacing.Right)
        {
            base.Initialize(operatorData, rarity, operatorFacing);
            charges = 1;
            chargeTimer = 0f;
            isFiringBurst = false;
            EnsureFramesLoaded();
            UpdateChargeVisuals();
        }

        protected override void OnDeployed()
        {
            base.OnDeployed();
            charges = 1;
            chargeTimer = 0f;
            isFiringBurst = false;
            EnsureFramesLoaded();
            UpdateChargeVisuals();
            CreateCrystalAuras();
            SetCrystalAuraEnabled(true);
            UpdateCrystalAuras();
        }

        private void Awake()
        {
            EnsureFramesLoaded();
            UpdateChargeVisuals();
        }

        private static Texture2D cachedCrystalTexture;
        private static Sprite[] cachedCrystal1Frames;
        private static Sprite[] cachedCrystal2Frames;
        private static Sprite[] cachedCrystal3Frames;
        private static Sprite[] cachedCharge1Frames;
        private static Sprite[] cachedCharge2Frames;
        private static Sprite[] cachedCharge3Frames;
    }
}
