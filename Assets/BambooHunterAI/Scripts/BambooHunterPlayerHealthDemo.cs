using System;
using UnityEngine;

namespace JadeSeal.BambooHunter
{
    /// <summary>
    /// Optional isolated demo receiver for prototype testing in the absence of the real player health system.
    /// Implements IBambooHunterDamageable. DO NOT attach to final player controller in integrated FPS.
    /// </summary>
    [AddComponentMenu("Jade Seal/Bamboo Hunter/Demo/Bamboo Hunter Player Health Demo")]
    public sealed class BambooHunterPlayerHealthDemo : MonoBehaviour, IBambooHunterDamageable
    {
        [SerializeField, Min(1f)] private float maxHealth = 100f;
        [SerializeField] private float currentHealth = 100f;
        private int damageEventCount = 0;

        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public float HealthRatio => Mathf.Clamp01(currentHealth / Mathf.Max(1f, maxHealth));
        public bool IsDead => currentHealth <= 0.0001f;
        public int DamageEventCount => damageEventCount;

        public event Action<float> OnDamaged;
        public event Action OnDied;

        private void Awake()
        {
            currentHealth = Mathf.Max(1f, maxHealth);
            damageEventCount = 0;
        }

        public void TakeDamage(float damage)
        {
            if (IsDead || damage <= 0.0001f || float.IsNaN(damage) || float.IsInfinity(damage)) return;

            damageEventCount++;
            currentHealth = Mathf.Max(0f, currentHealth - damage);
            OnDamaged?.Invoke(damage);

            if (currentHealth <= 0.0001f)
            {
                OnDied?.Invoke();
            }
        }

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0.0001f || float.IsNaN(amount) || float.IsInfinity(amount)) return;
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        }

        /// <summary>
        /// Resets player dummy health to maximum and revives if dead.
        /// </summary>
        public void ResetHealth()
        {
            currentHealth = maxHealth;
            damageEventCount = 0;
        }

        /// <summary>
        /// Initializes player dummy max and current health.
        /// </summary>
        public void Initialize(float startingMaxHealth)
        {
            maxHealth = Mathf.Max(1f, startingMaxHealth);
            currentHealth = maxHealth;
        }
    }
}
