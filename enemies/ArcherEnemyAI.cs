using UnityEngine;
using UnityEngine.AI;

namespace JadeSeal.Archery
{
    [RequireComponent(typeof(NavMeshAgent), typeof(EnemyHealth), typeof(ArcherCombat))]
    [RequireComponent(typeof(CapsuleCollider))]
    public sealed class ArcherEnemyAI : MonoBehaviour
    {
        public enum State { Idle, Patrol, Chase, Aim, Attack, Reposition, ReturnToSpawn, Dead }

        [SerializeField] private PlayerHealth player;
        [SerializeField] private Transform[] patrolPoints;
        [SerializeField, Min(0)] private float patrolWait = 1f;
        [SerializeField, Range(1, 360)] private float detectionAngle = 360f;
        [SerializeField] private bool retreatWhenClose = true;
        [SerializeField, Min(1)] private float detectionRange = 20f;
        [SerializeField, Min(1)] private float attackRange = 12f;
        [SerializeField, Min(0.5f)] private float preferredDistance = 9f;
        [SerializeField, Min(0)] private float minimumDistance = 4f;
        [SerializeField, Min(0.1f)] private float walkingSpeed = 2f;
        [SerializeField, Min(0.1f)] private float runningSpeed = 4f;
        [SerializeField, Min(1)] private float returnThreshold = 30f;
        [SerializeField, Min(1)] private float rotationSpeed = 480f;

        private NavMeshAgent agent;
        private ArcherCombat combat;
        private EnemyHealth health;
        private NavMeshPath retreatPath;
        private readonly Vector3[] pathCorners = new Vector3[32];
        private Vector3 spawn;
        private Vector3 returnPoint;
        private float nextPathTime, nextRetreatTime, lastSeenTime;
        private int patrolIndex;
        private Vector3[] patrolPositions;
        private float patrolWaitUntil = -1f;
        private bool tracking;
        public State CurrentState { get; private set; }

        private NavMeshQueryFilter Filter => new() { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };

        private void Start()
        {
            agent = GetComponent<NavMeshAgent>();
            combat = GetComponent<ArcherCombat>();
            health = GetComponent<EnemyHealth>();
            if (!combat.enabled)
            {
                enabled = false;
                return;
            }
            retreatPath = new NavMeshPath();
            agent.updateRotation = false;
            if (!player) player = FindAnyObjectByType<PlayerHealth>();
            if (!agent.isOnNavMesh && NavMesh.SamplePosition(transform.position, out var hit, 2f, Filter))
                agent.Warp(hit.position);
            spawn = transform.position;
            if (patrolPoints != null)
            {
                var positions = new System.Collections.Generic.List<Vector3>(patrolPoints.Length);
                foreach (var point in patrolPoints)
                    if (point) positions.Add(point.position);
                patrolPositions = positions.ToArray();
            }
            if (!agent.isOnNavMesh) Debug.LogWarning("Archer spawn is outside its NavMesh.", this);
            health.Died += HandleDeath;
            health.HealthChanged += HandleDamage;
            if (health.IsDead) HandleDeath();
        }

        private void OnDestroy()
        {
            if (!health) return;
            health.Died -= HandleDeath;
            health.HealthChanged -= HandleDamage;
        }

        private void HandleDamage(int current)
        {
            if (current <= 0 || !combat.TryPlayHitReaction()) return;
            Stop();
            combat.Move(Vector3.zero);
        }

        private void HandleDeath()
        {
            if (CurrentState == State.Dead) return;
            CurrentState = State.Dead;
            Stop();
            combat.Cancel(true);
            agent.enabled = false;
            GetComponent<CapsuleCollider>().enabled = false;
        }

        private void Update()
        {
            if (CurrentState == State.Dead || combat.IsReacting) return;

            float fromSpawn = Vector3.Distance(transform.position, spawn);
            if (CurrentState == State.ReturnToSpawn && Vector3.Distance(transform.position, returnPoint) > 0.65f)
            {
                Navigate(State.ReturnToSpawn, returnPoint, runningSpeed, 0.2f);
                return;
            }

            float distance = player ? Vector3.Distance(transform.position, player.transform.position) : float.PositiveInfinity;
            if (!player || player.IsDead || distance > detectionRange || fromSpawn > returnThreshold)
            {
                bool disengaging = tracking;
                tracking = false;
                if (fromSpawn > 0.65f && (disengaging || fromSpawn > returnThreshold ||
                    (CurrentState != State.Patrol && (patrolPositions == null || patrolPositions.Length == 0))))
                {
                    BeginReturn();
                    return;
                }
                IdleOrPatrol();
                return;
            }

            Vector3 towardPlayer = player.transform.position - transform.position;
            if (!tracking && detectionAngle < 360f)
            {
                bool withinAngle = Vector3.Angle(transform.forward, Vector3.ProjectOnPlane(towardPlayer, Vector3.up)) <= detectionAngle * 0.5f;
                if (!withinAngle)
                {
                    IdleOrPatrol();
                    return;
                }
            }
            bool hasDetectionSight = combat.HasSight(player, transform.position + Vector3.up * 1.25f);
            if (!tracking && detectionAngle < 360f && !hasDetectionSight)
            {
                IdleOrPatrol();
                return;
            }
            if (hasDetectionSight) lastSeenTime = Time.time;
            if (tracking && detectionAngle < 360f && Time.time - lastSeenTime > 2f)
            {
                tracking = false;
                BeginReturn();
                return;
            }
            tracking = true;
            patrolWaitUntil = -1f;
            if (retreatWhenClose && distance < preferredDistance && TryRetreat(distance, towardPlayer)) return;

            bool clear = distance <= attackRange && hasDetectionSight;
            float preferred = Mathf.Min(preferredDistance, attackRange - 0.25f);
            bool canApproach = agent.isOnNavMesh &&
                (agent.pathPending || agent.velocity.sqrMagnitude > 0.1f);
            if (!clear || (CurrentState == State.Chase && distance > preferred + 0.4f && canApproach))
            {
                Vector3 goal = spawn + Vector3.ClampMagnitude(player.transform.position - spawn, returnThreshold - 0.25f);
                Navigate(State.Chase, goal,
                    distance > attackRange + 2f ? runningSpeed : walkingSpeed, clear ? preferred : 0.4f);
                return;
            }

            Stop();
            combat.Move(Vector3.zero);
            combat.AimAndAttack(player, Face(towardPlayer, combat.AimYawOffset) < 6f, attackRange);
            CurrentState = combat.InSequence ? State.Attack : State.Aim;
        }

        private bool TryRetreat(float distance, Vector3 towardPlayer)
        {
            if (!agent.isOnNavMesh) return false;
            if (CurrentState == State.Reposition && (agent.pathPending || agent.remainingDistance > 0.55f) &&
                !agent.isPathStale && agent.pathStatus == NavMeshPathStatus.PathComplete)
            {
                Face(towardPlayer);
                combat.Move(agent.velocity);
                return true;
            }

            if (distance >= minimumDistance || Time.time < nextRetreatTime) return false;
            nextRetreatTime = Time.time + 0.8f;
            Vector3 away = Vector3.ProjectOnPlane(-towardPlayer, Vector3.up).normalized;
            for (int index = 0; index < 3; index++)
            {
                float angle = index == 0 ? 0 : index == 1 ? 65 : -65;
                Vector3 candidate = transform.position + Quaternion.Euler(0, angle, 0) * away *
                                    Mathf.Max(2f, preferredDistance - distance);
                if (!NavMesh.SamplePosition(candidate, out var hit, 2f, Filter) ||
                    Vector3.Distance(hit.position, player.transform.position) < distance + 1f ||
                    !agent.CalculatePath(hit.position, retreatPath) ||
                    !PathWithinLeash()) continue;

                agent.speed = walkingSpeed;
                agent.stoppingDistance = 0.25f;
                if (!agent.SetPath(retreatPath)) continue;
                agent.isStopped = false;
                CurrentState = State.Reposition;
                combat.Cancel();
                return true;
            }
            return false;
        }

        private void BeginReturn()
        {
            returnPoint = spawn;
            if (patrolPositions != null)
            {
                for (int i = 0; i < patrolPositions.Length; i++)
                {
                    if (i > 0 && (transform.position - patrolPositions[i]).sqrMagnitude >=
                        (transform.position - returnPoint).sqrMagnitude) continue;
                    patrolIndex = i;
                    returnPoint = patrolPositions[i];
                }
            }
            Navigate(State.ReturnToSpawn, returnPoint, runningSpeed, 0.2f);
        }

        private bool PathWithinLeash()
        {
            if (retreatPath.status != NavMeshPathStatus.PathComplete) return false;
            int count = retreatPath.GetCornersNonAlloc(pathCorners);
            if (count == 0 || count == pathCorners.Length) return false;
            for (int i = 0; i < count; i++)
                if ((pathCorners[i] - spawn).sqrMagnitude > returnThreshold * returnThreshold) return false;
            return true;
        }

        private void IdleOrPatrol()
        {
            if (patrolPositions != null && patrolPositions.Length > 0)
            {
                if ((transform.position - patrolPositions[patrolIndex]).sqrMagnitude < 0.49f)
                {
                    if (patrolWaitUntil < 0f) patrolWaitUntil = Time.time + patrolWait;
                    if (Time.time < patrolWaitUntil)
                    {
                        CurrentState = State.Patrol;
                        Stop();
                        combat.Cancel();
                        combat.Move(Vector3.zero);
                        return;
                    }
                    patrolIndex = (patrolIndex + 1) % patrolPositions.Length;
                    patrolWaitUntil = -1f;
                }
                Navigate(State.Patrol, patrolPositions[patrolIndex], walkingSpeed, 0.3f);
                return;
            }
            CurrentState = State.Idle;
            Stop();
            combat.Cancel();
            combat.Move(Vector3.zero);
        }

        private void Navigate(State state, Vector3 destination, float speed, float stoppingDistance)
        {
            bool changed = CurrentState != state;
            CurrentState = state;
            combat.Cancel();
            if (!agent.isOnNavMesh) return;

            agent.isStopped = false;
            agent.speed = speed;
            agent.stoppingDistance = stoppingDistance;
            if (changed || Time.time >= nextPathTime)
            {
                nextPathTime = Time.time + (state == State.Chase ? 0.35f : 0.8f);
                if (state != State.Chase && agent.hasPath && !agent.isPathStale &&
                    agent.pathStatus == NavMeshPathStatus.PathComplete &&
                    (agent.destination - destination).sqrMagnitude < 0.1f)
                {
                    Face(agent.desiredVelocity);
                    combat.Move(agent.velocity);
                    return;
                }
                if (NavMesh.SamplePosition(destination, out var hit, state == State.Chase ? 2f : 0.5f, Filter) &&
                    agent.CalculatePath(hit.position, retreatPath) &&
                    (state == State.ReturnToSpawn ? retreatPath.status == NavMeshPathStatus.PathComplete : PathWithinLeash()))
                    agent.SetPath(retreatPath);
                else
                    agent.ResetPath();
            }
            Face(agent.desiredVelocity);
            combat.Move(agent.velocity);
        }

        private float Face(Vector3 direction, float yawOffset = 0f)
        {
            direction.y = 0;
            if (direction.sqrMagnitude < 0.001f) return 0;
            var desired = Quaternion.LookRotation(direction) * Quaternion.Euler(0, yawOffset, 0);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, desired, rotationSpeed * Time.deltaTime);
            return Quaternion.Angle(transform.rotation, desired);
        }

        private void Stop()
        {
            if (!agent.isOnNavMesh || agent.isStopped) return;
            agent.isStopped = true;
            agent.ResetPath();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRange);
            if (detectionAngle < 360f)
            {
                var start = transform.position + Vector3.up * 0.1f;
                Gizmos.DrawRay(start, Quaternion.Euler(0, detectionAngle * 0.5f, 0) * transform.forward * detectionRange);
                Gizmos.DrawRay(start, Quaternion.Euler(0, -detectionAngle * 0.5f, 0) * transform.forward * detectionRange);
            }
            if (patrolPoints != null)
                foreach (var point in patrolPoints)
                    if (point) Gizmos.DrawWireSphere(point.position, 0.35f);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }
}
