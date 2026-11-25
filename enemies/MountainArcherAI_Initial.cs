using UnityEngine;
using UnityEngine.AI;

namespace JadeSeal.Archery
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyHealth), typeof(NavMeshAgent), typeof(CapsuleCollider))]
    public sealed class MountainArcherAI_Initial : MonoBehaviour
    {
        public enum Phase { Idle, DrawArrow, AimHold, AimWalk, Recoil, Death }

        [SerializeField] private PlayerHealth player;
        [SerializeField] private ArrowProjectile projectile;
        [SerializeField] private Transform arrowSpawnPoint;
        [SerializeField] private LayerMask sightMask = ~0;
        [SerializeField] private float detectionRange = 28f;
        [SerializeField] private float attackRange = 26f;
        [SerializeField] private float guardRadius = 6f;
        [SerializeField] private float walkingSpeed = 1.9f;
        [SerializeField] private float turnSpeed = 360f;
        [SerializeField] private float shotCooldown = 2.5f;
        [SerializeField] private float drawTime = 0.5f;
        [SerializeField] private float aimTime = 0.3f;
        [SerializeField] private float releaseDelay = 0.06f;
        [SerializeField] private float recoilTime = 0.4f;

        private NavMeshAgent agent;
        private EnemyHealth health;
        private NavMeshPath path;
        private Vector3 guard;
        private float phaseStarted;
        private float lastSeen = float.NegativeInfinity;
        private float nextShot;
        private float nextSearch;
        private bool returning;
        private bool released;
        private bool stepAttempted;
        private float stepUntil;
        private int nextStep;
        private static readonly Vector3[] StepDirections =
        {
            Vector3.left, Vector3.right, Vector3.forward,
            Vector3.left, Vector3.right, Vector3.back
        };

        public Phase CurrentPhase { get; private set; }

        private NavMeshQueryFilter Filter => new NavMeshQueryFilter
        {
            agentTypeID = agent.agentTypeID,
            areaMask = agent.areaMask
        };

        private void Start()
        {
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<EnemyHealth>();
            path = new NavMeshPath();
            agent.updateRotation = false;
            agent.speed = walkingSpeed;
            agent.stoppingDistance = 0.22f;
            if (!player) player = FindAnyObjectByType<PlayerHealth>();
            if (!agent.isOnNavMesh && NavMesh.SamplePosition(transform.position, out var start, 1.5f, Filter))
                agent.Warp(start.position);
            guard = transform.position;

            if (arrowSpawnPoint && projectile) return;
            Debug.LogError("Assign an arrow spawn point and projectile for the Mountain Archer.", this);
            enabled = false;
        }

        private void Update()
        {
            if (CurrentPhase == Phase.Death) return;
            if (health.IsDead)
            {
                StopMoving();
                ChangePhase(Phase.Death);
                agent.enabled = false;
                GetComponent<CapsuleCollider>().enabled = false;
                return;
            }

            if (!DetectPlayer() || returning || Vector3.Distance(transform.position, guard) > guardRadius + 0.15f)
            {
                ReturnToGuard();
                return;
            }

            bool aligned = Face(player.AimPosition - transform.position) < 5f;
            bool inRange = Vector3.Distance(transform.position, player.transform.position) <= attackRange;
            bool clearShot = inRange && HasLineOfSight(arrowSpawnPoint.position);
            float elapsed = Time.time - phaseStarted;

            if (stepUntil > 0f)
            {
                if (Time.time < stepUntil && agent.isOnNavMesh &&
                    (agent.pathPending || (agent.hasPath && agent.remainingDistance > 0.10f)))
                    return;

                stepUntil = 0f;
                StopMoving();
                agent.speed = walkingSpeed;
                agent.stoppingDistance = 0.22f;
            }

            switch (CurrentPhase)
            {
                case Phase.Idle:
                    if (inRange) ChangePhase(Phase.DrawArrow);
                    break;
                case Phase.DrawArrow:
                    if (elapsed >= drawTime) AimOrMove(clearShot);
                    break;
                case Phase.AimHold:
                    if (!clearShot)
                        AimOrMove(false);
                    else if (!stepAttempted && aligned && Time.time >= nextShot - 0.75f && TryCombatStep())
                        ChangePhase(Phase.AimWalk);
                    else if (aligned && elapsed >= aimTime && Time.time >= nextShot)
                    {
                        released = false;
                        ChangePhase(Phase.Recoil);
                    }
                    break;
                case Phase.AimWalk:
                    AimOrMove(clearShot);
                    break;
                case Phase.Recoil:
                    if (!released && !clearShot)
                    {
                        ChangePhase(Phase.AimHold);
                        break;
                    }
                    if (!released && aligned && elapsed >= releaseDelay) FireArrow();
                    if (elapsed >= recoilTime)
                        ChangePhase(inRange ? Phase.DrawArrow : Phase.AimHold);
                    break;
            }
        }

        private bool DetectPlayer()
        {
            if (!player || player.IsDead ||
                Vector3.Distance(transform.position, player.transform.position) > detectionRange)
                return false;

            // Briefly remember the player after losing sight during combat.
            if (HasLineOfSight(transform.position + Vector3.up * 1.45f))
            {
                lastSeen = Time.time;
                return true;
            }
            return CurrentPhase != Phase.Idle && Time.time - lastSeen <= 2f;
        }

        private bool HasLineOfSight(Vector3 origin)
        {
            // Use the same obstacle check as the projectile.
            return !ArrowProjectile.Trace(origin, player.AimPosition, transform, sightMask, out var hit, 0.02f)
                || hit.transform.IsChildOf(player.transform);
        }

        private void AimOrMove(bool clearShot)
        {
            if (!clearShot && TryReposition())
            {
                ChangePhase(Phase.AimWalk);
                return;
            }
            StopMoving();
            ChangePhase(Phase.AimHold);
        }

        private bool TryReposition()
        {
            if (!agent.isOnNavMesh) return false;
            if (CurrentPhase == Phase.AimWalk && !agent.isStopped &&
                (agent.pathPending || (agent.hasPath && agent.remainingDistance > 0.35f)))
                return true;
            if (Time.time < nextSearch) return false;
            nextSearch = Time.time + 0.45f;

            Vector3 toward = player.transform.position - guard;
            toward.y = 0f;
            if (toward.sqrMagnitude < 0.001f) toward = transform.forward;
            Vector3 muzzleOffset = arrowSpawnPoint.position - transform.position;

            // Search the inner ring first, alternating left and right from the player direction.
            for (int ring = 1; ring <= 2; ring++)
            {
                for (int index = 0; index < 12; index++)
                {
                    int step = index % 2 == 0 ? index / 2 : -(index + 1) / 2;
                    Vector3 direction = Quaternion.Euler(0f, step * 30f, 0f) * toward.normalized;
                    Vector3 candidate = guard + direction * (guardRadius * ring * 0.5f);
                    if (!NavMesh.SamplePosition(candidate, out var hit, 0.85f, Filter)) continue;
                    if ((hit.position - transform.position).sqrMagnitude < 0.45f) continue;
                    if (Vector3.Distance(hit.position, player.transform.position) > attackRange) continue;
                    if (!HasLineOfSight(hit.position + muzzleOffset)) continue;
                    if (TryMoveTo(hit.position, true)) return true;
                }
            }
            return false;
        }

        private bool TryCombatStep()
        {
            stepAttempted = true;
            if (!agent.isOnNavMesh) return false;

            Vector3 toward = player.transform.position - transform.position;
            toward.y = 0f;
            float range = toward.magnitude;
            Vector3 muzzleOffset = arrowSpawnPoint.position - transform.position;

            // Back away when close, step toward a distant player, or cycle through short steps.
            for (int i = -1; i < StepDirections.Length; i++)
            {
                Vector3 direction;
                if (i < 0)
                {
                    if (range < 4f) direction = -toward.normalized;
                    else if (range > attackRange - 2f) direction = toward.normalized;
                    else continue;
                }
                else
                {
                    direction = transform.TransformDirection(StepDirections[(nextStep + i) % StepDirections.Length]);
                }

                Vector3 start = transform.position;
                if (!NavMesh.SamplePosition(start + direction * 0.6f, out var hit, 0.25f, Filter)) continue;
                Vector3 step = hit.position - start;
                if (step.magnitude < 0.4f || step.magnitude > 1f) continue;
                if (Vector3.Dot(step.normalized, direction) < 0.85f) continue;
                if (NavMesh.Raycast(start, hit.position, out _, Filter)) continue;
                if (!HasLineOfSight(hit.position + muzzleOffset)) continue;
                Vector3 bodyHeight = Vector3.up * 0.8f;
                if (ArrowProjectile.Trace(start + bodyHeight, hit.position + bodyHeight,
                    transform, sightMask, out _, agent.radius)) continue;
                if (!TryMoveTo(hit.position, true)) continue;

                agent.speed = 2f;
                agent.stoppingDistance = 0.07f;
                stepUntil = Time.time + 0.95f;
                nextStep = (nextStep + Mathf.Max(0, i) + 1) % StepDirections.Length;
                return true;
            }
            return false;
        }

        private bool TryMoveTo(Vector3 destination, bool stayInsideGuard)
        {
            if (!agent.CalculatePath(destination, path) || path.status != NavMeshPathStatus.PathComplete)
                return false;

            if (stayInsideGuard)
            {
                foreach (Vector3 corner in path.corners)
                    if (Vector3.Distance(corner, guard) > guardRadius) return false;
            }

            agent.isStopped = false;
            return agent.SetPath(path);
        }

        private void ReturnToGuard()
        {
            stepUntil = 0f;
            stepAttempted = false;
            agent.speed = walkingSpeed;
            agent.stoppingDistance = 0.22f;

            if (!agent.isOnNavMesh || (transform.position - guard).sqrMagnitude <= 0.32f)
            {
                returning = false;
                StopMoving();
                ChangePhase(Phase.Idle);
                return;
            }

            if (!returning)
            {
                StopMoving();
                if (Time.time < nextSearch)
                {
                    ChangePhase(Phase.Idle);
                    return;
                }
                returning = TryMoveTo(guard, false);
                nextSearch = Time.time + 0.45f;
            }

            if (!returning || (!agent.hasPath && !agent.pathPending))
            {
                returning = false;
                ChangePhase(Phase.Idle);
                return;
            }

            ChangePhase(Phase.AimWalk);
            Face(agent.desiredVelocity);
        }

        private void FireArrow()
        {
            Vector3 direction = player.AimPosition - arrowSpawnPoint.position;
            if (direction.sqrMagnitude < 0.001f) return;
            ArrowProjectile arrow = Instantiate(projectile, arrowSpawnPoint.position, Quaternion.LookRotation(direction));
            arrow.Launch(transform, direction);
            released = true;
            nextShot = Time.time + shotCooldown;
        }

        private float Face(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f) return 0f;
            Quaternion target = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
            return Quaternion.Angle(transform.rotation, target);
        }

        private void StopMoving()
        {
            if (!agent.isOnNavMesh || agent.isStopped) return;
            agent.isStopped = true;
            agent.ResetPath();
        }

        private void ChangePhase(Phase phase)
        {
            if (CurrentPhase == phase) return;
            CurrentPhase = phase;
            phaseStarted = Time.time;
            if (phase == Phase.DrawArrow) stepAttempted = false;
        }
    }
}
