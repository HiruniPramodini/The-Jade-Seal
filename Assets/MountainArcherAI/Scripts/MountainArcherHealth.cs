using System;
using UnityEngine;

namespace JadeSeal.MountainArcher
{
    public interface IMountainArcherDamageable
    {
        void TakeDamage(float damage);
    }

    public sealed class MountainArcherHealth : MonoBehaviour, IMountainArcherDamageable
    {
        [Header("Health Balance Settings")]
        [Tooltip("Mountain Archer health: more than Bamboo Hunter (95), less than Royal Guard (180).")]
        [SerializeField, Min(1f)] private float maxHealth = 120f;

        public float MaxHealth => maxHealth;
        public float CurrentHealth { get; private set; }
        public float Ratio => Mathf.Clamp01(CurrentHealth / Mathf.Max(1f, maxHealth));
        public bool IsDead => CurrentHealth <= 0f;

        public event Action<float> Damaged;
        public event Action Died;

        private void Awake()
        {
            CurrentHealth = Mathf.Max(1f, maxHealth);
        }

        public void Recover(float amount, float capRatio)
        {
            if (!IsDead && amount > 0)
            {
                CurrentHealth = Mathf.Max(CurrentHealth, Mathf.Min(maxHealth * Mathf.Clamp01(capRatio), CurrentHealth + amount));
            }
        }

        public void TakeDamage(float damage)
        {
            if (IsDead || damage <= 0f) return;
            CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);
            Damaged?.Invoke(damage);
            if (IsDead) Died?.Invoke();
        }
    }
}
