# Student 4 — The Agent Controller
## Graphics Duty: Agent Animation & Movement

### Overview
This module is responsible for translating the mathematical path arrays calculated by the navigation and intelligence systems into smooth, lifelike 3D character movement, rotations, and animations inside the Unity engine.

### Key Responsibilities & Implementations:
1. **Mathematical Path Translation (`RoyalGuardAI.Go`, `PathInsideLeash`)**:
   - Extracts mathematical polygon corner arrays using non-allocating buffers (`candidatePath.GetCornersNonAlloc(corners)`).
   - Validates paths against the agent leash bounds before committing routes.
   - Converts straight-line vector arrays into continuous NavMesh agent velocity.

2. **Smooth 3D Rotational Alignment (`RoyalGuardAI.Face`)**:
   - Eliminates unnatural snap turns using `Quaternion.RotateTowards` at a bounded turn rate (270 deg/sec).
   - Projects target directions onto the horizontal plane (`Vector3.ProjectOnPlane`) to avoid character tilting.
   - Adjusts for dynamic weapon aiming yaw offsets.

3. **Humanoid Animation State Machine Mapping (`RoyalGuardAI.Play`, `Progress`)**:
   - Maps cached animator integer hashes (`StateHashes`) to eliminate runtime string allocations.
   - Evaluates normalized animation progress (`Progress()`) to guarantee smooth transitions at designated blend windows.
   - Coordinates multi-phase motion sequences: patrol walk, turn-in-place, cover traversal, peek emergence, and aim strafing.

4. **Locomotion & Audio Synchronization (`EnemyFootsteps`)**:
   - Measures planar horizontal displacement per frame.
   - Synchronizes footstep sound events precisely with character stride travel distance.
