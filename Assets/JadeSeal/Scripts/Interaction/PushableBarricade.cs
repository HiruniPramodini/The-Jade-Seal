using UnityEngine;

namespace JadeSeal.Interaction
{
    /// <summary>
    /// Presets defining physical weight and resistance profiles for pushable barricades.
    /// </summary>
    public enum BarricadeWeightCategory
    {
        Custom,
        LightWoodenCrate,
        HeavyStoneBarricade
    }

    /// <summary>
    /// Reusable physics-driven pushable barricade component.
    /// Interacts with the player's CharacterController push system to provide realistic,
    /// stable 3D physics movement without erratic jitter, tipping, or teleportation.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(Collider))]
    [DisallowMultipleComponent]
    public sealed class PushableBarricade : MonoBehaviour
    {
#pragma warning disable CS0649
        [Header("Physical Weight Profile")]
        [Tooltip("Preset category defining mass, damping, and resistance parameters.")]
        [SerializeField] private BarricadeWeightCategory weightCategory = BarricadeWeightCategory.LightWoodenCrate;

        [Tooltip("Mass of the barricade in kilograms.")]
        [SerializeField] private float mass = 35.0f;

        [Tooltip("Linear damping (drag) that decelerates the barricade once pushing stops.")]
        [SerializeField] private float linearDamping = 3.5f;

        [Tooltip("Resistance factor dividing the applied player force. Higher values make the object heavier to push.")]
        [SerializeField] private float pushResistance = 1.0f;

        [Tooltip("Maximum horizontal speed (m/s) to prevent runaway physics acceleration.")]
        [SerializeField] private float maxLinearSpeed = 2.5f;

        [Header("Physics Constraints")]
        [Tooltip("If true, freezes rotation along the X and Z axes to prevent the barricade from tipping over during pushing.")]
        [SerializeField] private bool freezeRotationXZ = true;

        [Tooltip("Whether the barricade is currently eligible to receive push forces.")]
        [SerializeField] private bool canBePushed = true;

        [Header("Audio Feedback (Optional)")]
        [Tooltip("Sound played when the barricade is pushed.")]
        [SerializeField] private AudioClip pushScrapeSound;

        [Tooltip("AudioSource for scraping sound playback.")]
        [SerializeField] private AudioSource audioSource;
#pragma warning restore CS0649

        private Rigidbody _rigidbody;
        private float _lastSoundTime;

        /// <summary>Whether this barricade can currently be pushed by the player.</summary>
        public bool CanBePushed => canBePushed && enabled && _rigidbody != null && !_rigidbody.isKinematic;

        /// <summary>Reference to the attached Rigidbody.</summary>
        public Rigidbody AttachedRigidbody => _rigidbody;

        /// <summary>The weight profile assigned to this barricade.</summary>
        public BarricadeWeightCategory WeightCategory => weightCategory;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            InitializePhysics();
        }

        private void OnValidate()
        {
            if (_rigidbody == null) _rigidbody = GetComponent<Rigidbody>();
            if (weightCategory != BarricadeWeightCategory.Custom)
            {
                ApplyCategoryPreset(weightCategory);
            }
            if (_rigidbody != null)
            {
                ApplyRigidbodySettings();
            }
        }

        private void InitializePhysics()
        {
            if (weightCategory != BarricadeWeightCategory.Custom)
            {
                ApplyCategoryPreset(weightCategory);
            }

            ApplyRigidbodySettings();
        }

        private void ApplyCategoryPreset(BarricadeWeightCategory category)
        {
            switch (category)
            {
                case BarricadeWeightCategory.LightWoodenCrate:
                    mass = 35.0f;
                    linearDamping = 3.5f;
                    pushResistance = 1.0f;
                    maxLinearSpeed = 2.5f;
                    freezeRotationXZ = true;
                    break;

                case BarricadeWeightCategory.HeavyStoneBarricade:
                    mass = 350.0f;
                    linearDamping = 5.0f;
                    pushResistance = 3.5f;
                    maxLinearSpeed = 0.8f;
                    freezeRotationXZ = true;
                    break;
            }
        }

        private void ApplyRigidbodySettings()
        {
            if (_rigidbody == null) return;

            _rigidbody.mass = mass;
            _rigidbody.linearDamping = linearDamping;
            _rigidbody.useGravity = true;
            _rigidbody.isKinematic = false;

            if (freezeRotationXZ)
            {
                _rigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            }
            else
            {
                _rigidbody.constraints = RigidbodyConstraints.None;
            }
        }

        /// <summary>
        /// Applies an impulse/continuous push force from an interacting character controller.
        /// </summary>
        /// <param name="pushDirection">Normalized horizontal direction of the push.</param>
        /// <param name="contactPoint">World position where the character contacted the barricade.</param>
        /// <param name="pushStrength">Raw force magnitude provided by the pusher.</param>
        public void ApplyPush(Vector3 pushDirection, Vector3 contactPoint, float pushStrength)
        {
            if (!CanBePushed) return;

            // Flatten force to horizontal plane to prevent launching or burying the object
            Vector3 horizontalDir = new Vector3(pushDirection.x, 0f, pushDirection.z);
            if (horizontalDir.sqrMagnitude < 0.001f) return;
            horizontalDir.Normalize();

            // Adjust force by push resistance
            float effectiveForce = pushStrength / Mathf.Max(pushResistance, 0.1f);
            _rigidbody.AddForce(horizontalDir * effectiveForce, ForceMode.Force);

            // Clamp horizontal velocity to prevent runaway motion
            Vector3 currentVel = _rigidbody.linearVelocity;
            Vector3 horizontalVel = new Vector3(currentVel.x, 0f, currentVel.z);

            if (horizontalVel.sqrMagnitude > maxLinearSpeed * maxLinearSpeed)
            {
                Vector3 clampedHorizontal = horizontalVel.normalized * maxLinearSpeed;
                _rigidbody.linearVelocity = new Vector3(clampedHorizontal.x, currentVel.y, clampedHorizontal.z);
            }

            PlayPushAudio();
        }

        private void PlayPushAudio()
        {
            if (pushScrapeSound == null || audioSource == null) return;

            // Throttle scrape sound so it does not spam every frame
            if (Time.time - _lastSoundTime > 0.4f)
            {
                _lastSoundTime = Time.time;
                audioSource.PlayOneShot(pushScrapeSound);
            }
        }

        /// <summary>
        /// Dynamically updates the pushable availability of the barricade.
        /// </summary>
        public void SetPushable(bool pushable)
        {
            canBePushed = pushable;
        }
    }
}

