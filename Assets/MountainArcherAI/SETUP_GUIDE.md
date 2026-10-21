# Mountain Archer AI: Setup & Integration Guide
**Project:** *The Jade Seal* (Unity 6.3 LTS)

---

## 1. Quick Setup in Unity Editor

1. Open the Unity Project.
2. Ensure the scene has a baked NavMesh Surface:
   * Menu: **`The Jade Seal`** $\rightarrow$ **`AI`** $\rightarrow$ **`Bake Scene NavMesh Surface`**
3. Create/Update Mountain Archer Prefabs:
   * Menu: **`Jade Seal`** $\rightarrow$ **`Mountain Archer`** $\rightarrow$ **`Create or update placeholder prefabs`**
4. Run Deterministic Verification Checks:
   * Menu: **`Jade Seal`** $\rightarrow$ **`Mountain Archer`** $\rightarrow$ **`Run deterministic checks`**
   * Output in Console: `<color=#00FF99>[MountainArcherValidation]</color> All deterministic tests PASSED!`

---

## 2. Prefab Structure (`Prefabs/MountainArcher.prefab`)

* **Root GameObject:** `MountainArcher`
  * `NavMeshAgent` ($6.0\text{ m/s}$ speed, $16\text{ m/s}^2$ acceleration, $360^\circ/\text{s}$ angular speed)
  * `MountainArcherHealth` ($120\text{ HP}$)
  * `MountainArcherPerception` ($28\text{m}$ range, $115^\circ$ FOV, $6\text{s}$ memory)
  * `MountainArcherNavigation` (Path complete check, $0.6\text{s}$ replan)
  * `MountainArcherTactics` (High ground cover scoring)
  * `MountainArcherBow` (Muzzle reference, $40\text{ m/s}$ arrow velocity, $0.65\text{s}$ firing interval)
  * `MountainArcherBrain` (Assigned `DefaultSettings.asset`)
  * `MountainArcherPresentation` (State events)
* **Children:**
  * `Eyes` (Transform at $y = 1.65\text{m}$)
  * `Muzzle` (Transform at $y = 1.35\text{m}, z = 0.7\text{m}$)
  * `Longbow` (Archery weapon mesh)
  * `Visual` (Capsule or 3D Character Model with Animator)

---

## 3. Playmode Physics Automated Test

* Menu: **`Jade Seal`** $\rightarrow$ **`Mountain Archer`** $\rightarrow$ **`Run playmode automated physics validation`**
* Validates runtime NavMesh movement, projectile arrow collision, health damage deduction, vision occlusion behind walls, and memory expiry.
