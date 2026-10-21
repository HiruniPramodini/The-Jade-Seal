# Test Plan — Final Boss AI

## 1. Automated Test Entry Points

### 1. Deterministic Assertion Suite
* **Menu**: `Jade Seal > Final Boss > Run deterministic checks`
* **Method**: `JadeSeal.FinalBoss.Editor.FinalBossValidation.Run`
* **Validates**:
  * Health clamping, zero-damage rejection, and negative-damage exclusion.
  * Substantial health pool invariant ($1500\text{ HP}$).
  * Guarding damage reduction math ($75\%$ mitigation).
  * Strict single-invocation of `Died` event and prevention of healing after death.
  * Attack cooldown enforcement for all three magical attacks.
  * Prefab wiring and required components.
* **Output**: `FinalBossValidation.txt`

### 2. Real-Frame Play Mode Test Harness
* **Method**: `JadeSeal.FinalBoss.Editor.FinalBossPlayValidation.Run`
* **Validates** (in an automated unsaved runtime test arena):
  * Runtime NavMesh build and NavMeshAgent binding.
  * Continuous arena awareness and player tracking behind obstacles.
  * Basic Magic Bolt physics flight and player damage delivery.
  * Giant Fireball direct/splash damage delivery and cooldown locking.
  * Stomp Shockwave radial ground expansion and single-hit enforcement.
  * Guard state entry, damage reduction verification in play mode, and clean exit.
  * Terminal death halting all combat updates.
* **Output**: `FinalBossPlayValidation.txt`

---

## 2. Manual Playtest Verification Matrix

| Test Case | Procedure | Expected Outcome |
|---|---|---|
| **Arena Tracking** | Move player anywhere in arena, including behind pillars. | Boss rotates and tracks player smoothly without requiring line of sight. |
| **Basic Magic Attack** | Stand at medium range ($10\text{m} - 25\text{m}$). | Boss casts and fires fast magic bolts every $2.2\text{s}$. Can be dodged by strafing. |
| **Giant Fireball** | Wait for $11\text{s}$ cooldown at range. | Boss raises hands and launches giant fireball; creates explosive blast on impact. |
| **Melee Stomp** | Run within $6\text{m}$ of boss. | Boss raises foot and stomps ground; green energy shockwave expands $12\text{m}$. |
| **Shockwave Jump Dodge**| Jump over the expanding shockwave. | Vertical clearance exempts player from damage; no damage registered. |
| **Shockwave Single Hit** | Stand inside the expanding shockwave ring. | Player takes damage exactly once; no multi-tick repeated hits. |
| **Boss Guarding** | Attack boss with rapid attacks ($3$ hits within $3\text{s}$). | Boss enters guard stance with jade barrier visual; damage reduced by $75\%$. |
| **Guard Timeout** | Wait $2.5\text{s}$ after guard activation. | Barrier deactivates and boss resumes offensive tracking. |
| **Boss Health UI** | Inflict damage on boss. | Boss health bar smoothly updates remaining HP and displays boss title. |
| **Boss Defeat** | Reduce boss health to $0$. | Boss triggers death animation, health bar displays "DEFEATED", all combat stops. |
