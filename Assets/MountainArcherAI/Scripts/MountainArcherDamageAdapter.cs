using UnityEngine;

namespace JadeSeal.MountainArcher
{
    public sealed class MountainArcherDamageAdapter : MonoBehaviour
    {
        private MountainArcherHealth health;

        private void Awake()
        {
            health = GetComponent<MountainArcherHealth>();
        }

        public void ApplyDamage(float amount)
        {
            if (health) health.TakeDamage(amount);
        }

        public void TakeDamage(float amount)
        {
            if (health) health.TakeDamage(amount);
        }
    }
}
