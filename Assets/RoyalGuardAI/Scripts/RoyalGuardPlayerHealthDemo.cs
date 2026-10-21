using UnityEngine;

namespace JadeSeal.RoyalGuard
{
    // Optional test target. Replace with your team's own health system in the integrated FPS.
    public sealed class RoyalGuardPlayerHealthDemo : MonoBehaviour, IRoyalGuardDamageable
    {
        [SerializeField] private float health = 100f;
        public void TakeDamage(float damage)
        {
            health = Mathf.Max(0f, health - Mathf.Max(0f, damage));
            Debug.Log($"Player hit by Royal Guard: {damage} damage, remaining HP={health}");
        }
    }
}
