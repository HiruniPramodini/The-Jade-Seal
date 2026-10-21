using UnityEngine;

namespace JadeSeal.RoyalGuard
{
    // Place on an empty GameObject at a safe standing location behind a solid obstacle.
    public sealed class RoyalGuardCoverPoint : MonoBehaviour
    {
        [SerializeField] private Transform peekPosition;
        [SerializeField] private float coverQuality = 1f;
        private RoyalGuardBrain occupant;
        public Vector3 HidePosition => transform.position;
        public Vector3 PeekPosition => peekPosition ? peekPosition.position : transform.position;
        public float Quality => Mathf.Clamp01(coverQuality);
        public bool IsAvailableFor(RoyalGuardBrain guard) => occupant == null || occupant == guard;
        public bool Reserve(RoyalGuardBrain guard)
        {
            if (!IsAvailableFor(guard)) return false;
            occupant = guard;
            return true;
        }
        public void Release(RoyalGuardBrain guard)
        {
            if (occupant == guard) occupant = null;
        }
        private void OnDisable() { occupant = null; }
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, .35f);
            if (peekPosition) Gizmos.DrawLine(transform.position, peekPosition.position);
        }
    }
}
