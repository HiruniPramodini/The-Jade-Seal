using JadeSeal.Sound;
using UnityEngine;
using UnityEngine.AI;

namespace JadeSeal.Archery
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyHealth), typeof(NavMeshAgent), typeof(CapsuleCollider))]
    public sealed class MountainArcherAI : MonoBehaviour
    {
        public enum Phase { Idle, DrawArrow, AimHold, AimWalk, Recoil, Death }
        private static readonly int[] States = System.Array.ConvertAll(
            (Phase[])System.Enum.GetValues(typeof(Phase)), value => Animator.StringToHash("Base Layer." + value));
        private static readonly int MoveX = Animator.StringToHash("MoveX");
        private static readonly int MoveY = Animator.StringToHash("MoveY");

        [SerializeField] private Animator animator;
        [SerializeField] private PlayerHealth player;
        [SerializeField] private ArrowProjectile projectile;
        [SerializeField] private Transform arrowSpawnPoint, upperTip, lowerTip, restNock, drawHand;
        [SerializeField] private LineRenderer bowstring;
        [SerializeField] private Transform nockedArrow;
        [SerializeField, Min(0)] private float detectionRange = 28f;
        [SerializeField, Min(0)] private float attackRange = 26f;
        [SerializeField, Min(0)] private float repositionRadius = 6f;
        [SerializeField, Min(0.1f)] private float walkingSpeed = 1.9f;
        [SerializeField, Min(1)] private float turnSpeed = 360f;
        [SerializeField, Min(0)] private float cooldown = 2.5f;
        [SerializeField, Min(0)] private float aimStabilizationDelay = 0.3f;
        [SerializeField, Min(0.1f)] private float combatStepDistance = 0.6f;
        [SerializeField, Min(0.1f)] private float minimumStepDistance = 0.4f;
        [SerializeField, Min(0.1f)] private float maximumStepDistance = 1f;
        [SerializeField, Min(0.1f)] private float combatStepSpeed = 2f;
        [SerializeField, Range(0.01f, 0.95f)] private float releaseTime = 0.06f;
        [SerializeField] private float aimYawOffset;
        [SerializeField] private LayerMask sightMask = ~0;

        private EnemyHealth health;
        private NavMeshAgent agent;
        private NavMeshPath path;
        private readonly Vector3[] corners = new Vector3[32];
        private Vector3 guard;
        private bool returning, released, pending, stepAttempted, takingStep;
        private float nextShot, nextSearch, holdUntil, stepDeadline, lastSeenTime;
        private int nextStep;
        private static readonly Vector3[] StepOrder =
        {
            Vector3.left, Vector3.right, Vector3.forward,
            Vector3.left, Vector3.right, Vector3.back
        };

        public Phase CurrentPhase { get; private set; }
        public int ShotsFired { get; private set; }
        public Vector3 GuardPosition => guard;

        private void Start()
        {
            health = GetComponent<EnemyHealth>();
            agent = GetComponent<NavMeshAgent>();
            path = new NavMeshPath();
            agent.updateRotation = false;
            agent.speed = walkingSpeed;
            if (!player) player = FindAnyObjectByType<PlayerHealth>();
            if (!agent.isOnNavMesh && NavMesh.SamplePosition(transform.position, out var hit, 1.5f, Filter))
                agent.Warp(hit.position);
            guard = transform.position;
            if (animator && projectile && arrowSpawnPoint && upperTip && lowerTip && restNock && drawHand && bowstring && nockedArrow)
                return;
            Debug.LogError("Mountain Archer needs its Animator, bow, and projectile references.", this);
            enabled = false;
        }

        private NavMeshQueryFilter Filter => new() { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };

        private void Update()
        {
            if (CurrentPhase == Phase.Death) return;
            if (health.IsDead)
            {
                Stop();
                Play(Phase.Death);
                agent.enabled = false;
                GetComponent<CapsuleCollider>().enabled = false;
                return;
            }

            bool target = player && !player.IsDead && InRange(detectionRange);
            if (target)
            {
                bool visible = ClearShot(transform.position + Vector3.up * 1.45f);
                if (visible) lastSeenTime = Time.time;
                target = visible || CurrentPhase != Phase.Idle && Time.time - lastSeenTime <= 2f;
            }
            if (!target || returning || (transform.position - guard).sqrMagnitude > (repositionRadius + 0.15f) * (repositionRadius + 0.15f))
            {
                ReturnToGuard();
                return;
            }

            bool aligned = Face(player.AimPosition - transform.position, aimYawOffset) < 5f;
            if (takingStep)
            {
                if (agent.isOnNavMesh && Time.time < stepDeadline &&
                    (agent.pathPending || (agent.hasPath && agent.pathStatus == NavMeshPathStatus.PathComplete &&
                    agent.remainingDistance > 0.10f)))
                {
                    UpdateMotion();
                    return;
                }
                takingStep = false;
                Stop();
            }
            float progress = Progress();
            if (CurrentPhase == Phase.Idle)
            {
                if (InRange(attackRange)) Play(Phase.DrawArrow);
                return;
            }
            if (CurrentPhase == Phase.DrawArrow && progress < 0.95f) return;
            bool hasShot = InRange(attackRange) && ClearShot(arrowSpawnPoint.position);
            if (CurrentPhase == Phase.Recoil)
            {
                if (hasShot && aligned && !released && progress >= releaseTime)
                {
                    released = true;
                    pending = true;
                }
                if (!hasShot && !released)
                    Play(Phase.AimHold);
                else if (progress < 0.97f || pending)
                    return;
                else
                {
                    Play(InRange(attackRange) ? Phase.DrawArrow : Phase.AimHold);
                    return;
                }
            }

            if (!hasShot)
            {
                if (TryReposition())
                {
                    Play(Phase.AimWalk);
                    UpdateMotion();
                }
                else
                {
                    Stop();
                    Play(Phase.AimHold);
                }
                return;
            }

            Stop();
            if (!stepAttempted && aligned && Time.time >= nextShot - 0.75f)
            {
                stepAttempted = true;
                if (TryCombatStep())
                {
                    Play(Phase.AimWalk);
                    UpdateMotion();
                    return;
                }
            }
            if (CurrentPhase != Phase.AimHold)
            {
                Play(Phase.AimHold);
                holdUntil = Time.time + aimStabilizationDelay;
            }
            if (aligned && Time.time >= holdUntil && Time.time >= nextShot && Progress() > 0f)
            {
                released = false;
                Play(Phase.Recoil);
            }
        }

        private bool InRange(float distance) => (player.transform.position - transform.position).sqrMagnitude <= distance * distance;

        private bool ClearShot(Vector3 from)
        {
            return !ArrowProjectile.Trace(from, player.AimPosition, transform, sightMask, out var closest, 0.02f)
                || closest.transform.IsChildOf(player.transform);
        }

        private bool TryCombatStep()
        {
            if (!agent.isOnNavMesh) return false;
            float max = Mathf.Max(minimumStepDistance, maximumStepDistance);
            float distance = Mathf.Clamp(combatStepDistance, minimumStepDistance, max);
            Vector3 toward = Vector3.ProjectOnPlane(player.transform.position - transform.position, Vector3.up);
            float range = toward.magnitude;
            int bias = range < 4f ? 2 : range > attackRange - 2f ? 3 : 0;
            Vector3 muzzle = arrowSpawnPoint.position - transform.position;
            for (int i = bias == 0 ? 0 : -1; i < StepOrder.Length; i++)
            {
                Vector3 direction = i == -1 ? toward.normalized * (bias == 2 ? -1f : 1f) :
                    transform.TransformDirection(StepOrder[(nextStep + i) % StepOrder.Length]);
                if (!NavMesh.SamplePosition(transform.position + direction * distance, out var hit, 0.25f, Filter))
                    continue;
                Vector3 delta = hit.position - transform.position;
                if (delta.magnitude < minimumStepDistance || delta.magnitude > max ||
                    Vector3.Dot(delta.normalized, direction) < 0.85f ||
                    NavMesh.Raycast(transform.position, hit.position, out _, Filter) ||
                    !ClearShot(hit.position + muzzle) ||
                    ArrowProjectile.Trace(transform.position + Vector3.up * 0.8f,
                        hit.position + Vector3.up * 0.8f, transform, sightMask, out _, agent.radius) ||
                    !agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete ||
                    !PathWithinGuard()) continue;

                agent.speed = combatStepSpeed;
                agent.stoppingDistance = 0.07f;
                agent.isStopped = false;
                if (!agent.SetPath(path)) continue;
                takingStep = true;
                stepDeadline = Time.time + distance / combatStepSpeed + 0.65f;
                nextStep = (nextStep + Mathf.Max(0, i) + 1) % StepOrder.Length;
                return true;
            }
            return false;
        }

        private bool TryReposition()
        {
            if (!agent.isOnNavMesh) return false;
            agent.speed = walkingSpeed;
            agent.stoppingDistance = 0.22f;
            if (CurrentPhase == Phase.AimWalk && !agent.isStopped &&
                (agent.pathPending || (agent.hasPath && agent.remainingDistance > 0.35f)))
                return true;
            if (Time.time < nextSearch) return false;
            nextSearch = Time.time + 0.45f;

            Vector3 toward = Vector3.ProjectOnPlane(player.transform.position - guard, Vector3.up).normalized;
            Vector3 muzzleOffset = arrowSpawnPoint.position - transform.position;
            for (int ring = 1; ring <= 2; ring++)
            for (int index = 0; index < 12; index++)
            {
                int step = index % 2 == 0 ? index / 2 : -(index + 1) / 2;
                var offset = Quaternion.Euler(0, step * 30f, 0) * toward * (ring * repositionRadius * 0.5f);
                if (!NavMesh.SamplePosition(guard + offset, out var hit, 0.85f, Filter) ||
                    (hit.position - transform.position).sqrMagnitude < 0.45f ||
                    (hit.position - player.transform.position).sqrMagnitude > attackRange * attackRange ||
                    !ClearShot(hit.position + muzzleOffset) ||
                    !agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete ||
                    !PathWithinGuard()) continue;
                agent.isStopped = false;
                if (agent.SetPath(path)) return true;
            }
            return false;
        }

        private bool PathWithinGuard()
        {
            int count = path.GetCornersNonAlloc(corners);
            if (count == corners.Length) return false;
            for (int i = 0; i < count; i++)
                if ((corners[i] - guard).sqrMagnitude > repositionRadius * repositionRadius) return false;
            return true;
        }

        private void ReturnToGuard()
        {
            takingStep = stepAttempted = false;
            if (agent.isOnNavMesh && (transform.position - guard).sqrMagnitude > 0.32f)
            {
                agent.speed = walkingSpeed;
                agent.stoppingDistance = 0.22f;
                if (Time.time >= nextSearch)
                {
                    nextSearch = Time.time + 0.45f;
                    if (!agent.CalculatePath(guard, path) || path.status != NavMeshPathStatus.PathComplete ||
                        !PathWithinGuard() || !agent.SetPath(path))
                    {
                        returning = false;
                        Stop();
                        Play(Phase.Idle);
                        return;
                    }
                    agent.isStopped = false;
                }
                if (!agent.hasPath && !agent.pathPending)
                {
                    Play(Phase.Idle);
                    return;
                }
                returning = true;
                Play(Phase.AimWalk);
                Face(agent.desiredVelocity);
                UpdateMotion();
                return;
            }

            returning = false;
            Stop();
            Play(Phase.Idle);
        }

        private void UpdateMotion()
        {
            Vector3 move = transform.InverseTransformDirection(agent.velocity) / Mathf.Max(0.1f, agent.speed);
            animator.SetFloat(MoveX, move.x, 0.075f, Time.deltaTime);
            animator.SetFloat(MoveY, move.z, 0.075f, Time.deltaTime);
        }

        private float Face(Vector3 direction, float yaw = 0f)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f) return 0f;
            var rotation = Quaternion.LookRotation(direction) * Quaternion.Euler(0f, yaw, 0f);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, rotation, turnSpeed * Time.deltaTime);
            return Quaternion.Angle(transform.rotation, rotation);
        }

        private void Stop()
        {
            if (!agent.isOnNavMesh || agent.isStopped) return;
            agent.isStopped = true;
            agent.ResetPath();
        }

        private float Progress()
        {
            int hash = States[(int)CurrentPhase];
            var state = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
            return state.fullPathHash == hash ? state.normalizedTime : 0f;
        }

        private void Play(Phase phase)
        {
            if (phase == CurrentPhase) return;
            if (phase == Phase.DrawArrow) JadeSealAudio.At(Cue.BowDraw, transform.position);
            if (phase == Phase.DrawArrow) stepAttempted = false;
            if (phase != Phase.Recoil) pending = false;
            CurrentPhase = phase;
            animator.CrossFadeInFixedTime(States[(int)phase], 0.075f);
        }

        private void LateUpdate()
        {
            float progress = Progress();
            float draw = CurrentPhase switch
            {
                Phase.DrawArrow => Mathf.InverseLerp(0.25f, 0.93f, progress),
                Phase.AimHold or Phase.AimWalk => returning ? 0f : 1f,
                Phase.Recoil => released ? 1f - Mathf.InverseLerp(releaseTime, releaseTime + 0.15f, progress) : 1f,
                _ => 0f
            };
            Vector3 center = Vector3.Lerp(restNock.position, drawHand.position, draw);
            bowstring.SetPosition(0, upperTip.position);
            bowstring.SetPosition(1, center);
            bowstring.SetPosition(2, lowerTip.position);
            bool show = draw > 0.05f && !(CurrentPhase == Phase.Recoil && released);
            nockedArrow.gameObject.SetActive(show);
            Vector3 shaft = arrowSpawnPoint.position - center;
            if (show && shaft.sqrMagnitude > 0.01f)
                nockedArrow.SetPositionAndRotation(center, Quaternion.LookRotation(shaft));

            if (!pending) return;
            pending = false;
            nextShot = Time.time + cooldown;
            if (health.IsDead || !player || player.IsDead || !InRange(attackRange) || !ClearShot(arrowSpawnPoint.position)) return;
            Vector3 direction = player.AimPosition - arrowSpawnPoint.position;
            if (direction.sqrMagnitude < 0.001f) return;
            Instantiate(projectile, arrowSpawnPoint.position, Quaternion.LookRotation(direction)).Launch(transform, direction);
            JadeSealAudio.At(Cue.BowRelease, arrowSpawnPoint.position);
            ShotsFired++;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRange);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(Application.isPlaying ? guard : transform.position, repositionRadius);
            if (arrowSpawnPoint && player) Gizmos.DrawLine(arrowSpawnPoint.position, player.AimPosition);
        }
    }
}
