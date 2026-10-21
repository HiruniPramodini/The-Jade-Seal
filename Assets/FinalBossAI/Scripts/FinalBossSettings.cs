using UnityEngine;

namespace JadeSeal.FinalBoss
{
    [CreateAssetMenu(fileName = "FinalBossSettings", menuName = "Jade Seal/Final Boss Settings")]
    public sealed class FinalBossSettings : ScriptableObject
    {
        [Header("Boss Identity & Health")]
        [Tooltip("Display name shown on the Boss Health HUD.")]
        public string bossName = "THE JADE WARLORD - GUARDIAN OF THE SEAL";

        [Tooltip("Substantially higher health than normal enemies (Royal Guard had 180 HP).")]
        [Min(100f)] public float maxHealth = 1500f;

        [Tooltip("Accumulated damage required in a short window to trigger a hit reaction flinch.")]
        [Min(10f)] public float hitReactionThreshold = 180f;

        [Header("Arena & Perception")]
        [Tooltip("Radius of the designated boss battle arena where the boss maintains magical awareness.")]
        [Min(5f)] public float arenaRadius = 45f;

        [Tooltip("Maximum engagement distance for ranged attacks.")]
        [Min(5f)] public float combatRange = 30f;

        [Tooltip("Distance threshold for melee/stomp proximity.")]
        [Min(1f)] public float meleeRange = 6f;

        [Tooltip("Minimum preferred combat distance when repositioning.")]
        [Min(1f)] public float minPreferredDistance = 8f;

        [Tooltip("Maximum preferred combat distance when repositioning.")]
        [Min(2f)] public float maxPreferredDistance = 16f;

        [Tooltip("Angular rotation tracking speed toward the player (degrees/sec multiplier).")]
        [Min(1f)] public float turnSpeed = 8f;

        [Header("Attack 1: Basic Magic Projectile")]
        [Min(1f)] public float basicDamage = 25f;
        [Min(5f)] public float basicSpeed = 35f;
        [Min(0.5f)] public float basicCooldown = 2.2f;
        [Min(1f)] public float basicCastDelay = 0.35f;
        [Min(1f)] public float basicLifetime = 5f;

        [Header("Attack 2: Giant Magical Fireball")]
        [Min(5f)] public float fireballDamage = 75f;
        [Min(5f)] public float fireballSplashDamage = 45f;
        [Min(1f)] public float fireballExplosionRadius = 5f;
        [Min(5f)] public float fireballSpeed = 22f;
        [Min(2f)] public float fireballCooldown = 11f;
        [Min(0.5f)] public float fireballCastDelay = 0.75f;
        [Min(1f)] public float fireballLifetime = 6f;

        [Header("Attack 3: Magical Stomp & Shockwave")]
        [Min(5f)] public float stompDamage = 50f;
        [Min(3f)] public float stompCooldown = 13f;
        [Min(2f)] public float stompRadius = 12f;
        [Min(2f)] public float stompExpansionSpeed = 15f;
        [Min(0.1f)] public float stompWindupDuration = 0.8f;
        [Min(0.1f)] public float stompRecoveryDuration = 0.6f;

        [Header("Defense & Guarding")]
        [Range(0f, 1f), Tooltip("Percentage of incoming damage mitigated while guarding (0.75 = 75% reduction).")]
        public float guardDamageReduction = 0.75f;

        [Min(0.5f), Tooltip("How long the defensive guard stance lasts.")]
        public float guardDuration = 2.5f;

        [Min(1f), Tooltip("Cooldown between guard activations.")]
        public float guardCooldown = 8f;

        [Range(1, 10), Tooltip("Number of rapid incoming hits to trigger defensive guarding.")]
        public int guardHitTriggerCount = 3;

        [Min(0.5f), Tooltip("Time window to accumulate hits for guard trigger.")]
        public float guardAccumulationWindow = 3f;

        [Header("Movement & Navigation")]
        [Min(0.5f)] public float moveSpeed = 3.2f;
        [Min(0.5f)] public float retreatSpeed = 2.4f;
        [Min(0.1f)] public float stoppingDistance = 2.5f;
        [Min(0.1f)] public float replanInterval = 0.5f;
        [Min(0.5f)] public float stuckTimeout = 2.5f;
    }
}
