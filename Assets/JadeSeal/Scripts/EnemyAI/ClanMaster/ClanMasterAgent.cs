using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using JadeSeal.EnemyAI.Common;
using JadeSeal.Combat;

namespace JadeSeal.EnemyAI.ClanMaster
{
    public enum ClanMasterState
    {
        Idle,
        Patrol,
        Investigate,
        Approach,
        CleaveAttack,
        RallyAllies,
        DefensiveStance,
        TacticalRetreat,
        Dead
    }

    /// <summary>
    /// Autonomous Clan Master Controller (Elite Tactical Warrior & Leader).
    /// Assigned to Thamod. Manages deliberate approach, heavy ceremonial glaive cleaves,
    /// tactical retreat below 30% HP, and a dynamic Leader-Support Rally Aura.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(EnemyPerception), typeof(ClanMasterHealth))]
    public class ClanMasterAgent : MonoBehaviour
    {
        [Header("Locomotion Speeds")]
        [SerializeField] private float walkSpeed = 2.0f;
        [SerializeField] private float runSpeed = 4.2f;

        [Header("Glaive Combat")]
        [SerializeField] private float cleaveRange = 3.2f;
        [SerializeField] private float attackDamage = 45f;
        [SerializeField] private float attackCooldown = 2.4f;

        [Header("Leader Support: Clan Rally Aura")]
        [Tooltip("Radius within which allies receive the Rally buff.")]
        [SerializeField] private float rallyRadius = 16f;
        [Tooltip("Cooldown between Rally Aura activations in seconds.")]
        [SerializeField] private float rallyCooldown = 12f;
        [Tooltip("Duration of the active rally aura.")]
        [SerializeField] private float rallyDuration = 5.0f;
        [SerializeField] private GameObject rallyAuraVfxPrefab;

        [Header("Patrol Settings")]
        [SerializeField] private List<Transform> patrolWaypoints = new List<Transform>();
        [SerializeField] private float waypointTolerance = 1.5f;

        public ClanMasterState CurrentState { get; private set; } = ClanMasterState.Idle;
        public bool IsDefending => CurrentState == ClanMasterState.DefensiveStance;
        public bool IsRallying => CurrentState == ClanMasterState.RallyAllies;

        private NavMeshAgent navAgent;
        private EnemyPerception perception;
        private ClanMasterHealth health;

        private float lastAttackTime = -10f;
        private float lastRallyTime = -20f;
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

            health = GetComponent<ClanMasterHealth>();
            if (health == null) health = gameObject.AddComponent<ClanMasterHealth>();

            var col = GetComponent<CapsuleCollider>();
            if (col == null)
            {
                col = gameObject.AddComponent<CapsuleCollider>();
                col.radius = 0.52f;
                col.height = 2.0f;
                col.center = new Vector3(0, 1.0f, 0);
            }

            health.OnDied += HandleDeath;
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
                case ClanMasterState.Idle:
                    UpdateIdle();
                    break;
                case ClanMasterState.Patrol:
                    UpdatePatrol();
                    break;
                case ClanMasterState.Investigate:
                    UpdateInvestigate();
                    break;
                case ClanMasterState.Approach:
                    UpdateApproach();
                    break;
                case ClanMasterState.CleaveAttack:
                    UpdateCleaveAttack();
                    break;
                case ClanMasterState.RallyAllies:
                    UpdateRallyAllies();
                    break;
                case ClanMasterState.DefensiveStance:
                    UpdateDefensiveStance();
                    break;
                case ClanMasterState.TacticalRetreat:
                    UpdateTacticalRetreat();
                    break;
            }
        }

        private void UpdateIdle()
        {
            stateTimer += Time.deltaTime;
            if (perception.HasVisualContact)
            {
                SwitchState(ClanMasterState.Approach);
                return;
            }
            if (perception.HasAuditoryDisturbance)
            {
                SwitchState(ClanMasterState.Investigate);
                return;
            }
            if (stateTimer > 2.5f && patrolWaypoints.Count > 0)
            {
                SwitchState(ClanMasterState.Patrol);
            }
        }

        private void UpdatePatrol()
        {
            if (perception.HasVisualContact)
            {
                SwitchState(ClanMasterState.Approach);
                return;
            }
            if (perception.HasAuditoryDisturbance)
            {
                SwitchState(ClanMasterState.Investigate);
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
                    SwitchState(ClanMasterState.Idle);
                }
            }
        }

        private void UpdateInvestigate()
        {
            if (perception.HasVisualContact)
            {
                SwitchState(ClanMasterState.Approach);
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
                    SwitchState(ClanMasterState.Patrol);
                }
            }
        }

        private void UpdateApproach()
        {
            if (!perception.IsTargetPerceived)
            {
                SwitchState(ClanMasterState.Investigate);
                return;
            }

            // Check if ready to channel Leader Rally Aura for nearby allies
            if (Time.time >= lastRallyTime + rallyCooldown && HasNearbyAllies())
            {
                SwitchState(ClanMasterState.RallyAllies);
                return;
            }

            Transform target = perception.CurrentTarget;
            if (target == null) return;

            float dist = Vector3.Distance(transform.position, target.position);
            if (dist <= cleaveRange)
            {
                if (Time.time >= lastAttackTime + attackCooldown)
                {
                    SwitchState(ClanMasterState.CleaveAttack);
                }
                else
                {
                    SwitchState(ClanMasterState.DefensiveStance);
                }
                return;
            }

            navAgent.speed = runSpeed;
            navAgent.stoppingDistance = cleaveRange * 0.85f;
            navAgent.SetDestination(target.position);
        }

        private void UpdateCleaveAttack()
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
            // Cleave impact at 0.5s
            if (stateTimer >= 0.5f && lastAttackTime < Time.time - 0.6f)
            {
                lastAttackTime = Time.time;
                PerformGlaiveCleave();
            }

            if (stateTimer >= 1.1f)
            {
                SwitchState(ClanMasterState.Approach);
            }
        }

        private void PerformGlaiveCleave()
        {
            Transform target = perception.CurrentTarget;
            if (target == null) return;

            float dist = Vector3.Distance(transform.position, target.position);
            if (dist <= cleaveRange + 0.6f)
            {
                var damageable = target.GetComponent<IDamageable>();
                if (damageable != null && !damageable.IsDead)
                {
                    Vector3 hitPoint = target.position + Vector3.up * 1.2f;
                    Vector3 hitNormal = (transform.position - target.position).normalized;
                    damageable.TakeDamage(attackDamage, hitPoint, hitNormal);
                    Debug.Log($"<color=#00FFCC><b>[ClanMasterCleave]</b></color> Heavy Dragon Glaive struck target for {attackDamage} damage!");
                }
            }
        }

        private void UpdateRallyAllies()
        {
            navAgent.ResetPath();
            stateTimer += Time.deltaTime;

            if (stateTimer == Time.deltaTime)
            {
                lastRallyTime = Time.time;
                TriggerClanRallyBuff();
            }

            if (stateTimer >= rallyDuration * 0.35f)
            {
                SwitchState(ClanMasterState.Approach);
            }
        }

        private void TriggerClanRallyBuff()
        {
            Debug.Log($"<color=#FFD700><b>[ClanMaster]</b> Clan Rally Aura unleashed! Buffing allies within {rallyRadius}m!</color>");

            // Visual shockwave aura
            GameObject auraObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            auraObj.name = "ClanRallyAuraVisual";
            auraObj.transform.position = transform.position + Vector3.up * 0.1f;
            auraObj.transform.localScale = new Vector3(rallyRadius * 1.5f, 0.2f, rallyRadius * 1.5f);
            Destroy(auraObj.GetComponent<Collider>());
            var r = auraObj.GetComponent<Renderer>();
            r.material.color = new Color(0.1f, 0.9f, 0.6f, 0.35f);
            Destroy(auraObj, 2.5f);

            // Buff nearby allies
            Collider[] hits = Physics.OverlapSphere(transform.position, rallyRadius);
            foreach (var hit in hits)
            {
                if (hit.gameObject == gameObject) continue;
                var nav = hit.GetComponent<NavMeshAgent>();
                if (nav != null)
                {
                    nav.speed *= 1.25f; // +25% speed buff
                }
            }
        }

        private bool HasNearbyAllies()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, rallyRadius);
            int allyCount = 0;
            foreach (var hit in hits)
            {
                if (hit.gameObject != gameObject && hit.CompareTag("Untagged") && hit.GetComponent<NavMeshAgent>() != null)
                {
                    allyCount++;
                }
            }
            return allyCount > 0;
        }

        private void UpdateDefensiveStance()
        {
            navAgent.ResetPath();
            Transform target = perception.CurrentTarget;
            if (target != null)
            {
                Vector3 lookDir = (target.position - transform.position);
                lookDir.y = 0;
                if (lookDir.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 6f);
                }
            }

            stateTimer += Time.deltaTime;
            if (stateTimer >= 1.5f || Time.time >= lastAttackTime + attackCooldown)
            {
                SwitchState(ClanMasterState.Approach);
            }
        }

        private void UpdateTacticalRetreat()
        {
            Transform target = perception.CurrentTarget;
            if (target == null)
            {
                SwitchState(ClanMasterState.Patrol);
                return;
            }

            navAgent.speed = runSpeed;
            Vector3 retreatDir = (transform.position - target.position).normalized;
            Vector3 retreatDest = transform.position + retreatDir * 10f;

            if (NavMesh.SamplePosition(retreatDest, out NavMeshHit hit, 8f, NavMesh.AllAreas))
            {
                navAgent.SetDestination(hit.position);
            }

            stateTimer += Time.deltaTime;
            if (stateTimer > 4.0f || Vector3.Distance(transform.position, target.position) > 15f)
            {
                SwitchState(ClanMasterState.Approach);
            }
        }

        public void CheckHealthThresholds(float currentHp, float maxHp)
        {
            if (currentHp / maxHp < 0.30f && CurrentState != ClanMasterState.TacticalRetreat)
            {
                Debug.Log("<color=#FF9900>[ClanMaster]</color> Health critical (<30%)! Initiating tactical regroup retreat.");
                SwitchState(ClanMasterState.TacticalRetreat);
            }
        }

        private void SwitchState(ClanMasterState newState)
        {
            CurrentState = newState;
            stateTimer = 0f;
        }

        private void HandleDeath()
        {
            isDead = true;
            CurrentState = ClanMasterState.Dead;
            navAgent.isStopped = true;
            navAgent.enabled = false;
        }
    }
}
