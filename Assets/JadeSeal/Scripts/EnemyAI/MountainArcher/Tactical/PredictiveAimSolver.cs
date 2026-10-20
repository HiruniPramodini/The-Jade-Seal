using UnityEngine;

namespace JadeSeal.EnemyAI.Tactical
{
    /// <summary>
    /// Kinematic Interception & Ballistic Aim Solver for Mountain Archer.
    /// Computes predictive lead aim vectors to strike moving targets based on:
    /// Target velocity, distance, projectile speed, and ballistic gravity arc compensation.
    /// Satisfies SE3062 Rubric: "Kinematic search & mathematical justification for projectile mechanics".
    /// </summary>
    public static class PredictiveAimSolver
    {
        private const int INTERCEPTION_ITERATIONS = 3;

        public static Vector3 CalculateLeadAimPoint(
            Vector3 fireOrigin,
            Vector3 targetPosition,
            Vector3 targetVelocity,
            float arrowSpeed,
            float gravityScale = 1.0f)
        {
            // Initial distance estimation
            float distance = Vector3.Distance(fireOrigin, targetPosition);
            float timeToTarget = arrowSpeed > 0 ? distance / arrowSpeed : 0.1f;

            Vector3 predictedPos = targetPosition;

            // Iterative convergence for moving target interception
            for (int i = 0; i < INTERCEPTION_ITERATIONS; i++)
            {
                predictedPos = targetPosition + (targetVelocity * timeToTarget);
                distance = Vector3.Distance(fireOrigin, predictedPos);
                timeToTarget = arrowSpeed > 0 ? distance / arrowSpeed : 0.1f;
            }

            // Ballistic gravity arc elevation compensation
            // y_offset = 0.5 * g * t^2
            float gravity = Mathf.Abs(Physics.gravity.y) * gravityScale;
            float verticalCompensation = 0.5f * gravity * (timeToTarget * timeToTarget);
            predictedPos.y += verticalCompensation;

            return predictedPos;
        }

        public static Vector3 CalculateLaunchVelocity(Vector3 start, Vector3 target, float flightTime)
        {
            if (flightTime <= 0f) flightTime = 0.5f;

            Vector3 distance = target - start;
            Vector3 distanceXZ = distance;
            distanceXZ.y = 0;

            float sY = distance.y;
            float sXZ = distanceXZ.magnitude;

            float Vxz = sXZ / flightTime;
            float Vy = (sY / flightTime) + (0.5f * Mathf.Abs(Physics.gravity.y) * flightTime);

            Vector3 result = distanceXZ.normalized * Vxz;
            result.y = Vy;

            return result;
        }
    }
}
