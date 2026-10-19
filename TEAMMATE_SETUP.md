# Teammate setup — after packaging holds are resolved

1. Use the approved fresh payload only; do not copy or merge the old project .git directory or its asset history.
2. Install Git LFS locally before cloning the approved repository, then fetch LFS objects. No Git initialization, commit, push or remote modification was done during preparation.
3. Install Unity 6000.3.25f1 with Windows Build Support. Open the project from Unity Hub. Keep Built-in rendering; do not run URP conversion.
4. Restore the included Packages/manifest.json and packages-lock.json: navigation 2.0.16, input system 1.20.0 and built-in 1.0.0 modules. Tool/AI/development package removal is intentional.
5. Open Assets/JadeSeal/Scenes/JadeSealFantasy.unity. Expected scene has four environments, 1844 renderers, 217 LODGroups, 194 probes and three lightmaps. Missing materials/scripts must remain zero.
6. The settings baseline is the clean validation ProjectSettings.asset: enableFrameTimingStats=1; preloadedAssets points to InputSystem_Actions.inputactions GUID 289c1b55c9541489481df5cc06664110. Keep both asset and metadata. The working copy's differing settings remain preserved separately.
7. Add the main scene to a Build Profile when a future build is authorized. The old build helper passed the scene explicitly; automatically generated EditorBuildSettings has an empty scene list and is outside the proposed 1094-file closure.
8. Editor-only DistributionCleanCheck and opt-in FinalClearanceBenchmark harness files are excluded. JadeSealPhase7World.cs contains serialized world metadata and an inert tester type in the same script; preserve its GUID. Do not remove that script merely to remove its unused tester type.
9. Respect attribution and modification notices. Keep original author/source/license links for CC BY assets and restore package notices. No excluded source pack or rollback scene may be reintroduced automatically.
10. Do not import or rebuild solely to repeat completed validation. A future fresh teammate checkout naturally imports its own Library; this preparation session did not repeat that operation.
