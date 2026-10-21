using System;
using UnityEngine;

namespace JadeSeal.FinalBoss
{
    /// <summary>
    /// Manages the Final Boss's massive health pool, damage mitigation through guarding,
    /// and terminal death events.
    /// Implements both IFinalBossDamageable and IRoyalGuardDamageable for backward compatibility.
    /// </summary>
    public sealed class FinalBossHealth : MonoBehaviour, IFinalBossDamageable, JadeSeal.RoyalGuard.IRoyalGuardDamageable
    {
        [SerializeField] private FinalBossSettings settings;
        [SerializeField, Min(100f)] private float overrideMaxHealth = 1500f;

        public float MaxHealth { get; private set; }
        public float CurrentHealth { get; private set; }
        public float Ratio => Mathf.Clamp01(CurrentHealth / Mathf.Max(1f, MaxHealth));
        public bool IsDead => CurrentHealth <= 0f;
        public bool IsGuarding { get; set; }

        public event Action<float, float, bool> Damaged; // (originalDamage, actualDamage, wasGuarded)
        public event Action Died;
        public event Action<float, float> HealthChanged; // (current, max)
        public event Action Guarded;

        private bool diedEventFired;

        private void Awake()
        {
            InitializeHealth();
        }

        public void InitializeHealth()
        {
            MaxHealth = settings != null ? settings.maxHealth : overrideMaxHealth;
            CurrentHealth = MaxHealth;
            diedEventFired = false;
            HealthChanged?.Invoke(CurrentHealth, MaxHealth);
        }

        public void BindSettings(FinalBossSettings newSettings)
        {
            settings = newSettings;
            if (settings != null)
            {
                MaxHealth = settings.maxHealth;
                CurrentHealth = Mathf.Min(CurrentHealth, MaxHealth);
                HealthChanged?.Invoke(CurrentHealth, MaxHealth);
            }
        }

        public void TakeDamage(float damage)
        {
            if (IsDead || damage <= 0f) return;

            float reduction = settings != null ? settings.guardDamageReduction : 0.75f;
            float actualDamage = damage;
            bool wasGuarded = IsGuarding;

            if (wasGuarded)
            {
                actualDamage = damage * (1f - Mathf.Clamp01(reduction));
                Guarded?.Invoke();
            }

            CurrentHealth = Mathf.Max(0f, CurrentHealth - actualDamage);
            Damaged?.Invoke(damage, actualDamage, wasGuarded);
            HealthChanged?.Invoke(CurrentHealth, MaxHealth);

            if (CurrentHealth <= 0f && !diedEventFired)
            {
                diedEventFired = true;
                Died?.Invoke();
            }
        }

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;
            CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
            HealthChanged?.Invoke(CurrentHealth, MaxHealth);
        }
    }
}
