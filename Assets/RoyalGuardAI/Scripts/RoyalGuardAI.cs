using UnityEngine;
using UnityEngine.AI;
using JadeSeal.Sound;
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

        public State CurrentState { get; private set; }
        private float stateStarted;
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
        [SerializeField, Min(1)] private float combatLeash = 20f;

        private EnemyHealth health;
        private NavMeshAgent agent;
        private Vector3 home, destination;
        private Vector3[] route;
        private NavMeshPath candidatePath;
        private readonly Vector3[] corners = new Vector3[32];
        private int waypoint, visited;
        private float waitUntil;
        private bool finishedPatrol;

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
            waitUntil = Time.time + waypointPause;
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

        private void ExecutePatrol()
        {
            if (route.Length < 2 || finishedPatrol)
            {
                Stop();
                return;
            }

            Vector3 goal = route[waypoint];
            if (At(goal))
            {
                Stop();
                if (Time.time < waitUntil) return;

                visited++;
                if (!patrolLoop && visited >= route.Length)
                {
                    finishedPatrol = true;
                    return;
                }
                waypoint = (waypoint + (reversePatrol ? route.Length - 1 : 1)) % route.Length;
                waitUntil = Time.time + waypointPause;
                return;
            }

            Go(goal, patrolSpeed);
        }
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
            if (CurrentState == state) return;
            CurrentState = state;
            stateStarted = Time.time;
            animator.Play(StateHashes[(int)state], 0, 0f);
        }
    }
}


