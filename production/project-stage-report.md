# Project Stage Analysis — Reboot The Earth

**Date**: 2026-11-26
**Stage**: Production
**Stage Confidence**: PASS — clearly detected
**Analysis Scope**: Full project (general, no role filter)
**Workflow tier**: `minimal` (`modes.rigor` not set in `project.yaml` → default `minimal`)

## Completeness Overview
- **Design**: 0 docs — `design/` does not exist. At `minimal` the only expected doc is `design/game-brief.md`, and it is absent. The README holds the play flow and per-file overview.
- **Code**: 44 C# files, ~6,000 lines under `Assets/Game/` — Core 8, Intro 10, WasteHunters 14, LastSeed 8, Editor 4. Full flow is playable: cover → character select → Game 1 practice → Game 1 → Game 2 practice → Game 2 → result → cover. Plus the Python tracker (`Python/`, YOLO + MediaPipe → UDP).
- **Architecture**: 0 ADRs (`docs/architecture/` has only `tr-registry.yaml`). Not a gap at `minimal`.
- **Production**: no epics/stories/sprints (not expected at `minimal`). 19 evidence entries in `production/qa/evidence/` from playthroughs on 2026-11-25/26. Large amount of uncommitted work.
- **Tests**: 0 Unity tests (no `Assets/Tests/`). Python side has `Python/dev/test_player_lock.py` plus simulators (`sim_wastehunters.py`, `sim_presence.py`). Not required at `minimal`.

## Stage Classification Rationale
**Configured stage**: not set (no `project.stage` in `project.yaml`, no `production/stage.txt`).
**Observed stage**: Production — engine configured (Unity 6000.3.22f1, URP) and the code root holds 44 source files with active development (recent commits plus uncommitted changes across 10+ scripts). Nothing has run `/gate-check`, so Polish/Release are not claimed.

## Gaps Identified

### Studio artifacts (what the `minimal` tier expects)
1. **No `design/game-brief.md`.** It is the only design record `minimal` requires. The game is already built, so it can be written from the code. *Question: does the MI316 final require a design document? If so this moves up.*

### Game / product gaps (from reading the code and assets)
2. **Never built as a standalone player.** There is no `Builds/` folder, although `commands.build` is configured. Untested for the exhibition setup:
   - `FullscreenGameView` (fullscreen on the second display) is **Editor-only**; a build needs its own fullscreen/display handling.
   - `commands.run` launches at 1280×720 windowed.
   - The Python tracker (`Python/run_tracker.bat`) must be started by hand; the game does not launch it.
   *Question: will the event run from the Unity Editor or from a built .exe?*
3. **No bridge between Mission 1 and Mission 2.** Game 1's result screen loads `The Last Seed` straight into its practice. Prepared but unused assets: `Assets/StreamingAssets/intro.mp4` (no code references it) and the cutscene music `Sound BGM/Cutscene 1/mixkit-valley-sunset-127.mp3`, `Sound BGM/Cutscene 2/mixkit-forest-mist-whispers-148.mp3`.
4. **No combined ending.** After Game 2 the session returns to the cover with no "Earth rebooted" ending and no team total across both missions.
5. **No operator controls.** Volume, round length and display choice can only be changed in the Unity Inspector (`GameSettings`, `SeedSettings`). In-game keys are just Esc (quit), R (back to cover), F1 (debug), F2 (bot in Game 2).
6. **Scenes have no AudioListener.** Fixed at runtime by `Core/AudioListenerGuard.cs` (2026-11-26). The scene builders (`Editor/WasteHuntersSceneBuilder.cs`, `Editor/LastSeedSceneBuilder.cs`) still create cameras without one.
7. **Leftovers to clean up.**
   - Build Settings lists `Assets/Scenes/Legacy/TrashBattle.unity`, which no longer exists (entry disabled, so harmless).
   - `Intro/TutorialScreen.cs` and `WasteHunters/CoverScreen.cs` are referenced by no other script.
8. **Uncommitted work at risk.** Audio wiring, `AudioListenerGuard`, `FullscreenGameView`, new sounds in `Resources/Audio/`, and earlier edits to `CharacterSelect`, `PlayerLane`, `TrashPiece`, `WasteEvents`, `WasteHunters`, `LastSeed` are not in git.
9. **No Unity tests** for the numeric rules (Game 1 score/rush multipliers, Game 2 growth, balance, PERFECT SYNC window). Optional at `minimal`, but these are cheap to cover.

## Recommended Next Steps
1. **Commit the pending work** — the cheapest risk reduction available.
2. **Make and test a build on the exhibition machine** with camera and second display: fullscreen on the right display, tracker start-up, audio.
3. **Add the Mission 1 → Mission 2 bridge** using `intro.mp4` plus the cutscene BGM, and a short combined ending.
4. **Write `design/game-brief.md`** with `/reverse-document` if the course needs a design record.
5. Clean-ups: add an AudioListener in the scene builders, remove the stale Build Settings entry, delete or archive `TutorialScreen.cs`/`CoverScreen.cs`.

## Follow-Up Skills to Run
- `/reverse-document design Assets/` — writes `design/game-brief.md` from the built game (the one gap the `minimal` tier surfaces).
- `/smoke-check` — once a build exists, before showing it.
- `/test-setup` — only if you decide to add Unity tests (gap 9).

## Appendix: File Counts by Directory

```
design/                 — (absent)

Assets/Game/            (Unity code root)
  Scripts/Core/          8 files
  Scripts/Intro/        10 files
  Scripts/WasteHunters/ 14 files
  Scripts/LastSeed/      8 files
  Editor/                4 files

docs/
  architecture/          0 ADRs

production/
  sprints/               — (absent; not used at minimal)
  qa/evidence/          19 entries

Assets/Tests/            — (absent)
Python/dev/              1 unit test + 3 simulators/tools
prototypes/              — (absent)
```

*Generated by `/project-stage-detect`*
