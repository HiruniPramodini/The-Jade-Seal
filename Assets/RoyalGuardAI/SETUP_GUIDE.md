# Integration guide

## Existing project

Base: origin/feature/world-environment at 3f4a16e. Unity 6000.3.25f1; AI Navigation 2.0.16. The base contains one saved scene, `Assets/JadeSeal/Scenes/JadeSealFantasy.unity`. Its NavMeshSurface has agent type 0, volume collection (160 x 60 x 140), PhysicsColliders geometry, all layers, 0.12 voxel size and 128 tile size. Its surface references NavMeshData GUID fab46212d74d7e24dad11a986194917b; two NavMeshModifierVolumes are serialized. The top-level legacy NavMeshSettings null field does not imply the surface has no bake. No existing NavMeshAgent, obstacle or link components were found in saved scenes/prefabs. No team player controller or actual player-health component is supplied on this base. Existing authored world/Phase6/Phase7 scripts are preserved.

## Placement and navigation

1. Open the isolated project with the exact editor version. Let imports finish; resolve any Console errors before proceeding. Use the committed prefab. The menu `Jade Seal > Royal Guard > Create or update placeholder prefabs` regenerates owned defaults and overwrites those two prefabs; do not use it after customizing their assets without backing up your changes.
2. Make a new test scene or a copy of the world scene in your own integration branch. Drag `RoyalGuard.prefab` onto a blue baked NavMesh area. Agent type 0, radius .4, height 2, speed 3.5 and stopping distance .35 are defaults. Match the surface bake settings to the agent dimensions. Keep root scale (1,1,1); no dynamic Rigidbody on the guard. Re-bake only when geometry changes require it, and inspect navigable space near columns and steps. Do not replace the world's existing surface blindly.
3. Assign optional patrol transforms to Brain.patrolWaypoints; all must lie on the same reachable NavMesh region. Null or invalid waypoints are skipped. No waypoints means idle patrol.
4. Assign player root to Perception.player. The default aim offset is +1m; adapt it in Perception for a differently scaled controller. Eyes is already wired. Destroying/disabling the player expires memory and stops further attacks. A new respawned player must be assigned explicitly.

## Collision layers and sight

Use separate world-obstacle, player, guard and projectile layers. Set Perception.obstacles to solid world geometry only, excluding ALL actor and projectile layers; this mask is used for vision, cover exposure and muzzle checks. If player colliders remain in the obstacle mask, the target can incorrectly obstruct itself. If guard colliders remain in it, the guard may occlude itself. Triggers are deliberately ignored. Do not omit walls from this mask. The placeholder is assigned built-in Ignore Raycast layer to avoid default self-occlusion; replace with the team's Guard layer during integration.

Project collision matrix: bolts collide with solid level geometry and player hitboxes. Keep bolt collider non-trigger and Rigidbody non-kinematic, gravity off, ContinuousDynamic. Owner colliders are explicitly ignored when a bolt spawns; other guards can stop bolts. A bolt applies damage once to the first IRoyalGuardDamageable found on the hit collider's parents and is destroyed on any solid impact or after six seconds. Very thin/fast-moving collider behavior still needs game-specific verification.

## Cover and tactical positioning

Create empty objects behind solid pillars/walls and add RoyalGuardCoverPoint. HidePosition is the object's transform; assign a separate peek transform at a reachable side of the obstacle, with clear sight from +1.6m to the player's +1m aim point. Both positions must be reachable, not inside colliders. Keep peek within attack range. Set coverQuality in [0,1]; this is an authored prior, not an inferred physical property. Assign these components to Tactics.coverPoints on each guard. No scene-wide scan is performed. Shared points support one guard reservation each. Nondefensive cover requires a reachable firing opportunity; retreat can select a safe non-firing cover. Bad points are rejected for five seconds after movement failure. With no usable cover the guard can attack from a clear lane or sample a flank/retreat destination and safely stop on invalid paths.

## Weapon, health and presentation

Crossbow.muzzle and boltPrefab are wired in the prefab. Default 15 damage, 38 m/s, .75s shot interval, five rounds and two-second reload. These are tunable values; the base has no standard enemy for a measured rate comparison. Muzzle must be outside body/wall colliders and point along local +Z. ClearShot checks the actual muzzle-to-target segment immediately before firing. Cadence does not force a state exit between shots.

Guard health defaults to 180. The team's player weapon should call `hit.collider.GetComponentInParent<RoyalGuardHealth>()?.TakeDamage(damage)` or dispatch IRoyalGuardDamageable on parent MonoBehaviours. This explicit integration example is not injected into any team script. Health is initialized in Awake, so object pooling would require a separate reset API.

For player damage add RoyalGuardDamageAdapter to the player root or collider parent. Wire Receive Damage(float) in the Inspector to the team's real player-health public float method. With no listener it intentionally does nothing. Do not also attach RoyalGuardPlayerHealthDemo on that hierarchy: only one receiver should own damage. The demo is for isolated tests and does not alter the team's health system.

Replace only the `TemporaryVisual_ReplaceWithLicensedModel` and `Crossbow_PLACEHOLDER` children with a suitably licensed model. Preserve root components, collider coverage, Eyes and Muzzle references. Never enable root motion that fights NavMeshAgent without implementing movement synchronization. Add/assign Animator and route RoyalGuardPresentation events through your animation/audio component: hit, death, and stateChanged(int), where enum values are Patrol=0, Investigate=1, SeekCover=2, Reposition=3, Attack=4, Reload=5, Retreat=6. Health damage events support hit reactions; decision logic has no Animator dependency. No animation clips/audio are supplied. Dead guards stop moving and release cover, but their body remains; a presentation/death listener can disable hitboxes or remove the corpse.

## Diagnostics

Select the guard for range/FOV/memory Gizmos and cover objects for hide/peek Gizmos. The Brain custom Inspector shows CurrentAction, visibility, memory, cover score and navigation failure during Play Mode. CurrentAction and DebugStatus are also public runtime properties for an optional HUD. Inspector tuning lives in DefaultSettings, Perception, Tactics, Crossbow and Health. Duplicate DefaultSettings to tune guard variants independently. Use a debug Inspector or add a HUD that reads DebugStatus for screenshots.

