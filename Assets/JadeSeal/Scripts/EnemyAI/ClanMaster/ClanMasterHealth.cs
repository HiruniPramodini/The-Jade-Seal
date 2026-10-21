using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using JadeSeal.Combat;

namespace JadeSeal.EnemyAI.ClanMaster
{
    /// <summary>
    /// Health, Damage Reception, and Death Controller for the Clan Master (Elite Boss).
    /// Implements IDamageable with high health pool and luminous jade hit feedback.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ClanMasterHealth : MonoBehaviour, IDamageable
    {
        [Header("Boss Health Settings")]
        [SerializeField] private float maxHealth = 220f;
        [SerializeField] private float currentHealth = 220f;

        [Header("Feedback")]
        [SerializeField] private AudioClip hurtSound;
        [SerializeField] private AudioClip deathSound;
        [SerializeField] private float deathDespawnDelay = 6.0f;

        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public bool IsDead { get; private set; } = false;

        public event System.Action<float, float> OnHealthChanged;
        public event System.Action<Vector3, Vector3> OnHitReceived;
        public event System.Action OnDied;

        private NavMeshAgent navAgent;
        private Collider mainCollider;
        private AudioSource audioSource;
        private ClanMasterAgent agent;

        private void Awake()
        {
            currentHealth = maxHealth;
            navAgent = GetComponent<NavMeshAgent>();
            mainCollider = GetComponent<Collider>();
            agent = GetComponent<ClanMasterAgent>();

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 1.0f;
                audioSource.playOnAwake = false;
                audioSource.maxDistance = 30f;
            }
        }

        public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal)
        {
            if (IsDead || amount <= 0f) return;

            // Defensive stance reduces damage by 50%
            if (agent != null && agent.IsDefending)
            {
                amount *= 0.5f;
                Debug.Log($"<color=#00FF99>[ClanMasterDefense]</color> Defended against strike! Damage reduced to {amount:F1}.");
            }

            currentHealth = Mathf.Max(0f, currentHealth - amount);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            OnHitReceived?.Invoke(hitPoint, hitNormal);

            if (hurtSound != null) audioSource.PlayOneShot(hurtSound);
            SpawnJadeHitFX(hitPoint, hitNormal);

            Debug.Log($"<color=#FF5555>[ClanMasterHealth]</color> Took {amount:F1} damage. HP: {currentHealth:F1}/{maxHealth:F1}");

            if (currentHealth <= 0f)
            {
                Die();
            }
            else if (agent != null)
            {
                agent.CheckHealthThresholds(currentHealth, maxHealth);
            }
        }

        public void TakeDamage(float amount)
        {
            TakeDamage(amount, transform.position + Vector3.up * 1.3f, -transform.forward);
        }

        private void SpawnJadeHitFX(Vector3 pos, Vector3 normal)
        {
            GameObject fx = new GameObject("JadeHitFX");
            fx.transform.position = pos;
            var ps = fx.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startSize = 0.16f;
            main.startSpeed = 3.5f;
            main.startLifetime = 0.45f;
            main.startColor = new Color(0.1f, 0.95f, 0.55f);
            var emission = ps.emission;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 22) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            Destroy(fx, 1.2f);
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

            Debug.Log($"<color=#00FFCC><b>[ClanMasterHealth]</b></color> The Clan Master has fallen.");
            StartCoroutine(DeathSinkAndDespawn());
        }

        private IEnumerator DeathSinkAndDespawn()
        {
            yield return new WaitForSeconds(3.0f);
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
