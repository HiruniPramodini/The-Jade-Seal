using UnityEngine;

namespace JadeSeal.EnemyAI.Sensory
{
    /// <summary>
    /// Handles sensory awareness for the Mountain Archer:
    /// 1. Vision cone (FOV + Range)
    /// 2. Spatial Obstacle & Terrain Raycast Line-of-Sight
    /// 3. Acoustic Footstep / Disturbance Detection
    /// 4. Target memory with last-known position tracking
    /// </summary>
    public class MountainArcherPerception : MonoBehaviour
    {
        [Header("Vision Parameters")]
        [Tooltip("Maximum detection range for player in meters.")]
        [SerializeField] private float visionRange = 25f;

        [Tooltip("Field of View angle in degrees.")]
        [Range(30f, 180f)]
        [SerializeField] private float fovAngle = 110f;

        [Tooltip("Offset from pivot to eye height for raycasts.")]
        [SerializeField] private Vector3 eyeOffset = new Vector3(0, 1.6f, 0);

        [Tooltip("Layers blocking line of sight (Environment, Rocks, Structures).")]
        [SerializeField] private LayerMask occlusionMask = ~0;

        [Header("Acoustic Detection")]
        [Tooltip("Range within which running footsteps can be heard even behind walls.")]
        [SerializeField] private float hearingRange = 12f;

        [Header("Memory")]
        [Tooltip("How long the archer remembers the target position after losing LOS.")]
        [SerializeField] private float memoryDuration = 5f;

        // Current Sensory State
        public Transform CurrentTarget { get; private set; }
        public Vector3 LastKnownPosition { get; private set; }
        public bool HasLineOfSight { get; private set; }
        public bool IsTargetPerceived { get; private set; }
        public float TargetLostTimer { get; private set; }

        private Transform playerTransform;

        private void Start()
        {
            FindPlayerReference();
        }

        private void FindPlayerReference()
        {
            // Auto-locate player by common tags or character controllers in scene
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
            else
            {
                // Fallback: look for Player_Spawn or any CharacterController / Camera
                var spawn = GameObject.Find("Player_Spawn");
                if (spawn != null) playerTransform = spawn.transform;
            }
        }

        public void UpdatePerception()
        {
            if (playerTransform == null)
            {
                FindPlayerReference();
                if (playerTransform == null) return;
            }

            Vector3 eyePos = transform.position + eyeOffset;
            Vector3 targetCenter = playerTransform.position + Vector3.up * 1.0f;
            Vector3 dirToPlayer = (targetCenter - eyePos);
            float distanceToPlayer = dirToPlayer.magnitude;

            bool directVision = false;
            bool acousticAlert = false;

            // 1. Vision Range & Cone Check
            if (distanceToPlayer <= visionRange)
            {
                float angle = Vector3.Angle(transform.forward, dirToPlayer.normalized);
                if (angle <= fovAngle * 0.5f)
                {
                    // 2. Line of Sight Raycast
                    if (!Physics.Raycast(eyePos, dirToPlayer.normalized, distanceToPlayer, occlusionMask))
                    {
                        directVision = true;
                    }
                }
            }

            // 3. Acoustic / Hearing Check (Detects close proximity even outside FOV)
            if (distanceToPlayer <= hearingRange)
            {
                acousticAlert = true;
            }

            // 4. Update Memory & Tracking
            if (directVision || acousticAlert)
            {
                CurrentTarget = playerTransform;
                LastKnownPosition = playerTransform.position;
                HasLineOfSight = directVision;
                IsTargetPerceived = true;
                TargetLostTimer = memoryDuration;
            }
            else
            {
                HasLineOfSight = false;
                if (TargetLostTimer > 0f)
                {
                    TargetLostTimer -= Time.deltaTime;
                    IsTargetPerceived = true;
                }
                else
                {
                    IsTargetPerceived = false;
                    CurrentTarget = null;
                }
            }
        }

        public void SetTarget(Transform target)
        {
            playerTransform = target;
        }

        public float VisionRange => visionRange;
        public float FovAngle => fovAngle;
        public float HearingRange => hearingRange;
        public Vector3 EyePosition => transform.position + eyeOffset;
    }
}
