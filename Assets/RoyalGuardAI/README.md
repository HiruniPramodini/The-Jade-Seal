# Royal Guard AI — THE JADE SEAL / SE3062

Owned namespace: `JadeSeal.RoyalGuard`. Owned directory: `Assets/RoyalGuardAI`.
This branch extends the supplied `RoyalGuard_IS_Unity_Package (1).zip`, discovered in Desktop/Downloads. The first commit imports its six scripts; subsequent commits implement actual improvements. No third-party character art was imported.

## Start here

Use Unity **6000.3.25f1**, AI Navigation **2.0.16** (already in the base manifest). Read SETUP_GUIDE.md before placing the reusable `Prefabs/RoyalGuard.prefab`. Its capsule body and primitive crossbow are explicitly temporary visuals. `Prefabs/RoyalGuardBolt.prefab` and `DefaultSettings.asset` are module assets.

The guard uses seven utility states, perception with bounded memory, validated NavMesh movement, weighted and reserved cover, peek-position combat, five-bolt magazines, defensive retreat, recovery and health events. It does not discover a player by tag or modify team scripts automatically. An unassigned player produces safe patrol behavior.

Documentation: SETUP_GUIDE.md (integration), AI_ARCHITECTURE.md (formulas and tradeoffs), TEST_PLAN.md (executed versus manual checks), VIVA_GUIDE.md (defense), VALIDATION_REPORT.md (actual results).

## Team handoff

After the owner explicitly approves and runs the push command:

```powershell
git push -u origin feature/royal-guard-ai
```

Teammates with a clean checkout can inspect the full branch:

```powershell
git fetch origin
git switch --track origin/feature/royal-guard-ai
```

If that local branch already exists, use `git switch feature/royal-guard-ai` then `git pull --ff-only`. To integrate only this module into another compatible Unity project, copy **Assets/RoyalGuardAI plus Assets/RoyalGuardAI.meta**, retaining all nested `.meta` files. Coordinate merges with the team; this work does not merge or push automatically. Do not copy Library, Temp, Logs or project caches. Player health hookup and scene references are project-specific and must be assigned as described in SETUP_GUIDE.md.
