using UnityEngine;

namespace JadeSeal.EnemyAI.Combat
{
    /// <summary>
    /// Ballistic arrow projectile fired by the Mountain Archer.
    /// Simulates aerodynamic rotation, gravity arc, and terrain/player collision impact.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(Collider))]
    public class ArrowProjectile : MonoBehaviour
    {
        [Header("Projectile Properties")]
        [SerializeField] private float speed = 28f;
        [SerializeField] private float damage = 25f;
        [SerializeField] private float lifetime = 8f;
        [SerializeField] private LayerMask collisionLayers = ~0;

        private Rigidbody rb;
        private bool hasHit = false;
        private Vector3 previousPosition;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.useGravity = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        public void Launch(Vector3 direction, float initialSpeedOverride = -1f)
        {
            float launchSpeed = initialSpeedOverride > 0 ? initialSpeedOverride : speed;
            rb.isKinematic = false;
            rb.linearVelocity = direction.normalized * launchSpeed;
            transform.forward = direction;
            previousPosition = transform.position;

            Destroy(gameObject, lifetime);
        }

        public void LaunchWithVelocity(Vector3 velocity)
        {
            rb.isKinematic = false;
            rb.linearVelocity = velocity;
            transform.forward = velocity.normalized;
            previousPosition = transform.position;

            Destroy(gameObject, lifetime);
        }

        private void FixedUpdate()
        {
            if (hasHit) return;

            // Align arrow rotation with current ballistic velocity vector
            if (rb.linearVelocity.sqrMagnitude > 0.1f)
            {
                transform.rotation = Quaternion.LookRotation(rb.linearVelocity);
            }

            // Continuous raycast check to prevent clipping through thin geometry at high speeds
            Vector3 currentPos = transform.position;
            Vector3 step = currentPos - previousPosition;
            if (Physics.Raycast(previousPosition, step.normalized, out RaycastHit hit, step.magnitude, collisionLayers))
            {
                OnArrowImpact(hit.collider, hit.point, hit.normal);
            }
            previousPosition = currentPos;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (hasHit) return;
            ContactPoint contact = collision.contacts[0];
            OnArrowImpact(collision.collider, contact.point, contact.normal);
        }

        private void OnArrowImpact(Collider targetCollider, Vector3 point, Vector3 normal)
        {
            if (hasHit) return;
            hasHit = true;

            // Stop physics simulation
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            // Stick arrow into surface
            transform.position = point;
            transform.parent = targetCollider.transform;

            // Check if player or damagable target
            if (targetCollider.CompareTag("Player") || targetCollider.name.ToLower().Contains("player"))
            {
                // Send damage message or interface trigger
                targetCollider.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
                Debug.Log($"[ArrowProjectile] Direct hit on player for {damage} damage!");
            }

            // Despawn embedded arrow after 4 seconds
            Destroy(gameObject, 4f);
        }
    }
}
