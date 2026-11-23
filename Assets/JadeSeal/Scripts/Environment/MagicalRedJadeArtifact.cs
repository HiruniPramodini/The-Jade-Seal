using UnityEngine;

namespace JadeSeal.Environment
{
    /// <summary>
    /// Reusable procedural animation component for the magical Red Jade Seal artifact.
    /// Handles smooth continuous Y-axis rotation, gentle sinusoidal vertical floating/bobbing,
    /// counter-rotating magical energy rings, and organic breathing light pulsation.
    ///
    /// Preserves baseline local position, rotation, and light intensity without frame-to-frame drift,
    /// allocations, or scene searches. Has zero dependencies on physics, colliders, or animators.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("The Jade Seal/Environment/Magical Red Jade Artifact")]
    public sealed class MagicalRedJadeArtifact : MonoBehaviour
    {
        [Header("Floating Crystal Rotation")]
        [Tooltip("Continuous rotation speed around the rotation axis in degrees per second. Default 20 deg/s.")]
        [Range(-180.0f, 180.0f)]
        [SerializeField] private float rotationSpeed = 20.0f;

        [Tooltip("Local axis around which the artifact crystal rotates. Defaults to local Up (0, 1, 0).")]
        [SerializeField] private Vector3 rotationAxis = Vector3.up;

        [Header("Sinusoidal Floating / Bobbing")]
        [Tooltip("Enable or disable gentle vertical bobbing motion.")]
        [SerializeField] private bool enableBobbing = true;

        [Tooltip("Peak vertical bobbing displacement in meters. Default 0.08m gives a smooth, majestic float.")]
        [Range(0.0f, 0.5f)]
        [SerializeField] private float bobbingAmplitude = 0.08f;

        [Tooltip("Bobbing frequency in Hertz (cycles per second). Default 0.2 Hz produces one full smooth cycle every 5.0 seconds.")]
        [Range(0.01f, 2.0f)]
        [SerializeField] private float bobbingFrequency = 0.2f;

        [Tooltip("Local directional axis for bobbing. Defaults to local Up (0, 1, 0).")]
        [SerializeField] private Vector3 bobbingAxis = Vector3.up;

        [Header("Magical Energy Rings (Optional)")]
        [Tooltip("Optional transform reference for the inner energy ring.")]
        [SerializeField] private Transform innerEnergyRing;

        [Tooltip("Inner energy ring rotation speed in degrees per second. Negative value rotates clockwise. Default -30 deg/s.")]
        [Range(-180.0f, 180.0f)]
        [SerializeField] private float innerRingRotationSpeed = -30.0f;

        [Tooltip("Optional transform reference for the outer energy ring.")]
        [SerializeField] private Transform outerEnergyRing;

        [Tooltip("Outer energy ring rotation speed in degrees per second. Positive value rotates counter-clockwise. Default 20 deg/s.")]
        [Range(-180.0f, 180.0f)]
        [SerializeField] private float outerRingRotationSpeed = 20.0f;

        [Tooltip("Local rotation axis for the energy rings. Defaults to local Up (0, 1, 0).")]
        [SerializeField] private Vector3 ringRotationAxis = Vector3.up;

        [Header("Magical Light Pulsing (Optional)")]
        [Tooltip("Optional Point Light illuminating the artifact.")]
        [SerializeField] private Light artifactLight;

        [Tooltip("Enable gentle breathing pulse on the artifact point light.")]
        [SerializeField] private bool enableLightPulse = true;

        [Tooltip("Relative pulse variation amplitude around base intensity (e.g. 0.25 = 25% variation).")]
        [Range(0.0f, 0.8f)]
        [SerializeField] private float lightPulseAmount = 0.25f;

        [Tooltip("Light breathing frequency in Hertz (cycles per second). Default 0.5 Hz is one smooth pulse every 2.0 seconds.")]
        [Range(0.05f, 2.0f)]
        [SerializeField] private float lightPulseFrequency = 0.5f;

        // Cached baseline transforms and states
        private Vector3 _initialLocalPosition;
        private Quaternion _initialLocalRotation;
        private bool _hasInitializedBaseline = false;

        private Quaternion _initialInnerRingRotation;
        private bool _hasInnerRingBaseline = false;

        private Quaternion _initialOuterRingRotation;
        private bool _hasOuterRingBaseline = false;

        private float _initialLightIntensity;
        private bool _hasLightBaseline = false;

        // Accumulated rotation angles to eliminate drift and preserve orientation cleanly
        private float _currentArtifactAngle = 0.0f;
        private float _currentInnerRingAngle = 0.0f;
        private float _currentOuterRingAngle = 0.0f;

        // Two-PI constant for frequency conversions
        private const float TwoPi = Mathf.PI * 2.0f;

        private void Awake()
        {
            InitializeBaselines();
        }

        private void OnEnable()
        {
            if (!_hasInitializedBaseline)
            {
                InitializeBaselines();
            }
        }

        private void OnDisable()
        {
            RestoreBaselines();
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            float elapsedTime = Time.time;

            // 1. Crystal Continuous Rotation
            if (Mathf.Abs(rotationSpeed) > Mathf.Epsilon)
            {
                _currentArtifactAngle = (_currentArtifactAngle + rotationSpeed * deltaTime) % 360.0f;
                Vector3 axis = rotationAxis.sqrMagnitude > 0.001f ? rotationAxis.normalized : Vector3.up;
                transform.localRotation = _initialLocalRotation * Quaternion.AngleAxis(_currentArtifactAngle, axis);
            }

            // 2. Sinusoidal Vertical Bobbing
            if (enableBobbing && bobbingAmplitude > 0.0f && bobbingFrequency > 0.0f)
            {
                float verticalOffset = Mathf.Sin(elapsedTime * bobbingFrequency * TwoPi) * bobbingAmplitude;
                Vector3 axis = bobbingAxis.sqrMagnitude > 0.001f ? bobbingAxis.normalized : Vector3.up;
                transform.localPosition = _initialLocalPosition + axis * verticalOffset;
            }

            // 3. Inner Energy Ring Continuous Rotation
            if (innerEnergyRing != null && Mathf.Abs(innerRingRotationSpeed) > Mathf.Epsilon)
            {
                _currentInnerRingAngle = (_currentInnerRingAngle + innerRingRotationSpeed * deltaTime) % 360.0f;
                Vector3 axis = ringRotationAxis.sqrMagnitude > 0.001f ? ringRotationAxis.normalized : Vector3.up;
                innerEnergyRing.localRotation = _initialInnerRingRotation * Quaternion.AngleAxis(_currentInnerRingAngle, axis);
            }

            // 4. Outer Energy Ring Continuous Rotation
            if (outerEnergyRing != null && Mathf.Abs(outerRingRotationSpeed) > Mathf.Epsilon)
            {
                _currentOuterRingAngle = (_currentOuterRingAngle + outerRingRotationSpeed * deltaTime) % 360.0f;
                Vector3 axis = ringRotationAxis.sqrMagnitude > 0.001f ? ringRotationAxis.normalized : Vector3.up;
                outerEnergyRing.localRotation = _initialOuterRingRotation * Quaternion.AngleAxis(_currentOuterRingAngle, axis);
            }

            // 5. Gentle Light Breathing Pulse
            if (artifactLight != null && enableLightPulse && _hasLightBaseline && lightPulseAmount > 0.0f)
            {
                float pulseFactor = 1.0f + Mathf.Sin(elapsedTime * lightPulseFrequency * TwoPi) * lightPulseAmount;
                artifactLight.intensity = Mathf.Max(0.0f, _initialLightIntensity * pulseFactor);
            }
        }

        private void OnValidate()
        {
            bobbingAmplitude = Mathf.Max(0.0f, bobbingAmplitude);
            bobbingFrequency = Mathf.Max(0.0f, bobbingFrequency);
            lightPulseAmount = Mathf.Clamp(lightPulseAmount, 0.0f, 0.95f);
            lightPulseFrequency = Mathf.Max(0.01f, lightPulseFrequency);

            if (rotationAxis.sqrMagnitude < 0.001f)
            {
                rotationAxis = Vector3.up;
            }

            if (bobbingAxis.sqrMagnitude < 0.001f)
            {
                bobbingAxis = Vector3.up;
            }

            if (ringRotationAxis.sqrMagnitude < 0.001f)
            {
                ringRotationAxis = Vector3.up;
            }
        }

        /// <summary>
        /// Captures and caches resting baselines for transform, rings, and light.
        /// </summary>
        public void InitializeBaselines()
        {
            _initialLocalPosition = transform.localPosition;
            _initialLocalRotation = transform.localRotation;
            _hasInitializedBaseline = true;

            if (innerEnergyRing != null)
            {
                _initialInnerRingRotation = innerEnergyRing.localRotation;
                _hasInnerRingBaseline = true;
            }

            if (outerEnergyRing != null)
            {
                _initialOuterRingRotation = outerEnergyRing.localRotation;
                _hasOuterRingBaseline = true;
            }

            if (artifactLight != null)
            {
                _initialLightIntensity = artifactLight.intensity;
                _hasLightBaseline = true;
            }
        }

        /// <summary>
        /// Safely restores resting baselines to prevent permanent transform displacement or intensity drift.
        /// </summary>
        public void RestoreBaselines()
        {
            if (_hasInitializedBaseline)
            {
                transform.localPosition = _initialLocalPosition;
                transform.localRotation = _initialLocalRotation;
            }

            if (_hasInnerRingBaseline && innerEnergyRing != null)
            {
                innerEnergyRing.localRotation = _initialInnerRingRotation;
            }

            if (_hasOuterRingBaseline && outerEnergyRing != null)
            {
                outerEnergyRing.localRotation = _initialOuterRingRotation;
            }

            if (_hasLightBaseline && artifactLight != null)
            {
                artifactLight.intensity = _initialLightIntensity;
            }
        }

        /// <summary>
        /// Rebinds resting baselines if the artifact is repositioned or re-anchored.
        /// </summary>
        public void RebindBaseline()
        {
            _hasInitializedBaseline = false;
            _hasInnerRingBaseline = false;
            _hasOuterRingBaseline = false;
            _hasLightBaseline = false;
            _currentArtifactAngle = 0.0f;
            _currentInnerRingAngle = 0.0f;
            _currentOuterRingAngle = 0.0f;
            InitializeBaselines();
        }

        /// <summary>
        /// Dynamically assigns and binds energy ring transforms.
        /// </summary>
        public void SetEnergyRings(Transform innerRing, Transform outerRing, float innerSpeed = -30.0f, float outerSpeed = 20.0f)
        {
            innerEnergyRing = innerRing;
            outerEnergyRing = outerRing;
            innerRingRotationSpeed = innerSpeed;
            outerRingRotationSpeed = outerSpeed;

            if (innerEnergyRing != null)
            {
                _initialInnerRingRotation = innerEnergyRing.localRotation;
                _hasInnerRingBaseline = true;
                _currentInnerRingAngle = 0.0f;
            }

            if (outerEnergyRing != null)
            {
                _initialOuterRingRotation = outerEnergyRing.localRotation;
                _hasOuterRingBaseline = true;
                _currentOuterRingAngle = 0.0f;
            }
        }

        /// <summary>
        /// Dynamically assigns and binds an artifact light component.
        /// </summary>
        public void SetArtifactLight(Light light, float pulseAmount = 0.25f, float pulseFrequency = 0.5f)
        {
            artifactLight = light;
            lightPulseAmount = pulseAmount;
            lightPulseFrequency = pulseFrequency;

            if (artifactLight != null)
            {
                _initialLightIntensity = artifactLight.intensity;
                _hasLightBaseline = true;
            }
        }
    }
}
