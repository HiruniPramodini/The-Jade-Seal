# Test plan

Automated entry point: `JadeSeal.RoyalGuard.Editor.RoyalGuardValidation.Run` (menu: Jade Seal > Royal Guard > Run deterministic checks). It creates/updates owned placeholder assets, checks deterministic policy outcomes, cover-score monotonicity and bounds, health/death invariants, and prefab component wiring. Run in a disposable checkout if you customized generated prefabs. It writes RoyalGuardValidation.txt at project root. Exact executed results and limitations belong in VALIDATION_REPORT.md. These checks are custom Unity editor assertions, not NUnit tests.

## Manual world/Play Mode matrix

Use a copied scene, exact editor version, Console clear and an assigned player/damage receiver. For each case record expected/observed, screenshot/video, settings and pass/fail. No manual row below is passed merely because it appears here.

| Scenario | Setup and expected observation |
|---|---|
| Patrol | Two reachable waypoints: cycle continuously; null point skipped; no points safely idle |
| Detection | Player beyond 28m or outside 57.5deg half-FOV: no acquisition. Enter both with clear line: acquire |
| LOS | Solid world-layer wall blocks detection. Trigger/actor layer does not count as world obstacle |
| Memory | Acquire then hide: Investigate saved position, not current hidden position; expiry at 6s returns Patrol |
| Cover score | Two reachable points: change path detour, quality, exposure and peek lane independently; confirm rank changes |
| Reservations | Two guards, one point: only one reserves it; disable/death releases it |
| Firing | Reach peek with clear muzzle, bolts travel and remove 15 HP once per impact; owner unharmed |
| Obstruction | Eyes can see but muzzle behind wall: no shot; Reposition selected |
| Cadence/reload | Five bolts at >=.75s intervals; no sixth until two-second reload; no per-shot state oscillation |
| Low health | Damage 180HP guard to <=50.4HP: Retreat immediately, behind cover if possible |
| Recovery | No sight and no damage for >=4s: +4 HP/s capped at 99HP; no recovery while seeing player |
| Missing cover | Empty array/all reserved: feasible attack or validated flank; retreat has validated fallback |
| Invalid paths | Off-mesh points, wrong area mask, disconnected island and carved blockage: old path stopped, no repeated agent errors |
| Stuck | Physical blockage with valid route: after ~3s failed progress reject cover for 5s and replan |
| Target removal | Destroy/disable assigned player: no further shot at destroyed object; memory expires; respawn needs reassignment |
| Death | Lethal hit: Died once, agent stopped, cover released, no further attacks/recovery |
| Integration | Wire real player health; team weapon damages guard; hit/death/state presentation events invoke configured listeners |
| Stability | Run several minutes with multiple guards and moving player; capture state durations and profile tactical evaluations |

Screenshots: select guard for FOV/range/memory Gizmos, select cover for hide/peek, enable Navigation overlay, show settings and clear Console. Video or sequential timestamps are required for transitions/cooldowns; a single screenshot is insufficient. Verify blue NavMesh and advancing agent positions, not just Play button state. Model/animation/audio quality and gameplay difficulty require human review.
