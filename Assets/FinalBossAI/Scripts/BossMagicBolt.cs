using UnityEngine;

namespace JadeSeal.FinalBoss
{
    /// <summary>
    /// Attack 1: Standard Magic Projectile.
    /// Fast-moving magical energy projectile aimed directly at the player.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(Collider))]
    public sealed class BossMagicBolt : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float lifetime = 5f;
        [SerializeField, Min(1f)] private float damage = 25f;
        [SerializeField] private GameObject impactEffectPrefab;

        private GameObject owner;
        private bool hasImpacted;
        private Rigidbody rb;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }

        public void Initialize(GameObject shooter, float projectileDamage, float speed, Vector3 targetPosition)
        {
            owner = shooter;
            damage = projectileDamage;

            // Ignore collisions with the boss hierarchy
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
            if (hasImpacted) return;
            if (owner != null && (collision.transform == owner.transform || collision.transform.IsChildOf(owner.transform)))
                return;

            hasImpacted = true;

            // Check for damage receivers on the hit hierarchy
            DeliverDamage(collision.gameObject);

            SpawnImpactFeedback(collision.contacts.Length > 0 ? collision.contacts[0].point : transform.position);
            Destroy(gameObject);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (hasImpacted) return;
            if (owner != null && (other.transform == owner.transform || other.transform.IsChildOf(owner.transform)))
                return;

            if (other.isTrigger) return; // Ignore non-solid triggers

            hasImpacted = true;
            DeliverDamage(other.gameObject);
            SpawnImpactFeedback(other.ClosestPoint(transform.position));
            Destroy(gameObject);
        }

        private void DeliverDamage(GameObject hitObject)
        {
            // Deliver to any interface receiver on parents
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

        private void SpawnImpactFeedback(Vector3 point)
        {
            if (impactEffectPrefab != null)
            {
                Instantiate(impactEffectPrefab, point, Quaternion.identity);
            }
        }
    }
}
