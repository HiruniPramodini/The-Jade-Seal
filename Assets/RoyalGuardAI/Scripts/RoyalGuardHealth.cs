using System;
using UnityEngine;

namespace JadeSeal.RoyalGuard
{
    public sealed class RoyalGuardHealth : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float maxHealth = 180f;
        public float MaxHealth => maxHealth;
        public float CurrentHealth { get; private set; }
        public float Ratio => Mathf.Clamp01(CurrentHealth / maxHealth);
        public bool IsDead => CurrentHealth <= 0f;
        public event Action<float> Damaged;
        public event Action Died;

        private void Awake() { CurrentHealth = maxHealth; }
        public void TakeDamage(float damage)
        {
            if (IsDead || damage <= 0f) return;
            CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);
            Damaged?.Invoke(damage);
            if (IsDead) Died?.Invoke();
        }
    }
}
