using System;
using UnityEngine;

namespace JadeSeal.BambooHunter
{
    /// <summary>
    /// Explicit enumeration of reasons why perception may reject target visibility.
    /// Used for rich HUD diagnostics and deterministic validation assertions.
    /// </summary>
    public enum VisibilityRejectionReason
    {
        None,
        TargetInvalid,
        OutOfRange,
        OutsideFOV,
        SelfColliderHit,
        ObstacleOcclusion
    }

    /// <summary>
    /// Autonomous perception and sensory processing component for the Bamboo Hunter AI.
    ///
    /// ARCHITECTURE & DESIGN CONTRACTS:
    /// 1. Target Acquisition: Explicit Transform assignment is prioritized. Tag-based fallback ("Player")
    ///    is rate-limited (never executes every frame) and disabled when target is valid.
    /// 2. Conical FOV & Range: Computes horizontal vision angle and Euclidean distance to prevent vertical bias.
    /// 3. Sensory vs Weapon Dual Line-of-Sight:
    ///    - Visibility: Linecast from EyePosition -> TargetAimPosition against visionObstacles LayerMask.
    ///    - Line of Fire: Linecast from BowPosition -> TargetAimPosition against projectileObstacles LayerMask.
    ///    - Recognizes that an enemy may be visible while the physical arrow trajectory remains obstructed.
    /// 4. Temporal Belief State (Memory): Updates LastKnownPosition and LastSeenTime when target is visible.
    ///    Provides bounded memory decay for the Investigate tactical state.
    /// 5. Environmental Constraint Notice:
    ///    Because base Bamboo Forest scene vegetation has no colliders, Physics linecasts detect world/terrain
    ///    surfaces. Dedicated vegetation cover proxies and obstacle layers integrate via LayerMasks.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BambooHunterPerception : MonoBehaviour
    {
        [Header("Target Acquisition")]
        [Tooltip("Explicit target Transform to track. If unassigned, optional tag search will be attempted.")]
        [SerializeField] private Transform target;

        [Tooltip("Tag used for fallback target acquisition if explicit target is null.")]
        [SerializeField] private string targetTag = "Player";

        [Tooltip("Enable rate-limited tag search if target Transform is unassigned.")]
        [SerializeField] private bool autoAcquireByTag = false;

        [Header("Sensory Anchors & Heights")]
        [Tooltip("Optional transform for eye position. If unassigned, eyeHeight offset from root is used.")]
        [SerializeField] private Transform eyeAnchor;

        [Tooltip("Vertical eye offset from agent transform root in meters.")]
        [Min(0.1f)] [SerializeField] private float eyeHeight = 1.65f;

        [Tooltip("Optional transform for bow muzzle/arrow release origin.")]
        [SerializeField] private Transform bowAnchor;

        [Tooltip("Vertical bow muzzle offset from agent transform root in meters.")]
        [Min(0.1f)] [SerializeField] private float bowHeight = 1.20f;

        [Tooltip("Vertical offset added to target position representing eye/chest center.")]
        [Min(0f)] [SerializeField] private float targetAimHeight = 1.50f;

        [Header("Obstacle LayerMasks")]
        [Tooltip("Layers that obstruct visual line-of-sight (terrain, architecture, future cover proxies).")]
        [SerializeField] private LayerMask visionObstacles = 1;

        [Tooltip("Layers that obstruct projectile flight from bow to target.")]
        [SerializeField] private LayerMask projectileObstacles = 1;

        [Header("Runtime Sensory State (Read Only)")]
        [SerializeField] private bool canSeeTarget;
        [SerializeField] private bool hasClearLineOfFire;
        [SerializeField] private float targetDistance;
        [SerializeField] private Vector3 lastKnownPosition;
        [SerializeField] private float lastSeenTime = float.NegativeInfinity;
        [SerializeField] private Vector3 targetVelocity;
        [SerializeField] private bool isThreatAimingAtMe;
        [SerializeField] private string lastOcclusionCollider = string.Empty;
        [SerializeField] private string lastLineOfFireBlocker = string.Empty;
        [SerializeField] private VisibilityRejectionReason lastVisibilityRejection = VisibilityRejectionReason.None;
        [SerializeField] private float lastFovAngle = 0f;

        [Header("Proximity Awareness")]
        [Tooltip("Proximity radius (in meters) within which sensory awareness expands beyond strict conical vision.")]
        [Min(0.5f)] [SerializeField] private float closeProximityRadius = 2.0f;

        // Rate limiting for tag search
        private float nextTagSearchTime;
        private Vector3 previousTargetPosition;
        private float previousSampleTime;

        // Reusable raycast hit buffer to prevent garbage collection allocation
        private static readonly RaycastHit[] s_PerceptionHits = new RaycastHit[16];

        #region Public Properties

        /// <summary>Active target Transform being tracked.</summary>
        public Transform Target
        {
            get => target;
            set
            {
                target = value;
                if (target == null)
                {
                    canSeeTarget = false;
                    hasClearLineOfFire = false;
                }
            }
        }

        /// <summary>True if target is currently within FOV, within detection range, and visually unblocked.</summary>
        public bool CanSeeTarget => canSeeTarget;

        /// <summary>True if bow muzzle has an unblocked trajectory to target center.</summary>
        public bool HasClearLineOfFire => hasClearLineOfFire;

        /// <summary>Euclidean distance from agent root to target root in meters.</summary>
        public float TargetDistance => targetDistance;

        /// <summary>Coordinates where target was last observed with direct visibility.</summary>
        public Vector3 LastKnownPosition => lastKnownPosition;

        /// <summary>Time.time stamp when target was last directly observed.</summary>
        public float LastSeenTime => lastSeenTime;

        /// <summary>Estimated linear velocity of target in meters per second.</summary>
        public Vector3 TargetVelocity => targetVelocity;

        /// <summary>True if target forward vector points roughly toward the hunter.</summary>
        public bool IsThreatAimingAtMe => isThreatAimingAtMe;

        /// <summary>Name of the collider currently occluding visual line of sight, or empty string if clear.</summary>
        public string LastOcclusionCollider => lastOcclusionCollider;

        /// <summary>Name of the collider currently blocking weapon trajectory line of fire, or empty string if clear.</summary>
        public string LastLineOfFireBlocker => lastLineOfFireBlocker;

        /// <summary>The explicit reason why target visibility was rejected in the most recent sensory sample.</summary>
        public VisibilityRejectionReason LastVisibilityRejectionReason => lastVisibilityRejection;

        /// <summary>Horizontal angle in degrees between observer forward orientation and target direction.</summary>
        public float LastFovAngle => lastFovAngle;

        /// <summary>Proximity radius within which wide sensory awareness is active.</summary>
        public float CloseProximityRadius
        {
            get => closeProximityRadius;
            set => closeProximityRadius = Mathf.Max(0.5f, value);
        }

        /// <summary>World position of sensory eyes.</summary>
        public Vector3 EyePosition => eyeAnchor ? eyeAnchor.position : transform.position + Vector3.up * eyeHeight;

        /// <summary>World position of bow arrow release point.</summary>
        public Vector3 BowPosition => bowAnchor ? bowAnchor.position : transform.position + Vector3.up * bowHeight;

        /// <summary>World position of target aiming center.</summary>
        public Vector3 TargetAimPosition => target ? target.position + Vector3.up * targetAimHeight : lastKnownPosition + Vector3.up * targetAimHeight;

        /// <summary>Elapsed time in seconds since target was last visible.</summary>
        public float MemoryAge => !float.IsNegativeInfinity(lastSeenTime) && !float.IsNaN(lastSeenTime) ? Mathf.Max(0f, Time.time - lastSeenTime) : float.PositiveInfinity;

        /// <summary>Optional ScriptableObject settings reference.</summary>
        public BambooHunterSettings Settings { get; set; }

        /// <summary>Transform representing the sensory eye origin.</summary>
        public Transform EyeAnchor
        {
            get => eyeAnchor;
            set => eyeAnchor = value;
        }

        /// <summary>Transform representing the arrow launch muzzle origin.</summary>
        public Transform BowAnchor
        {
            get => bowAnchor;
            set => bowAnchor = value;
        }

        /// <summary>Tag used for fallback player acquisition.</summary>
        public string TargetTag
        {
            get => targetTag;
            set => targetTag = value;
        }

        /// <summary>Whether to automatically seek target by tag if unassigned.</summary>
        public bool AutoAcquireByTag
        {
            get => autoAcquireByTag;
            set => autoAcquireByTag = value;
        }

        #endregion

        private void Awake()
        {
            AcquireTargetIfNeeded();
        }

        private void Start()
        {
            AcquireTargetIfNeeded();
        }

        /// <summary>
        /// Rate-limited fallback acquisition to prevent expensive per-frame global tag searches.
        /// </summary>
        private void AcquireTargetIfNeeded()
        {
            if (target != null) return;
            if (!autoAcquireByTag || string.IsNullOrEmpty(targetTag)) return;

            if (Time.time < nextTagSearchTime) return;
            nextTagSearchTime = Time.time + 2.0f; // Limit search to once every 2 seconds

            try
            {
                GameObject found = GameObject.FindWithTag(targetTag);
                if (found != null)
                {
                    target = found.transform;
                }
            }
            catch (Exception)
            {
                // Tag may not be defined in ProjectSettings; fail silently and safely
            }
        }

        /// <summary>
        /// Samples perception environment against provided settings configuration.
        /// Call periodically (e.g., at thinkInterval) from the Brain coordinator.
        /// </summary>
        public void Sample(BambooHunterSettings settings)
        {
            if (!enabled || !gameObject.activeInHierarchy)
            {
                canSeeTarget = false;
                hasClearLineOfFire = false;
                return;
            }

            AcquireTargetIfNeeded();

            canSeeTarget = false;
            hasClearLineOfFire = false;
            isThreatAimingAtMe = false;

            if (target == null || !target.gameObject.activeInHierarchy)
            {
                targetDistance = 0f;
                lastVisibilityRejection = VisibilityRejectionReason.TargetInvalid;
                return;
            }

            Vector3 eyePos = EyePosition;
            Vector3 targetAimPos = TargetAimPosition;
            Vector3 displacement = targetAimPos - eyePos;

            // 1. Distance Evaluation
            float maxRange = settings ? settings.detectionRange : 25f;
            if (!IsWithinDistance(eyePos, targetAimPos, maxRange, out targetDistance))
            {
                lastVisibilityRejection = VisibilityRejectionReason.OutOfRange;
                return;
            }

            // 2. Horizontal Field-of-View Evaluation (with close proximity awareness)
            float fov = settings ? settings.fieldOfView : 110f;
            if (!IsInsideFieldOfView(eyePos, transform.forward, targetAimPos, fov, closeProximityRadius, out lastFovAngle))
            {
                lastVisibilityRejection = VisibilityRejectionReason.OutsideFOV;
                return;
            }

            // 3. Visual Line-of-Sight Check (Physics Linecast / RaycastNonAlloc)
            if (!CheckClearLine(eyePos, targetAimPos, visionObstacles, transform, target, out lastOcclusionCollider))
            {
                lastVisibilityRejection = VisibilityRejectionReason.ObstacleOcclusion;
                return;
            }

            // Direct visual contact confirmed
            canSeeTarget = true;
            lastVisibilityRejection = VisibilityRejectionReason.None;
            lastKnownPosition = target.position;
            lastSeenTime = Time.time;

            // 4. Arrow Trajectory Line-of-Fire Check (Bow Position -> Target)
            Vector3 bowPos = BowPosition;
            hasClearLineOfFire = CheckClearLine(bowPos, targetAimPos, projectileObstacles, transform, target, out lastLineOfFireBlocker);

            // 5. Target Velocity Estimation
            if (previousSampleTime > 0f && Time.time > previousSampleTime)
            {
                float dt = Time.time - previousSampleTime;
                targetVelocity = CalculateTargetVelocity(targetAimPos, previousTargetPosition, dt);
            }

            previousTargetPosition = targetAimPos;
            previousSampleTime = Time.time;

            // 6. Threat Assessment: Is player aiming towards hunter?
            isThreatAimingAtMe = IsTargetAimingAtObserver(eyePos, target.position, target.forward, 0.70f);
        }

        /// <summary>
        /// Populates an existing decision context snapshot with verified perception outputs.
        /// </summary>
        public void PopulateDecisionContext(ref BambooHunterDecisionContext context, BambooHunterSettings settings)
        {
            float memDuration = settings ? settings.memoryDuration : 5f;
            bool hasMem = HasTargetMemory(memDuration);

            context.hasTargetVisibility = canSeeTarget;
            context.hasTargetMemory = hasMem;
            context.memoryAgeSeconds = MemoryAge;
            context.distanceToTarget = targetDistance;
            context.hasUnobstructedShot = hasClearLineOfFire;
            context.isThreatAimingAtMe = isThreatAimingAtMe;
        }

        /// <summary>
        /// Checks whether target memory remains active within the specified duration window.
        /// </summary>
        public bool HasTargetMemory(float duration)
        {
            if (float.IsNegativeInfinity(lastSeenTime) || float.IsNaN(lastSeenTime)) return false;
            return (Time.time - lastSeenTime) < Mathf.Max(0.1f, duration);
        }

        /// <summary>
        /// Explicitly injects a remembered target location and timestamp (useful for investigation testing).
        /// </summary>
        public void SetLastSeenMemory(Vector3 position, float timestamp)
        {
            lastKnownPosition = position;
            lastSeenTime = timestamp;
            canSeeTarget = false;
        }

        /// <summary>
        /// Clears all target memory, resetting to unalerted state.
        /// </summary>
        public void Forget()
        {
            lastSeenTime = float.NegativeInfinity;
            canSeeTarget = false;
            hasClearLineOfFire = false;
            isThreatAimingAtMe = false;
            targetVelocity = Vector3.zero;
            lastOcclusionCollider = string.Empty;
            lastLineOfFireBlocker = string.Empty;
            lastVisibilityRejection = VisibilityRejectionReason.None;
            lastFovAngle = 0f;
        }

        #region Static Mathematical Sensory Functions (Zero Engine Dependencies)

        /// <summary>
        /// Determines if a target position falls within the horizontal field-of-view of the observer.
        /// Projects vectors onto horizontal XZ plane to prevent vertical elevation distortion.
        /// </summary>
        public static bool IsInsideFieldOfView(Vector3 observerPosition, Vector3 observerForward, Vector3 targetPosition, float horizontalFov)
        {
            return IsInsideFieldOfView(observerPosition, observerForward, targetPosition, horizontalFov, 2.0f, out _);
        }

        /// <summary>
        /// Determines if a target position falls within the horizontal field-of-view of the observer,
        /// providing close-proximity awareness expansion and outputting the computed angle.
        /// </summary>
        public static bool IsInsideFieldOfView(
            Vector3 observerPosition,
            Vector3 observerForward,
            Vector3 targetPosition,
            float horizontalFov,
            float closeProximityRadius,
            out float computedAngle)
        {
            if (float.IsNaN(horizontalFov) || horizontalFov <= 0.001f)
            {
                computedAngle = 180f;
                return false;
            }

            Vector3 toTarget = targetPosition - observerPosition;
            toTarget.y = 0f;

            Vector3 forward = observerForward;
            forward.y = 0f;

            float horizontalDistSqr = toTarget.sqrMagnitude;
            if (horizontalDistSqr < 0.0001f || forward.sqrMagnitude < 0.0001f)
            {
                computedAngle = 0f;
                return true; // Right on top of observer
            }

            float horizontalDist = Mathf.Sqrt(horizontalDistSqr);
            computedAngle = Vector3.Angle(forward.normalized, toTarget.normalized);

            // Close proximity awareness:
            // 1. Immediate personal touch space (<= 1.0m): physical presence detects target regardless of facing direction
            if (horizontalDist <= 1.0f)
            {
                return true;
            }

            // 2. Close awareness range (<= closeProximityRadius, e.g. 2.0m): wide front-hemisphere awareness (<= 90 deg)
            if (horizontalDist <= closeProximityRadius)
            {
                return computedAngle <= 90.0f;
            }

            // 3. Standard conical field of view for targets beyond close proximity
            return computedAngle <= (horizontalFov * 0.5f);
        }

        /// <summary>
        /// Checks if Euclidean distance between observer and target is within maxDistance.
        /// Returns computed distance safely.
        /// </summary>
        public static bool IsWithinDistance(Vector3 observerPosition, Vector3 targetPosition, float maxDistance, out float distance)
        {
            if (float.IsNaN(maxDistance) || maxDistance <= 0f)
            {
                distance = 0f;
                return false;
            }

            Vector3 delta = targetPosition - observerPosition;
            float sqrDist = delta.sqrMagnitude;

            if (float.IsNaN(sqrDist) || float.IsInfinity(sqrDist))
            {
                distance = 0f;
                return false;
            }

            distance = Mathf.Sqrt(sqrDist);
            return distance <= maxDistance;
        }

        /// <summary>
        /// Computes smoothed linear velocity given displacement and elapsed time delta.
        /// </summary>
        public static Vector3 CalculateTargetVelocity(Vector3 currentPos, Vector3 previousPos, float deltaTime)
        {
            if (deltaTime <= 0.0001f || float.IsNaN(deltaTime)) return Vector3.zero;

            Vector3 velocity = (currentPos - previousPos) / deltaTime;
            if (float.IsNaN(velocity.x) || float.IsInfinity(velocity.x)) return Vector3.zero;

            return velocity;
        }

        /// <summary>
        /// Evaluates whether the target's forward orientation faces toward the observer.
        /// Dot product > 0.70 represents roughly a ~45 degree facing cone toward the observer.
        /// </summary>
        public static bool IsTargetAimingAtObserver(Vector3 observerPos, Vector3 targetPos, Vector3 targetForward, float dotThreshold = 0.70f)
        {
            Vector3 targetToObserver = observerPos - targetPos;
            targetToObserver.y = 0f;

            Vector3 forward = targetForward;
            forward.y = 0f;

            if (targetToObserver.sqrMagnitude < 0.0001f || forward.sqrMagnitude < 0.0001f) return false;

            float dot = Vector3.Dot(forward.normalized, targetToObserver.normalized);
            return dot >= dotThreshold;
        }

        /// <summary>
        /// Evaluates temporal memory expiration status.
        /// </summary>
        public static bool EvaluateMemory(float currentTime, float lastSeenTime, float memoryDuration, out float memoryAge)
        {
            if (lastSeenTime <= 0f || currentTime < lastSeenTime)
            {
                memoryAge = float.PositiveInfinity;
                return false;
            }

            memoryAge = currentTime - lastSeenTime;
            return memoryAge < Mathf.Max(0.1f, memoryDuration);
        }

        #endregion

        #region Internal Raycast Helpers

        /// <summary>
        /// Determines whether a collider is part of the specified entity hierarchy
        /// (either the root transform itself, a child, or an enclosing parent).
        /// </summary>
        public static bool IsPartOfHierarchy(Collider col, Transform entity)
        {
            if (col == null || entity == null) return false;
            Transform colT = col.transform;
            if (colT == entity) return true;
            if (colT.IsChildOf(entity)) return true;
            if (entity.IsChildOf(colT)) return true;
            return false;
        }

        /// <summary>
        /// Inspects raycast intersections along the trajectory between from and to.
        /// Resolves closest non-shooter collider hit: if the target is struck first (or path is open),
        /// returns true. If a foreign environmental obstacle is struck before reaching the target, returns false.
        /// </summary>
        public static bool CheckPathClear(
            Vector3 from,
            Vector3 to,
            LayerMask mask,
            Transform shooter,
            Transform target,
            out string blockingColliderName)
        {
            blockingColliderName = string.Empty;
            Vector3 delta = to - from;
            float dist = delta.magnitude;
            if (dist <= 0.001f) return true;

            int hitCount = Physics.RaycastNonAlloc(from, delta.normalized, s_PerceptionHits, dist, mask, QueryTriggerInteraction.Ignore);
            if (hitCount <= 0) return true;

            float closestDistance = float.PositiveInfinity;
            Collider closestCollider = null;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = s_PerceptionHits[i];
                if (hit.collider == null) continue;

                // Ignore shooter hierarchy
                if (IsPartOfHierarchy(hit.collider, shooter))
                {
                    continue;
                }

                if (hit.distance < closestDistance)
                {
                    closestDistance = hit.distance;
                    closestCollider = hit.collider;
                }
            }

            if (closestCollider == null)
            {
                return true;
            }

            // If the closest collider struck belongs to the target hierarchy, the trajectory reached target
            if (IsPartOfHierarchy(closestCollider, target))
            {
                return true;
            }

            // An intervening environmental obstacle was hit before reaching target
            blockingColliderName = closestCollider.gameObject.name;
            return false;
        }

        /// <summary>
        /// Checks line clearance ignoring shooter and target hierarchies.
        /// </summary>
        public static bool CheckClearLine(Vector3 from, Vector3 to, LayerMask mask, Transform shooter, Transform target, out string blockerName)
        {
            return CheckPathClear(from, to, mask, shooter, target, out blockerName);
        }

        /// <summary>
        /// Checks line clearance ignoring shooter and target hierarchies.
        /// </summary>
        public static bool CheckClearLine(Vector3 from, Vector3 to, LayerMask mask, Transform shooter, Transform target)
        {
            return CheckPathClear(from, to, mask, shooter, target, out _);
        }

        /// <summary>
        /// Backwards-compatible line clearance check.
        /// </summary>
        public static bool CheckClearLine(Vector3 from, Vector3 to, LayerMask mask)
        {
            return CheckPathClear(from, to, mask, null, null, out _);
        }

        #endregion

        #region Editor Visualization & Gizmos

        private void OnDrawGizmosSelected()
        {
            Vector3 eyePos = EyePosition;
            float range = 25f;
            float fov = 110f;

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(eyePos, range);

            Vector3 fwd = transform.forward;
            Vector3 leftRay = Quaternion.Euler(0f, -fov * 0.5f, 0f) * fwd * range;
            Vector3 rightRay = Quaternion.Euler(0f, fov * 0.5f, 0f) * fwd * range;

            Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.5f);
            Gizmos.DrawRay(eyePos, leftRay);
            Gizmos.DrawRay(eyePos, rightRay);

            // Last known position marker
            if (lastSeenTime > 0f)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(lastKnownPosition + Vector3.up, 0.35f);
                Gizmos.DrawLine(lastKnownPosition, lastKnownPosition + Vector3.up * 1.5f);
            }

            // Ray to active target
            if (target != null)
            {
                if (canSeeTarget)
                {
                    Gizmos.color = hasClearLineOfFire ? Color.green : new Color(1f, 0.5f, 0f);
                    Gizmos.DrawLine(BowPosition, TargetAimPosition);
                }
                else
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawLine(eyePos, TargetAimPosition);
                }
            }
        }

        #endregion
    }
}
