using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using JadeSeal.EnemyAI.Common;
using JadeSeal.Combat;

namespace JadeSeal.EnemyAI.RoyalGuard
{
    public enum GuardState
    {
        Idle,
        Patrol,
        Investigate,
        Chase,
        MeleeAttack,
        Blocking,
        Recover,
        Dead
    }

    /// <summary>
    /// Autonomous Royal Guard Controller (Heavy Melee Defender).
    /// Assigned to Hiruni. Manages military waypoint patrol, aggressive melee chase,
    /// reactive block stance (-75% damage reduction), and sword strikes.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(EnemyPerception), typeof(RoyalGuardHealth))]
    public class RoyalGuardAgent : MonoBehaviour
    {
        [Header("Locomotion Speeds")]
        [SerializeField] private float walkSpeed = 2.2f;
        [SerializeField] private float runSpeed = 4.8f;

        [Header("Melee Combat")]
        [SerializeField] private float meleeAttackRange = 2.3f;
        [SerializeField] private float attackDamage = 30f;
        [SerializeField] private float attackCooldown = 1.8f;
        [SerializeField] private float blockDuration = 1.5f;

        [Header("Patrol Settings")]
        [SerializeField] private List<Transform> patrolWaypoints = new List<Transform>();
        [SerializeField] private float waypointTolerance = 1.5f;

        public GuardState CurrentState { get; private set; } = GuardState.Idle;
        public bool IsBlocking => CurrentState == GuardState.Blocking;

        private NavMeshAgent navAgent;
        private EnemyPerception perception;
        private RoyalGuardHealth health;
        private float lastAttackTime = -10f;
        private float stateTimer = 0f;
        private int currentWaypointIndex = 0;
        private bool isDead = false;

        public void SetWaypoints(List<Transform> points)
        {
            patrolWaypoints = new List<Transform>(points);
        }

        private void Awake()
        {
            navAgent = GetComponent<NavMeshAgent>();
            if (navAgent == null) navAgent = gameObject.AddComponent<NavMeshAgent>();

            perception = GetComponent<EnemyPerception>();
            if (perception == null) perception = gameObject.AddComponent<EnemyPerception>();

            health = GetComponent<RoyalGuardHealth>();
            if (health == null) health = gameObject.AddComponent<RoyalGuardHealth>();

            var col = GetComponent<CapsuleCollider>();
            if (col == null)
            {
                col = gameObject.AddComponent<CapsuleCollider>();
                col.radius = 0.5f;
                col.height = 1.95f;
                col.center = new Vector3(0, 0.98f, 0);
            }

            health.OnDied += HandleDeath;
            health.OnBlocked += HandleBlockedAttack;
        }

        private void OnDestroy()
        {
            if (health != null)
            {
                health.OnDied -= HandleDeath;
                health.OnBlocked -= HandleBlockedAttack;
            }
        }

        private void Update()
        {
            if (isDead) return;

            perception.UpdatePerception();
            ExecuteStateMachine();
        }

        private void ExecuteStateMachine()
        {
            switch (CurrentState)
            {
                case GuardState.Idle:
                    UpdateIdle();
                    break;
                case GuardState.Patrol:
                    UpdatePatrol();
                    break;
                case GuardState.Investigate:
                    UpdateInvestigate();
                    break;
                case GuardState.Chase:
                    UpdateChase();
                    break;
                case GuardState.MeleeAttack:
                    UpdateMeleeAttack();
                    break;
                case GuardState.Blocking:
                    UpdateBlocking();
                    break;
                case GuardState.Recover:
                    UpdateRecover();
                    break;
            }
        }

        private void UpdateIdle()
        {
            stateTimer += Time.deltaTime;
            if (perception.HasVisualContact)
            {
                SwitchState(GuardState.Chase);
                return;
            }
            if (perception.HasAuditoryDisturbance)
            {
                SwitchState(GuardState.Investigate);
                return;
            }
            if (stateTimer > 2.0f && patrolWaypoints.Count > 0)
            {
                SwitchState(GuardState.Patrol);
            }
        }

        private void UpdatePatrol()
        {
            if (perception.HasVisualContact)
            {
                SwitchState(GuardState.Chase);
                return;
            }
            if (perception.HasAuditoryDisturbance)
            {
                SwitchState(GuardState.Investigate);
                return;
            }

            if (patrolWaypoints.Count == 0) return;

            navAgent.speed = walkSpeed;
            Transform targetWp = patrolWaypoints[currentWaypointIndex];
            if (targetWp != null)
            {
                if (!navAgent.hasPath || Vector3.Distance(navAgent.destination, targetWp.position) > 1.0f)
                {
                    navAgent.SetDestination(targetWp.position);
                }

                if (!navAgent.pathPending && navAgent.remainingDistance <= waypointTolerance)
                {
                    currentWaypointIndex = (currentWaypointIndex + 1) % patrolWaypoints.Count;
                    SwitchState(GuardState.Idle);
                }
            }
        }

        private void UpdateInvestigate()
        {
            if (perception.HasVisualContact)
            {
                SwitchState(GuardState.Chase);
                return;
            }

            navAgent.speed = walkSpeed * 1.2f;
            if (Vector3.Distance(transform.position, perception.LastKnownPosition) > 1.5f)
            {
                navAgent.SetDestination(perception.LastKnownPosition);
            }
            else
            {
                stateTimer += Time.deltaTime;
                if (stateTimer > 3.0f)
                {
                    SwitchState(GuardState.Patrol);
                }
            }
        }

        private void UpdateChase()
        {
            if (!perception.IsTargetPerceived)
            {
                SwitchState(GuardState.Investigate);
                return;
            }

            Transform target = perception.CurrentTarget;
            if (target == null) return;

            float dist = Vector3.Distance(transform.position, target.position);
            if (dist <= meleeAttackRange)
            {
                if (Time.time >= lastAttackTime + attackCooldown)
                {
                    SwitchState(GuardState.MeleeAttack);
                }
                else
                {
                    // If in melee range and attack on cooldown, raise guard / block!
                    SwitchState(GuardState.Blocking);
                }
                return;
            }

            navAgent.speed = runSpeed;
            navAgent.stoppingDistance = meleeAttackRange * 0.8f;
            navAgent.SetDestination(target.position);
        }

        private void UpdateMeleeAttack()
        {
            navAgent.ResetPath();
            Transform target = perception.CurrentTarget;
            if (target != null)
            {
                Vector3 lookDir = (target.position - transform.position);
                lookDir.y = 0;
                if (lookDir.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 12f);
                }
            }

            stateTimer += Time.deltaTime;
            // Strike frame at 0.4s
            if (stateTimer >= 0.4f && lastAttackTime < Time.time - 0.5f)
            {
                lastAttackTime = Time.time;
                PerformMeleeSlash();
            }

            if (stateTimer >= 0.9f)
            {
                SwitchState(GuardState.Recover);
            }
        }

        private void PerformMeleeSlash()
        {
            Transform target = perception.CurrentTarget;
            if (target == null) return;

            float dist = Vector3.Distance(transform.position, target.position);
            if (dist <= meleeAttackRange + 0.5f)
            {
                var damageable = target.GetComponent<IDamageable>();
                if (damageable != null && !damageable.IsDead)
                {
                    Vector3 hitPoint = target.position + Vector3.up * 1.2f;
                    Vector3 hitNormal = (transform.position - target.position).normalized;
                    damageable.TakeDamage(attackDamage, hitPoint, hitNormal);
                    Debug.Log($"<color=#FF9900>[RoyalGuardMelee]</color> Slashed target for {attackDamage} physical damage!");
                }
            }
        }

        private void UpdateBlocking()
        {
            navAgent.ResetPath();
            Transform target = perception.CurrentTarget;
            if (target != null)
            {
                Vector3 lookDir = (target.position - transform.position);
                lookDir.y = 0;
                if (lookDir.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 8f);
                }
            }

            stateTimer += Time.deltaTime;
            if (stateTimer >= blockDuration || Time.time >= lastAttackTime + attackCooldown)
            {
                SwitchState(GuardState.Chase);
            }
        }

        private void UpdateRecover()
        {
            stateTimer += Time.deltaTime;
            if (stateTimer >= 0.6f)
            {
                SwitchState(GuardState.Chase);
            }
        }

        public void TriggerHitReaction()
        {
            // When taking front damage, 60% chance to immediately enter blocking stance
            if (Random.value < 0.6f && CurrentState != GuardState.Blocking)
            {
                SwitchState(GuardState.Blocking);
            }
        }

        private void HandleBlockedAttack()
        {
            Debug.Log("<color=#00FF99>[RoyalGuardAgent]</color> Blocked incoming strike successfully!");
        }

        private void SwitchState(GuardState newState)
        {
            CurrentState = newState;
            stateTimer = 0f;
        }

        private void HandleDeath()
        {
            isDead = true;
            CurrentState = GuardState.Dead;
            navAgent.isStopped = true;
            navAgent.enabled = false;
        }
    }
}
