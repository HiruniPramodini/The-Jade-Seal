using UnityEngine;

namespace JadeSeal.MountainArcher
{
    public sealed class MountainArcherPlayerHealthDemo : MonoBehaviour, IMountainArcherDamageable
    {
        [SerializeField, Min(1f)] private float maxHealth = 100f;
        public float CurrentHealth { get; private set; }

        private void Awake()
        {
            CurrentHealth = maxHealth;
        }

        public void TakeDamage(float damage)
        {
            CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);
            Debug.Log($"<color=#00FFFF>[MountainArcherCombat]</color> Player struck by Mountain Archer arrow! Took {damage:F1} damage. HP: {CurrentHealth:F1}/{maxHealth:F1}");
        }
    }
}
