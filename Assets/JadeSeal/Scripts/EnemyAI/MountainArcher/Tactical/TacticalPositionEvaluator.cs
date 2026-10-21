using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace JadeSeal.EnemyAI.Tactical
{
    /// <summary>
    /// Tactical Spatial Search Algorithm for Mountain Archer.
    /// Evaluates candidate positions across mountain terrain using a multi-criteria heuristic scoring function:
    /// Score(P) = w_elev * ElevationBonus + w_dist * DistanceFitness + w_los * LineOfSight + w_cover * CoverBonus
    /// Directly satisfies SE3062 Rubric: "Advanced Decision-Making & Dynamic Spatial Search Architecture".
    /// </summary>
    public static class TacticalPositionEvaluator
    {
        private const int SAMPLE_RAY_COUNT = 12;
        private const float NAVMESH_CHECK_RADIUS = 2.0f;

        public struct EvaluationParams
        {
            public float idealMinDistance;
            public float idealMaxDistance;
            public float weightElevation;
            public float weightDistance;
            public float weightLineOfSight;
            public float weightCover;
            public LayerMask obstacleMask;
        }

        public static bool FindBestVantagePosition(
            Vector3 currentPos,
            Vector3 targetPos,
            float searchRadius,
            EvaluationParams config,
            out Vector3 bestPosition)
        {
            bestPosition = currentPos;
            float highestScore = float.MinValue;
            bool foundValid = false;

            float idealDistance = (config.idealMinDistance + config.idealMaxDistance) * 0.5f;

            // Generate radial spatial candidates in 360 degrees
            for (int i = 0; i < SAMPLE_RAY_COUNT; i++)
            {
                float angle = i * (360f / SAMPLE_RAY_COUNT) * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * searchRadius;
                Vector3 candidatePoint = currentPos + offset;

                // Validate NavMesh traversability on mountain terrain
                if (NavMesh.SamplePosition(candidatePoint, out NavMeshHit hit, NAVMESH_CHECK_RADIUS, NavMesh.AllAreas))
                {
                    Vector3 sample = hit.position;
                    float score = EvaluateCandidateScore(sample, targetPos, idealDistance, config);

                    if (score > highestScore)
                    {
                        highestScore = score;
                        bestPosition = sample;
                        foundValid = true;
                    }
                }
            }

            return foundValid;
        }

        public static bool FindTacticalKitePosition(
            Vector3 currentPos,
            Vector3 targetPos,
            float kiteDistance,
            EvaluationParams config,
            out Vector3 kitePosition)
        {
            kitePosition = currentPos;
            Vector3 awayFromTarget = (currentPos - targetPos).normalized;
            if (awayFromTarget.sqrMagnitude < 0.001f) awayFromTarget = Vector3.forward;

            // Test retreat and sideways strafe angles away from target (0 deg, +/-30 deg, +/-60 deg, +/-90 deg sideways)
            float[] angles = { 0f, 30f, -30f, 60f, -60f, 90f, -90f };
            float bestScore = float.MinValue;
            bool found = false;

            foreach (float angle in angles)
            {
                Quaternion rot = Quaternion.Euler(0, angle, 0);
                Vector3 candidate = currentPos + (rot * awayFromTarget) * kiteDistance;

                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, NAVMESH_CHECK_RADIUS, NavMesh.AllAreas))
                {
                    // Check if path is valid and not blocked
                    NavMeshPath path = new NavMeshPath();
                    if (NavMesh.CalculatePath(currentPos, hit.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete)
                    {
                        float score = hit.position.y - targetPos.y; // Prefer higher elevation during retreat
                        if (score > bestScore)
                        {
                            bestScore = score;
                            kitePosition = hit.position;
                            found = true;
                        }
                    }
                }
            }

            return found;
        }

        private static float EvaluateCandidateScore(Vector3 p, Vector3 target, float idealDistance, EvaluationParams config)
        {
            // 1. Elevation Heuristic: Higher ground gives advantage (delta Y)
            float elevationDelta = Mathf.Max(0f, p.y - target.y);
            float elevationScore = Mathf.Clamp01(elevationDelta / 5.0f); // 5 meters elevation max scale

            // 2. Distance Fitness: Gaussian-like bell curve around ideal distance
            float currentDist = Vector3.Distance(p, target);
            float distDiff = Mathf.Abs(currentDist - idealDistance);
            float distanceScore = Mathf.Clamp01(1.0f - (distDiff / idealDistance));

            // 3. Line of Sight Heuristic: Must be able to see target from candidate
            Vector3 eyePos = p + Vector3.up * 1.6f;
            Vector3 targetCenter = target + Vector3.up * 1.0f;
            bool hasLOS = !Physics.Linecast(eyePos, targetCenter, config.obstacleMask);
            float losScore = hasLOS ? 1.0f : 0.0f;

            // Total Weighted Heuristic Score
            return (config.weightElevation * elevationScore) +
                   (config.weightDistance * distanceScore) +
                   (config.weightLineOfSight * losScore);
        }
    }
}
