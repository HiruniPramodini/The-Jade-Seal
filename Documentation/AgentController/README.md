# Royal Guard

A courtyard defender with authored local patrol routes and a crossbow. Barricades are tactical cover, selected relative to the player rather than one fixed hiding spot.

Main controller is `RoyalGuardAI` in `Assets/Game/Enemies/RoyalGuard/Scripts/RoyalGuardAI.cs`. The saved map is `Assets/JadeSeal/Scenes/JadeSealFantasy.unity`. Find the instances and local overrides in Scene_References.md.

## Operation

SenseThreat combines range, field of view and collision visibility with a six-second target memory, recent HP loss and a projected line from the player’s last magic shot. That projection flags a threat near the guard, even when a shot has yet to deal damage. Target visibility uses Clear; LateUpdate rechecks range and muzzle visibility before spawning the bolt. SeekCoverOnDetection is disabled in the source prefab, so merely seeing the player does not request cover by default.

Patrol alternates waypoint goals with turn and walk states. WeaponRaise leads into aiming or a cover request. CoverSequence manages movement to hide, the take-cover animation, cover idle, and emergence toward the peek position. Fire is animation-timed and uses a cooldown. Losing alert eventually lowers the weapon and resumes patrol. Health loss can request cover and trigger hit reaction. Death stops navigation and releases the reservation.

## Navigation

ChooseCover scans a registry of active GuardCoverPoint components. TryPlan samples a hide point beyond the collider from the threat, requires that the same barricade occlude a crouched line, and tests up to six lateral peek candidates. The valid peek with the shortest squared hide-to-peek distance wins inside each plan. The outer cost is squared guard-to-hide distance plus 0.35 times squared hide-to-peek distance. Go and PathInsideLeash require a complete NavMesh route with corners bounded by the home leash. TryAimPosition checks an ordered fan around a proposed tactical offset. This is local candidate ranking and constraint filtering layered on Unity NavMesh.

## Configure in the original Unity project

Open the gameplay scene, select an instance from Scene_References.md, and check its controller, health, Animator and references. Compare prefab defaults with instance overrides before tuning. Resolve projectile, floor, weapon-origin, actor and scene references through the dependencies manifest. Agent routes and arena features rely on the original map.

## How to test

Approach front versus rear, break line of sight, then cast near two guards and inspect different reservations. Confirm cover entry, peek clearance, actual bolt release and return to the independent patrol loops. Start from a fresh run or load a checkpoint before the courtyard is cleared.

## Limitations

One guard reserves a barricade at a time. Reservation prevents double assignment, with no coordinated squad planning. Cover cost uses squared straight-line distances, while Unity supplies complete path validity, so it does not optimize travel time or moving exposure. The shared last-shot observation is a snapshot, not predictive trajectory tracking.

## Primary sources

- `Assets/Game/Enemies/RoyalGuard/Editor/RoyalGuardSetupEditor.cs` — Original authoring/setup tool. Review in project context before invoking.
- `Assets/Game/Enemies/RoyalGuard/Scripts/GuardCoverPoint.cs` — Cover geometry, line tests, hide and peek candidates, occupancy.
- `Assets/Game/Enemies/RoyalGuard/Scripts/RoyalGuardAI.cs` — Royal patrol, perception, memory, cover and firing decisions.
