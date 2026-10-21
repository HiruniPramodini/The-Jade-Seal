using UnityEngine;

namespace JadeSeal.RoyalGuard
{
    [RequireComponent(typeof(Rigidbody), typeof(Collider))]
    public sealed class RoyalGuardProjectile : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float lifetime = 6f;
        [SerializeField, Min(0f)] private float damage = 15f;
        private GameObject owner;
        private bool impacted;

        public void Initialize(GameObject shooter, float projectileDamage)
        {
            owner = shooter;
            damage = projectileDamage;
            if (owner) foreach (var ownCollider in owner.GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(GetComponent<Collider>(), ownCollider);
            Destroy(gameObject, lifetime);
        }
        private void OnCollisionEnter(Collision collision)
        {
            if (impacted || (owner && (collision.transform == owner.transform || collision.transform.IsChildOf(owner.transform)))) return;
            impacted = true;
            // Optional interface avoids coupling this AI to your team's player-health implementation.
            var targets = collision.collider.GetComponentsInParent<MonoBehaviour>();
            foreach (var target in targets)
            {
                if (target is IRoyalGuardDamageable damageable)
                {
                    damageable.TakeDamage(damage);
                    break;
                }
            }
            Destroy(gameObject);
        }
    }
    public interface IRoyalGuardDamageable { void TakeDamage(float damage); }
}

