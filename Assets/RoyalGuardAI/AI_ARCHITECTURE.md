# Architecture and decision model

## Ownership and dependencies

Brain orchestrates components; Utility is a deterministic policy with no scene dependencies. Settings is a shared ScriptableObject. Perception owns visibility and last-known position/time. Navigation owns path validation, planning, arrival and stuck handling. Tactics owns cover evaluation/reservations. Crossbow owns ammunition/cadence/reload. Projectile owns collision delivery. Health owns durability, damage and death events. Presentation subscribes through optional UnityEvents; the AI does not depend on a model, animator or sound.

## Utility equations

All feasibility gates return zero if invalid. Let V = visible, M = unexpired memory, L = latched low health, C = feasible cover, H/P = at hide/peek, R = within attack range, F = clear muzzle line, A = needs/is reloading, q = cover score.

| State | Score when feasible |
|---|---|
| Patrol | .35 when neither V nor M |
| Investigate | .72 when M and not V |
| SeekCover | .66 + .24q when threat, C, not H/P and not L |
| Reposition | .82 when V, not A and (not R or not F or H) |
| Attack | .92 when V, R, F, not A and (no C or P) |
| Reload | .94 when A |
| Retreat | 1 when L and threat, including no-cover case |

Retreat and reload interrupt minimum dwell; death bypasses all decisions. Invalid current states can change immediately. A valid current state is retained for .6s, then requires challenger > current + .08 until the 8s safeguard releases the margin. Equal scores have deterministic enum-order tie resolution. Decisions run at .25s intervals, not every frame; weapon cadence is independent, so a shot cooldown does not oscillate Attack. Retreat is latched at health ratio <= .28 until recovery >= max(.33, recoveryTarget=.55). Recovery adds 4 HP/s only while not seeing the player and at least 4s since damage, capped at .55. It cannot resurrect or reduce existing health. With memory expired the agent can patrol and recover; it never pursues the hidden player's live transform.

## Cover formula

For each enabled, available point within radius D=20, require a sampled complete path for the same agent type/area mask. Three line probes from last observed player +1m toward hide heights .5/1.05/1.6m estimate exposure E (fraction unobstructed). Reject E>.67. For ordinary combat require reachable peek, clear peek-to-last-position line and range; retreat does not require a firing lane.

`q = [wd(1-clamp(d/D)) + wp(1-clamp(pathLength/(2D))) + wq Q + we(1-E) + wf F] / sum(weights)`

Defaults wd=1, wp=1.5, wq=2, we=3, wf=2. Q is designer-authored quality in [0,1], F is binary tactical firing opportunity. Zero total weight returns zero. The selected cover receives a .08 comparison bonus to reduce reselection; its geometric feasibility is still checked each time. Euclidean distance and path length intentionally measure different costs: a nearby point behind a long detour should score worse than a reachable nearby point. Reserved points cannot be selected by another guard; death/disable/destroy releases reservations. A destroyed occupant compares null under Unity object semantics.

## Navigation and edge cases

Unity NavMeshAgent uses the baked graph and its native pathfinding/local avoidance; this module does not claim to implement a custom A*. Complete paths are required; partial/invalid paths never become accepted destinations. Destination sampling respects agent type and area mask. Replanning runs at .8s cadence; displacement checkpoints detect lack of progress after 3s. Failure stops the old path, temporarily rejects selected cover and requests reselection. Missing NavMesh safely stops AI updates without calling invalid agent APIs. Search visits the last position and rotates until six-second memory expires. Fallback flank/retreat points are sampled/validated and can fail safely on isolated islands. Static reference lists avoid global searches and per-tick score dictionaries. Cover checks are at most once per second per guard. Dynamic obstacles require an appropriately configured carving NavMeshObstacle; this module does not add obstacles to the shared scene.

## Cost, limitations and alternatives

With N assigned points and average K path corners, tactical evaluation costs O(N * (path-query cost + K + raycasts)); path queries are not constant-time and depend on the baked graph. Each candidate uses at most two path queries and four raycasts. Perception is one raycast per decision; firing checks one segment before each attempt. Unity path.corners allocates an array per validation; bounded cover lists and 1Hz evaluation control cost, but a large crowd should use scheduling, nonalloc corner buffers and cached/shared path costs. Decision scoring is O(7), memory is O(N+K) including assigned references/path storage. Reservations are local ownership, not a squad planner.

This is Utility AI plus graph navigation, beyond uninformed search: context-specific normalized tactical evaluation selects actions and reachable cover. It is not machine learning and does not learn weights. Three exposure probes approximate protection; unobserved player movement is not predicted. No suppression, strafing animation, squad coordination, ballistic gravity, animation-synchronized firing, pooling or authored character art is included. No universal movement timeout can distinguish all slow paths from stuck agents: tune stuckSeconds for the scene. Retreat can stop when no reachable escape exists. Designer cover placement and collision masks are essential.

Alternatives: FSM is easier for fixed sequences but scales poorly with many tactical priorities; behavior trees make hierarchy explicit but still need tactical scoring; GOAP supports multistep plans at greater authoring/search cost; learned policies require training/evaluation and are harder to explain in a short VIVA. Utility action selection is a defensible compromise, not a guarantee of optimal combat.
