using UnityEngine;

namespace JadeSeal.EnemyAI.Common
{
    /// <summary>
    /// Reusable Sensory Perception Component for Enemy Agents in The Jade Seal.
    /// Manages Vision Cone (FOV), Line-of-Sight Raycasts, Acoustic disturbance detection,
    /// and Target Memory tracking with last known position.
    /// </summary>
    public class EnemyPerception : MonoBehaviour
    {
        [Header("Vision Parameters")]
        [Tooltip("Maximum detection range for player in meters.")]
        [SerializeField] private float visionRange = 22f;

        [Tooltip("Field of View angle in degrees.")]
        [Range(30f, 180f)]
        [SerializeField] private float fovAngle = 110f;

        [Tooltip("Eye height offset for Line of Sight raycasts.")]
        [SerializeField] private Vector3 eyeOffset = new Vector3(0, 1.6f, 0);

        [Tooltip("Layers blocking line of sight (Environment, Rocks, Structures).")]
        [SerializeField] private LayerMask occlusionMask = ~0;

        [Header("Acoustic Detection")]
        [Tooltip("Range within which running footsteps can be heard.")]
        [SerializeField] private float hearingRange = 10f;

        [Header("Memory")]
        [Tooltip("Duration in seconds the agent remembers the target after losing visual contact.")]
        [SerializeField] private float memoryDuration = 5f;

        // Current Sensory State
        public Transform CurrentTarget { get; private set; }
        public Vector3 LastKnownPosition { get; private set; }
        public bool HasLineOfSight { get; private set; }
        public bool HasVisualContact { get; private set; }
        public bool HasAuditoryDisturbance { get; private set; }
        public Vector3 LastSoundPosition { get; private set; }
        public float TargetLostTimer { get; private set; }
        public bool IsTargetPerceived => HasVisualContact || (TargetLostTimer > 0f);

        private Transform playerTransform;

        private void Start()
        {
            FindPlayerReference();
        }

        public void FindPlayerReference()
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
            else
            {
                var spawn = GameObject.Find("Player_Spawn");
                if (spawn != null) playerTransform = spawn.transform;
                else
                {
                    var cc = Object.FindFirstObjectByType<CharacterController>();
                    if (cc != null) playerTransform = cc.transform;
                }
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
                    if (Physics.Raycast(eyePos, dirToPlayer.normalized, out RaycastHit hit, distanceToPlayer + 0.2f, occlusionMask))
                    {
                        if (hit.transform == playerTransform || hit.transform.IsChildOf(playerTransform) || hit.collider.CompareTag("Player") || hit.distance >= distanceToPlayer - 0.5f)
                        {
                            directVision = true;
                        }
                    }
                    else
                    {
                        directVision = true;
                    }
                }
            }

            // 3. Acoustic Footstep Check
            if (!directVision && distanceToPlayer <= hearingRange)
            {
                // Check if target is moving fast (running)
                var cc = playerTransform.GetComponent<CharacterController>();
                if (cc != null && cc.velocity.magnitude > 3.0f)
                {
                    acousticAlert = true;
                    LastSoundPosition = playerTransform.position;
                }
            }

            HasVisualContact = directVision;
            HasAuditoryDisturbance = acousticAlert;

            if (directVision)
            {
                CurrentTarget = playerTransform;
                LastKnownPosition = playerTransform.position;
                HasLineOfSight = true;
                TargetLostTimer = memoryDuration;
            }
            else if (acousticAlert)
            {
                CurrentTarget = playerTransform;
                LastKnownPosition = LastSoundPosition;
                TargetLostTimer = memoryDuration * 0.75f;
            }
            else
            {
                HasLineOfSight = false;
                if (TargetLostTimer > 0f)
                {
                    TargetLostTimer -= Time.deltaTime;
                    if (TargetLostTimer <= 0f)
                    {
                        CurrentTarget = null;
                    }
                }
            }
        }

        public void TriggerAcousticDisturbance(Vector3 soundPosition)
        {
            LastSoundPosition = soundPosition;
            LastKnownPosition = soundPosition;
            HasAuditoryDisturbance = true;
            TargetLostTimer = memoryDuration;
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 eyePos = transform.position + eyeOffset;

            // Vision Range Sphere
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.2f);
            Gizmos.DrawWireSphere(eyePos, visionRange);

            // Hearing Range Sphere
            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.15f);
            Gizmos.DrawWireSphere(eyePos, hearingRange);

            // FOV Cones
            Gizmos.color = Color.cyan;
            Vector3 leftRay = Quaternion.Euler(0, -fovAngle * 0.5f, 0) * transform.forward;
            Vector3 rightRay = Quaternion.Euler(0, fovAngle * 0.5f, 0) * transform.forward;
            Gizmos.DrawRay(eyePos, leftRay * visionRange);
            Gizmos.DrawRay(eyePos, rightRay * visionRange);
        }
    }
}
