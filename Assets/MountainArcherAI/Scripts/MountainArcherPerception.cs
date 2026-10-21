using UnityEngine;

namespace JadeSeal.MountainArcher
{
    public sealed class MountainArcherPerception : MonoBehaviour
    {
        public Transform player;
        public Transform eyes;
        public LayerMask obstacles = 1;

        public bool Visible { get; private set; }
        public Vector3 LastPosition { get; private set; }
        public float LastObservedAt { get; private set; } = -999f;

        private void Awake()
        {
            if (!eyes) eyes = transform;
            if (!player)
            {
                var tagged = GameObject.FindWithTag("Player");
                if (tagged) player = tagged.transform;
            }
        }

        public bool HasMemory(float memorySeconds) => Time.time - LastObservedAt <= memorySeconds;

        public bool Clear(Vector3 from, Vector3 to) =>
            !Physics.Linecast(from, to, obstacles, QueryTriggerInteraction.Ignore);

        public void Sample(MountainArcherSettings s)
        {
            if (!player)
            {
                var tagged = GameObject.FindWithTag("Player");
                if (tagged) player = tagged.transform;
            }
            if (!player) { Visible = false; return; }

            Vector3 eyePos = eyes ? eyes.position : transform.position + Vector3.up * 1.5f;
            Vector3 targetPos = player.position + Vector3.up * 1.0f;
            Vector3 delta = targetPos - eyePos;
            float distance = delta.magnitude;

            if (distance > s.detectionRange) { Visible = false; return; }

            float angle = Vector3.Angle(transform.forward, delta.normalized);
            if (angle > s.fieldOfView * .5f) { Visible = false; return; }

            Visible = Clear(eyePos, targetPos);
            if (Visible)
            {
                LastPosition = player.position;
                LastObservedAt = Time.time;
            }
        }
    }
}
