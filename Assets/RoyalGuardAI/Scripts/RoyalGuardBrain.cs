using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace JadeSeal.RoyalGuard
{
    // Utility AI: evaluates scores every thinkInterval and selects the highest-valued feasible action.
    [RequireComponent(typeof(NavMeshAgent), typeof(RoyalGuardHealth), typeof(RoyalGuardCrossbow))]
    public sealed class RoyalGuardBrain : MonoBehaviour
    {
        public enum Action { Patrol, Chase, SeekCover, Fire, Reload, Retreat, Search }
        [Header("References")]
        [SerializeField] private Transform player;
        [SerializeField] private Transform eyes;
        [SerializeField] private Transform[] patrolWaypoints;
        [SerializeField] private LayerMask sightObstacles = ~0;
        [Header("Sensing")]
        [SerializeField, Min(1f)] private float detectionRange = 28f;
        [SerializeField, Range(1f, 180f)] private float fieldOfView = 115f;
        [SerializeField, Min(1f)] private float attackRange = 21f;
        [SerializeField, Min(.1f)] private float memorySeconds = 5f;
        [Header("Decision")]
        [SerializeField, Range(.05f, 2f)] private float thinkInterval = .25f;
        [SerializeField, Min(1f)] private float coverSearchRadius = 20f;
        [SerializeField, Range(0f, 1f)] private float retreatHealthThreshold = .28f;
        [SerializeField, Min(.5f)] private float destinationRefreshSeconds = .8f;
        [SerializeField, Min(0f)] private float scoreSwitchMargin = .08f;
        [Header("Cover locations (assign in Inspector)")]
        [SerializeField] private RoyalGuardCoverPoint[] coverPoints;

        private NavMeshAgent agent;
        private RoyalGuardHealth health;
        private RoyalGuardCrossbow crossbow;
        private RoyalGuardCoverPoint reservedCover;
        private Action currentAction = Action.Patrol;
        private Vector3 lastSeenPosition;
        private float lastSeenTime = float.NegativeInfinity;
        private float nextThinkTime;
        private float nextDestinationTime;
        private int waypointIndex;
        private bool canSeePlayer;
        private readonly NavMeshPath path = new NavMeshPath();
        public Action CurrentAction => currentAction;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<RoyalGuardHealth>();
            crossbow = GetComponent<RoyalGuardCrossbow>();
            health.Died += OnDeath;
        }
        private void OnDestroy()
        {
            if (health) health.Died -= OnDeath;
            ReleaseCover();
        }
        private void OnDeath()
        {
            ReleaseCover();
            if (agent && agent.isOnNavMesh) { agent.isStopped = true; agent.ResetPath(); }
            enabled = false;
        }
        private void Update()
        {
            if (health.IsDead || !agent.isOnNavMesh) return;
            if (Time.time >= nextThinkTime)
            {
                nextThinkTime = Time.time + thinkInterval;
                Sense();
                Decide();
            }
            Act();
        }
        private void Sense()
        {
            canSeePlayer = false;
            if (!player) return;
            Vector3 origin = eyes ? eyes.position : transform.position + Vector3.up * 1.6f;
            Vector3 target = player.position + Vector3.up * 1f;
            Vector3 toTarget = target - origin;
            if (toTarget.magnitude > detectionRange) return;
            if (Vector3.Angle(transform.forward, toTarget) > fieldOfView * .5f) return;
            // Ignore triggers; ray must hit player or player's child before any other collider.
            if (Physics.Raycast(origin, toTarget.normalized, out RaycastHit hit, toTarget.magnitude + .15f,
                    sightObstacles, QueryTriggerInteraction.Ignore) &&
                (hit.transform == player || hit.transform.IsChildOf(player)))
            {
                canSeePlayer = true;
                lastSeenPosition = player.position;
                lastSeenTime = Time.time;
            }
        }
        private bool HasMemory => Time.time - lastSeenTime < memorySeconds;
        private float DistanceToPlayer => player ? Vector3.Distance(transform.position, player.position) : float.MaxValue;

        private void Decide()
        {
            float distance = DistanceToPlayer;
            bool close = distance <= attackRange;
            bool threatened = canSeePlayer || HasMemory;
            bool lowHealth = health.Ratio <= retreatHealthThreshold;
            bool hasCover = FindBestCover(out RoyalGuardCoverPoint candidate, out float coverScore);
            bool atReservedCover = reservedCover && Vector3.Distance(transform.position, reservedCover.HidePosition) < 1.8f;

            // Scores in [0,1], intentionally explainable for the VIVA.
            var scores = new Dictionary<Action, float>
            {
                [Action.Patrol] = threatened ? 0f : .35f,
                [Action.Search] = !canSeePlayer && HasMemory ? .72f : 0f,
                [Action.Chase] = threatened && !close ? .65f + .15f * Mathf.Clamp01(distance / detectionRange) : 0f,
                [Action.SeekCover] = threatened && hasCover && !lowHealth && !atReservedCover ? .55f + .35f * coverScore : 0f,
                [Action.Fire] = canSeePlayer && close && crossbow.CanFire ? .88f : 0f,
                [Action.Reload] = crossbow.NeedsReload || crossbow.IsReloading ? .93f : 0f,
                [Action.Retreat] = threatened && lowHealth && hasCover && !atReservedCover ? 1f : 0f
            };
            // Avoid reloading in plain sight when a nearby covered position is available.
            if (scores[Action.Reload] > 0f && threatened && hasCover && !atReservedCover)
                scores[Action.SeekCover] = .96f;

            Action best = currentAction;
            float bestScore = scores[currentAction];
            foreach (var pair in scores)
                if (pair.Value > bestScore + scoreSwitchMargin) { best = pair.Key; bestScore = pair.Value; }
            if (bestScore <= 0f) best = Action.Patrol;

            if (best != currentAction)
            {
                if (best != Action.SeekCover && best != Action.Retreat && best != Action.Reload && best != Action.Fire)
                    ReleaseCover();
                currentAction = best;
                nextDestinationTime = 0f;
            }
            if ((currentAction == Action.SeekCover || currentAction == Action.Retreat || currentAction == Action.Reload)
                && hasCover && (reservedCover == null || !reservedCover.IsAvailableFor(this)))
            {
                if (candidate != reservedCover)
                {
                    ReleaseCover();
                    if (candidate.Reserve(this)) reservedCover = candidate;
                }
            }
        }
        private bool FindBestCover(out RoyalGuardCoverPoint best, out float bestScore)
        {
            best = null; bestScore = 0f;
            if (!player) return false;
            foreach (var cover in coverPoints)
            {
                if (!cover || !cover.IsAvailableFor(this)) continue;
                float distance = Vector3.Distance(transform.position, cover.HidePosition);
                if (distance > coverSearchRadius) continue;
                if (!NavMesh.SamplePosition(cover.HidePosition, out NavMeshHit sample, 1.5f, NavMesh.AllAreas)) continue;
                if (!agent.CalculatePath(sample.position, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                // A candidate counts as cover only when a solid obstacle blocks the player's line to the hide point.
                Vector3 fromPlayer = player.position + Vector3.up;
                Vector3 toCover = sample.position + Vector3.up;
                Vector3 ray = toCover - fromPlayer;
                bool occluded = Physics.Raycast(fromPlayer, ray.normalized, out RaycastHit hit, ray.magnitude,
                    sightObstacles, QueryTriggerInteraction.Ignore) &&
                    hit.transform != transform && !hit.transform.IsChildOf(transform);
                if (!occluded) continue;
                float distanceScore = 1f - Mathf.Clamp01(distance / coverSearchRadius);
                float score = .6f * cover.Quality + .4f * distanceScore;
                if (score > bestScore) { bestScore = score; best = cover; }
            }
            return best != null;
        }
        private void Act()
        {
            switch (currentAction)
            {
                case Action.Patrol: Patrol(); break;
                case Action.Search: MoveTo(lastSeenPosition); break;
                case Action.Chase: if (player) MoveTo(player.position); break;
                case Action.SeekCover:
                case Action.Retreat: if (reservedCover) MoveTo(reservedCover.HidePosition); else if (HasMemory) MoveTo(lastSeenPosition); break;
                case Action.Reload:
                    if (reservedCover) MoveTo(reservedCover.HidePosition);
                    else StopMoving();
                    crossbow.StartReload();
                    break;
                case Action.Fire:
                    StopMoving();
                    if (player && canSeePlayer)
                    {
                        Vector3 aim = player.position + Vector3.up;
                        Vector3 face = aim - transform.position; face.y = 0f;
                        if (face.sqrMagnitude > .01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(face), Time.deltaTime * 8f);
                        crossbow.TryFire(aim, gameObject);
                    }
                    break;
            }
        }
        private void Patrol()
        {
            if (patrolWaypoints == null || patrolWaypoints.Length == 0) { StopMoving(); return; }
            if (!patrolWaypoints[waypointIndex]) { waypointIndex = (waypointIndex + 1) % patrolWaypoints.Length; return; }
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + .5f && agent.hasPath)
                waypointIndex = (waypointIndex + 1) % patrolWaypoints.Length;
            MoveTo(patrolWaypoints[waypointIndex].position);
        }
        private void MoveTo(Vector3 destination)
        {
            if (Time.time < nextDestinationTime) return;
            nextDestinationTime = Time.time + destinationRefreshSeconds;
            if (NavMesh.SamplePosition(destination, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                agent.isStopped = false;
                agent.SetDestination(hit.position);
            }
        }
        private void StopMoving()
        {
            if (!agent.isOnNavMesh) return;
            agent.isStopped = true;
            if (agent.hasPath) agent.ResetPath();
        }
        private void ReleaseCover()
        {
            if (reservedCover) reservedCover.Release(this);
            reservedCover = null;
        }
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRange);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }
}
