# Validation report — 21 October 2026 (Asia/Colombo)

## Environment and actual execution

Unity Editor 6000.3.25f1 (e1dba0a9aba4), Windows, batch mode, null graphics device, AI Navigation 2.0.16. Tests run only against C:/Users/hirun/The-Jade-Seal-RoyalGuard. No original-project Editor instance was controlled. The matching installed Editor was invoked directly; the optional Unity CLI was not installed. No package versions or world scenes were changed.

The final deterministic run passed **21 custom Unity editor assertions**, exit code 0, marker ROYAL_GUARD_VALIDATION_PASS 21. It covers seven-state feasibility, missing target/memory, emergency transitions, minimum dwell, utility hysteresis and maximum-duration release, cover exposure/path/firing terms and zero-weight handling, health/damage/death invariants, and prefab wiring. No C# compilation errors or exceptions occurred in that final deterministic log. An earlier run exposed InitializeNavMeshPath being called in a field initializer; navigation now creates the path in Awake.

The final Play Mode result is recorded in Evidence/PlayMode.txt. These are custom real-frame assertions, not NUnit results or a human gameplay evaluation. The fresh unsaved test scene contains a runtime-built NavMesh, floor, wall, guard prefab and synthetic player collider/adapter. Checks cover game-time memory expiry, range/FOV/occlusion, retained memory, muzzle blocking, physical projectile damage exactly once, ammo/reload timing, complete/invalid navigation paths and actual movement across frames, disabled/destroyed targets, capped recovery, weighted hide/peek cover selection, reservation contention, disabled-cover reselection, autonomous no-cover Attack, low-health Retreat and death shutdown.

Unity's editor SearchDatabase startup code logged ArgumentOutOfRangeException in Play Mode runs, with stack frames wholly in UnityEditor.Search (SearchDatabase.EnumerateAll/GetDefaultSearchDatabase and SearchInit.IndexationOnStartup). The Royal Guard assertions completed despite it. This is a recorded editor tooling limitation; the full log is preserved locally under Library/RoyalGuardValidation. It is not represented as a clean Console or a Royal Guard gameplay pass. No other project components were changed to suppress it.

## Reproduce

Close the isolated project's Editor first. Deterministic checks regenerate owned placeholder prefabs. In PowerShell:

```powershell
$editor = 'C:\Program Files\Unity\Hub\Editor\6000.3.25f1\Editor\Unity.exe'
$project = 'C:\Users\hirun\The-Jade-Seal-RoyalGuard'
& $editor -batchmode -nographics -quit -projectPath $project -executeMethod JadeSeal.RoyalGuard.Editor.RoyalGuardValidation.Run -logFile "$project\RoyalGuardUnity.log"
& $editor -batchmode -nographics -projectPath $project -executeMethod JadeSeal.RoyalGuard.Editor.RoyalGuardPlayValidation.Run -logFile "$project\RoyalGuardPlayUnity.log"
```

The Play Mode runner exits itself after completion; do not pass -quit to that run. Its tests advance actual game frames and have a 45-second execution deadline after Play Mode starts. Read result files and process exit status, then inspect logs for errors; an assertion marker alone cannot prove an error-free run. Run independently, waiting for each Editor process to exit before starting the next one. The Play runner is a batch-only harness that opens a fresh unsaved scene and exits Unity; do not invoke it in an Editor containing unsaved work.

## Asset/scope audit

Only Assets/RoyalGuardAI and its root meta belong to this implementation. The generated model is primitive placeholder geometry, with no new third-party model license dependency. Unity-generated metadata is preserved. Final repository checks verify unique module GUIDs, no missing module .meta files, prefab script/settings/bolt GUID resolution, no files changed outside the owned module, git diff --check and clean working status. Raw logs/caches are local ignored artifacts, never committed. Evidence text files are actual test results.

## Not run / remaining integration

No human visual playtest, world-scene combat walkthrough, game build, graphics screenshot, animation/audio test, crowd performance benchmark, real team player-health integration or measured difficulty/rate comparison with another enemy was executed. Full patrol routes, sustained investigate behavior in the authored world, dynamic-carving blockage, multi-minute oscillation behavior and authentic player-controller interaction remain manual cases in TEST_PLAN.md. The existing world bake is preserved; no claim is made that every proposed cover point or placement is navigable in that scene. A licensed final model, actual player references, scene-authored cover/waypoints, team collision layers and health event hookup are required before gameplay release. No push or merge was performed.
