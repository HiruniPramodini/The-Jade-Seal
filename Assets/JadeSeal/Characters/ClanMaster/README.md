# Clan Master (Elite Tactical Leader) — The Jade Seal

**Assigned Module:** Thamod  
**Role:** Elite Martial Arts Commander & Ancient Temple Guardian  
**3D Model:** [`ClanMaster.fbx`](file:///d:/game/The-Jade-Seal/Assets/JadeSeal/Characters/ClanMaster/ClanMaster.fbx)  
**Blender Source:** [`ClanMaster.blend`](file:///d:/game/The-Jade-Seal/Assets/JadeSeal/Art/Blender/ClanMaster.blend)  
**Weapon:** Ceremonial Dragon Glaive with Luminous Jade Ring (`ClanMaster_Glaive`)

---

## 1. Visual & Aesthetic Design
- **Robe:** Deep indigo-blue ceremonial martial arts silk robe (`M_ClanMaster_Robe.mat`).
- **Armor:** Charcoal hardened lamellar cuirass (`M_ClanMaster_Armor.mat`).
- **Jade Ornament:** Glowing imperial jade pendant with subtle emissive pulse (`M_ClanMaster_Jade.mat`).
- **Skin:** Natural medium-deep brown skin tone (`M_ClanMaster_Skin.mat`).
- **Weapon Blade:** Aged ceremonial bronze with dragon engravings (`M_ClanMaster_Bronze.mat`).
- **Silhouette:** Authoritative martial arts master stance with flowing robes and a commanding presence.

---

## 2. Autonomous AI Behaviour
- **Paced Approach:** Deliberate, disciplined pacing at 2.0 m/s walk and 4.2 m/s run.
- **Heavy Glaive Cleave:** Devastating wide horizontal sweep dealing **45 physical damage** (3.2m range, 2.4s cooldown).
- **Leader-Support: Clan Rally Aura:**
  - Configurable: `rallyRadius = 16m`, `rallyCooldown = 12s`, `rallyDuration = 5s`.
  - Channels ancient ancestral power, emitting a luminous jade-gold shockwave that buffs all nearby allies:
    - **+25% Movement Speed**
    - High-priority combat response
  - Uses non-allocating sphere checks and cooldown timers to avoid per-frame overhead.
- **Defensive Parry:** Lowers glaive into an immovable guard, mitigating 50% damage when attack is on cooldown.
- **Low-Health Tactical Retreat:** When health drops below 30%, breaks melee engagement to retreat toward allied patrols and regroup.
- **Terminal Death:** Dissipates aura, halts navigation, disables colliders, and triggers solemn death collapse.

---

## 3. Component Architecture
- [`ClanMasterAgent.cs`](file:///d:/game/The-Jade-Seal/Assets/JadeSeal/Scripts/EnemyAI/ClanMaster/ClanMasterAgent.cs) — Boss state machine & Rally aura system.
- [`ClanMasterHealth.cs`](file:///d:/game/The-Jade-Seal/Assets/JadeSeal/Scripts/EnemyAI/ClanMaster/ClanMasterHealth.cs) — High health pool (220 HP) implementing `JadeSeal.Combat.IDamageable`.
- [`ClanMasterVisualizer.cs`](file:///d:/game/The-Jade-Seal/Assets/JadeSeal/Scripts/EnemyAI/ClanMaster/ClanMasterVisualizer.cs) — Procedural bone IK gait, sweeping cleave, and aura channeling presentation.
