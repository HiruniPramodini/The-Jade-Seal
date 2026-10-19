# The Jade Seal — Unity world handover

The owner has confirmed the original-work permissions needed for private source sharing with authorized teammates and approved the saved courtyard paving, sanctuary paving and mountain-terrain appearances. This handover is prepared for a local commit on feature/world-environment. **No push or merge to main is authorized or performed by finalization.**

Unity **6000.3.25f1**, Built-in Render Pipeline. Open **Assets/JadeSeal/Scenes/JadeSealFantasy.unity**. The complete world includes four environment zones, terrain, vegetation, architecture, textures/materials, collision/navigation, three baked lightmap sets, 194 probes, reflection data and required scripts.

The 1,097-file cleared source allowlist is retained byte-for-byte from the successful clean validation project, including original head-generation source and Unity metadata. Five existing reviewed ProjectSettings files are retained separately, giving 1,102 Assets/Packages/ProjectSettings files. Documentation/Handoff/PAYLOAD_MANIFEST.csv records the exact file set and raw source hashes; PAYLOAD_INTEGRITY.json fingerprints it and explains self-exclusions. LFS stores designated assets as pointers in Git; checked-out payload hashes refer to the hydrated bytes.

The 21 environment commits through eca21bb remain ancestors of the handover commit. The separate successful handoff/verified-code-only commit c52861d and existing branches are preserved. No rebasing, squashing, force-push or reset is performed.

The existing clean Windows Development build succeeded with zero BuildReport errors/warnings; 274 navigation checks and 16 physical traversal routes passed. No Unity import, rebake or build was repeated during this finalization. Prior 54.01 FPS is historical; the final hidden traversal run is not rendered-performance evidence.

Read TEAMMATE_SETUP.md, THIRD_PARTY_ASSETS.md, GITHUB_UPLOAD_CHECKLIST.md and Documentation/Handoff/FINAL_HANDOVER_REPORT.md. Owner permission is limited to private teammate sharing; no project-wide open-source/publication license is assigned, and upstream rights remain unchanged. Excluded packs, rollback scenes, source archives, engine extractions, generated caches and validation harnesses are not included.
