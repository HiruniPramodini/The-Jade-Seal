using System;
using UnityEngine;

namespace JadeSeal.FinalBoss
{
    /// <summary>
    /// Interface for any entity that can receive damage from the Final Boss or deliver damage to it.
    /// </summary>
    public interface IFinalBossDamageable
    {
        void TakeDamage(float damage);
    }

    /// <summary>
    /// States supported by the Final Boss AI State Machine.
    /// </summary>
    public enum BossState
    {
        IdleCombatReady = 0,
        TrackingPlayer = 1,
        BasicMagicAttack = 2,
        SpecialFireballAttack = 3,
        StompWindup = 4,
        StompImpact = 5,
        Guarding = 6,
        HitReaction = 7,
        Death = 8
    }

    /// <summary>
    /// Types of attacks the boss can execute.
    /// </summary>
    public enum BossAttackType
    {
        None = 0,
        BasicMagic = 1,
        Fireball = 2,
        StompShockwave = 3
    }
}

namespace JadeSeal.RoyalGuard
{
    /// <summary>
    /// Legacy compatibility interface retained so existing player weapons and health systems
    /// function seamlessly with the Final Boss.
    /// </summary>
    public interface IRoyalGuardDamageable
    {
        void TakeDamage(float damage);
    }
}
