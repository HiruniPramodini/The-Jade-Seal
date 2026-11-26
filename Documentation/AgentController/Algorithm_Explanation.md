# Royal Guard — search and pathfinding

ChooseCover scans a registry of active GuardCoverPoint components. TryPlan samples a hide point beyond the collider from the threat, requires that the same barricade occlude a crouched line, and tests up to six lateral peek candidates. The valid peek with the shortest squared hide-to-peek distance wins inside each plan. The outer cost is squared guard-to-hide distance plus 0.35 times squared hide-to-peek distance. Go and PathInsideLeash require a complete NavMesh route with corners bounded by the home leash. TryAimPosition checks an ordered fan around a proposed tactical offset. This is local candidate ranking and constraint filtering layered on Unity NavMesh.

## What the project implements

RoyalGuardAI.SenseThreat, Patrol, ChooseCover, CoverSequence, AimOrAdvance, TryAimPosition, Go, PathInsideLeash, LateUpdate and GuardCoverPoint.TryPlan, Shelters, Reserve, Release.

## Framework boundary

Unity supplies the collision queries and, for mobile agents, NavMesh sampling and route calculation. No student-authored A*, Dijkstra, Greedy Best-First Search, Behavior Tree or GOAP implementation was found in the four agent execution paths. A ranked or first-feasible candidate search is distinct from graph search. The boss does not route across the NavMesh.

## Limitations and possible experiments

One guard reserves a barricade at a time. Reservation prevents double assignment, with no coordinated squad planning. Cover cost uses squared straight-line distances, while Unity supplies complete path validity, so it does not optimize travel time or moving exposure. The shared last-shot observation is a snapshot, not predictive trajectory tracking.

Compare current candidates against a logged baseline. Measure candidates inspected, failed navigation/visibility tests and action latency before changing the planner. Any future planner must be identified as future work.
