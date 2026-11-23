using UnityEngine;

namespace JadeSeal.Environment
{
    /// <summary>
    /// Lightweight procedural wind-sway component for bamboo, trees, and environmental foliage.
    /// Gently rotates the GameObject around its local X (pitch) and Z (roll) axes using smooth
    /// multi-harmonic sine waves anchored at its base pivot.
    ///
    /// Preserves the object's original rotation without frame-by-frame drift or heap allocations.
    /// Fully compatible with parent objects carrying LODGroup hierarchies without altering child LODs.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("The Jade Seal/Environment/Gentle Wind Sway")]
    public sealed class GentleWindSway : MonoBehaviour
    {
        [Header("Primary Wind Motion")]
        [Tooltip("Maximum primary sway angle in degrees. Default 1.8 deg creates gentle, believable bamboo motion.")]
        [Range(0.1f, 8.0f)]
        [SerializeField] private float primarySwayAngle = 1.8f;

        [Tooltip("Oscillation frequency / wind speed in radians per second. Default 1.2 rad/s evokes a peaceful mountain breeze.")]
        [Range(0.2f, 4.0f)]
        [SerializeField] private float windSpeed = 1.2f;

        [Header("Secondary Harmonic (Organic Flutter)")]
        [Tooltip("Secondary harmonic sway angle in degrees to break repetitive pendulum oscillation.")]
        [Range(0.0f, 3.0f)]
        [SerializeField] private float secondarySwayAngle = 0.6f;

        [Tooltip("Frequency multiplier for the secondary harmonic wave. Default 1.73 provides a non-repeating wave ratio.")]
        [Range(1.0f, 3.5f)]
        [SerializeField] private float secondarySpeedMultiplier = 1.73f;

        [Header("Wind Direction & Spatial Variation")]
        [Tooltip("Relative sway intensity weights along local X (pitch) and local Z (roll) axes.")]
        [SerializeField] private Vector2 axisWeights = new Vector2(1.0f, 0.7f);

        [Tooltip("Spatial frequency scale for deterministic position-based phase variation across the world.")]
        [Range(0.05f, 1.0f)]
        [SerializeField] private float spatialFrequency = 0.35f;

        // Cached baseline rotation and state
        private Quaternion _initialLocalRotation;
        private bool _hasInitializedRotation = false;
        private float _spatialPhaseOffset;
        private float _secondaryPhaseOffset;

        private void Awake()
        {
            InitializeRotation();
            InitializeSpatialOffsets();
        }

        private void OnEnable()
        {
            // Ensure baseline is safely recorded even if enabled dynamically
            if (!_hasInitializedRotation)
            {
                InitializeRotation();
                InitializeSpatialOffsets();
            }
        }

        private void OnDisable()
        {
            // Safely restore initial rotation when disabled or deactivated to prevent permanent angular drift
            if (_hasInitializedRotation)
            {
                transform.localRotation = _initialLocalRotation;
            }
        }

        private void Update()
        {
            float time = Time.time;

            // Primary sinusoidal wave with continuous spatial offset
            float primaryPhase = (time * windSpeed) + _spatialPhaseOffset;
            float sinPrimary = Mathf.Sin(primaryPhase);
            float cosPrimary = Mathf.Cos(primaryPhase);

            // Secondary higher-frequency wave with distinct spatial mix
            float secondaryPhase = (time * windSpeed * secondarySpeedMultiplier) + _secondaryPhaseOffset;
            float sinSecondary = Mathf.Sin(secondaryPhase);

            // Combined harmonic sway angles along X (pitch) and Z (roll) in degrees
            float pitchAngle = (sinPrimary * primarySwayAngle + sinSecondary * secondarySwayAngle) * axisWeights.x;
            float rollAngle = (cosPrimary * primarySwayAngle + sinSecondary * secondarySwayAngle * 0.5f) * axisWeights.y;

            // Apply sway strictly relative to preserved initial rotation (zero frame-by-frame accumulation)
            Quaternion swayRotation = Quaternion.Euler(pitchAngle, 0f, rollAngle);
            transform.localRotation = _initialLocalRotation * swayRotation;
        }

        private void OnValidate()
        {
            primarySwayAngle = Mathf.Max(0f, primarySwayAngle);
            secondarySwayAngle = Mathf.Max(0f, secondarySwayAngle);
            windSpeed = Mathf.Max(0f, windSpeed);
            secondarySpeedMultiplier = Mathf.Max(0.1f, secondarySpeedMultiplier);
            spatialFrequency = Mathf.Max(0.01f, spatialFrequency);
        }

        /// <summary>
        /// Records the current localRotation as the baseline resting pose.
        /// </summary>
        private void InitializeRotation()
        {
            if (!_hasInitializedRotation)
            {
                _initialLocalRotation = transform.localRotation;
                _hasInitializedRotation = true;
            }
        }

        /// <summary>
        /// Derives deterministic phase offsets from world position so neighboring trees sway in natural waves
        /// without requiring random number generators or runtime allocations.
        /// </summary>
        private void InitializeSpatialOffsets()
        {
            Vector3 worldPos = transform.position;
            _spatialPhaseOffset = (worldPos.x * 0.37f + worldPos.z * 0.43f + worldPos.y * 0.19f) * spatialFrequency;
            _secondaryPhaseOffset = (worldPos.x * 0.53f - worldPos.z * 0.29f) * spatialFrequency;
        }

        /// <summary>
        /// Public utility to re-capture the resting rotation if the object is repositioned or reoriented at runtime.
        /// </summary>
        public void ResetBaseRotation()
        {
            _initialLocalRotation = transform.localRotation;
            _hasInitializedRotation = true;
            InitializeSpatialOffsets();
        }

        /// <summary>
        /// Allows runtime or editor tools to adjust wind intensity without reallocating.
        /// </summary>
        public void SetWindIntensity(float primaryAngle, float speed)
        {
            primarySwayAngle = Mathf.Max(0f, primaryAngle);
            windSpeed = Mathf.Max(0f, speed);
        }
    }
}
