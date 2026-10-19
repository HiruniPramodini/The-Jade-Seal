# Teammate setup — approved private handover

1. Owner sharing permission and all three visual decisions are recorded under Documentation/Handoff. Finalization creates the reviewed local handover commit; remote push requires separate approval.
2. After the separately approved push, install Git LFS before cloning the private repository. Obtain authorized access, select feature/world-environment and run git lfs pull. No teammate branch is changed by this review.
3. Open using Unity 6000.3.25f1 with Windows Build Support. Keep Built-in rendering. Let Unity restore the pinned navigation 2.0.16, input system 1.20.0 and built-in packages from Packages/manifest.json and packages-lock.json. PackageCache is not included.
4. Open Assets/JadeSeal/Scenes/JadeSealFantasy.unity. Preserve all asset/folder .meta files and GUIDs. Expected retained data: four zones, three lightmaps, 194 probes and the existing NavMesh/reflection textures. Keep terrain, vegetation, materials, scripts and baked scene folders together.
5. The main scene is not listed in the retained generated EditorBuildSettings. For a future authorized build, add this scene to a Build Profile or pass it explicitly. Do not run earlier scene generators.
6. Keep Assets/Editor/AuthoredDummyHeadGeometry.cs and its metadata as original source evidence. DistributionCleanCheck, FinalHeadResolution and FinalClearanceBenchmark validation harnesses are excluded. JadeSealPhase7World.cs remains required serialized metadata; its inert traversal tester type is in the same script.
7. No fresh Unity import/build is performed for this handoff review. The already successful clean build and sampled validation reports are supplied under Documentation/Validation. A teammate's own fresh checkout naturally creates its Library.
8. Retain THIRD_PARTY_ASSETS.md, copyright/license notices and source/modification credits. Do not copy excluded local packs/archives or publish original contributions without the necessary authorization.
