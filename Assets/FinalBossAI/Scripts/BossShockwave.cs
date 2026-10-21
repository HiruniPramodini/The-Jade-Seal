using System.Collections.Generic;
using UnityEngine;

namespace JadeSeal.FinalBoss
{
    /// <summary>
    /// Attack 3: Magical Stomp Ground Shockwave.
    /// Expands outward along the floor plane.
    /// Deals damage strictly ONCE to any player inside the expanding ring.
    /// Allows dodge via jumping over the shockwave or moving outside the maximum radius.
    /// </summary>
    public sealed class BossShockwave : MonoBehaviour
    {
        [Header("Shockwave Parameters")]
        [SerializeField, Min(1f)] private float maxRadius = 12f;
        [SerializeField, Min(1f)] private float expansionSpeed = 15f;
        [SerializeField, Min(1f)] private float damage = 50f;
        [SerializeField, Min(0.2f)] private float waveThickness = 1.5f;
        [SerializeField, Min(0.5f)] private float maxWaveHeight = 1.8f; // Jump clearance
        [SerializeField] private LayerMask hitLayers = ~0;
        [SerializeField] private Transform visualRing;

        private GameObject owner;
        private float currentRadius;
        private bool isExpanding;
        private readonly HashSet<GameObject> alreadyDamaged = new HashSet<GameObject>();

        public void Initialize(GameObject bossShooter, float waveDamage, float radius, float speed)
        {
            owner = bossShooter;
            damage = waveDamage;
            maxRadius = radius;
            expansionSpeed = speed;
            currentRadius = 0.5f;
            isExpanding = true;
            alreadyDamaged.Clear();

            // Snap shockwave to floor if possible
            if (Physics.Raycast(transform.position + Vector3.up * 1f, Vector3.down, out RaycastHit hit, 3f))
            {
                transform.position = hit.point + Vector3.up * 0.05f;
            }

            // Estimate total duration plus grace period
            float totalDuration = (maxRadius / Mathf.Max(1f, expansionSpeed)) + 0.5f;
            Destroy(gameObject, totalDuration);
        }

        private void Update()
        {
            if (!isExpanding) return;

            currentRadius += expansionSpeed * Time.deltaTime;

            // Update visual ring scale
            if (visualRing != null)
            {
                visualRing.localScale = new Vector3(currentRadius * 2f, 0.1f, currentRadius * 2f);
            }

            // Detect targets in the expanding ring band [currentRadius - waveThickness, currentRadius]
            Collider[] colliders = Physics.OverlapSphere(transform.position, currentRadius, hitLayers, QueryTriggerInteraction.Ignore);
            float innerRadius = Mathf.Max(0f, currentRadius - waveThickness);

            foreach (var col in colliders)
            {
                if (owner != null && (col.transform == owner.transform || col.transform.IsChildOf(owner.transform)))
                    continue;

                GameObject rootTarget = col.transform.root.gameObject;
                if (alreadyDamaged.Contains(rootTarget))
                    continue; // Single-hit guarantee

                Vector3 targetPos = col.bounds.center;
                Vector3 flatOffset = targetPos - transform.position;
                flatOffset.y = 0f;
                float flatDist = flatOffset.magnitude;

                // Check radial band
                if (flatDist >= innerRadius && flatDist <= currentRadius + 0.5f)
                {
                    // Check vertical clearance (allows jumping over the shockwave)
                    float heightAboveGround = col.bounds.min.y - transform.position.y;
                    if (heightAboveGround > maxWaveHeight)
                    {
                        // Player jumped over the wave!
                        continue;
                    }

                    // Apply single-hit damage
                    alreadyDamaged.Add(rootTarget);
                    DeliverDamage(col.gameObject);
                }
            }

            if (currentRadius >= maxRadius)
            {
                isExpanding = false;
                Destroy(gameObject, 0.2f);
            }
        }

        private void DeliverDamage(GameObject hitObject)
        {
            var damageables = hitObject.GetComponentsInParent<MonoBehaviour>();
            foreach (var comp in damageables)
            {
                if (comp is IFinalBossDamageable bossDamageable)
                {
                    bossDamageable.TakeDamage(damage);
                    return;
                }
                if (comp is JadeSeal.RoyalGuard.IRoyalGuardDamageable legacyDamageable)
                {
                    legacyDamageable.TakeDamage(damage);
                    return;
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, maxRadius);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, currentRadius);
        }
    }
}
