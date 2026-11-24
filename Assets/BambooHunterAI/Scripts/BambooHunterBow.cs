using System;
using UnityEngine;

namespace JadeSeal.BambooHunter
{
    /// <summary>
    /// Weapon actuator managing bow tension draw, aiming readiness, shot execution,
    /// quiver ammunition, shot cooldown, and trajectory line-of-fire validation.
    /// Exposes a clean, decoupled public API for the Bamboo Hunter decision brain and FSM.
    /// </summary>
    [AddComponentMenu("Jade Seal/Bamboo Hunter/Bamboo Hunter Bow")]
    public sealed class BambooHunterBow : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private BambooHunterSettings settings;

        [Tooltip("Transform representing the physical arrow release origin.")]
        [SerializeField] private Transform launchAnchor;

        [Tooltip("Prefab instantiated when releasing an arrow. A lightweight procedural arrow is used if unassigned.")]
        [SerializeField] private GameObject arrowPrefab;

        [Tooltip("Layer mask filtering obstacles that obstruct arrows or line of fire.")]
        [SerializeField] private LayerMask lineOfFireMask = ~0;

        [Tooltip("Default local height above root for arrow launch if launchAnchor is unassigned.")]
        [SerializeField] private float defaultBowHeight = 1.3f;

        [Tooltip("Default vertical offset above target root for aiming.")]
        [SerializeField] private float targetAimHeight = 1.0f;

        [Header("Runtime State (Read Only)")]
        [SerializeField] private int currentArrows = 6;
        [SerializeField] private int consecutiveShots = 0;
        [SerializeField] private float drawProgress = 0f;
        [SerializeField] private bool isDrawing = false;
        [SerializeField] private float cooldownRemaining = 0f;
        [SerializeField] private bool isRestocking = false;
        [SerializeField] private float restockTimer = 0f;

        // Reusable raycast hit buffer to prevent garbage collection allocation
        private static readonly RaycastHit[] s_BowHits = new RaycastHit[16];

        #region Events

        /// <summary>Fired when an arrow is released. Parameters: target, aim point.</summary>
        public event Action<Transform, Vector3> OnShotFired;

        /// <summary>Fired when the bowstring pull commences.</summary>
        public event Action OnDrawStarted;

        /// <summary>Fired when an in-progress draw is cancelled.</summary>
        public event Action OnDrawCancelled;

        /// <summary>Fired when the last arrow in the quiver is expended.</summary>
        public event Action OnQuiverEmpty;

        /// <summary>Fired when the quiver completes restocking.</summary>
        public event Action OnQuiverRestocked;

        #endregion

        #region Public Properties

        /// <summary>Configured ScriptableObject settings.</summary>
        public BambooHunterSettings Settings
        {
            get => settings;
            set => settings = value;
        }

        /// <summary>Current count of arrows available in the quiver.</summary>
        public int CurrentArrows => currentArrows;

        /// <summary>Consecutive arrows shot from current stationary position without repositioning.</summary>
        public int ConsecutiveShots => consecutiveShots;

        /// <summary>Normalized bow draw progress in [0.0, 1.0].</summary>
        public float DrawProgress => drawProgress;

        /// <summary>True if bowstring is currently being drawn back.</summary>
        public bool IsDrawing => isDrawing;

        /// <summary>Cooldown time remaining in seconds before next draw can begin.</summary>
        public float CooldownRemaining => cooldownRemaining;

        /// <summary>True if bow is currently recovering from a recent shot.</summary>
        public bool IsCooldownActive => cooldownRemaining > 0.001f;

        /// <summary>True if currently restocking quiver.</summary>
        public bool IsRestocking => isRestocking;

        /// <summary>True if bow tension satisfies the required threshold to release an arrow.</summary>
        public bool IsDrawComplete
        {
            get
            {
                if (!isDrawing) return false;
                float threshold = settings ? settings.minDrawRatioToShoot : 0.99f;
                return drawProgress >= threshold;
            }
        }

        /// <summary>True if the shooter entity itself is incapacitated or dead.</summary>
        public bool IsShooterDead
        {
            get
            {
                var health = GetComponentInParent<BambooHunterHealth>();
                return health != null && health.IsDead;
            }
        }

        /// <summary>World position where arrows are spawned and released.</summary>
        public Vector3 LaunchPosition
        {
            get
            {
                if (launchAnchor != null) return launchAnchor.position;
                return transform.position + Vector3.up * defaultBowHeight;
            }
        }

        /// <summary>Forward vector representing default bow aiming direction.</summary>
        public Vector3 LaunchDirection => launchAnchor ? launchAnchor.forward : transform.forward;

        /// <summary>Layer mask applied to line-of-fire obstacle checks.</summary>
        public LayerMask LineOfFireMask
        {
            get => lineOfFireMask;
            set => lineOfFireMask = value;
        }

        /// <summary>Arrow projectile prefab instantiated when releasing a shot.</summary>
        public GameObject ArrowPrefab
        {
            get => arrowPrefab;
            set => arrowPrefab = value;
        }

        /// <summary>Transform anchor representing physical arrow launch origin.</summary>
        public Transform LaunchAnchor
        {
            get => launchAnchor;
            set => launchAnchor = value;
        }

        #endregion

        private void Awake()
        {
            if (settings != null)
            {
                currentArrows = settings.quiverCapacity;
            }
        }

        /// <summary>
        /// Explicit initialization helper for runtime or unit test setup.
        /// </summary>
        public void Initialize(BambooHunterSettings config, Transform anchor = null, GameObject prefab = null)
        {
            settings = config;
            if (anchor != null) launchAnchor = anchor;
            if (prefab != null) arrowPrefab = prefab;

            currentArrows = settings ? settings.quiverCapacity : 6;
            consecutiveShots = 0;
            drawProgress = 0f;
            isDrawing = false;
            cooldownRemaining = 0f;
            isRestocking = false;
            restockTimer = 0f;
        }

        /// <summary>
        /// Initiates drawing the bowstring if prerequisites are met.
        /// </summary>
        public bool BeginDraw(Transform target)
        {
            if (target == null) return false;
            if (IsShooterDead) return false;
            if (currentArrows <= 0) return false;
            if (cooldownRemaining > 0.001f) return false;
            if (isRestocking) return false;

            // Check if target is already dead
            var damageable = target.GetComponentInParent<IBambooHunterDamageable>();
            if (damageable != null && damageable.IsDead) return false;

            isDrawing = true;
            drawProgress = 0f;
            OnDrawStarted?.Invoke();
            return true;
        }

        /// <summary>
        /// Cancels the current draw, relaxing the bowstring without expending ammunition.
        /// </summary>
        public void CancelDraw()
        {
            if (isDrawing)
            {
                isDrawing = false;
                drawProgress = 0f;
                OnDrawCancelled?.Invoke();
            }
        }

        /// <summary>
        /// Advances draw progress and decrements cooldown timers. Call per frame or tick.
        /// </summary>
        public void UpdateDraw(float deltaTime)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime <= 0f) return;

            // Update shot cooldown
            if (cooldownRemaining > 0f)
            {
                cooldownRemaining = Mathf.Max(0f, cooldownRemaining - deltaTime);
            }

            // Update quiver restock timer
            if (isRestocking)
            {
                restockTimer -= deltaTime;
                if (restockTimer <= 0f)
                {
                    CompleteRestock();
                }
            }

            // Advance bowstring tension pull
            if (isDrawing && !isRestocking)
            {
                float duration = settings ? settings.bowDrawDuration : 1.0f;
                drawProgress = Mathf.Clamp01(drawProgress + (deltaTime / Mathf.Max(0.05f, duration)));
            }
        }

        [SerializeField] private string lastShotFailureReason = string.Empty;

        /// <summary>Diagnostic rejection reason if CanShoot or TryShoot fails.</summary>
        public string LastShotFailureReason => lastShotFailureReason;

        /// <summary>
        /// Validates whether all tactical and physical conditions permit shooting the target right now.
        /// Consistent with BambooHunterUtility decision constraints.
        /// </summary>
        public bool CanShoot(Transform target)
        {
            return CanShoot(target, out lastShotFailureReason);
        }

        /// <summary>
        /// Validates whether all tactical and physical conditions permit shooting the target right now,
        /// outputting the explicit failure reason if rejected.
        /// </summary>
        public bool CanShoot(Transform target, out string rejectionReason)
        {
            rejectionReason = string.Empty;
            if (target == null) { rejectionReason = "TargetNull"; return false; }
            if (IsShooterDead) { rejectionReason = "ShooterDead"; return false; }
            if (!IsDrawComplete) { rejectionReason = $"DrawIncomplete(isDrawing={isDrawing},progress={drawProgress:F2})"; return false; }
            if (currentArrows <= 0) { rejectionReason = "NoArrowsInQuiver"; return false; }
            if (cooldownRemaining > 0.001f) { rejectionReason = $"CooldownActive({cooldownRemaining:F2}s)"; return false; }
            if (isRestocking) { rejectionReason = "RestockingQuiver"; return false; }

            // Verify target is alive
            var damageable = target.GetComponentInParent<IBambooHunterDamageable>();
            if (damageable != null && damageable.IsDead) { rejectionReason = "TargetAlreadyDead"; return false; }

            // Verify target within maximum attack range
            Vector3 targetAim = GetTargetAimPosition(target);
            float dist = Vector3.Distance(LaunchPosition, targetAim);
            float maxRange = settings ? settings.attackRange : 20f;
            if (dist > maxRange) { rejectionReason = $"OutOfRange({dist:F1}m>{maxRange:F1}m)"; return false; }
            if (dist < 0.05f) { rejectionReason = $"TooClose({dist:F2}m)"; return false; }

            // Verify clean line-of-fire (both straight sightline and curved trajectory arc)
            if (!CheckStraightLineOfFire(LaunchPosition, targetAim, lineOfFireMask, target))
            {
                rejectionReason = "StraightLineOfFireBlocked";
                return false;
            }

            float speed = settings ? settings.arrowSpeed : 32f;
            float grav = settings ? settings.arrowGravityScale : 1.0f;
            int samples = settings ? settings.trajectoryArcSamples : 8;
            if (!CheckTrajectoryArcOfFire(LaunchPosition, targetAim, speed, grav, lineOfFireMask, samples, target))
            {
                rejectionReason = "TrajectoryArcBlocked";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Attempts to release an arrow at the specified target.
        /// Spawns the projectile, decrements ammunition, applies cooldown, and triggers events.
        /// </summary>
        public bool TryShoot(Transform target, Vector3? explicitAimPoint = null)
        {
            return TryShoot(target, out lastShotFailureReason, explicitAimPoint);
        }

        /// <summary>
        /// Attempts to release an arrow at the specified target, outputting the explicit failure reason if rejected.
        /// </summary>
        public bool TryShoot(Transform target, out string failureReason, Vector3? explicitAimPoint = null)
        {
            if (!CanShoot(target, out failureReason))
            {
                lastShotFailureReason = failureReason;
                return false;
            }

            lastShotFailureReason = string.Empty;

            Vector3 aimPos = explicitAimPoint.HasValue ? explicitAimPoint.Value : GetTargetAimPosition(target);
            Vector3 origin = LaunchPosition;
            Vector3 shootDir = (aimPos - origin);

            if (shootDir.sqrMagnitude < 0.0001f)
            {
                shootDir = LaunchDirection;
            }
            else
            {
                shootDir.Normalize();
            }

            float speed = settings ? settings.arrowSpeed : 32f;
            float dmg = settings ? settings.arrowDamage : 18f;
            float grav = settings ? settings.arrowGravityScale : 1.0f;
            float life = settings ? settings.arrowLifetime : 6.0f;

            // Instantiate or construct arrow projectile
            GameObject arrowObj;
            if (arrowPrefab != null)
            {
                arrowObj = Instantiate(arrowPrefab, origin, Quaternion.LookRotation(shootDir));
            }
            else
            {
                // Fallback procedural projectile for tests and missing prefab scenarios
                arrowObj = new GameObject("ProceduralArrow");
                arrowObj.transform.position = origin;
                arrowObj.transform.rotation = Quaternion.LookRotation(shootDir);
                var col = arrowObj.AddComponent<SphereCollider>();
                col.radius = 0.08f;
                arrowObj.AddComponent<Rigidbody>();
            }

            var arrowComp = arrowObj.GetComponent<BambooHunterArrow>();
            if (arrowComp == null)
            {
                arrowComp = arrowObj.AddComponent<BambooHunterArrow>();
            }

            // Launch physical projectile
            arrowComp.Launch(origin, shootDir, speed, dmg, gameObject, lineOfFireMask, grav, life);

            // Update combat state
            currentArrows = Mathf.Max(0, currentArrows - 1);
            consecutiveShots++;
            isDrawing = false;
            drawProgress = 0f;
            cooldownRemaining = settings ? settings.shotCooldownDuration : 0.8f;

            OnShotFired?.Invoke(target, aimPos);

            if (currentArrows <= 0)
            {
                OnQuiverEmpty?.Invoke();
                StartRestock();
            }

            return true;
        }

        /// <summary>
        /// Refills quiver ammunition to maximum capacity and clears restocking state.
        /// </summary>
        public void RestockQuiver(int? amount = null)
        {
            int capacity = settings ? settings.quiverCapacity : 6;
            currentArrows = amount.HasValue ? Mathf.Clamp(amount.Value, 0, capacity) : capacity;
            isRestocking = false;
            restockTimer = 0f;
            OnQuiverRestocked?.Invoke();
        }

        /// <summary>
        /// Resets consecutive shot counter (called when agent relocates to a new cover anchor).
        /// </summary>
        public void ResetConsecutiveShots()
        {
            consecutiveShots = 0;
        }

        /// <summary>
        /// Begins the timed restock sequence when quiver is exhausted.
        /// </summary>
        public void StartRestock()
        {
            if (isRestocking) return;
            isRestocking = true;
            restockTimer = settings ? settings.quiverRestockDuration : 2.5f;
            CancelDraw();
        }

        private void CompleteRestock()
        {
            RestockQuiver();
        }

        /// <summary>
        /// Injects bow and quiver readiness data into the decision context for Utility AI evaluation.
        /// </summary>
        public void PopulateDecisionContext(ref BambooHunterDecisionContext context)
        {
            context.currentArrows = currentArrows;
            context.isBowDrawn = isDrawing;
            context.bowDrawProgress = drawProgress;
            context.consecutiveShotsFired = consecutiveShots;
        }

        /// <summary>
        /// Returns the estimated center of mass aim position on the target.
        /// </summary>
        public Vector3 GetTargetAimPosition(Transform target)
        {
            if (target == null) return transform.position + transform.forward * 5f;
            return target.position + Vector3.up * targetAimHeight;
        }

        /// <summary>
        /// Evaluates both direct straight-line clearance and curved ballistic trajectory clearance.
        /// Returns true only if both paths are unobstructed.
        /// </summary>
        public bool HasClearShot(Vector3 targetAimPos)
        {
            return HasClearShot(targetAimPos, null);
        }

        /// <summary>
        /// Evaluates both direct straight-line clearance and curved ballistic trajectory clearance.
        /// Ignores target hierarchy colliders.
        /// </summary>
        public bool HasClearShot(Vector3 targetAimPos, Transform target)
        {
            Vector3 origin = LaunchPosition;

            // 1. Direct straight-line test
            if (!CheckStraightLineOfFire(origin, targetAimPos, lineOfFireMask, target))
            {
                return false;
            }

            // 2. Parabolic ballistic trajectory arc test
            float speed = settings ? settings.arrowSpeed : 32f;
            float grav = settings ? settings.arrowGravityScale : 1.0f;
            int samples = settings ? settings.trajectoryArcSamples : 8;

            if (!CheckTrajectoryArcOfFire(origin, targetAimPos, speed, grav, lineOfFireMask, samples, target))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Evaluates clear line of fire directly to a target Transform, ignoring target colliders.
        /// </summary>
        public bool HasClearLineOfFire(Transform target)
        {
            if (target == null) return false;
            return HasClearShot(GetTargetAimPosition(target), target);
        }

        /// <summary>
        /// Linecast check validating clear straight-line sightline between muzzle and target point.
        /// </summary>
        public bool CheckStraightLineOfFire(Vector3 from, Vector3 to, LayerMask mask)
        {
            return CheckStraightLineOfFire(from, to, mask, null);
        }

        /// <summary>
        /// Raycast check validating clear straight-line sightline, ignoring shooter and target hierarchies.
        /// Evaluates closest non-shooter collider: if the target is struck first (or path is open), returns true.
        /// </summary>
        public bool CheckStraightLineOfFire(Vector3 from, Vector3 to, LayerMask mask, Transform target)
        {
            Vector3 delta = to - from;
            float dist = delta.magnitude;
            if (dist <= 0.001f) return true;

            int hitCount = Physics.RaycastNonAlloc(from, delta.normalized, s_BowHits, dist, mask, QueryTriggerInteraction.Ignore);
            if (hitCount <= 0) return true;

            float closestDistance = float.PositiveInfinity;
            Collider closestCollider = null;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = s_BowHits[i];
                if (hit.collider == null) continue;

                // Ignore shooter hierarchy
                if (BambooHunterPerception.IsPartOfHierarchy(hit.collider, transform))
                {
                    continue;
                }

                if (hit.distance < closestDistance)
                {
                    closestDistance = hit.distance;
                    closestCollider = hit.collider;
                }
            }

            if (closestCollider == null) return true;

            // If closest collider belongs to target hierarchy, line of fire is clear directly to target
            if (BambooHunterPerception.IsPartOfHierarchy(closestCollider, target))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Samples multiple segments along the theoretical ballistic trajectory arc.
        /// Ensures curved projectile flight will not collide with low hanging obstacles, branches, or terrain.
        /// </summary>
        public bool CheckTrajectoryArcOfFire(
            Vector3 from,
            Vector3 to,
            float projectileSpeed,
            float gravityMultiplier,
            LayerMask mask,
            int sampleCount = 8)
        {
            return CheckTrajectoryArcOfFire(from, to, projectileSpeed, gravityMultiplier, mask, sampleCount, null);
        }

        /// <summary>
        /// Samples multiple segments along the theoretical ballistic trajectory arc, ignoring shooter and target colliders.
        /// </summary>
        public bool CheckTrajectoryArcOfFire(
            Vector3 from,
            Vector3 to,
            float projectileSpeed,
            float gravityMultiplier,
            LayerMask mask,
            int sampleCount,
            Transform target)
        {
            sampleCount = Mathf.Clamp(sampleCount, 3, 20);
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance < 0.1f) return true;

            float speed = Mathf.Max(5f, projectileSpeed);
            float flightTime = distance / speed;
            Vector3 gravity = Physics.gravity * Mathf.Max(0f, gravityMultiplier);

            // Initial launch velocity required to land at 'to' at time 'flightTime':
            // to = from + v0*T + 0.5*g*T^2  =>  v0 = (delta - 0.5*g*T^2) / T
            Vector3 v0 = (delta - 0.5f * gravity * (flightTime * flightTime)) / flightTime;

            Vector3 previousPoint = from;
            for (int i = 1; i <= sampleCount; i++)
            {
                float t = (float)i / sampleCount * flightTime;
                Vector3 currentPoint = from + v0 * t + 0.5f * gravity * (t * t);
                Vector3 segDelta = currentPoint - previousPoint;
                float segDist = segDelta.magnitude;

                if (segDist > 0.001f)
                {
                    int hitCount = Physics.RaycastNonAlloc(previousPoint, segDelta.normalized, s_BowHits, segDist, mask, QueryTriggerInteraction.Ignore);
                    for (int h = 0; h < hitCount; h++)
                    {
                        RaycastHit hit = s_BowHits[h];
                        if (hit.collider == null) continue;

                        if (BambooHunterPerception.IsPartOfHierarchy(hit.collider, transform))
                        {
                            continue;
                        }

                        if (BambooHunterPerception.IsPartOfHierarchy(hit.collider, target))
                        {
                            continue;
                        }

                        return false;
                    }
                }
                previousPoint = currentPoint;
            }

            return true;
        }
    }
}
