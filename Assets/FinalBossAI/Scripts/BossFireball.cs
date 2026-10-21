using System.Collections.Generic;
using UnityEngine;

namespace JadeSeal.FinalBoss
{
    /// <summary>
    /// Attack 2: Giant Magical Fireball.
    /// High-impact special spell that flies towards the player and explodes on impact,
    /// dealing severe direct and area-of-effect splash damage.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(Collider))]
    public sealed class BossFireball : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float directDamage = 75f;
        [SerializeField, Min(1f)] private float splashDamage = 45f;
        [SerializeField, Min(1f)] private float explosionRadius = 5f;
        [SerializeField, Min(0.1f)] private float lifetime = 6f;
        [SerializeField] private GameObject explosionEffectPrefab;
        [SerializeField] private LayerMask damageableLayers = ~0;

        private GameObject owner;
        private bool hasExploded;
        private Rigidbody rb;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }

        public void Initialize(GameObject shooter, float directDmg, float splashDmg, float radius, float speed, Vector3 targetPosition)
        {
            owner = shooter;
            directDamage = directDmg;
            splashDamage = splashDmg;
            explosionRadius = radius;

            // Ignore collisions with boss
            if (owner != null)
            {
                foreach (var col in owner.GetComponentsInChildren<Collider>())
                {
                    foreach (var myCol in GetComponentsInChildren<Collider>())
                    {
                        Physics.IgnoreCollision(myCol, col, true);
                    }
                }
            }

            Vector3 direction = (targetPosition - transform.position).normalized;
            if (direction.sqrMagnitude < 0.001f) direction = transform.forward;

            transform.rotation = Quaternion.LookRotation(direction);

            if (rb != null)
            {
                rb.useGravity = false;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.linearVelocity = direction * speed;
            }

            Destroy(gameObject, lifetime);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (hasExploded) return;
            if (owner != null && (collision.transform == owner.transform || collision.transform.IsChildOf(owner.transform)))
                return;

            Explode(collision.contacts.Length > 0 ? collision.contacts[0].point : transform.position, collision.gameObject);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (hasExploded) return;
            if (owner != null && (other.transform == owner.transform || other.transform.IsChildOf(owner.transform)))
                return;

            if (other.isTrigger) return;

            Explode(other.ClosestPoint(transform.position), other.gameObject);
        }

        private void Explode(Vector3 center, GameObject directTarget)
        {
            hasExploded = true;

            var damagedTargets = new HashSet<GameObject>();

            // 1. Direct hit damage
            if (directTarget != null)
            {
                ApplyDamageToHierarchy(directTarget, directDamage);
                damagedTargets.Add(directTarget.transform.root.gameObject);
            }

            // 2. Area-of-Effect Splash damage
            Collider[] colliders = Physics.OverlapSphere(center, explosionRadius, damageableLayers, QueryTriggerInteraction.Ignore);
            foreach (var col in colliders)
            {
                if (owner != null && (col.transform == owner.transform || col.transform.IsChildOf(owner.transform)))
                    continue;

                GameObject rootObj = col.transform.root.gameObject;
                if (damagedTargets.Contains(rootObj))
                    continue; // Already took direct hit

                float dist = Vector3.Distance(center, col.transform.position);
                float falloff = Mathf.Clamp01(1f - (dist / Mathf.Max(0.1f, explosionRadius)));
                float calculatedDamage = splashDamage * falloff;

                if (calculatedDamage > 1f)
                {
                    ApplyDamageToHierarchy(col.gameObject, calculatedDamage);
                    damagedTargets.Add(rootObj);
                }
            }

            SpawnExplosionFeedback(center);
            Destroy(gameObject);
        }

        private void ApplyDamageToHierarchy(GameObject target, float amount)
        {
            var damageables = target.GetComponentsInParent<MonoBehaviour>();
            foreach (var comp in damageables)
            {
                if (comp is IFinalBossDamageable bossDamageable)
                {
                    bossDamageable.TakeDamage(amount);
                    return;
                }
                if (comp is JadeSeal.RoyalGuard.IRoyalGuardDamageable legacyDamageable)
                {
                    legacyDamageable.TakeDamage(amount);
                    return;
                }
            }
        }

        private void SpawnExplosionFeedback(Vector3 position)
        {
            if (explosionEffectPrefab != null)
            {
                Instantiate(explosionEffectPrefab, position, Quaternion.identity);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.4f, 0f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, explosionRadius);
        }
    }
}
