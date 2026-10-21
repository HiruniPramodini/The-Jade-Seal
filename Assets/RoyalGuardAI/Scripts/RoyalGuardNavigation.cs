using UnityEngine;
using UnityEngine.AI;
namespace JadeSeal.RoyalGuard
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class RoyalGuardNavigation : MonoBehaviour
    {
        NavMeshAgent agent;
        NavMeshPath path;
        Vector3 checkpoint;
        float nextPlan, progressAt;
        public bool Failed { get; private set; }
        public bool Ready => agent && agent.enabled && agent.isOnNavMesh;
        public bool Arrived => Ready && !agent.pathPending && !agent.isPathStale &&
            (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + .25f);
        void Awake() { path = new NavMeshPath(); agent = GetComponent<NavMeshAgent>(); checkpoint = transform.position; progressAt = Time.time; }
        public bool Validate(Vector3 destination, out Vector3 sampled, out float length)
        {
            sampled = destination; length = 0;
            if (!Ready || !NavMesh.SamplePosition(destination, out NavMeshHit hit, 1.5f, new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask }) ||
                !agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) return false;
            sampled = hit.position;
            Vector3[] corners = path.corners;
            for (int i = 1; i < corners.Length; i++) length += Vector3.Distance(corners[i - 1], corners[i]);
            return true;
        }
        public bool Move(Vector3 destination, RoyalGuardSettings settings)
        {
            if (!Ready) { Failed = true; return false; }
            if (Time.time < nextPlan) return !Failed;
            nextPlan = Time.time + Mathf.Max(.1f, settings.replanSeconds);
            if ((transform.position - checkpoint).sqrMagnitude > .04f) { checkpoint = transform.position; progressAt = Time.time; }
            if (!Validate(destination, out Vector3 sampled, out _) ||
                (!Arrived && Time.time - progressAt > settings.stuckSeconds))
            { Stop(); Failed = true; progressAt = Time.time; return false; }
            agent.isStopped = false;
            Failed = !agent.SetDestination(sampled);
            return !Failed;
        }
        public void Refresh() { nextPlan = 0; progressAt = Time.time; checkpoint = transform.position; Failed = false; }
        public void Stop() { if (Ready) { agent.isStopped = true; agent.ResetPath(); } }
    }
}
