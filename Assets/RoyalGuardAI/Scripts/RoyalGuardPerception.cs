using UnityEngine;
namespace JadeSeal.RoyalGuard
{
    public sealed class RoyalGuardPerception : MonoBehaviour
    {
        public Transform player, eyes;
        [Tooltip("Solid world obstacles only. Exclude guard, player and projectile layers.")]
        public LayerMask obstacles = 1;
        public bool Visible { get; private set; }
        public bool HasMemory(float duration) => Time.time - lastSeen < duration;
        public Vector3 LastPosition { get; private set; }
        float lastSeen = float.NegativeInfinity;
        public Vector3 EyePosition => eyes ? eyes.position : transform.position + Vector3.up * 1.6f;
        public Vector3 AimPosition => player ? player.position + Vector3.up : LastPosition + Vector3.up;
        public bool Clear(Vector3 from, Vector3 to) => !Physics.Linecast(from, to, obstacles, QueryTriggerInteraction.Ignore);
        public void Sample(RoyalGuardSettings settings)
        {
            Visible = false;
            if (!player || !player.gameObject.activeInHierarchy) return;
            Vector3 offset = AimPosition - EyePosition;
            if (offset.sqrMagnitude > settings.detectionRange * settings.detectionRange ||
                Vector3.Angle(transform.forward, offset) > settings.fieldOfView * .5f) return;
            if (!Clear(EyePosition, AimPosition)) return;
            Visible = true;
            LastPosition = player.position;
            lastSeen = Time.time;
        }
        public void Forget() { lastSeen = float.NegativeInfinity; Visible = false; }
    }
}
