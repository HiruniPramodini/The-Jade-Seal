using JadeSeal.Sound;
using UnityEngine;
using UnityEngine.AI;
using JadeSeal.Magic;

namespace JadeSeal.Archery
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyHealth), typeof(NavMeshAgent), typeof(CapsuleCollider))]
    public sealed class RoyalGuardAI : MonoBehaviour
    {
        public enum State
        {
            Idle, PatrolWalk, PatrolTurn, WeaponRaise, AimIdle, AimWalk, AimWalkBackward,
            Fire, MoveToCover, TakeCover, CoverIdle, Emerge, HitReaction, WeaponLower, Death
        }

        private static readonly int[] StateHashes = System.Array.ConvertAll(
            (State[])System.Enum.GetValues(typeof(State)), value => Animator.StringToHash("Base Layer." + value));

        [SerializeField] private Animator animator;
        [SerializeField] private Transform boltSpawnPoint;
        [SerializeField] private ArrowProjectile bolt;
        [SerializeField] private PlayerHealth player;
        [SerializeField] private Transform[] patrolPoints;
        [SerializeField, Min(0.1f)] private float patrolSpeed = 2f;
        [SerializeField, Min(0.05f)] private float waypointStoppingDistance = 0.22f;
        [SerializeField, Min(0)] private float waypointPause = 0.5f;
        [SerializeField, Min(1)] private float turnSpeed = 270f;
        [SerializeField] private bool reversePatrol;
        [SerializeField] private bool patrolLoop = true;
        [SerializeField, Min(1)] private float detectionRange = 18f;
        [SerializeField, Range(1, 360)] private float fieldOfView = 140f;
        [SerializeField, Min(0.1f)] private float targetMemory = 6f;
        [SerializeField, Min(1)] private float attackRange = 18f;
        [SerializeField, Min(1)] private float combatLeash = 20f;
        [SerializeField, Min(0)] private float attackCooldown = 2.5f;
        [SerializeField, Range(0.01f, 0.95f)] private float releaseNormalizedTime = 0.1f;
        [SerializeField] private float weaponYawOffset;
        [SerializeField, Min(0)] private float coverHoldTime = 1.5f;
        [SerializeField, Min(0)] private float reCoverCooldown = 2f;
        [SerializeField, Min(0)] private float coverSearchRadius = 12f;
        [SerializeField] private bool seekCoverOnDetection;
        [SerializeField] private LayerMask sightMask = ~0;

        private EnemyHealth health;
        private NavMeshAgent agent;
        private NavMeshPath candidatePath;
        private PlayerMagicCombatController magic;
        private GuardCoverPoint cover;
        private Vector3 home, hide, peek, destination;
        private Vector3[] route;
        private readonly Vector3[] corners = new Vector3[32];
        private int waypoint, visited, previousHealth, seenShots;
        private float waitUntil, nextPathTime, alertUntil, nextShot, nextCoverSearch, stateStarted;
        private bool alerted, coverRequested, fired, releasePending, movingToPeek, finishedPatrol;

        public State CurrentState { get; private set; }
        public int ShotsFired { get; private set; }
        public int PatrolWaypoint => waypoint;
        public bool TargetVisible { get; private set; }
        public GuardCoverPoint SelectedCover => cover;

        private NavMeshQueryFilter Filter => new() { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };

        private void Start()
        {
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<EnemyHealth>();
            if (!animator || !bolt || !boltSpawnPoint)
            {
                Debug.LogError("Royal Guard requires the Humanoid Animator, bolt prefab, and bolt spawn point.", this);
                enabled = false;
                return;
            }

            agent.updateRotation = false;
            agent.stoppingDistance = waypointStoppingDistance;
            if (!agent.isOnNavMesh && NavMesh.SamplePosition(transform.position, out var ground, 1.5f, Filter))
                agent.Warp(ground.position);
            home = transform.position;
            candidatePath = new NavMeshPath();
            var points = new System.Collections.Generic.List<Vector3>();
            if (patrolPoints != null)
                foreach (var point in patrolPoints)
                    if (point) points.Add(point.position);
            route = points.ToArray();
            previousHealth = health.CurrentHealth;
            if (!player) player = FindAnyObjectByType<PlayerHealth>();
            if (player) magic = player.GetComponent<PlayerMagicCombatController>();
            if (magic) seenShots = magic.ShotsFired;
            waitUntil = Time.time + waypointPause;
        }

        private void Update()
        {
            if (CurrentState == State.Death) return;
            if (health.IsDead)
            {
                Stop();
                ReleaseCover();
                Play(State.Death);
                agent.enabled = false;
                GetComponent<CapsuleCollider>().enabled = false;
                return;
            }

            SenseThreat();
            float progress = Progress();
            if (CurrentState == State.HitReaction && progress < 0.94f) return;
            if (CurrentState == State.WeaponLower)
            {
                if (progress >= 0.94f) Play(alerted ? State.WeaponRaise : State.Idle);
                return;
            }
            if (!alerted)
            {
                if (CurrentState is State.Idle or State.PatrolWalk or State.PatrolTurn) Patrol(progress);
                else
                {
                    Stop();
                    ReleaseCover();
                    Play(State.WeaponLower);
                }
                return;
            }

            Face(player.AimPosition - transform.position, weaponYawOffset);
            if (CurrentState is State.Idle or State.PatrolWalk or State.PatrolTurn)
            {
                Stop();
                Play(State.WeaponRaise);
                return;
            }
            if (CurrentState == State.WeaponRaise && progress < 0.94f) return;

            if (CoverSequence(progress)) return;
            if (CurrentState == State.Fire)
            {
                if (!fired && progress >= releaseNormalizedTime)
                {
                    fired = true;
                    releasePending = true;
                }
                if (progress >= 0.96f && !releasePending) Play(State.AimIdle);
                return;
            }

            if (coverRequested && Time.time >= nextCoverSearch && ChooseCover())
                return;

            AimOrAdvance();
        }

        private void SenseThreat()
        {
            bool hit = health.CurrentHealth < previousHealth;
            previousHealth = health.CurrentHealth;
            if (!player || player.IsDead)
            {
                alerted = TargetVisible = false;
                return;
            }
            Vector3 direction = player.transform.position - transform.position;
            float range = direction.magnitude;
            bool inside = range <= (alerted ? Mathf.Max(detectionRange, attackRange) + 2f : detectionRange) &&
                Vector3.Distance(home, player.transform.position) <= combatLeash + detectionRange;
            Vector3 horizontal = Vector3.ProjectOnPlane(direction, Vector3.up);
            TargetVisible = inside && (alerted || Vector3.Angle(transform.forward, horizontal) <= fieldOfView * 0.5f)
                && Clear(transform.position + Vector3.up * 1.52f);
            bool threatened = false;
            if (magic && magic.ShotsFired != seenShots)
            {
                seenShots = magic.ShotsFired;
                Vector3 difference = transform.position + Vector3.up - magic.LastShotOrigin;
                float projected = Vector3.Dot(difference, magic.LastShotDirection);
                threatened = inside && projected > 0f &&
                    (difference - magic.LastShotDirection * projected).sqrMagnitude < 7f;
            }
            // Complete the current cover maneuver while the target remains nearby. The firing check still requires a clear muzzle path.
            bool maintainingCover = inside && cover;
            if (TargetVisible || maintainingCover || hit || threatened)
            {
                if (!alerted && seekCoverOnDetection) coverRequested = true;
                alerted = true;
                alertUntil = Time.time + targetMemory;
            }
            if (hit || threatened) coverRequested = true;
            if (hit && CurrentState != State.HitReaction)
            {
                Stop();
                ReleaseCover();
                Play(State.HitReaction);
            }

            if (Time.time > alertUntil ||
                (transform.position - home).sqrMagnitude > (combatLeash + 0.3f) * (combatLeash + 0.3f))
            {
                alerted = false;
                coverRequested = false;
            }
        }

        private void Patrol(float progress)
        {
            if (route.Length < 2 || finishedPatrol)
            {
                Stop();
                Play(State.Idle);
                return;
            }

            Vector3 goal = route[waypoint];
            if (CurrentState == State.PatrolTurn)
            {
                float angle = Face(goal - transform.position);
                if (angle <= 5f && progress >= 0.9f)
                {
                    Go(goal, patrolSpeed);
                    Play(State.PatrolWalk);
                }
                return;
            }
            if (At(goal))
            {
                Stop();
                if (CurrentState != State.Idle)
                {
                    waitUntil = Time.time + waypointPause;
                    Play(State.Idle);
                }
                if (Time.time < waitUntil) return;

                visited++;
                if (!patrolLoop && visited >= route.Length)
                {
                    finishedPatrol = true;
                    return;
                }
                waypoint = (waypoint + (reversePatrol ? route.Length - 1 : 1)) % route.Length;
                Play(State.PatrolTurn);
                return;
            }

            Go(goal, patrolSpeed);
            Face(agent.isOnNavMesh ? agent.desiredVelocity : goal - transform.position);
            Play(State.PatrolWalk);
        }

        private bool CoverSequence(float progress)
        {
            if (cover && !cover.isActiveAndEnabled) ReleaseCover();
            switch (CurrentState)
            {
                case State.MoveToCover:
                    if (!cover || Time.time - stateStarted > 7f)
                    {
                        ReleaseCover();
                        Stop();
                        Play(State.AimIdle);
                    }
                    else if (At(hide))
                    {
                        Stop();
                        if (cover.Shelters(hide, player, sightMask)) Play(State.TakeCover);
                        else { ReleaseCover(); Play(State.AimIdle); }
                    }
                    else Go(hide, patrolSpeed);
                    return true;
                case State.TakeCover:
                    if (progress >= 0.94f) Play(State.CoverIdle);
                    return true;
                case State.CoverIdle:
                    if (!cover || !cover.Shelters(hide, player, sightMask) ||
                        Time.time >= Mathf.Max(stateStarted + coverHoldTime, nextShot - 0.3f))
                    {
                        movingToPeek = false;
                        Play(State.Emerge);
                    }
                    return true;
                case State.Emerge:
                    if (progress < 0.9f) return true;
                    if (cover && !At(peek) && Go(peek, patrolSpeed))
                    {
                        movingToPeek = true;
                        Play(State.AimWalk);
                    }
                    else
                    {
                        ReleaseCover();
                        Play(State.AimIdle);
                    }
                    return true;
            }
            if (!movingToPeek) return false;
            if (!cover || At(peek) || Time.time - stateStarted > 4f)
            {
                Stop();
                ReleaseCover();
                Play(State.AimIdle);
            }
            else Go(peek, patrolSpeed);
            return true;
        }

        private bool ChooseCover()
        {
            nextCoverSearch = Time.time + 0.55f;
            if (!agent.isOnNavMesh) return false;
            GuardCoverPoint best = null;
            float shortest = float.PositiveInfinity;
            Vector3 bestHide = default, bestPeek = default;
            foreach (var point in GuardCoverPoint.Active)
            {
                if ((point.transform.position - transform.position).sqrMagnitude > coverSearchRadius * coverSearchRadius ||
                    !point.TryPlan(this, agent, player, sightMask, candidatePath, out var hidden, out var exposed) ||
                    !WithinLeash(hidden) || !WithinLeash(exposed) || !PathInsideLeash()) continue;
                float cost = (hidden - transform.position).sqrMagnitude + 0.35f * (exposed - hidden).sqrMagnitude;
                if (cost >= shortest) continue;
                best = point;
                shortest = cost;
                bestHide = hidden;
                bestPeek = exposed;
            }
            if (!best || !Go(bestHide, patrolSpeed)) return false;

            ReleaseCover();
            cover = best;
            cover.Reserve(this);
            hide = bestHide;
            peek = bestPeek;
            coverRequested = false;
            Play(State.MoveToCover);
            return true;
        }

        private bool Clear(Vector3 from) =>
            !ArrowProjectile.Trace(from, player.AimPosition, transform, sightMask, out var hit, 0.025f) ||
            hit.transform.IsChildOf(player.transform);

        private void AimOrAdvance()
        {
            float range = Vector3.Distance(transform.position, player.transform.position);
            bool clear = range <= attackRange && Clear(boltSpawnPoint.position);
            Vector3 toward = Vector3.ProjectOnPlane(player.transform.position - transform.position, Vector3.up).normalized;
            if (range < 3.2f || !clear)
            {
                if (CurrentState is State.AimWalk or State.AimWalkBackward && agent.isOnNavMesh &&
                    agent.hasPath && !At(destination)) return;
                if (Time.time >= nextPathTime)
                {
                    nextPathTime = Time.time + 0.5f;
                    if (TryAimPosition(toward, range, clear)) return;
                }
            }

            Stop();
            Play(State.AimIdle);
            var aim = Quaternion.LookRotation(toward.sqrMagnitude > 0.01f ? toward : transform.forward) *
                Quaternion.Euler(0f, weaponYawOffset, 0f);
            if (clear && Quaternion.Angle(transform.rotation, aim) < 6f
                && Time.time >= nextShot && Time.time - stateStarted >= 0.25f)
            {
                fired = false;
                Play(State.Fire);
            }
        }

        private bool TryAimPosition(Vector3 toward, float range, bool clear)
        {
            if (!agent.isOnNavMesh || toward.sqrMagnitude < 0.01f) return false;
            var muzzle = boltSpawnPoint.position - transform.position;
            for (int i = 0; i < 7; i++)
            {
                int step = i % 2 == 0 ? i / 2 : -(i + 1) / 2;
                Vector3 offset = Quaternion.Euler(0f, step * 37f, 0f) * toward * (range < 3.2f ? -1.6f : 2.1f);
                if (!NavMesh.SamplePosition(transform.position + offset, out var hit, 0.65f, Filter) ||
                    !WithinLeash(hit.position) ||
                    !clear && range <= attackRange && !Clear(hit.position + muzzle) ||
                    !Go(hit.position, patrolSpeed)) continue;
                Play(range < 3.2f ? State.AimWalkBackward : State.AimWalk);
                return true;
            }
            return false;
        }

        private bool WithinLeash(Vector3 position) => (position - home).sqrMagnitude <= combatLeash * combatLeash;

        private bool PathInsideLeash()
        {
            int count = candidatePath.GetCornersNonAlloc(corners);
            if (count == corners.Length) return false;
            for (int i = 0; i < count; i++)
                if (!WithinLeash(corners[i])) return false;
            return true;
        }

        private bool Go(Vector3 goal, float speed)
        {
            if (!agent.isOnNavMesh) return false;
            if (!agent.isStopped && (agent.hasPath || agent.pathPending) && (goal - destination).sqrMagnitude < 0.08f)
                return true;
            if (!agent.CalculatePath(goal, candidatePath) || candidatePath.status != NavMeshPathStatus.PathComplete ||
                !PathInsideLeash()) return false;
            agent.speed = speed;
            agent.isStopped = false;
            if (!agent.SetPath(candidatePath)) return false;
            destination = goal;
            return true;
        }

        private bool At(Vector3 goal) =>
            (transform.position - goal).sqrMagnitude <= Mathf.Pow(waypointStoppingDistance + 0.15f, 2f);

        private void Stop()
        {
            if (!agent.isOnNavMesh || agent.isStopped) return;
            agent.isStopped = true;
            agent.ResetPath();
        }

        private void ReleaseCover()
        {
            if (cover)
            {
                cover.Release(this);
                nextCoverSearch = Time.time + reCoverCooldown;
            }
            cover = null;
            movingToPeek = false;
        }

        private void OnDisable() => ReleaseCover();

        private float Face(Vector3 direction, float offset = 0f)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f) return 0f;
            var desired = Quaternion.LookRotation(direction) * Quaternion.Euler(0f, offset, 0f);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, desired, turnSpeed * Time.deltaTime);
            return Quaternion.Angle(transform.rotation, desired);
        }

        private float Progress()
        {
            var state = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
            return state.fullPathHash == StateHashes[(int)CurrentState] ? state.normalizedTime : 0f;
        }

        private void Play(State state)
        {
            if (state == CurrentState) return;
            if (state == State.WeaponRaise) JadeSealAudio.At(Cue.BoltRaise, transform.position);
            if (state == State.TakeCover) JadeSealAudio.At(Cue.Cover, transform.position);
            if (state == State.Emerge) JadeSealAudio.At(Cue.Emerge, transform.position);
            CurrentState = state;
            releasePending = false;
            stateStarted = Time.time;
            animator.CrossFadeInFixedTime(StateHashes[(int)state], 0.075f);
        }

        private void LateUpdate()
        {
            if (!releasePending) return;
            releasePending = false;
            nextShot = Time.time + attackCooldown;
            if (health.IsDead || !alerted || !player || player.IsDead || !Clear(boltSpawnPoint.position) ||
                Vector3.Distance(transform.position, player.transform.position) > attackRange) return;
            var direction = player.AimPosition - boltSpawnPoint.position;
            if (direction.sqrMagnitude < 0.001f) return;
            var projectile = Instantiate(bolt, boltSpawnPoint.position, Quaternion.LookRotation(direction));
            projectile.Launch(transform, direction);
            JadeSealAudio.At(Cue.BoltRelease, boltSpawnPoint.position);
            JadeSealAudio.Follow(Cue.BoltFlight, projectile.transform);
            ShotsFired++;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRange);
            foreach (float angle in new[] { -fieldOfView * 0.5f, fieldOfView * 0.5f })
                Gizmos.DrawRay(transform.position, Quaternion.Euler(0f, angle, 0f) * transform.forward * detectionRange);
            Gizmos.color = Color.cyan;
            if (patrolPoints != null)
                for (int i = 0; i < patrolPoints.Length; i++)
                {
                    var start = patrolPoints[i];
                    var end = patrolPoints[(i + 1) % patrolPoints.Length];
                    if (start && end) Gizmos.DrawLine(start.position, end.position);
                }
            if (cover)
            {
                Gizmos.DrawWireSphere(hide, 0.27f);
                Gizmos.DrawLine(hide, peek);
            }
            if (boltSpawnPoint) Gizmos.DrawWireSphere(boltSpawnPoint.position, 0.07f);
        }
    }
}
