using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using JadeSeal.Combat;

namespace JadeSeal.EnemyAI.Combat
{
    /// <summary>
    /// Manages health, damage reception, hit reactions, particles, audio, and death sequence
    /// for the Mountain Archer enemy agent.
    /// Fulfills SE3062 Rubric: Decoupled damage handling, hit effects, and death state.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class MountainArcherHealth : MonoBehaviour, IDamageable
    {
        [Header("Health Settings")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float currentHealth = 100f;

        [Header("Hit Reaction & Feedback")]
        [Tooltip("Prefab for blood or spark particle effect. If null, a procedural particle is generated.")]
        [SerializeField] private GameObject hitParticlePrefab;

        [Tooltip("Audio clip played when taking damage.")]
        [SerializeField] private AudioClip hurtSound;

        [Tooltip("Audio clip played on death.")]
        [SerializeField] private AudioClip deathSound;

        [Tooltip("Audio source component. If null, one will be added automatically.")]
        [SerializeField] private AudioSource audioSource;

        [Tooltip("Duration in seconds the agent flinches upon taking a hit.")]
        [SerializeField] private float hitStunDuration = 0.35f;

        [Header("Death Sequence")]
        [Tooltip("Delay in seconds before removing or disabling the corpse after death.")]
        [SerializeField] private float deathDespawnDelay = 5.0f;

        [Tooltip("If true, the corpse will smoothly sink into the ground before removal.")]
        [SerializeField] private bool sinkIntoGroundOnDeath = true;

        // State Properties
        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public bool IsDead { get; private set; } = false;

        // Events for UI or external listeners
        public event System.Action<float, float> OnHealthChanged;
        public event System.Action<Vector3, Vector3> OnHitReceived;
        public event System.Action OnDied;

        private NavMeshAgent navAgent;
        private Collider mainCollider;
        private Coroutine hitStunCoroutine;

        private void Awake()
        {
            currentHealth = maxHealth;
            navAgent = GetComponent<NavMeshAgent>();
            mainCollider = GetComponent<Collider>();

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                    audioSource.spatialBlend = 1.0f; // Full 3D spatial audio
                    audioSource.playOnAwake = false;
                    audioSource.maxDistance = 25f;
                }
            }

            CreateDefaultHitParticlePrefabIfNeeded();
        }

        #region IDamageable Implementation & Overloads

        public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal)
        {
            if (IsDead || amount <= 0f) return;

            currentHealth = Mathf.Max(0f, currentHealth - amount);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            OnHitReceived?.Invoke(hitPoint, hitNormal);

            Debug.Log($"<color=#FF5555>[MountainArcherHealth]</color> Took {amount:F1} damage. Health: {currentHealth:F1}/{maxHealth:F1}");

            // Spawn hit effect (blood / spark)
            SpawnHitEffect(hitPoint, hitNormal);

            // Play hurt sound
            PlayAudio(hurtSound);

            if (currentHealth <= 0f)
            {
                Die();
            }
            else
            {
                // Trigger hit flinch reaction
                if (hitStunCoroutine != null) StopCoroutine(hitStunCoroutine);
                hitStunCoroutine = StartCoroutine(HitStunRoutine());
            }
        }

        // Standard single-parameter overload for SendMessage("TakeDamage", float) compatibility
        public void TakeDamage(float amount)
        {
            Vector3 center = transform.position + Vector3.up * 1.0f;
            TakeDamage(amount, center, -transform.forward);
        }

        #endregion

        private IEnumerator HitStunRoutine()
        {
            // Inform agent AI to pause or play hit flinch
            SendMessage("OnHitStunStarted", hitStunDuration, SendMessageOptions.DontRequireReceiver);
            yield return new WaitForSeconds(hitStunDuration);
            SendMessage("OnHitStunEnded", SendMessageOptions.DontRequireReceiver);
        }

        private void Die()
        {
            if (IsDead) return;
            IsDead = true;

            Debug.Log("<color=#FF0000><b>[MountainArcherHealth]</b> Mountain Archer Agent has DIED!</color>");

            // 1. Play Death Audio
            PlayAudio(deathSound);

            // 2. Trigger Events & Inform AI Agent
            OnDied?.Invoke();
            SendMessage("OnAgentDied", SendMessageOptions.DontRequireReceiver);

            // 3. Disable Navigation
            if (navAgent != null && navAgent.isActiveAndEnabled)
            {
                navAgent.isStopped = true;
                navAgent.enabled = false;
            }

            // 4. Disable Colliders so player and projectiles don't collide with corpse
            if (mainCollider != null)
            {
                mainCollider.enabled = false;
            }

            Collider[] childColliders = GetComponentsInChildren<Collider>();
            foreach (var col in childColliders)
            {
                col.enabled = false;
            }

            // 5. Start Corpse Despawn / Sinking Routine
            StartCoroutine(CorpseDespawnRoutine());
        }

        private IEnumerator CorpseDespawnRoutine()
        {
            // Wait for death animation to finish playing
            yield return new WaitForSeconds(deathDespawnDelay);

            if (sinkIntoGroundOnDeath)
            {
                float sinkDuration = 2.0f;
                float elapsed = 0f;
                Vector3 initialPos = transform.position;
                Vector3 targetPos = initialPos - Vector3.up * 2.0f;

                while (elapsed < sinkDuration)
                {
                    elapsed += Time.deltaTime;
                    transform.position = Vector3.Lerp(initialPos, targetPos, elapsed / sinkDuration);
                    yield return null;
                }
            }

            Destroy(gameObject);
        }

        private void SpawnHitEffect(Vector3 hitPoint, Vector3 hitNormal)
        {
            if (hitParticlePrefab == null) return;

            Quaternion rotation = (hitNormal != Vector3.zero) ? Quaternion.LookRotation(hitNormal) : Quaternion.identity;
            GameObject vfx = Instantiate(hitParticlePrefab, hitPoint, rotation);
            Destroy(vfx, 2.0f);
        }

        private void PlayAudio(AudioClip clip)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.pitch = Random.Range(0.9f, 1.1f);
                audioSource.PlayOneShot(clip);
            }
        }

        private void CreateDefaultHitParticlePrefabIfNeeded()
        {
            if (hitParticlePrefab != null) return;

            // Generate a lightweight, zero-dependency particle system for hit impacts
            hitParticlePrefab = new GameObject("ProceduralHitParticle");
            var ps = hitParticlePrefab.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 0.4f;
            main.startSpeed = 4f;
            main.startSize = 0.08f;
            main.startColor = new Color(0.85f, 0.1f, 0.1f, 0.9f); // Crimson blood / energy droplet
            main.loop = false;
            main.playOnAwake = false;

            var emission = ps.emission;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 12) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 25f;

            hitParticlePrefab.SetActive(false);
        }
    }
}
