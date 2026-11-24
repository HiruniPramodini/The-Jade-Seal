using System;
using UnityEngine;
using UnityEngine.Events;

namespace JadeSeal.BambooHunter
{
    /// <summary>
    /// Decoupled damage receiver contract for entities capable of receiving damage from Bamboo Hunter projectiles.
    /// Provides 100% compile-time decoupling from teammate scripts and external namespaces.
    /// </summary>
    public interface IBambooHunterDamageable
    {
        /// <summary>Applies incoming damage payload.</summary>
        void TakeDamage(float damage);

        /// <summary>True if entity is incapacitated or dead.</summary>
        bool IsDead { get; }
    }

    /// <summary>
    /// Attach to damage hitboxes, player roots, or test targets.
    /// Bridges incoming arrow damage to any health system via UnityEvent or direct method calls,
    /// avoiding tight compile-time coupling with external scripts.
    /// </summary>
    [AddComponentMenu("Jade Seal/Bamboo Hunter/Bamboo Hunter Damage Adapter")]
    public sealed class BambooHunterDamageAdapter : MonoBehaviour, IBambooHunterDamageable
    {
        [Header("Damage Configuration")]
        [Tooltip("Invoked when valid damage is received. Wire to player or enemy health TakeDamage(float) in Inspector.")]
        public UnityEvent<float> onReceiveDamage = new UnityEvent<float>();

        [Tooltip("Invoked when target is destroyed or marked dead.")]
        public UnityEvent onDied = new UnityEvent();

        [Header("Runtime State")]
        [SerializeField] private bool isDead = false;
        [SerializeField] private float lastDamageReceived = 0f;

        /// <summary>True if target has been incapacitated or defeated.</summary>
        public bool IsDead => isDead;

        /// <summary>Most recent valid damage amount processed by this adapter.</summary>
        public float LastDamageReceived => lastDamageReceived;

        /// <summary>
        /// Explicitly marks entity dead or alive, triggering death events if transitioning to dead.
        /// </summary>
        public void SetDead(bool dead)
        {
            isDead = dead;
            if (isDead)
            {
                onDied.Invoke();
            }
        }

        /// <summary>
        /// Applies incoming damage if entity is alive and damage payload is positive and finite.
        /// </summary>
        public void TakeDamage(float damage)
        {
            if (isDead) return;
            if (float.IsNaN(damage) || float.IsInfinity(damage) || damage <= 0f) return;

            lastDamageReceived = damage;
            onReceiveDamage.Invoke(damage);
        }

        /// <summary>
        /// Universal decoupled damage dispatcher.
        /// Delivers damage to IBambooHunterDamageable first, or falls back to SendMessage("TakeDamage")
        /// to integrate with teammate or player health components without reflection or compile-time coupling.
        /// </summary>
        public static bool TryApplyDamage(GameObject target, float damage, GameObject source = null)
        {
            if (target == null) return false;
            if (source != null && (target == source || target.transform.IsChildOf(source.transform))) return false;
            if (float.IsNaN(damage) || float.IsInfinity(damage) || damage <= 0f) return false;

            // 1. Direct typed interface check on target hierarchy
            var damageable = target.GetComponentInParent<IBambooHunterDamageable>();
            if (damageable != null)
            {
                if (damageable.IsDead) return false;
                damageable.TakeDamage(damage);
                return true;
            }

            // 2. Reflection-free decoupled Unity message fallback for player/teammate scripts
            target.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
            return true;
        }
    }
}
