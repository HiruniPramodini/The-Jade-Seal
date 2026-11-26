using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace JadeSeal.Archery
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class GuardCoverPoint : MonoBehaviour
    {
        private static readonly List<GuardCoverPoint> Points = new();
        internal static IReadOnlyList<GuardCoverPoint> Active => Points;

        [SerializeField, Min(0.2f)] private float hideClearance = 0.75f;
        [SerializeField, Min(0.2f)] private float crouchedHeight = 0.78f;
        [SerializeField, Min(0.2f)] private float peekClearance = 0.8f;
        private BoxCollider barrier;
        private RoyalGuardAI occupant;

        public bool Available(RoyalGuardAI guard) => !occupant || occupant == guard;

        private void OnEnable()
        {
            barrier = GetComponent<BoxCollider>();
            Points.Add(this);
        }

        private void OnDisable()
        {
            Points.Remove(this);
            occupant = null;
        }

        internal void Reserve(RoyalGuardAI guard) => occupant = guard;
        internal void Release(RoyalGuardAI guard)
        {
            if (occupant == guard) occupant = null;
        }

        internal bool Shelters(Vector3 hide, PlayerHealth threat, LayerMask mask) =>
            ArrowProjectile.Trace(threat.AimPosition, hide + Vector3.up * crouchedHeight,
                threat.transform, mask, out var hit) && hit == barrier;

        internal bool TryPlan(RoyalGuardAI guard, NavMeshAgent agent, PlayerHealth threat, LayerMask mask,
            NavMeshPath path, out Vector3 hide, out Vector3 peek)
        {
            hide = peek = default;
            if (!Available(guard) || !barrier.enabled) return false;
            var bounds = barrier.bounds;
            var away = Vector3.ProjectOnPlane(bounds.center - threat.AimPosition, Vector3.up).normalized;
            if (away.sqrMagnitude < 0.01f) return false;
            var sideways = Vector3.Cross(Vector3.up, away);
            float Extent(Vector3 axis) => Mathf.Abs(axis.x) * bounds.extents.x + Mathf.Abs(axis.z) * bounds.extents.z;
            Vector3 center = new(bounds.center.x, bounds.min.y + 0.05f, bounds.center.z);
            var desired = center + away * (Extent(away) + hideClearance);
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            if (!NavMesh.SamplePosition(desired, out var hidden, 0.65f, filter) ||
                !Shelters(hidden.position, threat, mask) ||
                !agent.CalculatePath(hidden.position, path) || path.status != NavMeshPathStatus.PathComplete)
                return false;

            hide = hidden.position;
            float closest = float.PositiveInfinity;
            for (int side = -1; side <= 1; side += 2)
            for (int step = 0; step < 3; step++)
            {
                var candidate = hide + sideways * (side * (Extent(sideways) + peekClearance + 0.5f * step));
                if (!NavMesh.SamplePosition(candidate, out var exposed, 0.5f, filter) ||
                    NavMesh.Raycast(hide, exposed.position, out _, filter) ||
                    ArrowProjectile.Trace(exposed.position + Vector3.up * 1.5f, threat.AimPosition,
                        guard.transform, mask, out var hit) && !hit.transform.IsChildOf(threat.transform)) continue;

                float cost = (exposed.position - hide).sqrMagnitude;
                if (cost >= closest) continue;
                closest = cost;
                peek = exposed.position;
            }
            return closest < float.PositiveInfinity;
        }

        private void OnDrawGizmosSelected()
        {
            var box = GetComponent<BoxCollider>();
            if (!box) return;
            Gizmos.color = occupant ? Color.yellow : Color.cyan;
            Gizmos.DrawWireCube(box.bounds.center, box.bounds.size);
        }
    }
}
