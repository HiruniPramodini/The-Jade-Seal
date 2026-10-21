using UnityEngine;
using UnityEngine.AI;

namespace JadeSeal.FinalBoss
{
    /// <summary>
    /// Handles NavMesh movement, combat spacing, tactical repositioning,
    /// and smooth facing orientation for the Final Boss.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class FinalBossNavigation : MonoBehaviour
    {
        private NavMeshAgent agent;
        private NavMeshPath path;
        private Vector3 lastProgressPosition;
        private float lastProgressTime;
        private float nextReplanTime;

        public bool IsReady => agent != null && agent.enabled && agent.isOnNavMesh;
        public bool IsMoving => IsReady && !agent.isStopped && agent.hasPath && agent.remainingDistance > agent.stoppingDistance;
        public float CurrentSpeed => agent != null ? agent.velocity.magnitude : 0f;
        public bool IsStuck { get; private set; }

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            path = new NavMeshPath();
            lastProgressPosition = transform.position;
            lastProgressTime = Time.time;
        }

        public void ApplySettings(FinalBossSettings settings)
        {
            if (!IsReady || settings == null) return;
            agent.speed = settings.moveSpeed;
            agent.stoppingDistance = settings.stoppingDistance;
        }

        public bool MoveTo(Vector3 destination, FinalBossSettings settings)
        {
            if (!IsReady) return false;

            if (Time.time < nextReplanTime) return true;
            nextReplanTime = Time.time + (settings != null ? settings.replanInterval : 0.5f);

            // Check progress for stuck detection
            if ((transform.position - lastProgressPosition).sqrMagnitude > 0.05f)
            {
                lastProgressPosition = transform.position;
                lastProgressTime = Time.time;
                IsStuck = false;
            }
            else if (Time.time - lastProgressTime > (settings != null ? settings.stuckTimeout : 2.5f))
            {
                IsStuck = true;
                Stop();
                lastProgressTime = Time.time;
                return false;
            }

            // Sample destination on NavMesh
            if (NavMesh.SamplePosition(destination, out NavMeshHit hit, 2f, new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask }))
            {
                if (agent.CalculatePath(hit.position, path) && path.status == NavMeshPathStatus.PathComplete)
                {
                    agent.isStopped = false;
                    return agent.SetDestination(hit.position);
                }
            }

            return false;
        }

        public void Stop()
        {
            if (IsReady)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
        }

        public void RotateTowards(Vector3 targetPosition, float turnSpeed)
        {
            Vector3 direction = targetPosition - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * turnSpeed);
            }
        }
    }
}
