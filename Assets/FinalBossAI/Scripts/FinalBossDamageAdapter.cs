using UnityEngine;
using UnityEngine.Events;

namespace JadeSeal.FinalBoss
{
    /// <summary>
    /// Attach to the player root or damage hitbox hierarchy.
    /// Bridges incoming boss damage to the team's actual player health script via Inspector UnityEvent.
    /// Also supports legacy IRoyalGuardDamageable for seamless backwards compatibility.
    /// </summary>
    public sealed class FinalBossDamageAdapter : MonoBehaviour, IFinalBossDamageable, JadeSeal.RoyalGuard.IRoyalGuardDamageable
    {
        [Tooltip("Wire this UnityEvent to your team's real player health method taking a float damage.")]
        public UnityEvent<float> onReceiveDamage = new UnityEvent<float>();

        public void TakeDamage(float damage)
        {
            if (damage > 0f)
            {
                onReceiveDamage.Invoke(damage);
            }
        }
    }
}
