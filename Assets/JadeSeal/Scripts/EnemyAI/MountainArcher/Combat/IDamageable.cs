using UnityEngine;

namespace JadeSeal.Combat
{
    /// <summary>
    /// Universal combat interface for The Jade Seal.
    /// Can be implemented by Player, Enemy Agents, and Destructibles.
    /// </summary>
    public interface IDamageable
    {
        void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal);
        bool IsDead { get; }
    }
}
