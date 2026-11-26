# Bamboo Archer — Agent Controller Specification
## Graphics Duty: Agent Movement & Animation

### Overview
The Bamboo Archer operates in bamboo grove environments, relying on dynamic strafing, ranged line-of-sight checks, and agile repositioning.

### Implementation Details:
- **Locomotion State Machine**: Integrates NavMesh velocity tracking with aim-strafe blending.
- **Dynamic Retreat & Advance**: Calculates distance vectors to maintain optimal firing distance (between 8m and 16m) without entering melee vulnerability.
- **Rotational Tracking**: Smooth angular alignment towards player aim target during bow draw animation frames.
