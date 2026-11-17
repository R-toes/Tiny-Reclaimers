# Sound effects

**The easiest way: TRASH TD > Sound Manager** (in the Unity editor). It lists every sound in the game, shows
whether it has its own file, plays a stand-in or is silent, and lets you:

- **Play** any file (at its in-game level).
- **Add** a sound: drop audio files onto it (or press *Add file...*). They are copied into the right folder with the right
  name. The first one gets the sound's name, more become variations (`_2`, `_3` ...), picked at random in the game.
- **Replace** a file (keeps its name and volume), **Rename** it to another sound of the same unit, or **Delete** it
  (it goes to the trash).
- **Set volumes**: per file, per group (one stage, operator or enemy) and per category (UI, Enemies, Ambience ...).
- Find files the game never plays (misnamed ones) under *Files the game never plays*.

Changes apply straight away, even while the game runs in Play mode. Everything below is what the Sound Manager does
for you, for reference.

## Where files go

All paths are under `Assets/_Project/Audio/Resources/`. Any format Unity imports works (`.wav`, `.mp3`, `.ogg`, `.aif`);
the extension doesn't matter, the name does.

```
SFX/
├── UI/                                  menu and draft buttons, card animations, deck dragging
├── Gameplay/                            deploy, retreat, waves, lives, victory/defeat, pause, banners
├── Combat/                              shared attack fallbacks, stun, miss
├── Tiles/                               trap and hole tiles (any stage)
├── Stages/Stage01 … Stage06/            stage ambience loops and hazards
├── Operators/{Class}/{OperatorName}/    per-operator sounds
└── Enemies/{Archetype}/{EnemyName}/     per-enemy sounds
Music/                                   MainMenuMusic, GameplayMusic, Stages/Stage01 … Stage06
```

- Folder names are the unit's in-game name with spaces and symbols removed: "IRONHOWL MK-IX" → `IRONHOWLMKIX`,
  "Stag-ger" → `Stagger`, "Dead Eye" → `DeadEye`, "THE ERADICATOR" → `THEERADICATOR`, "O.F.O" → `OFO`,
  "Tick-Torque" → `TickTorque`. Operators are grouped by class (Drawgoo is a **Specialist**), enemies by archetype
  (Occulorm is a **Rusher**).
- **Variations:** `Attack_2`, `Attack_3` ... are picked at random. The plain name counts as one of them and can be left out.
- **Missing files are fine.** A sound without a file plays its stand-in (if it has one) or nothing.

## How it plays

- **Pitch** varies slightly on every unit sound, so even a single file doesn't sound robotic.
- **Repeats are throttled.** The same unit's sound won't restart within 0.08 s, the same shared sound within 0.05 s.
- **Attack sounds stop when the impact plays.** If a unit has an `Impact` file, its still-running `Attack` is cut
  (0.04 s fade) when the impact plays, so a long launch never overlaps its own explosion. Melee swings that hit at once are
  left alone.
- **Ambience** and **music** loop: make them seamless. Ambience fades in and out; a retry keeps both playing.

## Volume

A sound's volume = **category** × **group** × **its own file level** × the player's SFX volume (Settings).

| Category | Default | Covers |
|---|---|---|
| UI, Gameplay, Combat, Tiles | 100% | the shared folders |
| Stage hazards | 100% | `Stages/StageNN/*` except Ambience |
| Stage ambience | 45% | `Stages/StageNN/Ambience` |
| Operators | 100% | every operator sound |
| Enemies | 70% | enemy attacks, impacts and abilities (many enemies are on screen at once) |
| Enemy deaths | 80% | enemy `Death` files and `Gameplay/EnemyDeath` |

- **Groups:** one level per stage, operator and enemy (100% by default).
- **Bounce falloff** (Progeny, Resicochet): how loud each bounce is next to the one before (50% by default, never below 5%).
- Levels only go **down**: Unity can't play a clip louder than recorded. To make one louder, raise it in an audio editor.
- Music plays at the player's Music volume and has no levels here.
- The levels live in `Audio/Resources/SfxVolumeSettings.asset` (the Sound Manager edits it; the Inspector works too).
  A level at its default has no row.

## Shared sounds

### `UI/`

| File | When it plays | Without a file |
|---|---|---|
| `ButtonHover` | The pointer moves onto a button. | – |
| `ButtonClick` | A button is pressed. | – |
| `ButtonBack` | Back / close buttons. | – |
| `ButtonConfirm` | A draft card is picked; confirming in the tutorial, Intel and results. | – |
| `ButtonDenied` | Locked stage, card on cooldown, squad full, a tile the operator can't stand on. | – |
| `CardShuffle` | Draft cards riffle-shuffle (each swap), and the draft-odds reels spinning. | ButtonHover |
| `CardDeal` | Each draft card slides into place. | ButtonHover |
| `CardFlip` | A draft card flips face-up. | ButtonHover |
| `Reroll` | Draft REROLL. | ButtonClick |
| `CardDiscard` | A deck card is discarded (after confirming). | silent |
| `CardPickUp` | A deck card is picked up to drag it. | silent |
| `CardDrop` | A dragged deck card is dropped on another slot. | silent |
| `TutorialVoice` | Coalesce's talking blip, per typed character (≈40 ms). | a generated blip |

### `Gameplay/`

| File | When it plays |
|---|---|
| `Deploy` | Every deploy (the operator's own `Deploy` plays on top). |
| `Retreat` | An operator is retreated to the deck (not on death). |
| `WaveStart` | A wave begins (the INVADING banner). |
| `Preparation` | The PREPARATION banner, after the draft. |
| `HazardBriefing` | The stage hazard briefing pops up. |
| `WaveCleared` | Every enemy of a wave is dead (not the last wave). |
| `LifeLost` | An enemy reaches the exit. |
| `Victory` / `Defeat` | Stage cleared / out of lives. |
| `Pause` / `Resume` | The pause menu opens (Esc, the button, or the window losing focus) / closes. |
| `CardUpgrade` | 3 copies merge into a higher-rarity card; also the draft's ODDS UP! (ButtonConfirm until it exists). |
| `OperatorDeath` | An operator without its own `Death` dies. |
| `EnemyDeath` | An enemy without its own `Death` dies. |

### `Combat/`, `Tiles/`

| File | When it plays |
|---|---|
| `Combat/MeleeAttack`, `RangedPhysicalAttack`, `MagicAttack` | An operator's attack without its own `Attack`, by its damage type; also other stand-ins. |
| `Combat/Stun` | An operator or enemy is stunned. |
| `Combat/Miss` | An attack misses (sandstorm, evasive enemies). |
| `Tiles/TrapTick` | Each trap tile damage tick. |
| `Tiles/HoleEnter` / `HoleExit` | An enemy drops into / climbs out of a hole (Stage 5). |

### `Stages/`

| Folder | File | When it plays |
|---|---|---|
| any `StageNN` | `Ambience` | The stage's background loop (variations: one is picked per stage start). |
| `Stage02` | `ItemFall` | An item starts falling (every 10 s). |
| `Stage02` | `ItemLandGlass` / `ItemLandMetal` / `ItemLandTools` | That item lands (Combat/MeleeAttack until they exist). |
| `Stage03` | `Gust` | A strong sandstorm gust starts. |
| `Stage04` | `HeatBlast` | The reactor blasts heat at a wave start. |
| `Stage04` | `HeatUp` / `Ignite` / `BurnTick` / `Extinguish` | A heat stack / catches fire / burn tick / a burning operator retreats. |
| `Stage05` | `CarPass` / `CarHit` | A car enters / hits someone. |
| `Stage06` | `AcidTick` | Acid rain damage tick (every 2 s). |

## Units

| File | Operators | Enemies |
|---|---|---|
| `Deploy` | Placed on the field (on top of Gameplay/Deploy). | – |
| `Attack` | The attack starts; Medics: each heal. Stand-in: the Combat sound for its damage type. | The attack starts. Silent without a file (Talon Rat: Combat/MeleeAttack). |
| `Impact` | A hit lands (once per volley). | Its ranged attack lands. |
| `Ability` | Its special trigger (below). | Its ability (below). |
| `Death` | Dies (stand-in Gameplay/OperatorDeath). | Dies (stand-in Gameplay/EnemyDeath). |

A named ability `Ability_X` is also found as `X`, and falls back to the unit's plain `Ability` file, so one `Ability`
file covers all of a unit's triggers (except where noted).

| Operator | Named sounds |
|---|---|
| Qihou | `Ability`: knockback (every 4th hit) |
| Scrapper | `Ability`: its stun |
| Echosquire | `Ability_Dodge`: second-chance dodge |
| Vespierce | `Ability_Regen`: starts regenerating |
| Stag-ger | `Ability_Dodge` · `Ability_Empowered` (strike after a dodge) · `Ability_Pull` (stand-in Combat/Stun) |
| IRONHOWL MK-IX | `Ability`: shockwave, frenzy switching on |
| Thornchin | `Ability_Retaliate` (stand-in Combat/MeleeAttack) · `Ability_Surrounding` (upgraded AoE; falls back to Retaliate) |
| Mlem | `Ability_Swallow` · `Ability_Digest` (repeats while digesting; no Ability fallback) |
| Mossmo | `Ability` (or `Pulse`): reactive heal pulse (stand-in Combat/MagicAttack) · `Ability_Enrage`: an enemy turns enraged (no Ability fallback) |
| Drawgoo | `Ability`: the death card-draw |
| Oquloct | `Ability`: turns camouflaged |
| Nekhrost | `Ability_Freeze` · `Ability_ExtraHit` |
| Resicochet | `Ability`: stores a charge. `Attack` also accepts `Charge 1/2/3`. Bounces replay the attack quieter. |
| Bubblets | `Ability`: a bubble pops (stand-in Combat/MagicAttack) |
| Coalesce | `Ability`: bonus heal |
| Progeny | `Deploy` also accepts `Cry`, `Attack` also `Heal` / `Laser` / `Beam`; `Ability_Bounce`: the heal bouncing on (else the heal replays, quieter) |
| Framiliar | `Ability_Transform` (or `Transform`): morphing (stand-in Gameplay/CardUpgrade) |

| Enemy | Named sounds |
|---|---|
| Hextension | `Ability_Bind`: casts its bind |
| Gooelist | `Ability_Deflect`: deflects a non-blocker's hit ("DUEL") |
| O.F.O | `Ability_Summon`: starts summoning |
| Exploding Bug, Robozombie | `Ability_Detonate`: explodes on death (their `Death` plays too) |
| Robodrone | `Ability_Reload`: reloads |
| THE ERADICATOR | `Attack_Heavy` / `Impact_Heavy`: the heavy warhead (fall back to `Attack` / `Impact`) |

Code: `Scripts/Audio/SoundCatalog*.cs` (every sound, with these descriptions), `SoundNames.cs` (the named sounds),
`AudioManager.cs` (shared sounds: `SfxPaths`), `UnitSfx.cs` (unit sounds), `SoundVolumeGroups.cs` and
`SfxVolumeSettings.cs` (volumes), `Editor/SoundManagerWindow*.cs` (the window).
