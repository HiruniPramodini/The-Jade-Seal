# Integration & Setup Guide — Final Boss AI

## 1. Project Requirements & Versions
* **Engine**: Unity **6000.3.25f1** (Unity 6.3 LTS).
* **AI Navigation**: Unity `com.unity.ai.navigation` 2.0.16.
* **Dependencies**: Built-in Physics 3D, UI (`UnityEngine.UI`). No external asset store packages required.

---

## 2. Prefab Placement & Arena Setup

1. Open your boss arena scene (e.g. `JadeSealFantasy.unity` or a dedicated boss test scene).
2. Ensure the arena floor has a baked NavMesh with Agent Type 0 (Humanoid).
3. Drag `Prefabs/FinalBoss.prefab` onto the arena floor.
4. Set root transform scale to `(1, 1, 1)` and rotation facing the player entrance.
5. In `FinalBossPerception`:
   * Assign `Player`: your player GameObject transform (or ensure the player GameObject is tagged `"Player"`).
   * Assign `ArenaCenter`: an empty transform marking the center of the boss arena.
   * `EncounterActive`: Set to `true` (or toggle via an arena trigger volume when the player enters the boss room).

---

## 3. Player Health & Damage Hookup

The Final Boss does not hardcode any dependencies on the player's scripts. Integration takes only 30 seconds:

1. Select your Player GameObject.
2. Add the `FinalBossDamageAdapter` component to the player root or hit collider parent.
3. In the Inspector for `FinalBossDamageAdapter`:
   * Click **+** on `On Receive Damage(float)`.
   * Drag your player's health script into the target field.
   * Select the method that accepts a `float` damage (e.g. `PlayerHealth.TakeDamage`).
4. Any attack from the boss (Basic Bolt, Fireball direct/splash, or Stomp Shockwave) will invoke this method!

---

## 4. Damaging the Boss

To allow the player's magical attacks or weapons to damage the boss:
* When a player spell or weapon hits the boss collider, call:
  ```csharp
  var bossHealth = hitCollider.GetComponentInParent<FinalBossHealth>();
  if (bossHealth != null)
  {
      bossHealth.TakeDamage(playerAttackDamage);
  }
  ```
* Alternatively, if your project uses `IFinalBossDamageable` or legacy `IRoyalGuardDamageable`:
  ```csharp
  var damageable = hitCollider.GetComponentInParent<IFinalBossDamageable>();
  damageable?.TakeDamage(playerAttackDamage);
  ```
* Both interfaces are supported natively by `FinalBossHealth`.

---

## 5. Boss Health Bar UI Integration

1. Drag `FinalBossHealthBar` into your game's Canvas hierarchy.
2. In the Inspector:
   * `Boss Name Text`: Assign a UI Text element displaying the boss title.
   * `Health Slider` or `Health Fill Image`: Assign your slider or filled Image.
   * `Health Value Text`: Assign a UI Text element for numbers (e.g. "1500 / 1500").
3. When the boss spawns or enters combat, the HUD connects and automatically displays health changes, smooth damage transitions, and defeat notifications.

---

## 6. Animator Integration & Parameters

If you are binding a custom 3D model and Animator Controller to `FinalBossPresentation`:

| Parameter Name | Type | Purpose |
|---|---|---|
| `Speed` | Float | Movement blend tree velocity ($0.0$ to $3.2$) |
| `IsGuarding` | Bool | Toggled `true` while in defensive guarding stance |
| `IsDead` | Bool | Toggled `true` upon defeat |
| `CastBasic` | Trigger | Triggered when initiating Attack 1 (Basic Magic Bolt) |
| `CastFireball` | Trigger | Triggered when initiating Attack 2 (Giant Fireball) |
| `Stomp` | Trigger | Triggered when initiating Attack 3 (Melee Ground Stomp) |
| `Hit` | Trigger | Triggered on heavy damage flinch |

### Optional Animation Events (on clip timelines):
- `AnimEvent_ShootBasic()`: Keyframe where projectile spawns from hand.
- `AnimEvent_LaunchFireball()`: Keyframe where giant fireball releases.
- `AnimEvent_StompImpact()`: Exact frame foot contacts ground to expand shockwave.
- `AnimEvent_AttackComplete()`: Return to movement/tracking.

> [!NOTE]
> If animation clips or events are absent, `FinalBossBrain` contains automatic timeout fail-safes. The boss will never freeze or stall if an animation event is missing!

---

## 7. Diagnostics & Play Mode Testing

Select the boss during Play Mode to inspect:
- The custom `FinalBossBrainInspector` showing:
  - Real-time Boss State (`TrackingPlayer`, `BasicMagicAttack`, `Guarding`, etc.)
  - Distance to player
  - Live HP readouts
  - Attack cooldown gauges
- Quick manual test buttons:
  - **Fire Basic Magic**
  - **Launch Fireball**
  - **Trigger Stomp**
  - **Toggle Guard**
  - **Inflict 150 Damage**
