using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace JadeSeal.BambooHunter
{
    /// <summary>
    /// Tactical vegetation cover discovery, scoring, and repositioning system for the Bamboo Hunter AI.
    ///
    /// ARCHITECTURE & DESIGN CONTRACTS:
    /// 1. Explicit Anchor Collection: Prefers inspector-assigned or locally registered BambooHunterCoverPoint anchors.
    ///    Never executes expensive FindObjectsOfType or global hierarchy scans per frame.
    /// 2. Multi-Criteria Tactical Scoring: Evaluates candidate cover points using:
    ///    - Threat line-of-sight occlusion (multi-probe raycasts to hide position).
    ///    - Archery firing line opportunity (clear line-of-fire from peek position).
    ///    - NavMesh path length and straight-line proximity.
    ///    - Preferred bow engagement distance to threat.
    ///    - Flanking angle relative to threat forward vector.
    ///    - Anti-repetition cooldown penalty.
    /// 3. NavMesh Validation: Rejects incomplete, partial, or off-mesh paths using NavMeshAgent path validation.
    /// 4. Environmental Notice: Base Bamboo Forest vegetation does not have physics colliders or NavMesh obstacles.
    ///    This component evaluates authored BambooHunterCoverPoint nodes and optional proxy colliders,
    ///    guaranteeing tactical validity without mutating shared scene assets.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BambooHunterCoverSystem : MonoBehaviour
    {
        [Header("Authored Cover Anchors")]
        [Tooltip("Explicitly assigned cover points in the encounter zone. Primary source of tactical positions.")]
        [SerializeField] private List<BambooHunterCoverPoint> coverPoints = new List<BambooHunterCoverPoint>();

        [Header("Tactical LayerMasks")]
        [Tooltip("Layers that block visual sightlines from threat to hide position (terrain, proxies).")]
        [SerializeField] private LayerMask occlusionObstacles = 1;

        [Tooltip("Layers that block physical arrow trajectories from peek position to threat.")]
        [SerializeField] private LayerMask projectileObstacles = 1;

        [Header("Runtime State (Read Only)")]
        [SerializeField] private BambooHunterCoverPoint selectedCover;
        [SerializeField] private float selectedCoverScore;
        [SerializeField] private bool hasValidCover;
        [SerializeField] private float lastEvaluationTime;

        #region Public Properties

        /// <summary>Currently selected best tactical cover point.</summary>
        public BambooHunterCoverPoint SelectedCover => selectedCover;

        /// <summary>Utility score of the selected cover point in [0.0, 1.0].</summary>
        public float SelectedCoverScore => selectedCoverScore;

        /// <summary>True if a valid, reachable, and acceptable cover point is currently selected.</summary>
        public bool HasValidCover => hasValidCover && selectedCover != null;

        /// <summary>Hide position of the selected cover point, or agent position if none.</summary>
        public Vector3 SelectedHidePosition => selectedCover ? selectedCover.HidePosition : transform.position;

        /// <summary>Peek position of the selected cover point, or agent position if none.</summary>
        public Vector3 SelectedPeekPosition => selectedCover ? selectedCover.PeekPosition : transform.position;

        /// <summary>Number of registered cover anchors.</summary>
        public int RegisteredCoverCount => coverPoints != null ? coverPoints.Count : 0;

        /// <summary>Convenience alias for registered cover count.</summary>
        public int CoverCount => RegisteredCoverCount;

        /// <summary>True if agent is within the protection radius of the currently selected cover point.</summary>
        public bool IsAtCover => selectedCover != null && Vector3.Distance(transform.position, selectedCover.HidePosition) <= selectedCover.ProtectionRadius;

        #endregion

        private void OnDisable()
        {
            ReleaseCover();
        }

        #region Cover Registration API

        /// <summary>
        /// Registers a single cover anchor into the tactical candidate pool.
        /// </summary>
        public void RegisterCoverPoint(BambooHunterCoverPoint point)
        {
            if (point == null) return;
            if (coverPoints == null) coverPoints = new List<BambooHunterCoverPoint>();
            if (!coverPoints.Contains(point))
            {
                coverPoints.Add(point);
            }
        }

        /// <summary>
        /// Unregisters a cover anchor from the candidate pool.
        /// </summary>
        public void UnregisterCoverPoint(BambooHunterCoverPoint point)
        {
            if (coverPoints != null && point != null)
            {
                if (selectedCover == point) ReleaseCover();
                coverPoints.Remove(point);
            }
        }

        /// <summary>
        /// Replaces the active cover candidate collection with an explicit array.
        /// </summary>
        public void SetCoverPoints(BambooHunterCoverPoint[] points)
        {
            ReleaseCover();
            if (coverPoints == null) coverPoints = new List<BambooHunterCoverPoint>();
            coverPoints.Clear();

            if (points != null)
            {
                for (int i = 0; i < points.Length; i++)
                {
                    if (points[i] != null && !coverPoints.Contains(points[i]))
                    {
                        coverPoints.Add(points[i]);
                    }
                }
            }
        }

        #endregion

        #region Tactical Cover Evaluation

        /// <summary>
        /// Evaluates all registered cover points against current agent position, threat perception,
        /// and navigation reachability, selecting the optimal tactical destination.
        /// </summary>
        public bool EvaluateBestCover(
            BambooHunterNavigation nav,
            BambooHunterPerception perception,
            BambooHunterSettings settings,
            bool defensiveOnly)
        {
            lastEvaluationTime = Time.time;

            if (coverPoints == null || coverPoints.Count == 0)
            {
                ReleaseCover();
                return false;
            }

            Vector3 agentPos = transform.position;
            Vector3 threatPos = agentPos;
            Vector3 threatForward = transform.forward;
            bool hasThreat = false;

            if (perception != null && perception.Target != null)
            {
                threatPos = perception.TargetAimPosition;
                threatForward = perception.Target.forward;
                hasThreat = true;
            }
            else if (perception != null && perception.LastSeenTime > 0f)
            {
                threatPos = perception.LastKnownPosition + Vector3.up * 1.5f;
                hasThreat = true;
            }

            float searchRadius = settings ? settings.coverSearchRadius : 18f;
            float cooldownDur = settings ? settings.coverCooldownDuration : 5f;
            float maxExposureThreshold = settings ? settings.coverMaxExposureThreshold : 0.67f;
            float minThreatDist = settings ? settings.coverMinThreatDistance : 4.0f;

            BambooHunterCoverPoint bestCandidate = null;
            float highestScore = -1.0f;

            for (int i = 0; i < coverPoints.Count; i++)
            {
                BambooHunterCoverPoint candidate = coverPoints[i];
                if (candidate == null || !candidate.IsValid) continue;

                // 1. Availability & Anti-Repetition Cooldown Check
                if (!candidate.IsAvailable(this, cooldownDur, Time.time))
                {
                    continue;
                }

                Vector3 hidePos = candidate.HidePosition;

                // 2. Straight-line Search Radius Check
                float distToCover = Vector3.Distance(agentPos, hidePos);
                if (distToCover > searchRadius) continue;

                // 3. Minimum Safe Threat Distance Check
                if (hasThreat)
                {
                    float distCoverToThreat = Vector3.Distance(hidePos, threatPos);
                    if (distCoverToThreat < minThreatDist) continue;
                }

                // 4. NavMesh Complete Path Validation
                float pathLength = distToCover;
                if (nav != null && nav.IsReady)
                {
                    if (!nav.ValidatePath(hidePos, out _, out pathLength))
                    {
                        continue; // Path is incomplete, partial, or off-mesh
                    }
                }

                // 5. Tactical Threat Exposure Probing (Line-of-Sight Blockage)
                float exposure = 0.5f;
                if (hasThreat)
                {
                    exposure = candidate.CalculateExposure(threatPos, occlusionObstacles, 3);
                    if (exposure > maxExposureThreshold)
                    {
                        continue; // Insufficient concealment
                    }
                }

                float occlusion = 1.0f - exposure; // Higher occlusion is better

                // 6. Archery Firing Line Opportunity from Peek Position
                float firingOpportunity = 0f;
                if (hasThreat)
                {
                    Vector3 peekPos = candidate.PeekPosition;
                    bool peekReachable = true;

                    if (nav != null && nav.IsReady)
                    {
                        peekReachable = nav.ValidatePath(peekPos, out _, out _);
                    }

                    if (peekReachable)
                    {
                        Vector3 peekMuzzle = peekPos + Vector3.up * 1.2f;
                        Transform targetTransform = perception != null ? perception.Target : null;
                        bool clearShot = BambooHunterPerception.CheckPathClear(peekMuzzle, threatPos, projectileObstacles, transform, targetTransform, out _);
                        if (clearShot) firingOpportunity = 1.0f;
                    }

                    // For offensive repositioning, an unobstructed shot opportunity is highly favored
                    if (!defensiveOnly && firingOpportunity <= 0.001f && candidate.Quality < 0.8f)
                    {
                        // Moderate penalty if not defensive and cannot shoot from peek
                    }
                }
                else
                {
                    firingOpportunity = 1.0f; // Peaceful / patrol repositioning
                }

                // 7. Tactical Flank Angle Alignment
                float flankDot = 0f;
                if (hasThreat)
                {
                    Vector3 threatToCover = hidePos - threatPos;
                    threatToCover.y = 0f;
                    Vector3 fwd = threatForward;
                    fwd.y = 0f;

                    if (threatToCover.sqrMagnitude > 0.01f && fwd.sqrMagnitude > 0.01f)
                    {
                        // Perpendicular or rearward relative to threat vision gives higher flank value
                        flankDot = -Vector3.Dot(fwd.normalized, threatToCover.normalized);
                    }
                }

                // 8. Anti-Repetition Score Modifier
                float repetitionPenalty = 0f;
                float cooldownRemaining = candidate.CooldownRemaining(cooldownDur, Time.time);
                if (cooldownRemaining > 0f && cooldownDur > 0f)
                {
                    repetitionPenalty = Mathf.Clamp01(cooldownRemaining / cooldownDur);
                }

                // 9. Compute Comprehensive Multi-Criteria Cover Utility Score
                float score = BambooHunterUtility.CalculateCoverScore(
                    distToCover,
                    pathLength,
                    candidate.Quality,
                    occlusion,
                    flankDot,
                    firingOpportunity,
                    repetitionPenalty,
                    settings);

                // Stability margin bonus if candidate is currently selected
                if (candidate == selectedCover && settings != null)
                {
                    score += settings.switchMargin * 0.5f;
                }

                // Deterministic Tie-Breaking: strictly greater preserves earlier candidate on ties
                if (score > highestScore)
                {
                    highestScore = score;
                    bestCandidate = candidate;
                }
            }

            // Apply Selection and Reservation
            if (bestCandidate != null && highestScore > 0f)
            {
                if (bestCandidate != selectedCover)
                {
                    ReleaseCover();
                    bestCandidate.Reserve(this);
                    selectedCover = bestCandidate;
                }

                selectedCoverScore = Mathf.Clamp01(highestScore);
                hasValidCover = true;
                return true;
            }

            ReleaseCover();
            return false;
        }

        /// <summary>
        /// Populates decision context with current cover availability, quality, and occlusion metrics.
        /// </summary>
        public void PopulateDecisionContext(ref BambooHunterDecisionContext context)
        {
            context.hasReachableCover = HasValidCover || (coverPoints != null && coverPoints.Count > 0);
            context.coverQuality = selectedCover != null ? selectedCover.Quality : (coverPoints != null && coverPoints.Count > 0 ? coverPoints[0].Quality : 0f);
            context.coverOcclusion = selectedCover ? selectedCoverScore : (coverPoints != null && coverPoints.Count > 0 ? 0.5f : 0f);

            if (selectedCover != null)
            {
                float distToHide = Vector3.Distance(transform.position, selectedCover.HidePosition);
                context.isAtCover = distToHide <= selectedCover.ProtectionRadius;
            }
            else
            {
                context.isAtCover = false;
            }
        }

        /// <summary>
        /// Releases reservation on currently selected cover point.
        /// </summary>
        public void ReleaseCover()
        {
            if (selectedCover != null)
            {
                selectedCover.Release(this);
                selectedCover = null;
            }

            selectedCoverScore = 0f;
            hasValidCover = false;
        }

        /// <summary>
        /// Marks a specified cover anchor as temporarily rejected and clears selection if active.
        /// </summary>
        public void RejectCover(BambooHunterCoverPoint point, float penaltySeconds = 5.0f)
        {
            if (point != null)
            {
                point.Reject(penaltySeconds);
            }
            if (selectedCover == point)
            {
                RejectCurrentCover(penaltySeconds);
            }
        }

        /// <summary>
        /// Rejects current cover point (e.g., if path became blocked) and clears selection.
        /// </summary>
        public void RejectCurrentCover(float penaltySeconds = 5.0f)
        {
            if (selectedCover != null)
            {
                selectedCover.Reject(penaltySeconds);
                selectedCover = null;
            }

            selectedCoverScore = 0f;
            hasValidCover = false;
        }

        #endregion

        #region Editor Visualization & Gizmos

        private void OnDrawGizmosSelected()
        {
            if (selectedCover != null)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(transform.position, selectedCover.HidePosition);
                Gizmos.DrawWireCube(selectedCover.HidePosition + Vector3.up * 0.5f, Vector3.one * 0.6f);
            }
        }

        #endregion
    }
}
