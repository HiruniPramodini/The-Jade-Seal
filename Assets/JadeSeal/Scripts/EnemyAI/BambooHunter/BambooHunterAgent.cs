using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using JadeSeal.EnemyAI.Common;
using JadeSeal.EnemyAI.Combat;
using JadeSeal.Combat;

namespace JadeSeal.EnemyAI.BambooHunter
{
    public enum HunterState
    {
        Idle,
        Patrol,
        InvestigateSound,
        Search,
        Chase,
        Kiting,
        Aiming,
        Shooting,
        Dead
    }

    /// <summary>
    /// Autonomous Bamboo Hunter Controller (Stealthy Ranged Hunter).
    /// Assigned to Keshika. Manages bamboo forest patrol, acoustic footstep tracking,
    /// tactical cover repositioning, distance keeping (kiting), and archery combat.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(EnemyPerception), typeof(BambooHunterHealth))]
    public class BambooHunterAgent : MonoBehaviour
    {
        [Header("Locomotion Speeds")]
        [SerializeField] private float walkSpeed = 2.4f;
        [SerializeField] private float runSpeed = 5.2f;

        [Header("Archery Standoff & Range")]
        [SerializeField] private float minKiteDistance = 6.5f;
        [SerializeField] private float idealShootDistance = 16f;
        [SerializeField] private float maxShootDistance = 22f;

        [Header("Archery Combat")]
        [SerializeField] private float shootCooldown = 2.0f;
        [SerializeField] private float aimDuration = 0.7f;
        [SerializeField] private float arrowSpeed = 28f;
        [SerializeField] private float arrowDamage = 22f;
        [SerializeField] private Transform bowFirePoint;
        [SerializeField] private GameObject arrowPrefab;

        [Header("Audio")]
        [SerializeField] private AudioClip bowShootSound;

        [Header("Patrol Settings")]
        [SerializeField] private List<Transform> forestPatrolWaypoints = new List<Transform>();
        [SerializeField] private float waypointTolerance = 1.5f;

        public HunterState CurrentState { get; private set; } = HunterState.Idle;

        private NavMeshAgent navAgent;
        private EnemyPerception perception;
        private BambooHunterHealth health;
        private AudioSource audioSource;

        private float lastShotTime = -10f;
        private float stateTimer = 0f;
        private int currentWaypointIndex = 0;
        private bool isDead = false;

        public void SetWaypoints(List<Transform> points)
        {
            forestPatrolWaypoints = new List<Transform>(points);
        }

        private void Awake()
        {
            navAgent = GetComponent<NavMeshAgent>();
            if (navAgent == null) navAgent = gameObject.AddComponent<NavMeshAgent>();

            perception = GetComponent<EnemyPerception>();
            if (perception == null) perception = gameObject.AddComponent<EnemyPerception>();

            health = GetComponent<BambooHunterHealth>();
            if (health == null) health = gameObject.AddComponent<BambooHunterHealth>();

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

            var col = GetComponent<CapsuleCollider>();
            if (col == null)
            {
                col = gameObject.AddComponent<CapsuleCollider>();
                col.radius = 0.45f;
                col.height = 1.85f;
                col.center = new Vector3(0, 0.92f, 0);
            }

            health.OnDied += HandleDeath;

            if (bowFirePoint == null)
            {
                GameObject fp = new GameObject("BowFirePoint");
                fp.transform.SetParent(transform);
                fp.transform.localPosition = new Vector3(0.2f, 1.3f, 0.5f);
                bowFirePoint = fp.transform;
            }
        }

        private void OnDestroy()
        {
            if (health != null) health.OnDied -= HandleDeath;
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
                case HunterState.Idle:
                    UpdateIdle();
                    break;
                case HunterState.Patrol:
                    UpdatePatrol();
                    break;
                case HunterState.InvestigateSound:
                    UpdateInvestigateSound();
                    break;
                case HunterState.Search:
                    UpdateSearch();
                    break;
                case HunterState.Chase:
                    UpdateChase();
                    break;
                case HunterState.Kiting:
                    UpdateKiting();
                    break;
                case HunterState.Aiming:
                    UpdateAiming();
                    break;
                case HunterState.Shooting:
                    UpdateShooting();
                    break;
            }
        }

        private void UpdateIdle()
        {
            stateTimer += Time.deltaTime;
            if (perception.HasVisualContact)
            {
                SwitchState(HunterState.Chase);
                return;
            }
            if (perception.HasAuditoryDisturbance)
            {
                SwitchState(HunterState.InvestigateSound);
                return;
            }
            if (stateTimer > 2.0f && forestPatrolWaypoints.Count > 0)
            {
                SwitchState(HunterState.Patrol);
            }
        }

        private void UpdatePatrol()
        {
            if (perception.HasVisualContact)
            {
                SwitchState(HunterState.Chase);
                return;
            }
            if (perception.HasAuditoryDisturbance)
            {
                SwitchState(HunterState.InvestigateSound);
                return;
            }

            if (forestPatrolWaypoints.Count == 0) return;

            navAgent.speed = walkSpeed;
            Transform targetWp = forestPatrolWaypoints[currentWaypointIndex];
            if (targetWp != null)
            {
                if (!navAgent.hasPath || Vector3.Distance(navAgent.destination, targetWp.position) > 1.0f)
                {
                    navAgent.SetDestination(targetWp.position);
                }

                if (!navAgent.pathPending && navAgent.remainingDistance <= waypointTolerance)
                {
                    currentWaypointIndex = (currentWaypointIndex + 1) % forestPatrolWaypoints.Count;
                    SwitchState(HunterState.Idle);
                }
            }
        }

        private void UpdateInvestigateSound()
        {
            if (perception.HasVisualContact)
            {
                SwitchState(HunterState.Chase);
                return;
            }

            navAgent.speed = walkSpeed * 1.3f;
            if (Vector3.Distance(transform.position, perception.LastKnownPosition) > 1.5f)
            {
                navAgent.SetDestination(perception.LastKnownPosition);
            }
            else
            {
                SwitchState(HunterState.Search);
            }
        }

        private void UpdateSearch()
        {
            if (perception.HasVisualContact)
            {
                SwitchState(HunterState.Chase);
                return;
            }

            stateTimer += Time.deltaTime;
            transform.Rotate(0, 45f * Time.deltaTime, 0);

            if (stateTimer > 3.5f)
            {
                SwitchState(HunterState.Patrol);
            }
        }

        private void UpdateChase()
        {
            if (!perception.IsTargetPerceived)
            {
                SwitchState(HunterState.Search);
                return;
            }

            Transform target = perception.CurrentTarget;
            if (target == null) return;

            float dist = Vector3.Distance(transform.position, target.position);

            // Too close: Kite backwards!
            if (dist < minKiteDistance)
            {
                SwitchState(HunterState.Kiting);
                return;
            }

            // In ideal archery range with LOS: Aim and Shoot!
            if (dist <= maxShootDistance && perception.HasLineOfSight)
            {
                if (Time.time >= lastShotTime + shootCooldown)
                {
                    SwitchState(HunterState.Aiming);
                }
                else
                {
                    navAgent.ResetPath();
                }
                return;
            }

            // Reposition towards player
            navAgent.speed = runSpeed;
            navAgent.stoppingDistance = minKiteDistance + 1f;
            navAgent.SetDestination(target.position);
        }

        private void UpdateKiting()
        {
            Transform target = perception.CurrentTarget;
            if (target == null)
            {
                SwitchState(HunterState.Search);
                return;
            }

            float dist = Vector3.Distance(transform.position, target.position);
            if (dist >= minKiteDistance + 3.0f)
            {
                SwitchState(HunterState.Chase);
                return;
            }

            navAgent.speed = runSpeed;
            Vector3 retreatDir = (transform.position - target.position).normalized;
            Vector3 kiteDest = transform.position + retreatDir * 6f;

            if (NavMesh.SamplePosition(kiteDest, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            {
                navAgent.SetDestination(hit.position);
            }
        }

        private void UpdateAiming()
        {
            navAgent.ResetPath();
            Transform target = perception.CurrentTarget;
            if (target != null)
            {
                Vector3 lookDir = (target.position - transform.position);
                lookDir.y = 0;
                if (lookDir.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 10f);
                }
            }

            stateTimer += Time.deltaTime;
            if (stateTimer >= aimDuration)
            {
                SwitchState(HunterState.Shooting);
            }
        }

        private void UpdateShooting()
        {
            lastShotTime = Time.time;
            FireArrow();
            SwitchState(HunterState.Chase);
        }

        private void FireArrow()
        {
            Transform target = perception.CurrentTarget;
            if (target == null) return;

            Vector3 spawnPos = (bowFirePoint != null) ? bowFirePoint.position : transform.position + Vector3.up * 1.3f + transform.forward * 0.5f;
            Vector3 targetCenter = target.position + Vector3.up * 1.0f;
            Vector3 aimDir = (targetCenter - spawnPos).normalized;

            if (arrowPrefab != null)
            {
                GameObject arrowObj = Instantiate(arrowPrefab, spawnPos, Quaternion.LookRotation(aimDir));
                var arrowComp = arrowObj.GetComponent<ArrowProjectile>();
                if (arrowComp != null)
                {
                    arrowComp.SetShooter(GetComponent<Collider>());
                    arrowComp.Launch(aimDir, arrowSpeed);
                }
            }
            else
            {
                // Create simple clean dynamic arrow projectile
                CreateProceduralArrow(spawnPos, aimDir);
            }

            if (bowShootSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(bowShootSound);
            }
            Debug.Log("<color=#00FF99>[BambooHunter]</color> Fired arrow at player!");
        }

        private void CreateProceduralArrow(Vector3 origin, Vector3 dir)
        {
            GameObject arrow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            arrow.name = "BambooHunter_Arrow";
            arrow.transform.position = origin;
            arrow.transform.localScale = new Vector3(0.04f, 0.4f, 0.04f);
            arrow.transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(90f, 0, 0);

            var rb = arrow.AddComponent<Rigidbody>();
            rb.linearVelocity = dir * arrowSpeed;
            rb.useGravity = true;

            var projectile = arrow.AddComponent<ArrowProjectile>();
            projectile.SetShooter(GetComponent<Collider>());
            projectile.Launch(dir, arrowSpeed);
        }

        public void TriggerDamageAlert()
        {
            if (CurrentState == HunterState.Idle || CurrentState == HunterState.Patrol)
            {
                SwitchState(HunterState.Kiting);
            }
        }

        private void SwitchState(HunterState newState)
        {
            CurrentState = newState;
            stateTimer = 0f;
        }

        private void HandleDeath()
        {
            isDead = true;
            CurrentState = HunterState.Dead;
            navAgent.isStopped = true;
            navAgent.enabled = false;
        }
    }
}
