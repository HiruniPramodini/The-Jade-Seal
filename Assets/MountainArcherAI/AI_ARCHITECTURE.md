# Mountain Archer AI: Architecture & Decision Model
**Project:** *The Jade Seal* (Unity 6.3 LTS)  
**Component:** Autonomous Mountain Archer AI Module (`JadeSeal.MountainArcher`)

---

## 1. Ownership & Component Architecture

* **MountainArcherBrain:** Orchestrates perception, navigation, tactics, health, and bow combat.
* **MountainArcherUtility:** Pure, deterministic mathematical utility action selector with zero scene/animation coupling.
* **MountainArcherSettings:** ScriptableObject holding tuned balance parameters (perception ranges, mobility speeds, decision weights, retreat/recovery thresholds).
* **MountainArcherPerception:** Vision frustum ($115^\circ$ FOV, $28\text{m}$ detection range) and temporal memory ($6.0\text{s}$).
* **MountainArcherNavigation:** High-mobility NavMeshAgent handling complete path validation, $6.0\text{ m/s}$ sprint locomotion, $0.6\text{s}$ replanning cadence, and stuck resolution.
* **MountainArcherTactics:** Vantage point and high-ground cover evaluation ($q$-score equation).
* **MountainArcherBow:** Bow-and-arrow ballistic archery system firing at high frequency ($0.65\text{s}$ fire interval, $40\text{ m/s}$ arrow velocity, 8-arrow quiver).
* **MountainArcherArrow:** Ballistic projectile delivering kinetic damage upon collision with damageable targets.
* **MountainArcherHealth:** Durability manager ($120\text{ HP}$), damage reception, and health ratio calculation for tactical retreat and post-combat recovery.
* **MountainArcherPresentation:** Decoupled UnityEvents bridging AI states to 3D character animations, audio clips, and particle FX.

---

## 2. Utility Action Selection Equations

Decisions execute at $0.25\text{s}$ intervals. Let:
* $V$ = Target visible
* $M$ = Unexpired temporal memory ($6.0\text{s}$)
* $L$ = Low health ratio ($\le 0.25$)
* $C$ = Valid tactical cover/vantage candidate
* $H/P$ = Reached hide / peek point
* $R$ = Within maximum attack range ($24\text{m}$)
* $F$ = Clear projectile firing line
* $A$ = Needs quiver restock
* $q$ = Evaluated tactical cover score $[0, 1]$

| Action State | Score when Feasible | Tactical Behavior |
|---|---|---|
| **Patrol** | $0.35$ when neither $V$ nor $M$ | Loops designated patrol waypoints across mountain ridge. |
| **Investigate** | $0.72$ when $M$ and not $V$ | Advances toward last-seen player position upon losing line of sight. |
| **SeekCover** | $0.66 + 0.24q$ when threat, $C$, not $H/P$, not $L$ | Moves toward elevated high-ground cover position. |
| **Reposition** | $0.85$ when $V$, not $A$, and (not $R$ or not $F$ or $H$) | Sprints along high terrain at $6.0\text{ m/s}$ to establish optimal archery angle. |
| **Attack** | $0.94$ when $V, R, F$, not $A$, and (no $C$ or $P$) | Draws bow and releases arrows at player. |
| **Reload (Restock)** | $0.95$ when $A$ | Quiver restock during cover or distance keeping. |
| **Retreat** | $1.00$ when $L$ and threat | High-speed sprint away from threat to recover HP. |

---

## 3. Balance & Stat Validation Matrix

| Metric | Bamboo Hunter | Mountain Archer | Royal Guard (Ref) | Validation Status |
|---|---|---|---|---|
| **Max Health** | $95\text{ HP}$ | **$120\text{ HP}$** | $180\text{ HP}$ | ✅ **PASSED** ($95 < 120 < 180$) |
| **Sprint Mobility** | $5.2\text{ m/s}$ | **$6.0\text{ m/s}$** | $3.5\text{ m/s}$ | ✅ **PASSED** (Faster than both) |
| **Attack Firing Rate** | $2.0\text{s}$ cooldown | **$0.65\text{s}-1.4\text{s}$ cooldown** | $2.4\text{s}$ cooldown | ✅ **PASSED** (Significantly faster firing frequency) |
| **Weapon System** | Bow | **Rapid Longbow & Arrows** | Crossbow & Katana | ✅ **PASSED** (Pure bow-and-arrow skirmisher) |
