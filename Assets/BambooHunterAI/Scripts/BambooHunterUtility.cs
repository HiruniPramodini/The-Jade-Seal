using System;
using UnityEngine;

namespace JadeSeal.BambooHunter
{
    /// <summary>
    /// Pure, deterministic Utility AI calculation engine for the Bamboo Hunter.
    ///
    /// ARCHITECTURE & ACADEMIC VIVA JUSTIFICATION:
    /// 1. Mathematical Scoring: Each tactical action receives a normalized utility score in [0.0, 1.0].
    ///    The scoring combines sensory evidence (visibility, distance, firing line), internal resource status
    ///    (arrow count, bow draw progress), tactical environmental factors (cover quality, occlusion), and survival need.
    /// 2. Hysteresis Arbitration: To prevent high-frequency decision thrashing between close alternatives,
    ///    an alternate action must exceed the current action's utility by an inertia switch margin (epsilon),
    ///    and the agent must satisfy a minimum dwell time (t_min).
    /// 3. Invariant Safety Overrides: Terminal death and emergency low-health retreat immediately bypass dwell
    ///    times and hysteresis margins, ensuring life-preserving reactivity.
    /// 4. Decoupled Execution: Functions take pure value structs and Settings, containing ZERO MonoBehaviour
    ///    or scene object dependencies, enabling 100% deterministic unit testing and verification.
    /// </summary>
    public static class BambooHunterUtility
    {
        /// <summary>
        /// Computes the normalized utility score U in [0.0, 1.0] for a candidate state given the current context.
        /// </summary>
        public static float Score(BambooHunterState state, BambooHunterDecisionContext c, BambooHunterSettings s)
        {
            if (c.isDead)
            {
                return state == BambooHunterState.Dead ? 1.0f : 0.0f;
            }

            if (state == BambooHunterState.Dead)
            {
                return 0.0f;
            }

            c.Sanitize();

            switch (state)
            {
                case BambooHunterState.Patrol:
                    return ScorePatrol(c, s);

                case BambooHunterState.Investigate:
                    return ScoreInvestigate(c, s);

                case BambooHunterState.Aim:
                    return ScoreAim(c, s);

                case BambooHunterState.Shoot:
                    return ScoreShoot(c, s);

                case BambooHunterState.Reposition:
                    return ScoreReposition(c, s);

                case BambooHunterState.Retreat:
                    return ScoreRetreat(c, s);

                default:
                    return 0.0f;
            }
        }

        /// <summary>
        /// Computes normalized utility scores for all discrete tactical states in a single evaluation.
        /// </summary>
        public static BambooHunterUtilityScores EvaluateAll(BambooHunterDecisionContext c, BambooHunterSettings s)
        {
            return new BambooHunterUtilityScores
            {
                patrol = Score(BambooHunterState.Patrol, c, s),
                investigate = Score(BambooHunterState.Investigate, c, s),
                aim = Score(BambooHunterState.Aim, c, s),
                shoot = Score(BambooHunterState.Shoot, c, s),
                reposition = Score(BambooHunterState.Reposition, c, s),
                retreat = Score(BambooHunterState.Retreat, c, s),
                dead = Score(BambooHunterState.Dead, c, s)
            };
        }

        /// <summary>
        /// PATROL SCORING:
        /// Highest when calm (no visible threat, no recent target memory).
        /// Drops to 0 whenever an active threat is present.
        /// </summary>
        private static float ScorePatrol(BambooHunterDecisionContext c, BambooHunterSettings s)
        {
            if (c.HasActiveThreat) return 0.0f;
            return 0.35f;
        }

        /// <summary>
        /// INVESTIGATE SCORING:
        /// Evaluates desire to search the target's last-seen position.
        /// Active ONLY when direct visibility is lost but target memory remains valid.
        /// Scores highest immediately after line-of-sight loss and decays linearly with memory age.
        /// Formula: U = Base (0.45) + DecayBonus (0.30) * Clamp01(1 - memoryAge / memoryDuration)
        /// </summary>
        private static float ScoreInvestigate(BambooHunterDecisionContext c, BambooHunterSettings s)
        {
            if (c.hasTargetVisibility || !c.hasTargetMemory) return 0.0f;

            float maxMem = Mathf.Max(0.5f, s ? s.memoryDuration : 5f);
            float freshness = Mathf.Clamp01(1.0f - (c.memoryAgeSeconds / maxMem));
            return 0.45f + 0.30f * freshness; // Range: [0.45, 0.75]
        }

        /// <summary>
        /// AIM SCORING:
        /// Evaluates desire to track target, align sightline, and draw bow tension.
        /// Prerequisites:
        /// - Target must be directly visible.
        /// - Must have at least 1 arrow in quiver.
        /// - Must have unobstructed line of fire.
        /// - Target must be within maximum attack range.
        /// - Must not exceed maximum consecutive shots before repositioning.
        /// - If bow is already fully drawn (>= 0.99), Aim is complete and yields to Shoot.
        /// Formula: Multiplies distance suitability curve with clean-shot bonus.
        /// </summary>
        private static float ScoreAim(BambooHunterDecisionContext c, BambooHunterSettings s)
        {
            if (!c.hasTargetVisibility) return 0.0f;
            if (c.currentArrows <= 0) return 0.0f;
            if (!c.hasUnobstructedShot) return 0.0f;

            // When bow is fully drawn, aiming phase is complete; Shoot must execute
            if (c.isBowDrawn && c.bowDrawProgress >= 0.99f) return 0.0f;

            float maxRange = s ? s.attackRange : 20f;
            if (c.distanceToTarget > maxRange || c.distanceToTarget <= 0.001f) return 0.0f;

            int maxShots = s ? s.maxShotsBeforeReposition : 2;
            if (c.consecutiveShotsFired >= maxShots && c.hasReachableCover)
            {
                // Archer must relocate behind vegetation rather than lingering in one spot
                return 0.0f;
            }

            // Distance suitability curve: peaks between preferredMinRange and preferredMaxRange
            float minPref = s ? s.preferredMinRange : 7f;
            float maxPref = s ? s.preferredMaxRange : 16f;
            float distanceSuitability;

            if (c.distanceToTarget < minPref)
            {
                // Too close: point-blank crowding reduces aiming desire, favoring reposition
                distanceSuitability = Mathf.Clamp01(c.distanceToTarget / Mathf.Max(1f, minPref));
            }
            else if (c.distanceToTarget <= maxPref)
            {
                // Sweet spot for archery
                distanceSuitability = 1.0f;
            }
            else
            {
                // Approaching maximum range
                float falloff = (c.distanceToTarget - maxPref) / Mathf.Max(1f, maxRange - maxPref);
                distanceSuitability = Mathf.Clamp01(1.0f - falloff);
            }

            // Base desire to aim when target is valid: [0.65, 0.88]
            return 0.65f + 0.23f * distanceSuitability;
        }

        /// <summary>
        /// SHOOT SCORING:
        /// Evaluates instantaneous arrow release.
        /// STRICT PREREQUISITES:
        /// - Target visible, in range, clean shot, ammo > 0, bow fully drawn (>= 0.99).
        /// When ready, takes absolute precedence (0.95), firing without hesitation.
        /// If ANY prerequisite fails, returns strictly 0.0.
        /// </summary>
        private static float ScoreShoot(BambooHunterDecisionContext c, BambooHunterSettings s)
        {
            if (!c.CanPhysicallyShoot) return 0.0f;

            float maxRange = s ? s.attackRange : 20f;
            if (c.distanceToTarget > maxRange) return 0.0f;

            // Arrow release is an instantaneous execution action
            return 0.95f;
        }

        /// <summary>
        /// REPOSITION SCORING:
        /// Evaluates desire to break line-of-sight behind bamboo clusters or shift tactical angle.
        /// STRICT PREREQUISITE:
        /// - Must have a reachable cover candidate (hasReachableCover == true).
        /// Drivers:
        /// a) Out of arrows in quiver (must seek cover to reload/restock safely).
        /// b) Consecutive shots fired >= threshold (Tactical archer relocation rule).
        /// c) Target visible but shot is obstructed (Needs to maneuver for clear line).
        /// d) Target too close (< preferredMinRange) and moving in.
        /// e) Exposed to threat fire (isThreatAimingAtMe or low cover occlusion).
        /// </summary>
        private static float ScoreReposition(BambooHunterDecisionContext c, BambooHunterSettings s)
        {
            // Reposition is physically impossible without a reachable cover anchor
            if (!c.hasReachableCover) return 0.0f;

            float baseReposition = 0.0f;
            int maxShots = s ? s.maxShotsBeforeReposition : 2;

            if (c.currentArrows <= 0 && (c.hasTargetVisibility || c.hasTargetMemory))
            {
                // Out of ammunition; immediately disengage to vegetation cover
                baseReposition = 0.86f;
            }
            else if (c.consecutiveShotsFired >= maxShots && (c.hasTargetVisibility || c.hasTargetMemory))
            {
                // Tactical duty to break visual contact after firing
                baseReposition = 0.88f;
            }
            else if (c.hasTargetVisibility && !c.hasUnobstructedShot)
            {
                // Target is visible but obstructed by bamboo; maneuver for line of fire
                baseReposition = 0.82f;
            }
            else if (c.hasTargetVisibility && c.distanceToTarget < (s ? s.preferredMinRange : 7f))
            {
                // Target is crowding; back away into forest cover
                baseReposition = 0.84f;
            }
            else if (c.isThreatAimingAtMe && !c.isAtCover)
            {
                // Under incoming threat without concealment:
                // If hunter has already fired opening shots, is wounded, or shot is obstructed, highly prioritize cover (0.80).
                // If fresh, unengaged, and trajectory is clear, prioritize opening shot engagement (0.65).
                bool shouldEvade = (c.consecutiveShotsFired > 0) || (c.HealthRatio < 0.75f) || !c.hasUnobstructedShot;
                baseReposition = shouldEvade ? 0.80f : 0.65f;
            }
            else if (c.hasTargetVisibility && !c.isAtCover)
            {
                // General skirmish repositioning
                baseReposition = 0.60f;
            }

            if (baseReposition <= 0.0f) return 0.0f;

            // Modulate base desire by the quality and occlusion of the candidate cover
            float qualityBonus = 0.10f * Mathf.Clamp01(c.coverQuality);
            float occlusionBonus = 0.08f * Mathf.Clamp01(c.coverOcclusion);

            return Mathf.Clamp01(baseReposition + qualityBonus + occlusionBonus);
        }

        /// <summary>
        /// RETREAT SCORING:
        /// Evaluates emergency survival flight.
        /// Triggers when health falls below retreat threshold AND an active threat exists.
        /// Urgency scales as health approaches zero: U in [0.93, 1.00].
        /// </summary>
        private static float ScoreRetreat(BambooHunterDecisionContext c, BambooHunterSettings s)
        {
            if (!c.HasActiveThreat) return 0.0f;

            float threshold = s ? s.retreatThreshold : 0.25f;
            if (c.HealthRatio > threshold) return 0.0f;

            // Urgency escalates as remaining health ratio drops below threshold
            float deficit = 1.0f - Mathf.Clamp01(c.HealthRatio / Mathf.Max(0.01f, threshold));
            return 0.93f + 0.07f * deficit; // Range: [0.93, 1.00]
        }

        /// <summary>
        /// ARBITRATION & STATE SELECTION:
        /// Selects the best state by evaluating utility scores alongside hysteresis and dwell-time constraints.
        /// </summary>
        public static BambooHunterState Choose(
            BambooHunterDecisionContext context,
            BambooHunterState currentState,
            float stateElapsedTime,
            BambooHunterSettings settings)
        {
            // 1. INVARIANT OVERRIDE: Terminal death cannot be escaped
            if (context.isDead) return BambooHunterState.Dead;
            if (currentState == BambooHunterState.Dead) return BambooHunterState.Dead;

            context.Sanitize();

            // 2. INVARIANT OVERRIDE: Emergency survival retreat interrupts all dwell times
            float retreatScore = Score(BambooHunterState.Retreat, context, settings);
            if (retreatScore > 0.0f)
            {
                return BambooHunterState.Retreat;
            }

            // 3. INVARIANT OVERRIDE: Completed shoot execution automatically transitions
            if (currentState == BambooHunterState.Shoot && !context.isBowDrawn)
            {
                // Arrow released; immediately seek reposition or evaluate next move
                if (context.hasReachableCover) return BambooHunterState.Reposition;
            }

            // 4. Score all viable candidate tactical states
            float currentScore = Score(currentState, context, settings);
            BambooHunterState bestCandidate = currentState;
            float highestScore = 0.0f;
            bool foundViableChallenger = false;

            BambooHunterState bestAlternative = currentState;
            float highestAlternativeScore = 0.0f;

            // Ordered candidate evaluation with deterministic tie-breaking
            // Candidate priority order: Shoot, Aim, Reposition, Investigate, Patrol
            BambooHunterState[] candidates = {
                BambooHunterState.Shoot,
                BambooHunterState.Aim,
                BambooHunterState.Reposition,
                BambooHunterState.Investigate,
                BambooHunterState.Patrol
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                BambooHunterState candidate = candidates[i];
                float score = Score(candidate, context, settings);

                // Candidates must have positive utility (> 0) to be viable
                if (score > 0.0f && score > highestScore)
                {
                    highestScore = score;
                    bestCandidate = candidate;
                    foundViableChallenger = true;
                }

                if (candidate != currentState && score > 0.0f && score > highestAlternativeScore)
                {
                    highestAlternativeScore = score;
                    bestAlternative = candidate;
                }
            }

            // If no candidate scored > 0, fallback gracefully
            if (!foundViableChallenger)
            {
                if (currentScore > 0.0f) return currentState;
                return context.hasReachableCover ? BambooHunterState.Reposition : BambooHunterState.Patrol;
            }

            // 5. If current state has become completely invalid (score == 0), switch immediately
            if (currentScore <= 0.0f)
            {
                return bestCandidate;
            }

            // 6. DWELL TIME PROTECTION: If minimum state duration has not elapsed, hold state
            float minDuration = settings ? settings.minimumStateDuration : 0.5f;
            if (stateElapsedTime < minDuration)
            {
                return currentState;
            }

            // 7. TIMEOUT OVERRIDE: If maximum duration exceeded, force switch to highest challenger
            float maxDuration = settings ? settings.maximumStateDuration : 6.0f;
            if (stateElapsedTime >= maxDuration)
            {
                if (highestAlternativeScore > 0.0f && bestAlternative != currentState)
                {
                    return bestAlternative;
                }

                // If no alternative candidate has positive utility but target is visible,
                // switch to Aim (or Reposition if Aiming) to prevent indefinite freezing.
                if (context.hasTargetVisibility)
                {
                    if (currentState == BambooHunterState.Reposition)
                    {
                        return BambooHunterState.Aim;
                    }
                    if (currentState == BambooHunterState.Aim)
                    {
                        return BambooHunterState.Reposition;
                    }
                }

                return bestCandidate;
            }

            // 8. HYSTERESIS PROTECTION: Challenger must beat current score by switchMargin
            float margin = settings ? settings.switchMargin : 0.08f;
            if (highestScore > currentScore + margin)
            {
                return bestCandidate;
            }

            // Otherwise, stability inertia retains the current state
            return currentState;
        }

        /// <summary>
        /// CALCULATE COVER SCORE:
        /// Evaluates candidate bamboo cover points based on normalized geometric and visibility criteria.
        /// Returns bounded score in [0.0, 1.0].
        /// </summary>
        public static float CalculateCoverScore(
            float distance,
            float pathLength,
            float authoredQuality,
            float vegetationOcclusion,
            float flankAlignmentDot,
            BambooHunterSettings settings)
        {
            float searchRadius = settings ? Mathf.Max(1f, settings.coverSearchRadius) : 18f;

            // Numerical sanity guards
            if (float.IsNaN(distance) || float.IsInfinity(distance)) distance = searchRadius;
            if (float.IsNaN(pathLength) || float.IsInfinity(pathLength)) pathLength = searchRadius * 2.5f;
            if (float.IsNaN(authoredQuality) || float.IsInfinity(authoredQuality)) authoredQuality = 0f;
            if (float.IsNaN(vegetationOcclusion) || float.IsInfinity(vegetationOcclusion)) vegetationOcclusion = 0f;
            if (float.IsNaN(flankAlignmentDot) || float.IsInfinity(flankAlignmentDot)) flankAlignmentDot = 0f;

            float wDist = settings ? Mathf.Max(0f, settings.coverDistanceWeight) : 1.0f;
            float wPath = settings ? Mathf.Max(0f, settings.coverPathLengthWeight) : 1.5f;
            float wOccl = settings ? Mathf.Max(0f, settings.coverOcclusionWeight) : 2.5f;
            float wFlank = settings ? Mathf.Max(0f, settings.coverFlankWeight) : 1.2f;

            float weightSum = wDist + wPath + wOccl + wFlank;
            if (weightSum <= 0.0001f) return 0.0f;

            // Proximity component: closer is better
            float normDist = 1.0f - Mathf.Clamp01(Mathf.Max(0f, distance) / searchRadius);

            // Path efficiency component: direct path is better than detour
            float maxPath = searchRadius * 2.5f;
            float normPath = 1.0f - Mathf.Clamp01(Mathf.Max(0f, pathLength) / maxPath);

            // Occlusion component: line-of-sight blockage by bamboo vegetation
            float normOccl = Mathf.Clamp01(vegetationOcclusion);

            // Flank bonus: angles perpendicular or behind player vision provide tactical advantage
            // flankAlignmentDot in [-1, 1] mapped to [0, 1]
            float normFlank = Mathf.Clamp01((flankAlignmentDot + 1.0f) * 0.5f);

            // Authored cover quality modifier (scaled by authored asset quality [0, 1])
            float baseScore = (wDist * normDist + wPath * normPath + wOccl * normOccl + wFlank * normFlank) / weightSum;
            float qualityFactor = Mathf.Clamp01(authoredQuality);

            return Mathf.Clamp01(baseScore * (0.5f + 0.5f * qualityFactor));
        }

        /// <summary>
        /// CALCULATE COVER SCORE (EXTENDED MULTI-CRITERIA):
        /// Evaluates candidate bamboo cover points based on normalized geometric, visibility,
        /// firing opportunity, and anti-repetition criteria.
        /// Returns bounded score in [0.0, 1.0].
        /// </summary>
        public static float CalculateCoverScore(
            float distance,
            float pathLength,
            float authoredQuality,
            float vegetationOcclusion,
            float flankAlignmentDot,
            float firingOpportunity,
            float repetitionPenalty,
            BambooHunterSettings settings)
        {
            float searchRadius = settings ? Mathf.Max(1f, settings.coverSearchRadius) : 18f;

            // Numerical sanity guards
            if (float.IsNaN(distance) || float.IsInfinity(distance)) distance = searchRadius;
            if (float.IsNaN(pathLength) || float.IsInfinity(pathLength)) pathLength = searchRadius * 2.5f;
            if (float.IsNaN(authoredQuality) || float.IsInfinity(authoredQuality)) authoredQuality = 0f;
            if (float.IsNaN(vegetationOcclusion) || float.IsInfinity(vegetationOcclusion)) vegetationOcclusion = 0f;
            if (float.IsNaN(flankAlignmentDot) || float.IsInfinity(flankAlignmentDot)) flankAlignmentDot = 0f;
            if (float.IsNaN(firingOpportunity) || float.IsInfinity(firingOpportunity)) firingOpportunity = 0f;
            if (float.IsNaN(repetitionPenalty) || float.IsInfinity(repetitionPenalty)) repetitionPenalty = 0f;

            float wDist = settings ? Mathf.Max(0f, settings.coverDistanceWeight) : 1.0f;
            float wPath = settings ? Mathf.Max(0f, settings.coverPathLengthWeight) : 1.5f;
            float wOccl = settings ? Mathf.Max(0f, settings.coverOcclusionWeight) : 2.5f;
            float wFlank = settings ? Mathf.Max(0f, settings.coverFlankWeight) : 1.2f;
            float wFiring = settings ? Mathf.Max(0f, settings.coverFiringWeight) : 1.5f;

            float weightSum = wDist + wPath + wOccl + wFlank + wFiring;
            if (weightSum <= 0.0001f) return 0.0f;

            // Proximity component: closer is better
            float normDist = 1.0f - Mathf.Clamp01(Mathf.Max(0f, distance) / searchRadius);

            // Path efficiency component: direct path is better than detour
            float maxPath = searchRadius * 2.5f;
            float normPath = 1.0f - Mathf.Clamp01(Mathf.Max(0f, pathLength) / maxPath);

            // Occlusion component: line-of-sight blockage by bamboo vegetation
            float normOccl = Mathf.Clamp01(vegetationOcclusion);

            // Flank bonus: angles perpendicular or behind player vision provide tactical advantage
            float normFlank = Mathf.Clamp01((flankAlignmentDot + 1.0f) * 0.5f);

            // Firing opportunity from peek position
            float normFiring = Mathf.Clamp01(firingOpportunity);

            // Base weighted criteria
            float baseScore = (wDist * normDist + wPath * normPath + wOccl * normOccl + wFlank * normFlank + wFiring * normFiring) / weightSum;
            float qualityFactor = Mathf.Clamp01(authoredQuality);

            // Anti-repetition penalty multiplier [0.0, 1.0] (1.0 = no penalty, 0.0 = maximum penalty)
            float penaltyMultiplier = 1.0f - Mathf.Clamp01(repetitionPenalty);

            return Mathf.Clamp01(baseScore * (0.5f + 0.5f * qualityFactor) * penaltyMultiplier);
        }
    }
}
