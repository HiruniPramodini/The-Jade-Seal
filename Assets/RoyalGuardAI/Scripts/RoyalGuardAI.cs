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
        private Vector3 home;
        private Vector3[] route;
        private NavMeshPath candidatePath;
        private readonly Vector3[] corners = new Vector3[32];
        private int waypoint;
        private float waitUntil;

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

        // Mathematical path array evaluation
        private bool WithinLeash(Vector3 position) => (position - home).sqrMagnitude <= combatLeash * combatLeash;

        private bool PathInsideLeash()
        {
            int count = candidatePath.GetCornersNonAlloc(corners);
            if (count == corners.Length) return false;
            for (int i = 0; i < count; i++)
                if (!WithinLeash(corners[i])) return false;
            return true;
        }
    }
}
