using UnityEngine;

namespace JadeSeal.MountainArcher
{
    public sealed class MountainArcherCoverPoint : MonoBehaviour
    {
        [SerializeField] private Transform hidePoint;
        [SerializeField] private Transform peekPoint;
        [SerializeField, Range(0, 1)] private float quality = 1f;

        private MountainArcherBrain occupant;

        public Vector3 HidePosition => hidePoint ? hidePoint.position : transform.position;
        public Vector3 PeekPosition => peekPoint ? peekPoint.position : transform.position + transform.right * 1.2f;
        public float Quality => quality;

        public bool IsAvailableFor(MountainArcherBrain brain) => !occupant || occupant == brain;

        public bool Reserve(MountainArcherBrain brain)
        {
            if (occupant && occupant != brain) return false;
            occupant = brain;
            return true;
        }

        public void Release(MountainArcherBrain brain)
        {
            if (occupant == brain) occupant = null;
        }

        private void OnDisable()
        {
            occupant = null;
        }
    }
}
