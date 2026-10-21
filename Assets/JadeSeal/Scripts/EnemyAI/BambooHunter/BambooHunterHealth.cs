using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using JadeSeal.Combat;

namespace JadeSeal.EnemyAI.BambooHunter
{
    /// <summary>
    /// Health, Damage Reception, and Death sequence for the Bamboo Hunter.
    /// Implements IDamageable interface with hit reactions and particle effects.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class BambooHunterHealth : MonoBehaviour, IDamageable
    {
        [Header("Health Settings")]
        [SerializeField] private float maxHealth = 95f;
        [SerializeField] private float currentHealth = 95f;

        [Header("Hit Feedback")]
        [SerializeField] private AudioClip hurtSound;
        [SerializeField] private AudioClip deathSound;
        [SerializeField] private float deathDespawnDelay = 5.0f;

        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public bool IsDead { get; private set; } = false;

        public event System.Action<float, float> OnHealthChanged;
        public event System.Action<Vector3, Vector3> OnHitReceived;
        public event System.Action OnDied;

        private NavMeshAgent navAgent;
        private Collider mainCollider;
        private AudioSource audioSource;
        private BambooHunterAgent agent;

        private void Awake()
        {
            currentHealth = maxHealth;
            navAgent = GetComponent<NavMeshAgent>();
            mainCollider = GetComponent<Collider>();
            agent = GetComponent<BambooHunterAgent>();

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

            currentHealth = Mathf.Max(0f, currentHealth - amount);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            OnHitReceived?.Invoke(hitPoint, hitNormal);

            if (hurtSound != null) audioSource.PlayOneShot(hurtSound);
            SpawnBloodParticle(hitPoint, hitNormal);

            Debug.Log($"<color=#FF5555>[BambooHunterHealth]</color> Took {amount:F1} damage. HP: {currentHealth:F1}/{maxHealth:F1}");

            if (currentHealth <= 0f)
            {
                Die();
            }
            else if (agent != null)
            {
                agent.TriggerDamageAlert();
            }
        }

        public void TakeDamage(float amount)
        {
            TakeDamage(amount, transform.position + Vector3.up * 1.2f, -transform.forward);
        }

        private void SpawnBloodParticle(Vector3 pos, Vector3 normal)
        {
            GameObject fx = new GameObject("BloodFX");
            fx.transform.position = pos;
            var ps = fx.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startSize = 0.12f;
            main.startSpeed = 2.2f;
            main.startLifetime = 0.35f;
            main.startColor = new Color(0.7f, 0.08f, 0.08f);
            var emission = ps.emission;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 16) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 30f;
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

            Debug.Log($"<color=#33FF33>[BambooHunterHealth]</color> {name} perished in the bamboo forest.");
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
