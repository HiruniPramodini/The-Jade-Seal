using System.Collections.Generic;
using JadeSeal.Archery;
using JadeSeal.Progression;
using UnityEngine;

namespace JadeSeal.Boss
{
    public sealed class BossArenaController : MonoBehaviour
    {
        [SerializeField] private JadeSealBossAI boss;
        [SerializeField] private GameProgression progression;
        [SerializeField] private Transform center;
        [SerializeField] private Vector2[] boundary;
        [SerializeField] private BossMeteorStrike meteor;
        [SerializeField, Min(0.1f)] private float impactRadius = 2.5f;
        [SerializeField, Min(0.1f)] private float warningDuration = 2.5f;
        [SerializeField, Min(0.1f)] private float fallDuration = 0.7f;
        [SerializeField, Min(1)] private int meteorDamage = 30;
        [SerializeField, Min(0.1f)] private float meteorSideOffset = 3f;
        [SerializeField] private LayerMask groundMask = ~0;
        private readonly List<BossMeteorStrike> strikes = new(3);
        private readonly Vector3[] sites = new Vector3[3];
        private PlayerHealth player;
        private readonly RaycastHit[] groundHits = new RaycastHit[16];

        public bool Started { get; private set; }
        public bool Defeated { get; private set; }
        public bool MeteorsActive => strikes.Count > 0;
        public int MeteorImpacts { get; private set; }
        public int MeteorBatches { get; private set; }
        public Vector3 Center => center.position;
        public float MeteorTravelDuration => warningDuration + fallDuration;

        private void Start()
        {
            player = FindAnyObjectByType<PlayerHealth>();
        }

        private void Update()
        {
            if (!Started && (!progression || progression.BossAvailable) &&
                player && !player.IsDead && Mathf.Abs(player.transform.position.y - Center.y) < 3f &&
                Contains(player.transform.position))
            {
                Started = true;
                boss.BeginFight(this, player);
            }
            for (int i = strikes.Count - 1; i >= 0; i--)
                if (!strikes[i] || strikes[i].Resolved) strikes.RemoveAt(i);
        }

        public bool Contains(Vector3 world, float margin = 0f)
        {
            var p = new Vector2(world.x - Center.x, world.z - Center.z);
            bool inside = false;
            for (int i = 0, j = boundary.Length - 1; i < boundary.Length; j = i++)
            {
                Vector2 a = boundary[j], b = boundary[i];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
                if (margin <= 0f) continue;
                Vector2 edge = b - a;
                float along = Mathf.Clamp01(Vector2.Dot(p - a, edge) / Mathf.Max(edge.sqrMagnitude, 0.001f));
                if ((p - (a + edge * along)).sqrMagnitude < margin * margin) return false;
            }
            return inside;
        }

        public Vector3 ClampPush(Vector3 start, Vector3 push, float playerRadius)
        {
            if (!Contains(start)) return Vector3.zero;
            float margin = playerRadius + 0.05f;
            int segments = Mathf.Max(1, Mathf.CeilToInt(push.magnitude / 0.1f));
            float reachable = 0f;
            for (int i = 1; i <= segments; i++)
            {
                float next = (float)i / segments;
                if (!Contains(start + push * next, margin))
                {
                    for (int j = 0; j < 8; j++)
                    {
                        float middle = (reachable + next) * 0.5f;
                        if (Contains(start + push * middle, margin)) reachable = middle;
                        else next = middle;
                    }
                    break;
                }
                reachable = next;
            }
            return push * reachable;
        }

        public void LaunchMeteors(Vector3 sample)
        {
            if (Defeated || !meteor || MeteorsActive) return;
            if (!TryFindMeteorSites(sample))
            {
                Debug.LogWarning("Boss arena could not fit three player-centered meteor sites.", this);
                return;
            }
            MeteorBatches++;
            foreach (Vector3 site in sites)
            {
                var strike = Instantiate(meteor, site, Quaternion.identity, transform);
                strike.Arm(this, player, impactRadius, warningDuration, fallDuration, meteorDamage);
                strikes.Add(strike);
            }
        }

        private bool TryFindMeteorSites(Vector3 sample)
        {
            sample.y = Center.y;
            Vector3 radial = sample - Center;
            Vector3 lateral = radial.sqrMagnitude > 0.001f
                ? Vector3.Cross(Vector3.up, radial.normalized)
                : boss.transform.right;

            int steps = Mathf.Max(1, Mathf.CeilToInt(radial.magnitude / 0.25f));
            for (int i = 0; i <= steps; i++)
            {
                Vector3 candidate = Vector3.Lerp(sample, Center, (float)i / steps);
                if (!TryProjectToFloor(candidate, out sites[1])) continue;
                for (int spacing = 0; spacing <= 4; spacing++)
                {
                    Vector3 offset = lateral * (meteorSideOffset * (1f - spacing * 0.1f));
                    if (TryProjectToFloor(candidate - offset, out sites[0]) &&
                        TryProjectToFloor(candidate + offset, out sites[2])) return true;
                }
            }
            return false;
        }

        private bool TryProjectToFloor(Vector3 candidate, out Vector3 site)
        {
            site = default;
            if (!Contains(candidate, impactRadius + 0.03f)) return false;
            int count = Physics.RaycastNonAlloc(candidate + Vector3.up * 2f, Vector3.down, groundHits,
                4f, groundMask, QueryTriggerInteraction.Ignore);
            float closest = float.PositiveInfinity;
            RaycastHit surface = default;
            for (int i = 0; i < count; i++)
            {
                var hit = groundHits[i];
                if (hit.distance >= closest || hit.collider.GetComponentInParent<CombatHealth>()) continue;
                closest = hit.distance;
                surface = hit;
            }
            if (!surface.collider || surface.normal.y < 0.75f || Mathf.Abs(surface.point.y - Center.y) > 0.35f)
                return false;
            site = surface.point;
            return true;
        }

        internal void CountImpact() => MeteorImpacts++;
        public void EndFight()
        {
            Defeated = true;
            CancelMeteors();
        }

        public void CancelMeteors()
        {
            foreach (var strike in strikes) if (strike)
            {
                strike.gameObject.SetActive(false);
                Destroy(strike.gameObject);
            }
            strikes.Clear();
        }

        private void OnDrawGizmosSelected()
        {
            if (!center || boundary == null) return;
            Gizmos.color = new Color(1, 0.5f, 0.15f);
            for (int i = 0; i < boundary.Length; i++)
            {
                var a = boundary[i]; var b = boundary[(i + 1) % boundary.Length];
                Gizmos.DrawLine(Center + new Vector3(a.x, 0.05f, a.y), Center + new Vector3(b.x, 0.05f, b.y));
            }
        }
    }
}
