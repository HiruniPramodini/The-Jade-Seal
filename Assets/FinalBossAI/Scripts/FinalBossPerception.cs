using UnityEngine;

namespace JadeSeal.FinalBoss
{
    /// <summary>
    /// Magical arena perception for the Final Boss.
    /// Provides continuous omnipresent awareness of the player's position within the boss arena,
    /// regardless of environmental cover or line of sight, while keeping this awareness
    /// scoped strictly to the boss encounter.
    /// </summary>
    public sealed class FinalBossPerception : MonoBehaviour
    {
        [Tooltip("The player transform to track. Can be assigned in Inspector or dynamically bound.")]
        public Transform player;

        [Tooltip("Obstacle layer mask used only for projectile clearance checks, not for player awareness.")]
        public LayerMask obstacleMask = 1;

        [Tooltip("Arena center point. If unassigned, defaults to boss's initial spawn position.")]
        public Transform arenaCenter;

        [Tooltip("Offset added to player position to target torso/chest rather than feet.")]
        public Vector3 aimOffset = new Vector3(0f, 1.1f, 0f);

        public bool EncounterActive { get; set; } = true;
        public Vector3 LastKnownPosition { get; private set; }
        public Vector3 AimPosition => player ? player.position + aimOffset : LastKnownPosition + aimOffset;
        public float DistanceToPlayer => player ? Vector3.Distance(transform.position, player.position) : float.PositiveInfinity;
        public bool PlayerInArena { get; private set; }
        public bool HasValidTarget => player != null && player.gameObject.activeInHierarchy && PlayerInArena && EncounterActive;

        private Vector3 spawnPosition;

        private void Awake()
        {
            spawnPosition = transform.position;
            if (!player)
            {
                // Safe automatic lookup if not assigned
                GameObject foundPlayer = GameObject.FindWithTag("Player");
                if (foundPlayer) player = foundPlayer.transform;
            }
        }

        public void SetTarget(Transform target)
        {
            player = target;
        }

        public void UpdatePerception(FinalBossSettings settings)
        {
            if (!EncounterActive || !player || !player.gameObject.activeInHierarchy)
            {
                PlayerInArena = false;
                return;
            }

            Vector3 center = arenaCenter ? arenaCenter.position : spawnPosition;
            float radius = settings != null ? settings.arenaRadius : 45f;

            // Player is considered in arena if within radius of the arena center
            PlayerInArena = Vector3.Distance(center, player.position) <= radius;

            if (PlayerInArena)
            {
                LastKnownPosition = player.position;
            }
        }

        /// <summary>
        /// Checks if a direct line between from and to is clear of physical level obstacles.
        /// Used by projectile casting to ensure projectiles don't immediately hit a wall in front of the boss.
        /// </summary>
        public bool HasClearLine(Vector3 from, Vector3 to)
        {
            return !Physics.Linecast(from, to, obstacleMask, QueryTriggerInteraction.Ignore);
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 center = arenaCenter ? arenaCenter.position : (Application.isPlaying ? spawnPosition : transform.position);
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
            Gizmos.DrawWireSphere(center, 45f);

            if (player)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position + Vector3.up * 1.5f, AimPosition);
                Gizmos.DrawWireSphere(AimPosition, 0.3f);
            }
        }
    }
}
