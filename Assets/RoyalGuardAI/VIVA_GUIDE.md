# VIVA guide — Royal Guard

1. Explain the problem: a durable ranged enemy must choose cover, fire, reload, investigate uncertainty and retreat rather than simply chase the player. Team ownership is Assets/RoyalGuardAI and JadeSeal.RoyalGuard; other agents are independent.
2. Show Utility.Score/Choose. Explain normalized scores, feasibility gates, .08 hysteresis, .6s dwell, eight-second margin safeguard and emergency transitions. Explain why weapon cooldown is not Attack infeasibility.
3. Work a cover example: compare the same quality at distances 2m and 12m, path lengths 3m and 30m, full/partial exposure and a blocked peek. Calculate each normalized term and show why the detour/exposure changes the choice. Show reservation when two guards request one point.
4. Trace sensing: distance, FOV, world-only linecast, last-known position/time. After the player disappears the guard investigates saved information rather than tracking the live hidden transform. Show Gizmos and expiry.
5. Explain NavMesh as a graph abstraction with native pathfinding/local avoidance. Show complete-path validation, area mask and agent-type checks; distinguish global path planning from local avoidance and carving/rebake requirements.
6. Trace magazine ammo and physics collision: .75s cadence, five bolts, two-second reload, owner collision exclusion, exactly one damage receiver, lifetime cleanup. Demonstrate the adapter's Inspector connection to actual team health.
7. Apply enough damage to reach <=28%; explain retreat priority, cover-first/fallback movement, delayed recovery to 55%, and death's terminal behavior.
8. Show the supplied-package baseline commit and genuine improvement commits. Discuss executed validation separately from planned manual scenarios. Never claim game-world balancing or visual approval from headless tests.
9. Be candid: authored cover priors, approximate exposure, manually assigned player/cover, no ML, no squad planner, primitive visuals, no verified comparison to absent teammate enemy stats. Identify scaling improvements and how you would tune weights with recorded playtest outcomes.

Suggested evidence: Inspector with settings and player reference; guard and blue NavMesh overlay; FOV boundary and blocked view; two cover alternatives with path detour; guard at peek firing; reload ammo transition; low-health retreat; lost-target investigation; terminal death; Console with actual validation marker. Record editor version and scene name for every capture. Human screenshots must show successive moments or a video when claiming behavior over time.
