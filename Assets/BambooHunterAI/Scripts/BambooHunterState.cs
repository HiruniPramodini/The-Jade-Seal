using System;
using UnityEngine;

namespace JadeSeal.BambooHunter
{
    /// <summary>
    /// Discrete execution states for the Bamboo Hunter Finite State Machine (FSM).
    /// Represents the currently executing behaviour of the autonomous forest archer agent.
    /// </summary>
    public enum BambooHunterState
    {
        /// <summary>
        /// Routine surveillance traversal along authored patrol waypoints or within the encounter boundary.
        /// </summary>
        Patrol = 0,

        /// <summary>
        /// Searching the target's last-known position after direct line-of-sight is lost but memory remains active.
        /// </summary>
        Investigate = 1,

        /// <summary>
        /// Drawing bow tension, acquiring line-of-sight alignment, and tracking target position.
        /// </summary>
        Aim = 2,

        /// <summary>
        /// Releasing an arrow projectile once draw tension, target alignment, and firing line are satisfied.
        /// </summary>
        Shoot = 3,

        /// <summary>
        /// Tactical relocation behind vegetation or into flanking cover to break line-of-sight or gain advantage.
        /// </summary>
        Reposition = 4,

        /// <summary>
        /// Emergency disengagement when critically wounded to seek deep forest concealment and recovery.
        /// </summary>
        Retreat = 5,

        /// <summary>
        /// Defeated state; halts navigation, perception, weapon actuators, and transitions.
        /// </summary>
        Dead = 6
    }

    /// <summary>
    /// High-level tactical intents produced by the Utility AI decision engine.
    /// Distinguishes the strategic goal chosen by continuous utility scoring from the mechanical FSM execution state.
    /// </summary>
    public enum BambooHunterTacticalIntent
    {
        /// <summary>Surveillance patrolling within the Whispering Bamboo Approach.</summary>
        PatrolGrove = 0,

        /// <summary>Inspect last-known target coordinates before memory expires.</summary>
        InvestigateLastSeen = 1,

        /// <summary>Acquire target lock, align posture, and draw bowstring.</summary>
        PrepareShot = 2,

        /// <summary>Release loaded arrow projectile along clean firing line.</summary>
        ReleaseArrow = 3,

        /// <summary>Seek vegetation concealment or flank position behind bamboo cluster.</summary>
        RelocateToVegetationCover = 4,

        /// <summary>Defensive flight to preserve life at critical health.</summary>
        EmergencyRetreat = 5,

        /// <summary>Agent is incapacitated or dead; all tactical intents disabled.</summary>
        CeaseActions = 6
    }

    /// <summary>
    /// Snapshot of normalized utility scores for all discrete tactical states.
    /// Exposed in the Unity Inspector and validation suite for transparent VIVA demonstration.
    /// </summary>
    [Serializable]
    public struct BambooHunterUtilityScores
    {
        public float patrol;
        public float investigate;
        public float aim;
        public float shoot;
        public float reposition;
        public float retreat;
        public float dead;
    }

    /// <summary>
    /// Strongly-typed sensory and tactical context snapshot passed to the utility evaluation engine.
    /// Decoupled from Unity scene GameObjects and MonoBehaviours to enable 100% deterministic testing.
    /// </summary>
    [Serializable]
    public struct BambooHunterDecisionContext
    {
        [Header("Survival Status")]
        public bool isDead;
        public float currentHealth;
        public float maxHealth;

        [Header("Perception & Target Tracking")]
        public bool hasTargetVisibility;
        public bool hasTargetMemory;
        public float memoryAgeSeconds;
        public float distanceToTarget;
        public bool hasUnobstructedShot;

        [Header("Bow & Quiver Readiness")]
        public int currentArrows;
        public bool isBowDrawn;
        public float bowDrawProgress; // Normalized [0.0, 1.0]
        public int consecutiveShotsFired;

        [Header("Tactical Cover & Vegetation")]
        public bool hasReachableCover;
        public float coverQuality;    // Normalized [0.0, 1.0]
        public float coverOcclusion;  // Normalized [0.0, 1.0]
        public bool isAtCover;
        public bool isThreatAimingAtMe;

        /// <summary>
        /// Normalized health ratio bounded in [0.0, 1.0]. Avoids divide-by-zero.
        /// </summary>
        public float HealthRatio
        {
            get
            {
                if (isDead || maxHealth <= 0.0001f) return 0f;
                return Mathf.Clamp01(currentHealth / maxHealth);
            }
        }

        /// <summary>
        /// Returns true if a threat is currently visible or recently remembered.
        /// </summary>
        public bool HasActiveThreat => !isDead && (hasTargetVisibility || hasTargetMemory);

        /// <summary>
        /// Returns true if all physical prerequisites for releasing an arrow are satisfied.
        /// </summary>
        public bool CanPhysicallyShoot =>
            !isDead &&
            hasTargetVisibility &&
            hasUnobstructedShot &&
            currentArrows > 0 &&
            isBowDrawn &&
            bowDrawProgress >= 0.99f;

        /// <summary>
        /// Sanitizes input numeric values to prevent NaN, infinity, or negative metrics from entering scoring.
        /// </summary>
        public void Sanitize()
        {
            if (float.IsNaN(currentHealth) || float.IsInfinity(currentHealth)) currentHealth = 0f;
            if (float.IsNaN(maxHealth) || float.IsInfinity(maxHealth) || maxHealth < 0f) maxHealth = 1f;
            currentHealth = Mathf.Max(0f, currentHealth);
            maxHealth = Mathf.Max(1f, maxHealth);

            if (float.IsNaN(memoryAgeSeconds) || float.IsInfinity(memoryAgeSeconds)) memoryAgeSeconds = 0f;
            memoryAgeSeconds = Mathf.Max(0f, memoryAgeSeconds);

            if (float.IsNaN(distanceToTarget) || float.IsInfinity(distanceToTarget)) distanceToTarget = 0f;
            distanceToTarget = Mathf.Max(0f, distanceToTarget);

            if (float.IsNaN(bowDrawProgress) || float.IsInfinity(bowDrawProgress)) bowDrawProgress = 0f;
            bowDrawProgress = Mathf.Clamp01(bowDrawProgress);

            if (float.IsNaN(coverQuality) || float.IsInfinity(coverQuality)) coverQuality = 0f;
            coverQuality = Mathf.Clamp01(coverQuality);

            if (float.IsNaN(coverOcclusion) || float.IsInfinity(coverOcclusion)) coverOcclusion = 0f;
            coverOcclusion = Mathf.Clamp01(coverOcclusion);

            currentArrows = Mathf.Max(0, currentArrows);
            consecutiveShotsFired = Mathf.Max(0, consecutiveShotsFired);
        }
    }
}
