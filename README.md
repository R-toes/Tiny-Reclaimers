# Tiny Reclaimers TD (TRASH TD)

A 2D tower-defense game built in Unity. Instead of picking a squad up front, you **draft operator cards** between waves
and deploy them from your deck onto the map to hold back waves of enemies. Operators upgrade by merging three copies of
the same card. Six stages, each with its own environment (weather, falling scrap, sandstorm, factory heat, highway
traffic, acid rain).

## Requirements

- **Unity 6000.6.1f1** (Unity 6; the exact version is in `TRASH-TD/ProjectSettings/ProjectVersion.txt`)
- Universal Render Pipeline with the 2D Renderer, the new Input System, uGUI
- Python 3 (only for the repository checks in `Tools/`)

## Open and run

1. Open the `TRASH-TD/` folder (the one with `Assets/` and `ProjectSettings/`) in Unity Hub.
2. Open `Assets/_Project/Scenes/MainMenu.unity` and press Play. The menu loads the stages; `GameplayTest.unity` is the
   gameplay scene they run in.
3. **TRASH TD > Dev Mode** (editor menu) turns on developer tools: the QA sandbox, the developer card picker and every
   stage unlocked. It is off by default, which is how a player sees the game.

## Tests and checks

- **Unity tests:** Window > General > Test Runner > **EditMode** > Run All. They cover combat maths, pathfinding, the
  card draft, waves, stage hazards and the save system.
- **Repository checks:** `python Tools/check_project.py` (no Unity needed). It verifies that every asset has its `.meta`
  file and no GUID is used twice, that C# files have balanced braces, that no merge-conflict markers were left behind,
  and it warns about oversized files and stage-id string checks.
- **Stage Maker:** *TRASH TD > Stage Maker* is a level editor for every stage: set the grid size, paint or drag tiles
  (spawn, exit, low / high ground, acid low / high ground, blocked, hole), add stage and bounds art (size-checked), preview
  the enemy routes and redraw them by hand. See
  `PROJECT_GUIDELINES.md`, "Stage Maker".
- **Git hooks:** `.githooks/` runs those repository checks before every commit and every push, so a half-resolved merge
  or stash never leaves your machine. Opening the Unity project turns them on for your clone; without Unity run
  `python Tools/setup_hooks.py` once. Python 3 must be installed or the commit is stopped.
- **CI:** `.github/workflows/ci.yml` runs the repository checks on every push and pull request, and the Unity EditMode
  tests too once the `UNITY_LICENSE` secret is set (see the comments in that file).

## Saves

Progress (stage clears and stars, the tutorial flag, the Intel enemy files) is one JSON file,
`trash_td_save.json`, in `%USERPROFILE%\AppData\LocalLow\<company>\<product>` on Windows. Settings (audio, graphics,
controls) are kept separately in `PlayerPrefs`.

- In game: **Settings > Gameplay > SAVE DATA > RESET** (main menu only) erases progress.
- In the editor: **TRASH TD > Reset Player Progress**, and **TRASH TD > Open Save Folder** to find the file.

## Where things are

| Path | What |
|---|---|
| `TRASH-TD/Assets/_Project/Scripts/` | All game code (`Core`, `Combat`, `Enemies`, `Operators`, `UI`, `Audio`, `Data`, `Systems`, `Development`, `Editor`) |
| `TRASH-TD/Assets/_Project/Tests/EditMode/` | Unity EditMode tests |
| `TRASH-TD/Assets/_Project/Art`, `Audio`, `Data`, `Prefabs`, `Scenes` | Content, one folder per unit or level |
| `TRASH-TD/Assets/Resources/Stages/` | One `StageData` asset per stage (map, waves, environment) |
| `TRASH-TD/Assets/Resources/Acts/` | One `ActData` asset per act: its stages, marker positions on its stage map, map art and zone text (see "Acts" in `PROJECT_GUIDELINES.md`) |
| `Docs/` | The audio guide |
| `Tools/` | Repository scripts (`check_project.py`, `setup_hooks.py`, placeholder texture generator) |
| `.githooks/` | Git hooks (pre-commit, pre-push) that run the repository checks |
| `PROJECT_GUIDELINES.md` | **Read this first when working on the project:** design rules, implementation notes, working agreements |

Large classes are split into partial files by topic (`GameplayHUDUI.TopBar.cs`, `EnemyBase.Movement.cs`, ...); the
table in the "Code layout" section of `PROJECT_GUIDELINES.md` lists the parts.
