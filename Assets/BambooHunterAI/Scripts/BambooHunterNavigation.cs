using System;
using UnityEngine;
using UnityEngine.AI;

namespace JadeSeal.BambooHunter
{
    /// <summary>
    /// High-level navigation activity status for the Bamboo Hunter agent.
    /// Exposes read-only state for brain coordination, Inspector diagnostics, and validation.
    /// </summary>
    public enum NavigationActivity
    {
        Idle = 0,
        Patrolling = 1,
        Investigating = 2,
        Repositioning = 3,
        Retreating = 4,
        StoppedForAim = 5,
        Stuck = 6,
        Failed = 7
    }

    /// <summary>
    /// Navigation and waypoint patrol component for the Bamboo Hunter AI.
    /// Controls locomotion via NavMeshAgent, validates destination paths, executes anti-stuck
    /// progress checks, and provides state-oriented movement commands for the AI Brain.
    ///
    /// ARCHITECTURE & DESIGN CONTRACTS:
    /// 1. Path Completeness: Accepts ONLY complete paths (NavMeshPathStatus.PathComplete).
    ///    Rejects partial, off-mesh, or unreachable destinations to prevent agents walking into voids.
    /// 2. Patrol Waypoint Support: Robustly handles 0, 1, or multiple waypoints with configurable dwell times.
    ///    Zero waypoints defaults safely to stationary surveillance.
    /// 3. Anti-Stuck & Progress Monitoring: Tracks displacement over time. If movement stalls while an active
    ///    path exists beyond stuckTimeout, signals failure/stuck state rather than infinitely replanning.
    /// 4. Environmental Notice: Base Bamboo Forest vegetation lacks physics colliders or NavMesh obstacles.
    ///    This component pathfinds over the baked NavMesh; future vegetation obstacle proxies are evaluated
    ///    via NavMesh queries and tactical cover points without modifying base scene assets.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [DisallowMultipleComponent]
    public sealed class BambooHunterNavigation : MonoBehaviour
    {
        [Header("Patrol Waypoint Configuration")]
        [Tooltip("Authored patrol waypoints. May be empty, single, or multiple.")]
        [SerializeField] private Transform[] patrolWaypoints;

        [Tooltip("Tolerance added to NavMeshAgent stopping distance to determine arrival.")]
        [Min(0.05f)] [SerializeField] private float arrivalTolerance = 0.35f;

        [Tooltip("Duration in seconds agent waits at each waypoint before advancing.")]
        [Min(0f)] [SerializeField] private float waypointDwellTime = 1.0f;

        [Header("Navigation Parameters")]
        [Tooltip("Search radius in meters when sampling nearest point on NavMesh.")]
        [Min(0.5f)] [SerializeField] private float sampleRadius = 1.5f;

        [Tooltip("Minimum elapsed seconds between path replanning requests to conserve performance.")]
        [Min(0.1f)] [SerializeField] private float replanInterval = 0.5f;

        [Tooltip("Default timeout in seconds before unprogressed movement is marked stuck.")]
        [Min(1.0f)] [SerializeField] private float defaultStuckTimeout = 3.0f;

        [Header("Runtime Navigation State (Read Only)")]
        [SerializeField] private NavigationActivity currentActivity = NavigationActivity.Idle;
        [SerializeField] private Vector3 currentDestination;
        [SerializeField] private int currentWaypointIndex;
        [SerializeField] private bool hasFailed;
        [SerializeField] private bool isStuck;
        [SerializeField] private float nextDwellEndTime;

        private NavMeshAgent agent;
        private NavMeshPath calculatedPath;
        private Vector3 lastProgressPosition;
        private float lastProgressTime;
        private float nextAllowedReplanTime;

        #region Public Properties

        /// <summary>Active high-level navigation activity.</summary>
        public NavigationActivity CurrentActivity => currentActivity;

        /// <summary>Current target destination on the NavMesh.</summary>
        public Vector3 CurrentDestination => currentDestination;

        /// <summary>Index of the currently targeted patrol waypoint.</summary>
        public int CurrentWaypointIndex => currentWaypointIndex;

        /// <summary>Total number of assigned patrol waypoints.</summary>
        public int WaypointCount => patrolWaypoints != null ? patrolWaypoints.Length : 0;

        /// <summary>True if agent has failed to find a valid path or became stuck.</summary>
        public bool HasFailed => hasFailed;

        /// <summary>True if movement has stalled beyond the stuck timeout.</summary>
        public bool IsStuck => isStuck;

        /// <summary>Underlying NavMeshAgent component.</summary>
        public NavMeshAgent Agent => agent != null ? agent : (agent = GetComponent<NavMeshAgent>());

        /// <summary>True if NavMeshAgent exists, is active/enabled, and is genuinely placed on a valid NavMesh.</summary>
        public bool IsReady
        {
            get
            {
                if (agent == null) agent = GetComponent<NavMeshAgent>();
                return agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;
            }
        }

        /// <summary>
        /// Explicit initialization helper for runtime or editor validation setup.
        /// Binds the NavMeshAgent and refreshes navigation state.
        /// </summary>
        public void Initialize(NavMeshAgent customAgent = null)
        {
            if (customAgent != null)
            {
                agent = customAgent;
            }
            else if (agent == null)
            {
                agent = GetComponent<NavMeshAgent>();
            }

            if (calculatedPath == null)
            {
                calculatedPath = new NavMeshPath();
            }

            Refresh();
        }

        /// <summary>
        /// True if agent has arrived within stopping distance of its active destination.
        /// </summary>
        public bool HasArrived
        {
            get
            {
                if (!IsReady) return false;
                if (agent.pathPending) return false;
                if (agent.isPathStale) return false;
                if (!agent.hasPath) return true;

                float effectiveThreshold = agent.stoppingDistance + arrivalTolerance;
                return agent.remainingDistance <= effectiveThreshold;
            }
        }

        #endregion

        private void Awake()
        {
            if (agent == null) agent = GetComponent<NavMeshAgent>();
            calculatedPath = new NavMeshPath();
            lastProgressPosition = transform.position;
            lastProgressTime = Time.time;
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void OnDisable()
        {
            Stop();
        }

        #region Pure Geometric Calculations & Edit-Mode Decision Helpers

        /// <summary>
        /// Pure, deterministic, side-effect-free geometric calculation of the tactical investigation standoff destination.
        /// Can be evaluated in Edit Mode and unit tests without requiring an active or NavMesh-bound NavMeshAgent.
        /// </summary>
        /// <param name="agentPosition">Current world position of the agent.</param>
        /// <param name="agentForward">Facing direction of the agent (used if coincident with target).</param>
        /// <param name="lastKnownPosition">Target or last-known threat coordinates.</param>
        /// <param name="settings">Configured settings asset (uses preferredMinRange, fallback 7.0m).</param>
        /// <param name="standoffDestination">Calculated destination preserving safe combat standoff.</param>
        /// <param name="isAlreadyWithinStandoff">True if agent is already within safe standoff range.</param>
        /// <param name="tolerance">Additional arrival tolerance (default 0.35m).</param>
        /// <returns>True if inputs are valid and finite; false if inputs contain NaN or Infinity.</returns>
        public static bool TryCalculateStandoffDestination(
            Vector3 agentPosition,
            Vector3 agentForward,
            Vector3 lastKnownPosition,
            BambooHunterSettings settings,
            out Vector3 standoffDestination,
            out bool isAlreadyWithinStandoff,
            float tolerance = 0.35f)
        {
            standoffDestination = agentPosition;
            isAlreadyWithinStandoff = false;

            if (!IsFinitePosition(agentPosition) || !IsFinitePosition(agentForward) || !IsFinitePosition(lastKnownPosition))
            {
                return false;
            }

            float standoffDist = settings ? settings.preferredMinRange : 7.0f;
            Vector3 toAgent = agentPosition - lastKnownPosition;
            toAgent.y = 0f;
            float currentDist = toAgent.magnitude;

            if (currentDist <= standoffDist + tolerance)
            {
                isAlreadyWithinStandoff = true;
                standoffDestination = agentPosition;
                return true;
            }

            Vector3 fwd = agentForward;
            fwd.y = 0f;
            Vector3 approachDir = toAgent.sqrMagnitude > 0.001f ? toAgent.normalized : (fwd.sqrMagnitude > 0.001f ? -fwd.normalized : Vector3.back);
            standoffDestination = lastKnownPosition + approachDir * standoffDist;
            return true;
        }

        /// <summary>
        /// Instance convenience helper to evaluate standoff calculation from current agent state.
        /// Does not execute locomotion or mutate navigation state.
        /// </summary>
        public bool TryGetInvestigationStandoffDestination(
            Vector3 lastKnownPosition,
            BambooHunterSettings settings,
            out Vector3 standoffDestination,
            out bool isAlreadyWithinStandoff)
        {
            return TryCalculateStandoffDestination(
                transform.position,
                transform.forward,
                lastKnownPosition,
                settings,
                out standoffDestination,
                out isAlreadyWithinStandoff,
                arrivalTolerance);
        }

        #endregion

        #region State Movement Commands

        /// <summary>
        /// Moves the agent along the configured patrol waypoint cycle.
        /// Handles 0 waypoints (stops agent), 1 waypoint (dwells upon arrival),
        /// and multiple waypoints (advances index after dwell time).
        /// </summary>
        public bool MoveToPatrol(BambooHunterSettings settings)
        {
            if (patrolWaypoints == null || patrolWaypoints.Length == 0)
            {
                Stop();
                currentActivity = NavigationActivity.Idle;
                return true;
            }

            int count = patrolWaypoints.Length;
            currentWaypointIndex = Mathf.Clamp(currentWaypointIndex, 0, count - 1);
            Transform targetWp = patrolWaypoints[currentWaypointIndex];

            if (targetWp == null)
            {
                // Skip missing/destroyed waypoint
                currentWaypointIndex = AdvanceWaypointIndex(currentWaypointIndex, count);
                return false;
            }

            Vector3 dest = targetWp.position;

            // Check if agent is already within arrival threshold of current waypoint
            if (HasArrived || Vector3.Distance(transform.position, dest) <= (agent ? agent.stoppingDistance + arrivalTolerance : 0.5f))
            {
                if (count == 1)
                {
                    // Single waypoint: remain stationary at post
                    Stop();
                    currentActivity = NavigationActivity.Patrolling;
                    return true;
                }

                // Dwell timer before proceeding to next waypoint
                if (Time.time < nextDwellEndTime)
                {
                    Stop();
                    currentActivity = NavigationActivity.Patrolling;
                    return true;
                }

                // Dwell completed: advance to next waypoint
                currentWaypointIndex = AdvanceWaypointIndex(currentWaypointIndex, count);
                nextDwellEndTime = Time.time + waypointDwellTime;
                targetWp = patrolWaypoints[currentWaypointIndex];
                if (targetWp != null) dest = targetWp.position;
            }

            float stuckTime = settings ? settings.stuckTimeout : defaultStuckTimeout;
            return MoveToDestination(dest, stuckTime, NavigationActivity.Patrolling);
        }

        /// <summary>
        /// Moves toward the target's last-known coordinates for investigation,
        /// maintaining a tactical ranged standoff distance so an archer does not walk into point-blank touch range.
        /// </summary>
        public bool MoveToInvestigate(Vector3 lastKnownPosition, BambooHunterSettings settings)
        {
            if (!TryGetInvestigationStandoffDestination(
                lastKnownPosition,
                settings,
                out Vector3 standoffDestination,
                out bool isAlreadyWithinStandoff))
            {
                hasFailed = true;
                return false;
            }

            currentDestination = standoffDestination;

            // If agent is already within safe investigation standoff distance, halt and maintain surveillance
            if (isAlreadyWithinStandoff)
            {
                Stop();
                currentActivity = NavigationActivity.Investigating;
                return true;
            }

            float stuckTime = settings ? settings.stuckTimeout : defaultStuckTimeout;
            return MoveToDestination(standoffDestination, stuckTime, NavigationActivity.Investigating);
        }

        /// <summary>
        /// Moves toward a tactical vegetation cover or flanking position.
        /// </summary>
        public bool MoveToReposition(Vector3 coverPosition, BambooHunterSettings settings)
        {
            if (!IsFinitePosition(coverPosition))
            {
                hasFailed = true;
                return false;
            }

            float stuckTime = settings ? settings.stuckTimeout : defaultStuckTimeout;
            return MoveToDestination(coverPosition, stuckTime, NavigationActivity.Repositioning);
        }

        /// <summary>
        /// Moves toward an emergency retreat destination.
        /// </summary>
        public bool MoveToRetreat(Vector3 retreatPosition, BambooHunterSettings settings)
        {
            if (!IsFinitePosition(retreatPosition))
            {
                hasFailed = true;
                return false;
            }

            float stuckTime = settings ? settings.stuckTimeout : defaultStuckTimeout;
            return MoveToDestination(retreatPosition, stuckTime, NavigationActivity.Retreating);
        }

        /// <summary>
        /// Halts all locomotion and clears active NavMesh paths for precision aiming.
        /// </summary>
        public void StopForAim()
        {
            Stop();
            currentActivity = NavigationActivity.StoppedForAim;
        }

        /// <summary>
        /// Halts locomotion and sets activity to Idle.
        /// Native agent methods are strictly guarded to prevent native exceptions on unbound agents.
        /// </summary>
        public void Stop()
        {
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }

            currentActivity = NavigationActivity.Idle;
        }

        /// <summary>
        /// Halts all navigation permanently upon agent death and disables the NavMeshAgent.
        /// </summary>
        public void StopForDeath()
        {
            Stop();
            if (agent != null)
            {
                agent.enabled = false;
            }
        }

        /// <summary>
        /// Resets failure and stuck flags and permits immediate replanning.
        /// </summary>
        public void Refresh()
        {
            hasFailed = false;
            isStuck = false;
            nextAllowedReplanTime = 0f;
            lastProgressTime = Time.time;
            if (IsReady) lastProgressPosition = transform.position;
        }

        #endregion

        #region Core Navigation & Path Validation

        /// <summary>
        /// Core path validation and execution pipeline.
        /// Validates destination on NavMesh, samples valid location, checks path completeness,
        /// applies replan rate-limiting, and monitors stuck progress.
        /// </summary>
        public bool MoveToDestination(Vector3 destination, float stuckTimeout, NavigationActivity activity)
        {
            if (!IsReady)
            {
                hasFailed = true;
                currentActivity = NavigationActivity.Failed;
                return false;
            }

            // Anti-stuck progress tracking
            float movedDistSqr = (transform.position - lastProgressPosition).sqrMagnitude;
            if (movedDistSqr > 0.04f) // Progressed more than 0.2m
            {
                lastProgressPosition = transform.position;
                lastProgressTime = Time.time;
                isStuck = false;
            }
            else if (!HasArrived && (Time.time - lastProgressTime > stuckTimeout))
            {
                // Movement stalled beyond timeout threshold
                isStuck = true;
                hasFailed = true;
                currentActivity = NavigationActivity.Stuck;
                Stop();
                return false;
            }

            // Replan frequency rate-limiting
            if (Time.time < nextAllowedReplanTime && currentDestination == destination)
            {
                return !hasFailed;
            }

            nextAllowedReplanTime = Time.time + replanInterval;

            // Validate destination on NavMesh
            if (!ValidatePath(destination, out Vector3 sampledDestination, out _))
            {
                hasFailed = true;
                currentActivity = NavigationActivity.Failed;
                Stop();
                return false;
            }

            // Execute destination request (strictly guarded by IsReady)
            agent.isStopped = false;
            bool success = agent.SetDestination(sampledDestination);

            if (success)
            {
                currentDestination = sampledDestination;
                currentActivity = activity;
                hasFailed = false;
            }
            else
            {
                hasFailed = true;
                currentActivity = NavigationActivity.Failed;
            }

            return success;
        }

        /// <summary>
        /// Validates that destination is reachable with a complete NavMesh path.
        /// Returns true only if path.status is NavMeshPathStatus.PathComplete.
        /// </summary>
        public bool ValidatePath(Vector3 destination, out Vector3 sampledPosition, out float pathLength)
        {
            sampledPosition = destination;
            pathLength = 0f;

            if (!IsReady) return false;
            if (!IsFinitePosition(destination)) return false;

            // Sample nearest point on NavMesh
            int mask = agent.areaMask;
            int agentType = agent.agentTypeID;

            NavMeshQueryFilter filter = new NavMeshQueryFilter
            {
                areaMask = mask,
                agentTypeID = agentType
            };

            if (!NavMesh.SamplePosition(destination, out NavMeshHit hit, sampleRadius, filter))
            {
                return false;
            }

            sampledPosition = hit.position;

            // Compute complete path via static query using agent query filter
            if (calculatedPath == null) calculatedPath = new NavMeshPath();
            if (!NavMesh.CalculatePath(transform.position, sampledPosition, filter, calculatedPath)) return false;

            // Reject invalid or partial paths
            if (calculatedPath.status != NavMeshPathStatus.PathComplete)
            {
                return false;
            }

            pathLength = CalculatePathDistance(calculatedPath.corners);
            return true;
        }

        #endregion

        #region Waypoint Configuration

        /// <summary>
        /// Assigns patrol waypoints dynamically at runtime (e.g. from encounter setup).
        /// </summary>
        public void SetPatrolWaypoints(Transform[] waypoints)
        {
            patrolWaypoints = waypoints;
            currentWaypointIndex = 0;
            Refresh();
        }

        /// <summary>
        /// Convenience params alias for SetPatrolWaypoints.
        /// </summary>
        public void SetWaypoints(params Transform[] waypoints)
        {
            SetPatrolWaypoints(waypoints);
        }

        #endregion

        #region Static Mathematical & Algorithmic Helpers

        /// <summary>
        /// Evaluates whether an agent has arrived within the effective stopping threshold.
        /// </summary>
        public static bool CheckArrival(Vector3 currentPos, Vector3 targetPos, float stoppingDistance, float tolerance)
        {
            if (!IsFinitePosition(currentPos) || !IsFinitePosition(targetPos)) return false;
            float threshold = Mathf.Max(0.01f, stoppingDistance + tolerance);
            return (currentPos - targetPos).sqrMagnitude <= (threshold * threshold);
        }

        /// <summary>
        /// Deterministically advances the waypoint index in a cyclical loop.
        /// </summary>
        public static int AdvanceWaypointIndex(int currentIndex, int totalWaypoints)
        {
            if (totalWaypoints <= 0) return 0;
            return (currentIndex + 1) % totalWaypoints;
        }

        /// <summary>
        /// Evaluates whether movement progress has stalled beyond timeout.
        /// </summary>
        public static bool CheckStuckCondition(Vector3 currentPos, Vector3 checkpointPos, float timeSinceProgress, float timeout, float minMovementThreshold = 0.2f)
        {
            if (timeout <= 0f) return false;
            float movedDist = Vector3.Distance(currentPos, checkpointPos);

            if (movedDist >= minMovementThreshold)
            {
                return false; // Made sufficient progress
            }

            return timeSinceProgress >= timeout;
        }

        /// <summary>
        /// Calculates total path distance along NavMesh corner segments.
        /// </summary>
        public static float CalculatePathDistance(Vector3[] corners)
        {
            if (corners == null || corners.Length < 2) return 0f;

            float length = 0f;
            for (int i = 1; i < corners.Length; i++)
            {
                length += Vector3.Distance(corners[i - 1], corners[i]);
            }

            return length;
        }

        /// <summary>
        /// Validates that a vector contains no NaN or Infinite coordinates.
        /// </summary>
        public static bool IsFinitePosition(Vector3 pos)
        {
            return !float.IsNaN(pos.x) && !float.IsNaN(pos.y) && !float.IsNaN(pos.z) &&
                   !float.IsInfinity(pos.x) && !float.IsInfinity(pos.y) && !float.IsInfinity(pos.z);
        }

        #endregion

        #region Editor Visualization & Gizmos

        private void OnDrawGizmosSelected()
        {
            if (patrolWaypoints != null && patrolWaypoints.Length > 0)
            {
                Gizmos.color = Color.cyan;
                for (int i = 0; i < patrolWaypoints.Length; i++)
                {
                    Transform wp = patrolWaypoints[i];
                    if (wp == null) continue;

                    Gizmos.DrawWireSphere(wp.position, 0.35f);

                    Transform nextWp = patrolWaypoints[(i + 1) % patrolWaypoints.Length];
                    if (nextWp != null)
                    {
                        Gizmos.DrawLine(wp.position, nextWp.position);
                    }
                }
            }

            if (currentDestination != Vector3.zero)
            {
                Gizmos.color = isStuck ? Color.red : Color.green;
                Gizmos.DrawWireCube(currentDestination, Vector3.one * 0.4f);
            }
        }

        #endregion
    }
}
