# Final Boss AI Architecture — The Jade Seal

## 1. System Overview & Component Ownership

The **Final Boss** is the fourth and climactic encounter in *The Jade Seal*. The boss is designed as a powerful, imposing magical warlord with high durability (1500 HP vs normal enemy 180 HP), omniscient arena awareness, defensive guarding capability, and three distinct magical attacks (Basic Magic Bolt, Giant Fireball with AoE splash, and Ground Stomp Shockwave).

Component responsibilities:
- **`FinalBossBrain`**: Master state coordinator. Evaluates tactical conditions, enforces attack cooldowns, transitions between states, and drives behaviors.
- **`FinalBossPerception`**: Magical arena awareness. Provides continuous 360-degree awareness of player location in the arena, regardless of line of sight or physical cover.
- **`FinalBossCombat`**: Manages casting sockets, attack timers, projectile launches, and AoE shockwave instantiation.
- **`FinalBossGuard`**: Controls defensive stance, rapid-hit accumulation detection, damage mitigation (75% reduction), and defensive barrier visuals.
- **`FinalBossHealth`**: Manages 1500 HP durability, applies guarding mitigation, triggers events (`Damaged`, `Guarded`, `Died`, `HealthChanged`), and enforces terminal death invariants.
- **`FinalBossNavigation`**: Directs `NavMeshAgent` movement, maintains optimal combat spacing (8m–16m), prevents getting stuck, and provides smooth facing orientation.
- **`FinalBossPresentation`**: Bridges the state machine with Unity's Animator controller and Animation Events with safe timeout fallbacks.
- **`FinalBossDamageAdapter`**: Decoupled interface on player hierarchy routing boss damage into team player health via Inspector `UnityEvent<float>`.
- **`FinalBossHealthBar`**: Dedicated Boss UI HUD showing boss title, smooth health slider, and numeric health readouts.

---

## 2. State Machine & Decision Pipeline

The Boss State Machine supports 9 coordinated states:

```
                            ┌────────────────────────┐
                            │    IdleCombatReady     │
                            └───────────┬────────────┘
                                        │ Player detected in arena
                                        ▼
                            ┌────────────────────────┐
                 ┌─────────►│     TrackingPlayer     │◄─────────┐
                 │          └───────────┬────────────┘          │
                 │                      │                       │
      Distance <= meleeRange            │ Distance <= 30m       │
      & Stomp ready                     │ & Basic ready         │
                 ▼                      ▼                       │
        ┌────────────────┐     ┌────────────────┐               │
        │  StompWindup   │     │BasicMagicAttack│───────────────┤
        └───────┬────────┘     └────────────────┘               │
                │ Impact timing                                 │
                ▼                                               │
        ┌────────────────┐     ┌────────────────┐               │
        │  StompImpact   │     │SpecialFireball ├───────────────┤
        └───────┬────────┘     └────────────────┘               │
                │                                               │
                └───────────────────────────────────────────────┘
                                        ▲
                 Rapid hits / reaction  │ Guard duration ends
                 ┌──────────────────────┴─────────┐
                 │            Guarding            │
                 └────────────────────────────────┘
                                        │
                 Fatal blow (HP <= 0)   │
                                        ▼
                                ┌───────────────┐
                                │     Death     │
                                └───────────────┘
```

### State Priorities & Conditions:
1. **Death**: Bypasses all other states immediately when health reaches zero. Halts navigation, disables attack loops, cancels guard, and triggers death animation.
2. **Guarding**: Triggered upon taking rapid accumulated hits (e.g. 3 hits within 3s) or via tactical defense check. Reduces all incoming damage by 75%. Lasts 2.5s before returning to tracking.
3. **HitReaction**: Flinch reaction triggered if burst damage exceeds threshold (`180f`) outside of heavy attacks.
4. **StompWindup -> StompImpact**: Triggered when the player closes in to melee distance ($\le 6\text{m}$) and stomp cooldown is elapsed. Deals 50 AoE damage across a 12m ground shockwave.
5. **SpecialFireballAttack**: Periodic high-impact attack (cooldown 11s) dealing 75 direct damage and 45 splash damage (5m radius).
6. **BasicMagicAttack**: Standard ranged attack (cooldown 2.2s) dealing 25 damage with high velocity (35 m/s).
7. **TrackingPlayer**: Continuously rotates towards the player, evaluates attack distances, and repositions within the 8m–16m combat envelope.
8. **IdleCombatReady**: Default safe idle when player is outside the designated arena.

---

## 3. The Three Distinct Magical Attacks

| Attack | Name | Damage | Range / Radius | Cooldown | Dodge Mechanism |
|---|---|---|---|---|---|
| **Attack 1** | **Basic Magic Bolt** | 25 HP | 30m range, 35 m/s velocity | 2.2s | Sidestepping / sprinting |
| **Attack 2** | **Giant Magical Fireball** | 75 Direct / 45 Splash | 5m explosion radius, 22 m/s | 11.0s | Rolling / distance away from blast center |
| **Attack 3** | **Magical Stomp Shockwave** | 50 HP | 12m ground radius, 15 m/s speed | 13.0s | Jumping over shockwave or escaping 12m radius |

### Shockwave Single-Hit Guarantee
`BossShockwave` maintains an internal `HashSet<GameObject> alreadyDamaged` to track affected root hierarchies during wave expansion. This strictly guarantees that multi-collider hierarchies or prolonged exposure inside the expanding ring apply damage **exactly once** per stomp. Targets whose collider bottom is elevated above `maxWaveHeight` (1.8m) are classified as jumping and successfully dodge the attack.

---

## 4. Defense and Guarding Formula

When guarding is active:
$$\text{Damage}_{\text{actual}} = \text{Damage}_{\text{incoming}} \times (1 - \text{guardDamageReduction})$$
* Default reduction: $75\%$ ($0.75$).
* Example: A 100 damage player spell inflicts only 25 damage.
* Visual feedback: Spawns/enables the translucent jade barrier shield.
* Prevents invulnerability exploit: Guarding duration is strictly time-bounded (2.5s) with an 8s cooldown.

---

## 5. Performance and Complexity

* **Perception**: $O(1)$ sphere distance check with target transform reference. No scene-wide raycast spam.
* **Decision Pipeline**: $O(1)$ state machine evaluation.
* **Combat & Projectiles**: Rigidbody-driven with ContinuousDynamic collision. Clean lifetime timeouts prevent memory leaks.
* **Shockwave Detection**: Radial band overlap query with hash set deduplication.
