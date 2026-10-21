using UnityEngine;
using UnityEngine.Events;
namespace JadeSeal.RoyalGuard
{
    // Hook ReceiveDamage(float) to the team's actual player health method in the Inspector.
    public sealed class RoyalGuardDamageAdapter : MonoBehaviour, IRoyalGuardDamageable
    {
        public UnityEvent<float> receiveDamage = new UnityEvent<float>();
        public void TakeDamage(float damage) { if (damage > 0) receiveDamage.Invoke(damage); }
    }
}
