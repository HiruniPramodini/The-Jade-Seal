using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using JadeSeal.Combat;

namespace JadeSeal.EnemyAI.RoyalGuard
{
    /// <summary>
    /// Health, Damage Mitigation, and Death controller for the Royal Guard.
    /// Implements IDamageable with dynamic 75% damage block mitigation.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class RoyalGuardHealth : MonoBehaviour, IDamageable
    {
        [Header("Health Settings")]
        [SerializeField] private float maxHealth = 140f;
        [SerializeField] private float currentHealth = 140f;

        [Header("Blocking & Defense")]
        [Tooltip("Percentage of damage blocked when guard is in defensive stance (0.75 = 75% mitigation).")]
        [Range(0f, 1f)]
        [SerializeField] private float blockMitigation = 0.75f;
        [SerializeField] private AudioClip parrySound;

        [Header("Hit Feedback")]
        [SerializeField] private GameObject hitParticlePrefab;
        [SerializeField] private AudioClip hurtSound;
        [SerializeField] private AudioClip deathSound;
        [SerializeField] private float hitStunDuration = 0.3f;

        [Header("Death Settings")]
        [SerializeField] private float deathDespawnDelay = 5.0f;

        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public bool IsDead { get; private set; } = false;

        public event System.Action<float, float> OnHealthChanged;
        public event System.Action<Vector3, Vector3> OnHitReceived;
        public event System.Action OnBlocked;
        public event System.Action OnDied;

        private NavMeshAgent navAgent;
        private Collider mainCollider;
        private AudioSource audioSource;
        private RoyalGuardAgent agent;

        private void Awake()
        {
            currentHealth = maxHealth;
            navAgent = GetComponent<NavMeshAgent>();
            mainCollider = GetComponent<Collider>();
            agent = GetComponent<RoyalGuardAgent>();

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 1.0f;
                audioSource.playOnAwake = false;
                audioSource.maxDistance = 25f;
            }
        }

        public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal)
        {
            if (IsDead || amount <= 0f) return;

            // Check if Royal Guard is actively blocking
            bool isBlocked = (agent != null && agent.IsBlocking);
            if (isBlocked)
            {
                float mitigatedAmount = amount * (1f - blockMitigation);
                Debug.Log($"<color=#00FFFF>[RoyalGuardDefense]</color> Blocked attack! Mitigated {amount - mitigatedAmount:F1} damage. Took {mitigatedAmount:F1}.");
                amount = mitigatedAmount;
                OnBlocked?.Invoke();
                if (parrySound != null) audioSource.PlayOneShot(parrySound);
            }
            else
            {
                if (hurtSound != null) audioSource.PlayOneShot(hurtSound);
            }

            currentHealth = Mathf.Max(0f, currentHealth - amount);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            OnHitReceived?.Invoke(hitPoint, hitNormal);

            SpawnHitParticle(hitPoint, hitNormal, isBlocked);

            if (currentHealth <= 0f)
            {
                Die();
            }
            else if (!isBlocked && agent != null)
            {
                agent.TriggerHitReaction();
            }
        }

        public void TakeDamage(float amount)
        {
            TakeDamage(amount, transform.position + Vector3.up * 1.2f, -transform.forward);
        }

        private void SpawnHitParticle(Vector3 pos, Vector3 normal, bool blocked)
        {
            GameObject fx = new GameObject("HitFX");
            fx.transform.position = pos;
            var ps = fx.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startSize = 0.15f;
            main.startSpeed = blocked ? 4.5f : 2.5f;
            main.startLifetime = 0.35f;
            main.startColor = blocked ? new Color(1f, 0.85f, 0.2f) : new Color(0.85f, 0.1f, 0.1f);
            var emission = ps.emission;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, blocked ? 25 : 18) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            fx.transform.forward = normal;
            Destroy(fx, 1.0f);
        }

        private void Die()
        {
            if (IsDead) return;
            IsDead = true;

            if (deathSound != null) audioSource.PlayOneShot(deathSound);
            OnDied?.Invoke();

            if (navAgent != null)
            {
                navAgent.isStopped = true;
                navAgent.enabled = false;
            }

            if (mainCollider != null)
            {
                mainCollider.enabled = false;
            }

            Debug.Log($"<color=#FF3333>[RoyalGuardHealth]</color> {name} has fallen in battle.");
            StartCoroutine(DeathSinkAndDespawn());
        }

        private IEnumerator DeathSinkAndDespawn()
        {
            yield return new WaitForSeconds(2.0f);
            float elapsed = 0f;
            Vector3 startPos = transform.position;
            Vector3 endPos = startPos + Vector3.down * 1.8f;
            while (elapsed < 3.0f)
            {
                elapsed += Time.deltaTime;
                transform.position = Vector3.Lerp(startPos, endPos, elapsed / 3.0f);
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
