# The Jade Seal — Autonomous Bamboo Hunter AI
**Module:** SE3062 Intelligent Systems (Student 2: Autonomous Bamboo Hunter AI)  
**Workspace:** `C:\Users\hp\Desktop\The-Jade-Seal-BambooHunter-IS`  
**Branch:** `feature/bamboo-hunter-ai`  
**Author:** kesh24610784 (`keshikapathirana78@gmail.com`)

---

## 1. Executive Architecture Overview

The Bamboo Hunter is an autonomous ranged enemy designed for the Bamboo Forest biome in *The Jade Seal*. It combines a **Hybrid Utility AI + Finite State Machine (FSM)** brain to produce responsive, lifelike tactical combat behavior:
- **Continuous Perception Sampling:** Evaluates target visibility, distance, field-of-view, and physical sightline/line-of-fire occlusion via raycasts.
- **Utility AI Scoring:** Computes continuous normalized utility scores across candidate states using mathematical response curves (logistic/sigmoid, exponential penalties, and distance falloff).
- **FSM Arbitration & Execution:** Evaluates state transitions through hysteresis damping, deterministic tie-breaking, and critical invariant overrides (e.g. Death, Low Health Retreat, Out of Ammo).
- **Tactical Movement & Archery Locomotion:** Coordinates NavMesh pathing with minimum safe standoff enforcement (7.0m) so archers avoid walking into point-blank player touch range.
- **Vegetation Cover System:** Evaluates authored cover clusters with multi-criteria tactical scoring (threat occlusion, firing line opportunity from peek offset, path length, and anti-repetition cooldown).
- **Ballistic Bow & Arrow Combat:** Manages bow draw progress, aiming readiness, quiver ammunition (6 arrows), shot cooldowns, physical projectile trajectory simulation, and atomic damage reception.

---

## 2. Core Subsystems

### A. Sensory Perception (`BambooHunterPerception.cs`)
- Horizontal field-of-view angle checks with personal space proximity awareness (targets <= 1.0m detected regardless of facing; targets <= 2.0m in forward hemisphere).
- Target memory persistence with configurable decay timeouts (`targetMemoryDuration = 6.0s`).
- Raycast line-of-sight and line-of-fire obstacle checking with hierarchy recognition (`IsPartOfHierarchy`) and closest-hit distance sorting to prevent false occlusion from target colliders or internal enemy colliders.

### B. Navigation & Locomotion (`BambooHunterNavigation.cs`)
- Controls locomotion via `NavMeshAgent` with strict production readiness checks (`IsReady => agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh`).
- Deterministic investigation standoff calculation (`TryCalculateStandoffDestination`) that clamps destinations to a configurable standoff range (`preferredMinRange = 7.0m`).
- Immediate surveillance halt when already within standoff range.
- Path completeness validation (`NavMeshPathStatus.PathComplete`) and anti-stuck displacement monitoring.
- Safe offline/Edit Mode operation: all native agent methods (`Stop`, `ResetPath`, `SetDestination`, `CalculatePath`) are strictly guarded against unbound agents to eliminate native engine errors.

### C. Tactical Cover System (`BambooHunterCoverSystem.cs`, `BambooHunterCoverPoint.cs`)
- Evaluates candidate cover points using multi-criteria tactical utility scoring:
  1. Threat sightline occlusion (multi-probe raycasts to hide position).
  2. Firing line opportunity from peek anchor offset.
  3. NavMesh path length and proximity.
  4. Flanking angle relative to threat forward vector.
  5. Anti-repetition cooldown penalty to discourage camping.

### D. Ballistic Bow Combat (`BambooHunterBow.cs`, `BambooHunterArrow.cs`)
- Configurable draw duration (`drawDuration = 1.0s`), projectile launch velocity (`arrowSpeed = 32 m/s`), and trajectory drop (`gravityMultiplier = 0.55`).
- Trajectory clearance checking via straight and multi-sample parabolic arc raycasts.
- Physical arrow projectile with kinematic collision detection, single-damage delivery atomicity, and arrow mesh embedding upon impact.

### E. Health & Damage Reception (`BambooHunterHealth.cs`, `BambooHunterDamageAdapter.cs`)
- Implements `IBambooHunterDamageable` interface, supporting atomic damage application, normalized health ratios, invulnerability frames, and death events.
- Integrates seamlessly with existing teammate damage contracts via reflection-safe adapter.

### F. Hybrid Brain & Utility Arbitration (`BambooHunterBrain.cs`, `BambooHunterUtility.cs`, `BambooHunterState.cs`)
- Evaluates 6 core states: `Patrol`, `Investigate`, `Aim`, `Shoot`, `Reposition`, `Retreat`, and `Dead`.
- State arbitration features momentum hysteresis (`hysteresisBonus = 0.08`), deterministic tie-breaking, and rate-limited decision cycles (`decisionInterval = 0.2s`).

---

## 3. Demo Scene & Runtime Verification Instructions

### Demo Scene Details:
- **Scene Path:** `Assets/BambooHunterAI/Scenes/BambooHunterDemoScene.unity`
- **Enemy Prefab:** `Assets/BambooHunterAI/Prefabs/BambooHunterEnemy.prefab`
- **Arrow Prefab:** `Assets/BambooHunterAI/Prefabs/BambooHunterArrow.prefab`
- **Settings Asset:** `Assets/BambooHunterAI/Settings/BambooHunterSettings_Default.asset`

### How to Run Validation:
1. Open the project in Unity Editor.
2. Select **Jade Seal -> Bamboo Hunter -> Run Deterministic Validation** from the top menu bar.
3. Review results in the Unity Console or inspect `Assets/BambooHunterAI/Evidence/Deterministic.txt`.

### How to Play the Interactive Demo:
1. Open `Assets/BambooHunterAI/Scenes/BambooHunterDemoScene.unity`.
2. Press **Play** in the Unity Editor.
3. View real-time tactical telemetry in the Game View HUD (Agent State, Intent, Health, Arrows, Visibility, Target Distance, Cover Status).
4. Use the on-screen buttons to trigger test damage, restock arrows, or reset the player dummy.

---

## 4. Verified Results & Test Evidence

### Deterministic Validation Suite:
- **Total Assertions:** **305 / 305 PASSED** (0 failures, 0 errors, 0 warnings).
- **Log Location:** `Assets/BambooHunterAI/Evidence/Deterministic.txt`
- **Coverage Areas:**
  - Part 1: Core utility scoring curves and arbitration (Tests 1-12)
  - Part 2: State overrides and boundary conditions (Tests 13-25)
  - Part 3: Navigation contracts, arrival tolerance, and stuck monitoring (Tests 26-36)
  - Part 4: Sensory perception, FOV, and target memory (Tests 37-50)
  - Part 5: Tactical cover discovery and exposure probing (Tests 51-58)
  - Part 6: Bow mechanics, quiver ammo, and trajectory clearance (Tests 59-66)
  - Part 7: Health, damage reception, and death transitions (Tests 67-70)
  - Part 8: Production assets, prefabs, and demo scene integrity (Tests 71-76)
  - Part 9: Arrow physics simulation and single-damage delivery (Test 77)
  - Part 10: Regression coverage for close-range visibility, first-shot sequence, and standoff investigation (Tests 78-85)

### Observed Real Play Mode Verification:
- **Locomotion:** Enemy smoothly navigates along waypoints and patrols the forest ground.
- **Ranged Combat:** Enemy detects the player dummy, aims, draws the bow to 100%, and releases an arrow.
- **Ammunition:** Quiver decrements accurately from 6 to 5 arrows upon release.
- **Damage Delivery:** Arrow physical trajectory lands on the player dummy; dummy health decreases from 100 HP to 46 HP.
- **Standoff Distance:** Enemy preserves safe ranged combat distance (>= 7.0m) without colliding with the player dummy.
- **Cover Flanking:** Enemy evaluates vegetation clusters and repositions when line-of-fire is obstructed.

---

## 5. Remaining Runtime Verification Gaps

While all unit contracts and deterministic integration tests pass 100% and core interactive combat has been verified in Play Mode, the following production runtime aspects remain for full-game integration:
1. **Full-Game Player Controller Integration:** Play Mode testing was conducted against the validated `PlayerTestDummy` capsule. Integration with the final human-controlled third-person character controller and combat animators should be tested in the shared game world.
2. **Teammate Multi-Agent Encounters:** Concurrent interactions between multiple Bamboo Hunters and teammate AI units (Student 1, Student 3, Student 4) in high-density combat zones.
3. **Complex Multi-Tier Geometry:** The demo scene uses a 60m x 60m baked terrain ground. Testing on steep hills, off-mesh links, bridges, or vertical structures in the final open-world environment should be verified.
4. **Extended Soak Testing:** Multi-hour continuous runtime profiling for memory leaks or prolonged path replanning performance under extreme physics load.
