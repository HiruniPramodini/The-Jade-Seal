using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using JadeSeal.EnemyAI.BehaviorTree;
using JadeSeal.EnemyAI.Sensory;
using JadeSeal.EnemyAI.Tactical;
using JadeSeal.EnemyAI.Combat;

namespace JadeSeal.EnemyAI.Core
{
    public enum ArcherState
    {
        Idle,
        Patrol,
        Kiting,
        Aiming,
        Shooting,
        Repositioning,
        Dead
    }

    /// <summary>
    /// Autonomous Mountain Archer Agent Controller.
    /// Implements:
    /// 1. Modular Behavior Tree decision architecture (SE3062 Rubric).
    /// 2. Spatial heuristic high-ground vantage point search.
    /// 3. Tactical kiting (retreat mechanics when player approaches).
    /// 4. Kinematic predictive lead aim solver.
    /// 5. Decoupled visual & animator bridge.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(MountainArcherPerception))]
    public class MountainArcherAgent : MonoBehaviour
    {
        [Header("Tactical Range Settings")]
        [Tooltip("Minimum distance before archer retreats/kites backwards.")]
        [SerializeField] private float minKiteDistance = 7f;

        [Tooltip("Ideal maximum range for shooting arrows.")]
        [SerializeField] private float maxShootDistance = 22f;

        [Tooltip("Search radius when seeking high ground vantage points.")]
        [SerializeField] private float tacticalSearchRadius = 15f;

        [Header("Combat & Archery")]
        [SerializeField] private float shootCooldown = 2.2f;
        [SerializeField] private float aimDrawDuration = 0.8f;
        [SerializeField] private float arrowSpeed = 30f;
        [SerializeField] private Transform bowFirePoint;
        [SerializeField] private GameObject arrowPrefab;

        [Header("Heuristic Evaluation Weights")]
        [SerializeField] private float weightElevation = 1.8f;
        [SerializeField] private float weightDistance = 1.2f;
        [SerializeField] private float weightLineOfSight = 2.0f;
        [SerializeField] private LayerMask obstacleMask = ~0;

        [Header("Patrol Waypoints")]
        [SerializeField] private List<Transform> mountainPatrolPoints = new List<Transform>();
        [SerializeField] private float waypointTolerance = 1.5f;

        public void SetPatrolWaypoints(List<Transform> points)
        {
            mountainPatrolPoints = new List<Transform>(points);
        }

        // Components
        private NavMeshAgent navAgent;
        private MountainArcherPerception perception;
        private BTNode behaviorTreeRoot;

        // Runtime State
        public ArcherState CurrentState { get; private set; } = ArcherState.Idle;
        private float lastShotTime = -10f;
        private float currentAimTimer = 0f;
        private int currentWaypointIndex = 0;
        private Vector3 currentVantageTarget;
        private Vector3 targetPreviousPos;
        private Vector3 targetVelocity;

        private void Awake()
        {
            navAgent = GetComponent<NavMeshAgent>();
            perception = GetComponent<MountainArcherPerception>();
            BuildBehaviorTree();
        }

        private void Start()
        {
            if (bowFirePoint == null)
            {
                // Default fire point at chest level
                GameObject fp = new GameObject("DefaultBowFirePoint");
                fp.transform.SetParent(transform);
                fp.transform.localPosition = new Vector3(0, 1.4f, 0.5f);
                bowFirePoint = fp.transform;
            }

            CreateDefaultArrowPrefabIfNeeded();
        }

        private void BuildBehaviorTree()
        {
            // Root Selector: Priority of behaviors
            // 1. Tactical Kiting (Retreat if player gets too close)
            // 2. Ranged Combat (Aim and shoot if in range and has LOS)
            // 3. Tactical Reposition (Seek high ground vantage point if lost LOS or too far)
            // 4. Mountain Patrol (Patrol waypoints when idle)

            var rootChildren = new List<BTNode>
            {
                new BTSequence(new List<BTNode>
                {
                    new BTActionNode(CheckShouldKite),
                    new BTActionNode(ExecuteTacticalKite)
                }),
                new BTSequence(new List<BTNode>
                {
                    new BTActionNode(CheckCanEngageCombat),
                    new BTActionNode(ExecuteCombatAimAndShoot)
                }),
                new BTSequence(new List<BTNode>
                {
                    new BTActionNode(CheckShouldRepositionToVantage),
                    new BTActionNode(ExecuteRepositionToVantage)
                }),
                new BTActionNode(ExecuteMountainPatrol)
            };

            behaviorTreeRoot = new BTSelector(rootChildren);
        }

        private void Update()
        {
            perception.UpdatePerception();
            TrackTargetVelocity();
            behaviorTreeRoot?.Evaluate();
        }

        private void TrackTargetVelocity()
        {
            if (perception.CurrentTarget != null)
            {
                Vector3 currentPos = perception.CurrentTarget.position;
                if (Time.deltaTime > 0f)
                {
                    targetVelocity = (currentPos - targetPreviousPos) / Time.deltaTime;
                }
                targetPreviousPos = currentPos;
            }
            else
            {
                targetVelocity = Vector3.zero;
            }
        }

        #region Behavior Tree Action Implementations

        private NodeState CheckShouldKite()
        {
            if (!perception.IsTargetPerceived || perception.CurrentTarget == null)
                return NodeState.FAILURE;

            float dist = Vector3.Distance(transform.position, perception.CurrentTarget.position);
            return (dist < minKiteDistance) ? NodeState.SUCCESS : NodeState.FAILURE;
        }

        private NodeState ExecuteTacticalKite()
        {
            CurrentState = ArcherState.Kiting;
            var config = GetEvaluationConfig();

            if (TacticalPositionEvaluator.FindTacticalKitePosition(
                transform.position,
                perception.CurrentTarget.position,
                tacticalSearchRadius * 0.7f,
                config,
                out Vector3 kitePos))
            {
                navAgent.isStopped = false;
                navAgent.SetDestination(kitePos);
                return NodeState.RUNNING;
            }

            return NodeState.FAILURE;
        }

        private NodeState CheckCanEngageCombat()
        {
            if (!perception.IsTargetPerceived || perception.CurrentTarget == null)
                return NodeState.FAILURE;

            if (!perception.HasLineOfSight)
                return NodeState.FAILURE;

            float dist = Vector3.Distance(transform.position, perception.CurrentTarget.position);
            return (dist >= minKiteDistance && dist <= maxShootDistance) ? NodeState.SUCCESS : NodeState.FAILURE;
        }

        private NodeState ExecuteCombatAimAndShoot()
        {
            navAgent.isStopped = true;

            // Compute predictive lead aim vector
            Vector3 aimTarget = PredictiveAimSolver.CalculateLeadAimPoint(
                bowFirePoint.position,
                perception.CurrentTarget.position + Vector3.up * 1.0f,
                targetVelocity,
                arrowSpeed);

            // Rotate smoothly towards predicted lead position
            Vector3 lookDir = (aimTarget - transform.position);
            lookDir.y = 0;
            if (lookDir.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 8f);
            }

            // Draw Bow & Fire Timing
            if (Time.time >= lastShotTime + shootCooldown)
            {
                CurrentState = ArcherState.Aiming;
                currentAimTimer += Time.deltaTime;

                if (currentAimTimer >= aimDrawDuration)
                {
                    FireArrow(aimTarget);
                    lastShotTime = Time.time;
                    currentAimTimer = 0f;
                    CurrentState = ArcherState.Shooting;
                    return NodeState.SUCCESS;
                }

                return NodeState.RUNNING;
            }

            CurrentState = ArcherState.Aiming;
            return NodeState.RUNNING;
        }

        private NodeState CheckShouldRepositionToVantage()
        {
            if (!perception.IsTargetPerceived) return NodeState.FAILURE;

            float dist = Vector3.Distance(transform.position, perception.LastKnownPosition);
            bool outOfRange = dist > maxShootDistance;
            bool noLOS = !perception.HasLineOfSight;

            return (outOfRange || noLOS) ? NodeState.SUCCESS : NodeState.FAILURE;
        }

        private NodeState ExecuteRepositionToVantage()
        {
            CurrentState = ArcherState.Repositioning;
            var config = GetEvaluationConfig();

            if (TacticalPositionEvaluator.FindBestVantagePosition(
                transform.position,
                perception.LastKnownPosition,
                tacticalSearchRadius,
                config,
                out Vector3 bestVantage))
            {
                currentVantageTarget = bestVantage;
                navAgent.isStopped = false;
                navAgent.SetDestination(bestVantage);

                if (Vector3.Distance(transform.position, bestVantage) <= waypointTolerance)
                {
                    return NodeState.SUCCESS;
                }
                return NodeState.RUNNING;
            }

            // Fallback: move towards last known position
            navAgent.isStopped = false;
            navAgent.SetDestination(perception.LastKnownPosition);
            return NodeState.RUNNING;
        }

        private NodeState ExecuteMountainPatrol()
        {
            CurrentState = ArcherState.Patrol;

            if (mountainPatrolPoints.Count == 0)
            {
                CurrentState = ArcherState.Idle;
                navAgent.isStopped = true;
                return NodeState.SUCCESS;
            }

            navAgent.isStopped = false;
            Transform targetWaypoint = mountainPatrolPoints[currentWaypointIndex];
            navAgent.SetDestination(targetWaypoint.position);

            if (Vector3.Distance(transform.position, targetWaypoint.position) <= waypointTolerance)
            {
                currentWaypointIndex = (currentWaypointIndex + 1) % mountainPatrolPoints.Count;
            }

            return NodeState.RUNNING;
        }

        #endregion

        private void FireArrow(Vector3 targetLeadPoint)
        {
            if (arrowPrefab == null || bowFirePoint == null) return;

            GameObject arrowObj = Instantiate(arrowPrefab, bowFirePoint.position, bowFirePoint.rotation);
            ArrowProjectile arrow = arrowObj.GetComponent<ArrowProjectile>();
            if (arrow == null) arrow = arrowObj.AddComponent<ArrowProjectile>();

            Vector3 fireDirection = (targetLeadPoint - bowFirePoint.position).normalized;
            arrow.Launch(fireDirection, arrowSpeed);
            Debug.Log($"[MountainArcher] Fired predictive arrow towards {targetLeadPoint} (Speed: {arrowSpeed}m/s)");
        }

        private TacticalPositionEvaluator.EvaluationParams GetEvaluationConfig()
        {
            return new TacticalPositionEvaluator.EvaluationParams
            {
                idealMinDistance = minKiteDistance,
                idealMaxDistance = maxShootDistance,
                weightElevation = weightElevation,
                weightDistance = weightDistance,
                weightLineOfSight = weightLineOfSight,
                weightCover = 1.0f,
                obstacleMask = obstacleMask
            };
        }

        private void CreateDefaultArrowPrefabIfNeeded()
        {
            if (arrowPrefab != null) return;

            // Generate procedural arrow model if no external asset assigned
            arrowPrefab = new GameObject("ProceduralArrowPrefab");
            var capsule = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            capsule.transform.SetParent(arrowPrefab.transform);
            capsule.transform.localScale = new Vector3(0.04f, 0.4f, 0.04f);
            capsule.transform.localRotation = Quaternion.Euler(90, 0, 0);

            var col = arrowPrefab.AddComponent<CapsuleCollider>();
            col.radius = 0.05f;
            col.height = 0.8f;
            col.direction = 2; // Z-axis

            var rb = arrowPrefab.AddComponent<Rigidbody>();
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            arrowPrefab.AddComponent<ArrowProjectile>();
            arrowPrefab.SetActive(false);
        }

        #region In-Engine Gizmos & Diagnostics

        private void OnDrawGizmosSelected()
        {
            Vector3 center = transform.position;

            // Min Kiting Range (Red)
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(center, minKiteDistance);

            // Max Shooting Range (Green)
            Gizmos.color = new Color(0.2f, 1f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(center, maxShootDistance);

            // Tactical Search Radius (Cyan)
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.3f);
            Gizmos.DrawWireSphere(center, tacticalSearchRadius);

            // Line of Sight & Predictive Lead Ray (Yellow)
            if (perception != null && perception.CurrentTarget != null)
            {
                Gizmos.color = perception.HasLineOfSight ? Color.green : Color.red;
                Gizmos.DrawLine(perception.EyePosition, perception.CurrentTarget.position + Vector3.up * 1f);

                Gizmos.color = Color.yellow;
                Vector3 leadPoint = PredictiveAimSolver.CalculateLeadAimPoint(
                    center + Vector3.up * 1.5f,
                    perception.CurrentTarget.position + Vector3.up * 1f,
                    targetVelocity,
                    arrowSpeed);
                Gizmos.DrawWireSphere(leadPoint, 0.4f);
                Gizmos.DrawLine(center + Vector3.up * 1.5f, leadPoint);
            }
        }

        private void OnGUI()
        {
            GUI.Box(new Rect(20, 20, 360, 250), GUIContent.none);
            GUILayout.BeginArea(new Rect(25, 25, 350, 240));

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 14, fontStyle = FontStyle.Bold };
            GUIStyle textStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 12 };

            GUILayout.Label("<color=#00FF99>🏹 MOUNTAIN ARCHER AI (SE3062)</color>", titleStyle);
            GUILayout.Label($"<b>Behavior State:</b> <color=yellow>{CurrentState}</color>", textStyle);

            float speed = navAgent != null ? navAgent.velocity.magnitude : 0f;
            GUILayout.Label($"<b>Locomotion Speed:</b> {speed:F2} m/s", textStyle);

            if (perception != null && perception.CurrentTarget != null)
            {
                float dist = Vector3.Distance(transform.position, perception.CurrentTarget.position);
                string losColor = perception.HasLineOfSight ? "#00FF66" : "#FF4444";
                string losText = perception.HasLineOfSight ? "CLEAR" : "BLOCKED";
                GUILayout.Label($"<b>Target Distance:</b> {dist:F1} m (LOS: <color={losColor}><b>{losText}</b></color>)", textStyle);
                GUILayout.Label($"<b>Target Velocity:</b> {targetVelocity.magnitude:F1} m/s", textStyle);
            }
            else
            {
                GUILayout.Label("<b>Target:</b> <color=gray>Patrolling Mountain Pass</color>", textStyle);
            }

            GUILayout.Space(8);
            if (GUILayout.Button("⚡ Trigger Player Rush (7m - Test Kiting)", GUILayout.Height(30)))
            {
                TriggerTestTarget(6.5f);
            }
            GUILayout.Space(4);
            if (GUILayout.Button("🎯 Move Player Far (16m - Test Archery)", GUILayout.Height(30)))
            {
                TriggerTestTarget(16f);
            }

            GUILayout.EndArea();
        }

        private void TriggerTestTarget(float distance)
        {
            GameObject testTarget = GameObject.Find("Test_Player_Target");
            if (testTarget == null)
            {
                testTarget = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                testTarget.name = "Test_Player_Target";
                testTarget.tag = "Player";
                testTarget.GetComponent<Renderer>().material.color = Color.cyan;
            }

            Vector3 targetPos = transform.position + transform.forward * distance;
            if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            {
                targetPos = hit.position;
            }
            testTarget.transform.position = targetPos;
            perception.SetTarget(testTarget.transform);
        }

        #endregion
    }
}
