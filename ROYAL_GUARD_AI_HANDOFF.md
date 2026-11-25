# Royal Guard AI

Scripts: `Assets/RoyalGuardAI/Scripts`, namespace `JadeSeal.RoyalGuard`. Targets Unity 6000.3.25f1 and the existing NavMesh. The AI is not yet placed or play-tested in the final world.

## Setup

1. Create a guard on the baked NavMesh. Add `RoyalGuardBrain`; Unity also adds `NavMeshAgent`, `RoyalGuardHealth` and `RoyalGuardCrossbow`. Match the existing navigation agent type and dimensions. Add suitable body colliders and assign a visible model separately.
2. Assign the player's root Transform, an eyes Transform and patrol waypoints to the brain. Player colliders must be on that root or its descendants. The sight mask must include both the player and solid obstacles. Keep eyes outside the guard's own colliders.
3. Create a bolt prefab with `RoyalGuardProjectile`, a non-trigger collider and a Rigidbody. Use a dynamic Rigidbody; the crossbow sets continuous collision detection, disables gravity and applies Unity 6 linear velocity. Assign the prefab and a muzzle outside the guard's body to the crossbow. Owner colliders are ignored when each bolt is initialized.
4. Add `RoyalGuardCoverPoint` objects at reachable standing positions behind solid obstacles and assign them to the brain. Give each guard sufficient space. Cover is reserved by one active guard and released on disable, death or a transition away from combat. Optional peek transforms are currently metadata; firing from a separate peek position is not implemented.
5. For a disposable test player, add `RoyalGuardPlayerHealthDemo`. For the actual player, implement `IRoyalGuardDamageable.TakeDamage(float)` on a component on the hit collider or a parent. Guard health also implements this interface. Team filtering is not implemented, so bolts can damage other guards.

## Behavior and validation

The brain scores patrol, chase, search, cover, firing, reload and retreat. Visible targets update a timed last-seen position; hidden targets are searched at that remembered position. Low health prioritizes reachable cover. Reloading and firing use the assigned crossbow; firing stays selected during the interval between shots.

In a separate test scene or unsaved Play Mode setup, verify:

- Patrol paths and target detection, including walls and field-of-view boundaries.
- Losing sight leads to the last-seen location, then patrol after memory expires.
- Five hits consume a magazine; reload completes and shooting resumes.
- Bolts ignore their shooter, hit walls and damage the demo target or guard health once.
- Two guards cannot reserve the same cover; disabling or killing its occupant releases it.
- Low-health retreat, unreachable cover, empty cover arrays and missing optional references produce no Console errors.

Current limitations: cover has no peek/return cycle, animation or squad coordination; player aim uses a fixed one-meter height offset; human combat tuning and world integration remain outstanding. Moving cover or world geometry requires revalidation of navigation and firing lines.

## Resumed work — 21 November 2026

Fixed guard damage-interface integration, hidden-target chase information, null cover arrays, inactive cover availability, reservation release on disable, utility-score selection, emergency retreat priority, aim stability during shot cooldown, waypoint index bounds and physical shooter collision filtering. Updated projectile velocity to the Unity 6 API.

Compilation was attempted but could not be completed: the sandbox denied access to the user NuGet configuration, and the requested elevated build was declined. No successful build or Play Mode result is claimed. The approved world scene, lighting and NavMesh were not edited. At that time, changes remained uncommitted.
