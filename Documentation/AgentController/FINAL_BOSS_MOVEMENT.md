# Final Boss — Agent Controller Specification
## Graphics Duty: Agent Movement & Animation

### Overview
The Final Boss manages phase-based arena movements, transition sequences, and special spell animation telegraphs inside the temple sanctum.

### Implementation Details:
- **Arena Navigation**: Coordinates with `BossArenaController` for multi-tier tactical arena repositioning.
- **Phase-Driven Movement**: Changes movement speeds and aggression profiles across HP phases.
- **Telegraph Animation Synchronization**: Delays damage volumes to synchronize with large-scale spell casting animations (Meteor Strike, Red Orb summoning).
