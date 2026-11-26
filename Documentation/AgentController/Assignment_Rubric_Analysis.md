# Royal Guard — SE3062 rubric analysis

| Criterion | Weight | Evidence and limits |
| --- | ---: | --- |
| Gameplay integration and stability | 20% | Serialized actor, Animator and scene references retained, with existing health and checkpoint lifecycle. Static source evidence supports the listed safeguards. Demonstrate the focused scenarios from README in Unity. |
| Continuous effort and code quality | 15% | Real runtime and setup code is exported with responsibilities and integrity hashes. Setup scripts and runtime code have distinct roles. This audit does not establish duration of effort or individual authorship. |
| Creative agent design and originality | 15% | A courtyard defender with authored local patrol routes and a crossbow. Barricades are tactical cover, selected relative to the player rather than one fixed hiding spot. Code behavior is distinguishable from the other three roles. Originality relative to external work is not assessed. |
| Implementation and complexity | 25% | ChooseCover scans a registry of active GuardCoverPoint components. TryPlan samples a hide point beyond the collider from the threat, requires that the same barricade occlude a crouched line, and tests up to six lateral peek candidates. The valid peek with the shortest squared hide-to-peek distance wins inside each plan. The outer cost is squared guard-to-hide distance plus 0.35 times squared hide-to-peek distance. Go and PathInsideLeash require a complete NavMesh route with corners bounded by the home leash. TryAimPosition checks an ordered fan around a proposed tactical offset. This is local candidate ranking and constraint filtering layered on Unity NavMesh. |
| Technical justification | 25% | Explain the observations, state gating, candidate constraints and failure cases in the VIVA. Cite RoyalGuardAI.SenseThreat, Patrol, ChooseCover, CoverSequence, AimOrAdvance, TryAimPosition, Go, PathInsideLeash, LateUpdate and GuardCoverPoint.TryPlan, Shelters, Reserve, Release. |

## Gaps

One guard reserves a barricade at a time. Reservation prevents double assignment, with no coordinated squad planning. Cover cost uses squared straight-line distances, while Unity supplies complete path validity, so it does not optimize travel time or moving exposure. The shared last-shot observation is a snapshot, not predictive trajectory tracking. Record a Unity demonstration with an unobstructed, obstructed and failure scenario. Add decision traces and comparative measurements before claiming an improvement. This is evidence and guidance, with no grade prediction.
