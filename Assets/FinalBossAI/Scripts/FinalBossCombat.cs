using System;
using UnityEngine;

namespace JadeSeal.FinalBoss
{
    /// <summary>
    /// Manages the boss's three distinct magical attacks:
    /// 1. Basic Magic Projectile
    /// 2. Giant Magical Fireball
    /// 3. Magical Stomp Shockwave
    /// Tracks cooldowns, instantiation sockets, and visual effects.
    /// </summary>
    public sealed class FinalBossCombat : MonoBehaviour
    {
        [Header("Spawn Points")]
        [Tooltip("Hand or casting socket for basic magic projectile.")]
        [SerializeField] private Transform magicCastPoint;

        [Tooltip("Overhead or staff socket for the giant fireball.")]
        [SerializeField] private Transform fireballCastPoint;

        [Tooltip("Foot or base point where the stomp shockwave emanates.")]
        [SerializeField] private Transform stompPoint;

        [Header("Projectile & Effect Prefabs")]
        [SerializeField] private BossMagicBolt magicBoltPrefab;
        [SerializeField] private BossFireball fireballPrefab;
        [SerializeField] private BossShockwave shockwavePrefab;

        [Header("Visual Spellcharge Effects (Optional)")]
        [SerializeField] private GameObject basicChargeVFX;
        [SerializeField] private GameObject fireballChargeVFX;
        [SerializeField] private GameObject stompImpactVFX;

        // Cooldown timestamps
        private float nextBasicTime;
        private float nextFireballTime;
        private float nextStompTime;

        public float BasicCooldownRemaining => Mathf.Max(0f, nextBasicTime - Time.time);
        public float FireballCooldownRemaining => Mathf.Max(0f, nextFireballTime - Time.time);
        public float StompCooldownRemaining => Mathf.Max(0f, nextStompTime - Time.time);

        public bool CanCastBasic => Time.time >= nextBasicTime && magicBoltPrefab != null;
        public bool CanCastFireball => Time.time >= nextFireballTime && fireballPrefab != null;
        public bool CanStomp => Time.time >= nextStompTime && shockwavePrefab != null;

        public event Action<BossAttackType> AttackExecuted;

        public void BindPrefabs(BossMagicBolt bolt, BossFireball fireball, BossShockwave shockwave)
        {
            if (bolt != null) magicBoltPrefab = bolt;
            if (fireball != null) fireballPrefab = fireball;
            if (shockwave != null) shockwavePrefab = shockwave;
        }

        public void SetSockets(Transform hand, Transform fireballSocket, Transform foot)
        {
            if (hand != null) magicCastPoint = hand;
            if (fireballSocket != null) fireballCastPoint = fireballSocket;
            if (foot != null) stompPoint = foot;
        }

        public bool FireBasicMagic(Vector3 targetPosition, FinalBossSettings settings)
        {
            if (!CanCastBasic) return false;

            Transform origin = magicCastPoint ? magicCastPoint : transform;
            Vector3 spawnPos = origin.position;

            float damage = settings != null ? settings.basicDamage : 25f;
            float speed = settings != null ? settings.basicSpeed : 35f;
            float cooldown = settings != null ? settings.basicCooldown : 2.2f;

            if (basicChargeVFX != null)
            {
                Instantiate(basicChargeVFX, spawnPos, Quaternion.identity);
            }

            var bolt = Instantiate(magicBoltPrefab, spawnPos, Quaternion.identity);
            bolt.Initialize(gameObject, damage, speed, targetPosition);

            nextBasicTime = Time.time + cooldown;
            AttackExecuted?.Invoke(BossAttackType.BasicMagic);
            return true;
        }

        public bool LaunchFireball(Vector3 targetPosition, FinalBossSettings settings)
        {
            if (!CanCastFireball) return false;

            Transform origin = fireballCastPoint ? fireballCastPoint : (magicCastPoint ? magicCastPoint : transform);
            Vector3 spawnPos = origin.position;

            float directDmg = settings != null ? settings.fireballDamage : 75f;
            float splashDmg = settings != null ? settings.fireballSplashDamage : 45f;
            float radius = settings != null ? settings.fireballExplosionRadius : 5f;
            float speed = settings != null ? settings.fireballSpeed : 22f;
            float cooldown = settings != null ? settings.fireballCooldown : 11f;

            if (fireballChargeVFX != null)
            {
                Instantiate(fireballChargeVFX, spawnPos, Quaternion.identity);
            }

            var fireball = Instantiate(fireballPrefab, spawnPos, Quaternion.identity);
            fireball.Initialize(gameObject, directDmg, splashDmg, radius, speed, targetPosition);

            nextFireballTime = Time.time + cooldown;
            AttackExecuted?.Invoke(BossAttackType.Fireball);
            return true;
        }

        public bool TriggerStompShockwave(FinalBossSettings settings)
        {
            if (!CanStomp) return false;

            Vector3 originPos = stompPoint ? stompPoint.position : transform.position;

            float damage = settings != null ? settings.stompDamage : 50f;
            float radius = settings != null ? settings.stompRadius : 12f;
            float speed = settings != null ? settings.stompExpansionSpeed : 15f;
            float cooldown = settings != null ? settings.stompCooldown : 13f;

            if (stompImpactVFX != null)
            {
                Instantiate(stompImpactVFX, originPos, Quaternion.identity);
            }

            var shockwave = Instantiate(shockwavePrefab, originPos, Quaternion.identity);
            shockwave.Initialize(gameObject, damage, radius, speed);

            nextStompTime = Time.time + cooldown;
            AttackExecuted?.Invoke(BossAttackType.StompShockwave);
            return true;
        }

        public void ResetCooldowns()
        {
            nextBasicTime = 0f;
            nextFireballTime = 0f;
            nextStompTime = 0f;
        }
    }
}
