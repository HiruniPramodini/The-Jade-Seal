# Final Boss AI — THE JADE SEAL / SE3062

Owned namespace: `JadeSeal.FinalBoss`. Owned directory: `Assets/RoyalGuardAI` (or `Assets/FinalBossAI`).

This module implements the **Final Boss AI** for *The Jade Seal*. It represents the fourth and final enemy type in the game, possessing significantly greater power, 1500 HP durability, magical arena perception, defensive guarding stance, and three distinct magical attacks.

---

## Quick Start

1. **Unity Version**: Unity **6000.3.25f1** (Unity 6.3 LTS), AI Navigation **2.0.16**.
2. **Prefab**: Drag `Prefabs/FinalBoss.prefab` into the boss arena on a baked NavMesh area.
3. **Boss Settings**: Tunable stats reside in `FinalBossSettings.asset` (Health, ranges, projectile speeds, damages, cooldowns, guard reduction).
4. **Player Binding**:
   - Assign the player's root transform to `FinalBossPerception.player` (or the boss will automatically find the object tagged `"Player"`).
   - Attach `FinalBossDamageAdapter` to the player root or damage receiver and wire its `onReceiveDamage(float)` UnityEvent to the player health system.
5. **Boss Health HUD**:
   - Attach `FinalBossHealthBar` to your canvas to display the boss name, health bar slider, and numeric HP readout.

---

## Boss Attacks Summary

1. **Basic Magic Bolt**: High-velocity energy projectile dealing 25 damage (2.2s cooldown).
2. **Giant Magical Fireball**: Heavy special spell dealing 75 direct damage and 45 splash damage across a 5m explosion radius (11s cooldown).
3. **Magical Stomp Shockwave**: Melee counter dealing 50 damage across an expanding 12m ground shockwave (13s cooldown). Can be dodged by jumping or escaping the radius.

---

## Defensive Guarding

- Boss enters guarding stance automatically when taking repeated burst hits, or via tactical timing.
- Incoming damage is reduced by **75%** while guarding.
- Activates a jade energy barrier visual during the 2.5s guard window.

---

## Documentation

- [`AI_ARCHITECTURE.md`](file:///c:/Users/USER/Desktop/chara/boss/RoyalGuardAI/AI_ARCHITECTURE.md): State machine design, attack formulas, and timing.
- [`SETUP_GUIDE.md`](file:///c:/Users/USER/Desktop/chara/boss/RoyalGuardAI/SETUP_GUIDE.md): Scene setup, Animator parameters, and integration with player systems.
- [`TEST_PLAN.md`](file:///c:/Users/USER/Desktop/chara/boss/RoyalGuardAI/TEST_PLAN.md): Manual and automated test cases.
- [`VALIDATION_REPORT.md`](file:///c:/Users/USER/Desktop/chara/boss/RoyalGuardAI/VALIDATION_REPORT.md): Verification results and test assertions.
- [`VIVA_GUIDE.md`](file:///c:/Users/USER/Desktop/chara/boss/RoyalGuardAI/VIVA_GUIDE.md): Viva presentation guide and defense script.
