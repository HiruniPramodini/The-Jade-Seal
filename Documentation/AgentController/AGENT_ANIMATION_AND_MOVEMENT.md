# Student 4 — The Agent Controller
## Graphics Duty: Agent Animation & Movement

### Overview
This module is responsible for translating the mathematical path arrays calculated by the navigation and intelligence systems into smooth, lifelike 3D character movement, rotations, and animations inside the Unity engine across **all four enemy archetypes**:

1. **Royal Guard (`RoyalGuardAI.cs`)**: Tactical cover traversal, peek emergence, and directional patrol walks.
2. **Mountain Archer (`MountainArcherAI.cs`)**: Elevated vantage navigation, terrain slope alignment, and ranged aiming.
3. **Bamboo Archer (`ArcherEnemyAI.cs`)**: Grove strafe locomotion, dynamic engagement distance maintenance, and bow draw animation synchronization.
4. **Final Boss (`JadeSealBossAI.cs`)**: Arena site navigation, phase-dependent speed transitions, and telegraph spell animation triggers.

### Core Architecture Across All Agents:
- **Mathematical Path Extraction**: Non-allocating polygon corner array buffering (`GetCornersNonAlloc`).
- **Smooth 3D Angular Turning**: Bounded rotational slerp (`Quaternion.RotateTowards`) preventing instant character snapping.
- **Humanoid Animator State Synchronization**: Hash-mapped states (`StateHashes`) and normalized playback tracking (`Progress()`).
- **Locomotion Dynamics**: Frame displacement tracking tied to synchronized footstep audio pacing (`EnemyFootsteps`).
