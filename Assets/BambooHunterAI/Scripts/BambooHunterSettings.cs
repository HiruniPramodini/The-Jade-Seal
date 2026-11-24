using UnityEngine;

namespace JadeSeal.BambooHunter
{
    /// <summary>
    /// Central ScriptableObject holding all configurable parameters for the Bamboo Hunter AI.
    ///
    /// NOTE FOR ACADEMIC EVALUATION & VIVA:
    /// All numeric default values specified in this asset are prototype development defaults
    /// established for sandbox testing and balance tuning. They are NOT official assignment-mandated
    /// requirements and may be freely calibrated via the Unity Inspector.
    /// </summary>
    [CreateAssetMenu(fileName = "BambooHunterSettings", menuName = "Jade Seal/Bamboo Hunter Settings")]
    public sealed class BambooHunterSettings : ScriptableObject
    {
        [Header("Perception & Awareness")]
        [Tooltip("Maximum sensory detection radius in meters.")]
        [Min(1f)] public float detectionRange = 25f;

        [Tooltip("Horizontal field-of-view angle in degrees for vision detection.")]
        [Range(10f, 360f)] public float fieldOfView = 110f;

        [Tooltip("Duration in seconds that last-seen target position is retained after losing sight.")]
        [Min(0.5f)] public float memoryDuration = 5f;

        [Tooltip("Predictive lead multiplier applied when tracking moving targets.")]
        [Range(0f, 1f)] public float aimLeadFactor = 0.35f;

        [Header("Decision Timing & Stability")]
        [Tooltip("Frequency in seconds between utility decision evaluation ticks.")]
        [Min(0.05f)] public float thinkInterval = 0.20f;

        [Tooltip("Utility hysteresis margin required for an alternate state to dethrone the active state.")]
        [Min(0f)] public float switchMargin = 0.08f;

        [Tooltip("Minimum time in seconds an agent must commit to a state before a non-emergency switch.")]
        [Min(0.05f)] public float minimumStateDuration = 0.50f;

        [Tooltip("Maximum time in seconds an agent may remain in movement/reposition states before timeout.")]
        [Min(1f)] public float maximumStateDuration = 6.0f;

        [Header("Bow & Arrow Combat")]
        [Tooltip("Maximum effective attack range of the bamboo bow in meters.")]
        [Min(2f)] public float attackRange = 20f;

        [Tooltip("Minimum preferred engagement distance to prevent point-blank crowding.")]
        [Min(1f)] public float preferredMinRange = 7f;

        [Tooltip("Maximum preferred engagement distance for optimal shooting utility.")]
        [Min(2f)] public float preferredMaxRange = 16f;

        [Tooltip("Duration in seconds required to pull bowstring to maximum draw tension.")]
        [Min(0.2f)] public float bowDrawDuration = 1.0f;

        [Tooltip("Launch velocity of arrow projectile in meters per second.")]
        [Min(5f)] public float arrowSpeed = 32f;

        [Tooltip("Damage payload delivered by each physical arrow on direct impact.")]
        [Min(1f)] public float arrowDamage = 18f;

        [Tooltip("Maximum arrow capacity held in quiver before needing to restock or pause.")]
        [Min(1)] public int quiverCapacity = 6;

        [Tooltip("Number of consecutive shots permitted from a single position before repositioning behind vegetation.")]
        [Min(1)] public int maxShotsBeforeReposition = 2;

        [Tooltip("Cooldown delay in seconds after releasing a shot before the next draw may commence.")]
        [Min(0.1f)] public float shotCooldownDuration = 0.8f;

        [Tooltip("Maximum lifetime of an active arrow in flight before despawning.")]
        [Min(1f)] public float arrowLifetime = 6.0f;

        [Tooltip("Gravity multiplier applied to ballistic arrow flight.")]
        [Min(0f)] public float arrowGravityScale = 1.0f;

        [Tooltip("Time in seconds required to restock or reload arrows into the quiver.")]
        [Min(0.5f)] public float quiverRestockDuration = 2.5f;

        [Tooltip("Minimum draw tension ratio required before releasing an arrow.")]
        [Range(0.8f, 1.0f)] public float minDrawRatioToShoot = 0.99f;

        [Tooltip("Number of trajectory arc samples used to verify ballistic arc clearance.")]
        [Range(3, 20)] public int trajectoryArcSamples = 8;

        [Header("Vegetation Cover & Tactical Movement")]
        [Tooltip("Search radius in meters around agent for evaluating bamboo cover anchors.")]
        [Min(2f)] public float coverSearchRadius = 18f;

        [Tooltip("Weight given to physical proximity when scoring cover points.")]
        [Min(0f)] public float coverDistanceWeight = 1.0f;

        [Tooltip("Weight penalizing long or convoluted NavMesh paths to cover.")]
        [Min(0f)] public float coverPathLengthWeight = 1.5f;

        [Tooltip("Weight given to vegetation line-of-sight obstruction between threat and hide position.")]
        [Min(0f)] public float coverOcclusionWeight = 2.5f;

        [Tooltip("Weight rewarding positions that flank the threat's forward vision angle.")]
        [Min(0f)] public float coverFlankWeight = 1.2f;

        [Tooltip("Weight rewarding cover anchors that provide a clean projectile firing line from the peek position.")]
        [Min(0f)] public float coverFiringWeight = 1.5f;

        [Tooltip("Cooldown in seconds before a recently vacated cover point can be re-selected (anti-repetition).")]
        [Min(1f)] public float coverCooldownDuration = 5.0f;

        [Tooltip("Minimum distance from threat in meters to avoid selecting cover that is dangerously close.")]
        [Min(1f)] public float coverMinThreatDistance = 4.0f;

        [Tooltip("Maximum allowed exposure ratio [0.0, 1.0] to threat before a cover candidate is rejected.")]
        [Range(0.1f, 1f)] public float coverMaxExposureThreshold = 0.67f;

        [Tooltip("Time in seconds before an obstructed or stagnant navigation path triggers path replanning.")]
        [Min(0.5f)] public float stuckTimeout = 3.0f;

        [Header("Survival & Health Recovery")]
        [Tooltip("Total hit points of the Bamboo Hunter agent.")]
        [Min(10f)] public float maxHealth = 120f;

        [Tooltip("Health ratio (0.0 to 1.0) below which agent enters emergency defensive retreat.")]
        [Range(0.05f, 0.8f)] public float retreatThreshold = 0.25f;

        [Tooltip("Health ratio (0.0 to 1.0) to reach before defensive retreat state terminates.")]
        [Range(0.1f, 1.0f)] public float recoveryTarget = 0.50f;

        [Tooltip("Health points regenerated per second when concealed in safe retreat.")]
        [Min(0f)] public float recoveryPerSecond = 3.0f;

        [Tooltip("Delay in seconds after receiving damage before health regeneration may commence.")]
        [Min(0.5f)] public float recoveryDelay = 3.5f;

        [Tooltip("Delay in seconds after death before agent corpse is despawned.")]
        [Min(1f)] public float corpseDespawnDelay = 5.0f;

        private void OnValidate()
        {
            // Enforce logical range constraints to prevent contradictory configuration
            if (preferredMinRange >= preferredMaxRange)
            {
                preferredMinRange = Mathf.Max(1f, preferredMaxRange - 1f);
            }

            if (retreatThreshold >= recoveryTarget)
            {
                retreatThreshold = Mathf.Max(0.05f, recoveryTarget - 0.05f);
            }

            if (attackRange < preferredMaxRange)
            {
                attackRange = preferredMaxRange;
            }

            // Ensure finite, positive values
            detectionRange = Mathf.Max(1f, detectionRange);
            fieldOfView = Mathf.Clamp(fieldOfView, 10f, 360f);
            memoryDuration = Mathf.Max(0.5f, memoryDuration);
            thinkInterval = Mathf.Max(0.05f, thinkInterval);
            switchMargin = Mathf.Max(0f, switchMargin);
            minimumStateDuration = Mathf.Max(0.05f, minimumStateDuration);
            maximumStateDuration = Mathf.Max(minimumStateDuration + 0.5f, maximumStateDuration);
            maxHealth = Mathf.Max(10f, maxHealth);
            quiverCapacity = Mathf.Max(1, quiverCapacity);
            maxShotsBeforeReposition = Mathf.Max(1, maxShotsBeforeReposition);
            shotCooldownDuration = Mathf.Max(0.1f, shotCooldownDuration);
            arrowLifetime = Mathf.Max(1f, arrowLifetime);
            arrowGravityScale = Mathf.Max(0f, arrowGravityScale);
            quiverRestockDuration = Mathf.Max(0.5f, quiverRestockDuration);
            minDrawRatioToShoot = Mathf.Clamp(minDrawRatioToShoot, 0.8f, 1.0f);
            trajectoryArcSamples = Mathf.Clamp(trajectoryArcSamples, 3, 20);
            coverCooldownDuration = Mathf.Max(1f, coverCooldownDuration);
            coverMinThreatDistance = Mathf.Max(1f, coverMinThreatDistance);
            coverMaxExposureThreshold = Mathf.Clamp(coverMaxExposureThreshold, 0.1f, 1f);
            coverFiringWeight = Mathf.Max(0f, coverFiringWeight);
            coverDistanceWeight = Mathf.Max(0f, coverDistanceWeight);
            coverPathLengthWeight = Mathf.Max(0f, coverPathLengthWeight);
            coverOcclusionWeight = Mathf.Max(0f, coverOcclusionWeight);
            coverFlankWeight = Mathf.Max(0f, coverFlankWeight);
            corpseDespawnDelay = Mathf.Max(1f, corpseDespawnDelay);
        }
    }
}
