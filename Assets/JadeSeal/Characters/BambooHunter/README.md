# Bamboo Hunter (Stealthy Ranged Hunter) — The Jade Seal

**Assigned Module:** Keshika  
**Role:** Stealthy Ranged Hunter & Bamboo Grove Stalker  
**3D Model:** [`BambooHunter.fbx`](file:///d:/game/The-Jade-Seal/Assets/JadeSeal/Characters/BambooHunter/BambooHunter.fbx)  
**Blender Source:** [`BambooHunter.blend`](file:///d:/game/The-Jade-Seal/Assets/JadeSeal/Art/Blender/BambooHunter.blend)  
**Weapon:** Bamboo Recurve Hunting Bow & Quiver (`BambooHunter_Bow`)

---

## 1. Visual & Aesthetic Design
- **Cloth:** Forest-green hood and dark olive hunting garments (`M_BambooHunter_Cloth.mat`).
- **Leather:** Hardened brown leather armor and wrist guards (`M_BambooHunter_Leather.mat`).
- **Skin:** Natural medium-brown sun-tanned skin tone (`M_BambooHunter_Skin.mat`).
- **Bow:** Curvature-carved bamboo wood grain (`M_BambooHunter_Bow.mat`).
- **Silhouette:** Lightweight, agile stalker silhouette blending into dense bamboo forests.

---

## 2. Autonomous AI Behaviour
- **Patrol:** Fluid patrol between bamboo grove waypoints at 2.4 m/s.
- **Acoustic Footstep Investigation:** Tracks running footsteps within 10m even around corners and stalks toward disturbance points.
- **Ranged Archery Combat:** Maintains an optimal 16m standoff distance. Draws bow and launches ballistic arrows dealing **22 damage** (2.0s cooldown, 28 m/s speed).
- **Distance Keeping (Kiting):** If the player closes to within 6.5m, the hunter agilely retreats/kites backwards to open up archery range.
- **Tactical Cover & Search:** Repositions behind obstacles when line of sight is broken.
- **Terminal Death:** Immediately halts `NavMeshAgent`, cancels archery routines, disables colliders, and sinks after 5 seconds.

---

## 3. Component Architecture
- [`BambooHunterAgent.cs`](file:///d:/game/The-Jade-Seal/Assets/JadeSeal/Scripts/EnemyAI/BambooHunter/BambooHunterAgent.cs) — State machine controller.
- [`BambooHunterHealth.cs`](file:///d:/game/The-Jade-Seal/Assets/JadeSeal/Scripts/EnemyAI/BambooHunter/BambooHunterHealth.cs) — Implements `JadeSeal.Combat.IDamageable` with blood feedback.
- [`BambooHunterVisualizer.cs`](file:///d:/game/The-Jade-Seal/Assets/JadeSeal/Scripts/EnemyAI/BambooHunter/BambooHunterVisualizer.cs) — Procedural bone IK gait, draw/aim stance, and material styling.
- [`ArrowProjectile.cs`](file:///d:/game/The-Jade-Seal/Assets/JadeSeal/Scripts/EnemyAI/MountainArcher/Combat/ArrowProjectile.cs) — Ballistic arrow physics with gravity arc and player damage.
