using System;
using UnityEngine;

namespace JadeSeal.BambooHunter
{
    /// <summary>
    /// Health, damage reception, death lifecycle, and survival system for the Bamboo Hunter agent.
    /// Implements IBambooHunterDamageable for decoupled damage delivery.
    /// Manages damage mitigation, defensive retreat threshold monitoring, optional delayed
    /// health regeneration during concealment, and clean single-fire death state transitions.
    /// </summary>
    [AddComponentMenu("Jade Seal/Bamboo Hunter/Bamboo Hunter Health")]
    public sealed class BambooHunterHealth : MonoBehaviour, IBambooHunterDamageable
    {
        [Header("Configuration")]
        [SerializeField] private BambooHunterSettings settings;

        [Tooltip("Authored base health points. Overridden by settings if assigned.")]
        [Min(1f)] [SerializeField] private float baseMaxHealth = 120f;

        [Tooltip("Whether to automatically despawn corpse GameObject after death delay.")]
        [SerializeField] private bool autoDespawnOnDeath = false;

        [Tooltip("Delay in seconds before despawning corpse if autoDespawnOnDeath is enabled.")]
        [Min(1f)] [SerializeField] private float corpseDespawnDelay = 5.0f;

        [Header("Runtime State (Read Only)")]
        [SerializeField] private float currentHealth = 120f;
        [SerializeField] private float maxHealth = 120f;
        [SerializeField] private bool isDead = false;
        [SerializeField] private float lastDamageTime = float.NegativeInfinity;
        [SerializeField] private float lastDamageAmount = 0f;

        #region Events

        /// <summary>Fired when valid damage is received. Parameter: damage amount.</summary>
        public event Action<float> OnDamaged;

        /// <summary>Fired when health points are restored. Parameter: heal amount.</summary>
        public event Action<float> OnHealed;

        /// <summary>Fired EXACTLY ONCE when health drops to zero and agent dies.</summary>
        public event Action OnDied;

        #endregion

        #region Public Properties

        /// <summary>Configured ScriptableObject settings.</summary>
        public BambooHunterSettings Settings
        {
            get => settings;
            set => settings = value;
        }

        /// <summary>Current hit points remaining.</summary>
        public float CurrentHealth => currentHealth;

        /// <summary>Maximum possible hit points.</summary>
        public float MaxHealth => maxHealth;

        /// <summary>Normalized health ratio bounded strictly in [0.0, 1.0].</summary>
        public float HealthRatio
        {
            get
            {
                if (isDead || maxHealth <= 0.0001f) return 0f;
                return Mathf.Clamp01(currentHealth / maxHealth);
            }
        }

        /// <summary>True if agent has been defeated and entered death state.</summary>
        public bool IsDead => isDead;

        /// <summary>True if health is below maximum capacity.</summary>
        public bool IsWounded => !isDead && currentHealth < maxHealth;

        /// <summary>True if health ratio has dropped below the defensive retreat threshold.</summary>
        public bool IsInRetreatThreshold
        {
            get
            {
                if (isDead) return false;
                float threshold = settings ? settings.retreatThreshold : 0.25f;
                return HealthRatio <= threshold;
            }
        }

        /// <summary>True if health ratio has recovered to the defensive recovery target.</summary>
        public bool IsRecoveryComplete
        {
            get
            {
                if (isDead) return false;
                float target = settings ? settings.recoveryTarget : 0.50f;
                return HealthRatio >= target;
            }
        }

        /// <summary>Time.time timestamp of most recent damage event.</summary>
        public float LastDamageTime => lastDamageTime;

        /// <summary>Amount of damage delivered in the most recent damage event.</summary>
        public float LastDamageAmount => lastDamageAmount;

        #endregion

        private void Awake()
        {
            Initialize(settings);
        }

        /// <summary>
        /// Explicit initialization helper for runtime and unit test setups.
        /// </summary>
        public void Initialize(BambooHunterSettings config, float? startingHealth = null)
        {
            settings = config;
            maxHealth = settings ? settings.maxHealth : Mathf.Max(1f, baseMaxHealth);

            if (startingHealth.HasValue)
            {
                currentHealth = Mathf.Clamp(startingHealth.Value, 0f, maxHealth);
            }
            else
            {
                currentHealth = maxHealth;
            }

            isDead = currentHealth <= 0.0001f;
            lastDamageTime = float.NegativeInfinity;
            lastDamageAmount = 0f;
        }

        /// <summary>
        /// Resets health to maximum and revives the agent if dead.
        /// </summary>
        public void ResetHealth()
        {
            Initialize(settings);
        }

        /// <summary>
        /// Applies incoming damage payload with strict numerical sanity, death uniqueness,
        /// and post-death invulnerability guarantees.
        /// </summary>
        public void TakeDamage(float damage)
        {
            TakeDamage(damage, Time.time);
        }

        /// <summary>
        /// Overload accepting an explicit timestamp for deterministic testing.
        /// </summary>
        public void TakeDamage(float damage, float currentTime)
        {
            // Invulnerable once dead
            if (isDead) return;

            // Reject invalid, zero, or negative damage payloads safely
            if (float.IsNaN(damage) || float.IsInfinity(damage) || damage <= 0.0001f) return;

            lastDamageAmount = damage;
            lastDamageTime = currentTime;
            currentHealth = Mathf.Max(0f, currentHealth - damage);

            OnDamaged?.Invoke(damage);

            // Atomic death transition: fires exactly once
            if (currentHealth <= 0.0001f && !isDead)
            {
                isDead = true;
                currentHealth = 0f;
                HandleDeath();
            }
        }

        /// <summary>
        /// Restores health points up to maximum capacity. Cannot revive dead agents.
        /// </summary>
        public void Heal(float amount)
        {
            if (isDead) return;
            if (float.IsNaN(amount) || float.IsInfinity(amount) || amount <= 0.0001f) return;

            float previous = currentHealth;
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            float actualHeal = currentHealth - previous;

            if (actualHeal > 0f)
            {
                OnHealed?.Invoke(actualHeal);
            }
        }

        /// <summary>
        /// Simulates delayed health regeneration while concealed in vegetation cover.
        /// Requires that the recovery delay has elapsed since last receiving damage.
        /// Regenerates up to the configured recoveryTarget ratio.
        /// </summary>
        public void UpdateRegeneration(float deltaTime, bool isConcealedInCover, float currentTime = -1f)
        {
            if (isDead || !isConcealedInCover) return;
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime <= 0f) return;

            float timeNow = currentTime >= 0f ? currentTime : Time.time;
            float delay = settings ? settings.recoveryDelay : 3.5f;

            // Enforce delay since last received damage
            if (timeNow - lastDamageTime < delay) return;

            float targetRatio = settings ? settings.recoveryTarget : 0.50f;
            float targetHealth = maxHealth * targetRatio;

            // Cannot regenerate beyond the recovery target cap
            if (currentHealth >= targetHealth) return;

            float rate = settings ? settings.recoveryPerSecond : 3.0f;
            float regenAmount = rate * deltaTime;
            float newHealth = Mathf.Min(targetHealth, currentHealth + regenAmount);
            float actualRegen = newHealth - currentHealth;

            if (actualRegen > 0f)
            {
                currentHealth = newHealth;
                OnHealed?.Invoke(actualRegen);
            }
        }

        /// <summary>
        /// Executes death sequence: invokes OnDied, disables physical colliders,
        /// and optionally schedules corpse cleanup.
        /// </summary>
        private void HandleDeath()
        {
            OnDied?.Invoke();

            // Disable all child colliders to prevent dead body from blocking sightlines or catching arrows
            var colliders = GetComponentsInChildren<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled = false;
                }
            }

            // Despawn corpse in Play Mode if enabled
            if (autoDespawnOnDeath && Application.isPlaying)
            {
                float delay = settings ? settings.corpseDespawnDelay : corpseDespawnDelay;
                Destroy(gameObject, delay);
            }
        }

        /// <summary>
        /// Injects survival health status into the decision context for Utility AI evaluation.
        /// </summary>
        public void PopulateDecisionContext(ref BambooHunterDecisionContext context)
        {
            context.isDead = isDead;
            context.currentHealth = currentHealth;
            context.maxHealth = maxHealth;
        }
    }
}
