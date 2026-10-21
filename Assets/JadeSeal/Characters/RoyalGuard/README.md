# Royal Guard (Heavy Melee Defender) — The Jade Seal

**Assigned Module:** Hiruni  
**Role:** Heavy Melee Defender & Castle Gate Protector  
**3D Model:** [`RoyalGuard.fbx`](file:///d:/game/The-Jade-Seal/Assets/JadeSeal/Characters/RoyalGuard/RoyalGuard.fbx)  
**Blender Source:** [`RoyalGuard.blend`](file:///d:/game/The-Jade-Seal/Assets/JadeSeal/Art/Blender/RoyalGuard.blend)  
**Weapon:** Imperial Steel Sword with Ceremonial Gold Hilt (`RoyalGuard_Sword`)

---

## 1. Visual & Aesthetic Design
- **Armor:** Dark charcoal iron lamellar (`M_RoyalGuard_Armor.mat`).
- **Cloth:** Imperial deep crimson-red tunic (`M_RoyalGuard_Cloth.mat`).
- **Trim:** Restrained ceremonial gold borders (`M_RoyalGuard_Gold.mat`).
- **Skin:** Natural medium tan-brown skin tone (`M_RoyalGuard_Skin.mat`).
- **Silhouette:** Imposing heavy military appearance with distinctive shoulder pauldrons, waist guard, and boots.

---

## 2. Autonomous AI Behaviour
- **Patrol:** Disciplined traversal between mountain gate waypoints at 2.2 m/s.
- **Perception:** 110° vision cone, raycast line-of-sight checking against mountain geometry, acoustic disturbance detection.
- **Chase:** Aggressive sprint toward detected player at 4.8 m/s.
- **Melee Attack:** Two-handed overhead sword slash dealing **30 physical damage** (2.3m range, 1.8s cooldown).
- **Reactive Defensive Block:** When taking frontal damage or awaiting attack cooldown, enters a defensive parry stance that **mitigates 75% of incoming damage** (`blockMitigation = 0.75`), accompanied by a metallic parry sound and spark particle burst.
- **Terminal Death:** Immediately ceases navigation, disables colliders, plays collapse animation, and despawns smoothly.

---

## 3. Component Architecture
- [`RoyalGuardAgent.cs`](file:///d:/game/The-Jade-Seal/Assets/JadeSeal/Scripts/EnemyAI/RoyalGuard/RoyalGuardAgent.cs) — State machine controller.
- [`RoyalGuardHealth.cs`](file:///d:/game/The-Jade-Seal/Assets/JadeSeal/Scripts/EnemyAI/RoyalGuard/RoyalGuardHealth.cs) — Implements `JadeSeal.Combat.IDamageable` with 75% block mitigation.
- [`RoyalGuardVisualizer.cs`](file:///d:/game/The-Jade-Seal/Assets/JadeSeal/Scripts/EnemyAI/RoyalGuard/RoyalGuardVisualizer.cs) — Decoupled procedural bone IK gait, slash swing, and block pose presentation.
- [`EnemyPerception.cs`](file:///d:/game/The-Jade-Seal/Assets/JadeSeal/Scripts/EnemyAI/Common/EnemyPerception.cs) — Reusable vision cone & acoustic sensory component.
