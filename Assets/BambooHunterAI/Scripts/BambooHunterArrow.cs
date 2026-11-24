using System;
using UnityEngine;

namespace JadeSeal.BambooHunter
{
    /// <summary>
    /// Physical bow-and-arrow projectile fired by the Bamboo Hunter.
    /// Utilizes Rigidbody ballistic motion, Continuous collision detection to prevent tunneling,
    /// and strict single-hit atomic damage delivery to avoid duplicate damage events.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [AddComponentMenu("Jade Seal/Bamboo Hunter/Bamboo Hunter Arrow")]
    public sealed class BambooHunterArrow : MonoBehaviour
    {
        [Header("Projectile Properties")]
        [Tooltip("Forward launch speed in meters per second.")]
        [SerializeField] private float speed = 32f;

        [Tooltip("Damage payload delivered on direct impact.")]
        [SerializeField] private float damage = 18f;

        [Tooltip("Lifetime in seconds before in-flight arrow is automatically destroyed.")]
        [SerializeField] private float lifetime = 6.0f;

        [Tooltip("Gravity multiplier applied to ballistic arc flight.")]
        [SerializeField] private float gravityScale = 1.0f;

        [Tooltip("Layer mask filter for impact targets.")]
        [SerializeField] private LayerMask hitLayers = ~0;

        [Tooltip("Whether to orient arrow forward vector along velocity trajectory.")]
        [SerializeField] private bool alignWithTrajectory = true;

        [Tooltip("Duration in seconds arrow remains embedded in target/obstacle before despawning.")]
        [SerializeField] private float stickDuration = 4.0f;

        [Header("Runtime State")]
        [SerializeField] private bool hasImpacted = false;
        [SerializeField] private GameObject shooter;

        private Rigidbody rb;
        private Collider col;
        private float spawnTime;
        private bool isLaunched = false;

        #region Public Properties

        /// <summary>True if arrow has collided with a valid surface or target.</summary>
        public bool HasImpacted => hasImpacted;

        /// <summary>Damage delivered on impact.</summary>
        public float Damage => damage;

        /// <summary>Launch speed in meters per second.</summary>
        public float Speed => speed;

        /// <summary>Gravity scale multiplier.</summary>
        public float GravityScale => gravityScale;

        /// <summary>Reference to the agent that fired this arrow.</summary>
        public GameObject Shooter => shooter;

        /// <summary>Current linear velocity of the arrow projectile.</summary>
        public Vector3 Velocity
        {
            get => rb != null ? rb.linearVelocity : Vector3.zero;
            set { if (rb != null) rb.linearVelocity = value; }
        }

        #endregion

        private void Awake()
        {
            EnsureComponents();
        }

        private void EnsureComponents()
        {
            if (rb == null)
            {
                rb = GetComponent<Rigidbody>();
                if (rb == null)
                {
                    rb = gameObject.AddComponent<Rigidbody>();
                }
            }

            if (col == null)
            {
                col = GetComponent<Collider>();
                if (col == null)
                {
                    var sphere = gameObject.AddComponent<SphereCollider>();
                    sphere.radius = 0.08f;
                    col = sphere;
                }
            }

            // High-speed projectile setting to prevent tunneling through thin geometry
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        /// <summary>
        /// Initializes and launches the arrow projectile along the specified direction vector.
        /// </summary>
        public void Launch(
            Vector3 origin,
            Vector3 direction,
            float launchSpeed,
            float payloadDamage,
            GameObject firingShooter,
            LayerMask? targetLayers = null,
            float? customGravityScale = null,
            float? customLifetime = null)
        {
            EnsureComponents();

            // Sanitize numerical inputs
            if (float.IsNaN(launchSpeed) || float.IsInfinity(launchSpeed) || launchSpeed <= 0f) launchSpeed = 32f;
            if (float.IsNaN(payloadDamage) || float.IsInfinity(payloadDamage) || payloadDamage <= 0f) payloadDamage = 18f;

            speed = launchSpeed;
            damage = payloadDamage;
            shooter = firingShooter;
            hasImpacted = false;
            isLaunched = true;
            spawnTime = Time.time;

            if (targetLayers.HasValue) hitLayers = targetLayers.Value;
            if (customGravityScale.HasValue && !float.IsNaN(customGravityScale.Value)) gravityScale = Mathf.Max(0f, customGravityScale.Value);
            if (customLifetime.HasValue && !float.IsNaN(customLifetime.Value) && customLifetime.Value > 0f) lifetime = customLifetime.Value;

            // Normalize direction safely
            Vector3 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;

            // Position and orient
            transform.position = origin;
            transform.forward = dir;

            // Prevent self-damage or collision with shooter and its child colliders
            if (shooter != null && col != null)
            {
                var shooterColliders = shooter.GetComponentsInChildren<Collider>();
                for (int i = 0; i < shooterColliders.Length; i++)
                {
                    if (shooterColliders[i] != null && shooterColliders[i] != col)
                    {
                        Physics.IgnoreCollision(col, shooterColliders[i], true);
                    }
                }
            }

            // Configure Rigidbody physics
            rb.isKinematic = false;
            rb.useGravity = Mathf.Approximately(gravityScale, 1.0f);
            rb.linearVelocity = dir * speed;
            rb.angularVelocity = Vector3.zero;

            // Schedule lifetime auto-cleanup in Play Mode
            if (Application.isPlaying)
            {
                Destroy(gameObject, lifetime);
            }
        }

        private void FixedUpdate()
        {
            if (hasImpacted || !isLaunched || rb == null || rb.isKinematic) return;

            // Apply custom ballistic gravity acceleration when gravityScale is non-standard
            if (!Mathf.Approximately(gravityScale, 1.0f) && gravityScale > 0f)
            {
                Vector3 extraGravity = Physics.gravity * (gravityScale - (rb.useGravity ? 1.0f : 0f));
                rb.AddForce(extraGravity, ForceMode.Acceleration);
            }

            // Align arrow model with velocity vector during flight
            if (alignWithTrajectory && rb.linearVelocity.sqrMagnitude > 0.25f)
            {
                transform.forward = rb.linearVelocity.normalized;
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            ProcessHit(collision.collider, collision.contacts.Length > 0 ? collision.contacts[0].point : transform.position);
        }

        private void OnTriggerEnter(Collider other)
        {
            ProcessHit(other, transform.position);
        }

        /// <summary>
        /// Atomic single-hit processing ensuring damage is delivered exactly once per projectile.
        /// </summary>
        public void ProcessHit(Collider hitCollider, Vector3 contactPoint)
        {
            if (hasImpacted) return;
            if (hitCollider == null) return;

            // Discard collision with the firing shooter or its hierarchy
            if (shooter != null)
            {
                if (hitCollider.gameObject == shooter || hitCollider.transform.IsChildOf(shooter.transform))
                {
                    return;
                }
            }

            // Layer mask check
            if ((hitLayers.value & (1 << hitCollider.gameObject.layer)) == 0)
            {
                return;
            }

            // Single-hit guard: immediately mark impacted to prevent duplicate callbacks
            hasImpacted = true;

            // Deliver damage via decoupled dispatcher
            BambooHunterDamageAdapter.TryApplyDamage(hitCollider.gameObject, damage, shooter);

            // Halt projectile physics
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }

            if (col != null)
            {
                col.enabled = false;
            }

            // Parent arrow to hit transform so it sticks visually
            if (hitCollider.transform != null && hitCollider.gameObject.activeInHierarchy)
            {
                transform.position = contactPoint;
                transform.SetParent(hitCollider.transform, true);
            }

            // Schedule post-impact cleanup in Play Mode
            if (Application.isPlaying)
            {
                Destroy(gameObject, stickDuration);
            }
        }
    }
}
