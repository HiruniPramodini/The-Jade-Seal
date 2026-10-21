using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using JadeSeal.Player;

namespace JadeSeal.Interaction
{
    /// <summary>
    /// Reusable physics pickup object implementing <see cref="IInteractable"/>.
    /// Allows the player to pick up, carry, drop, and throw physical objects (e.g. stones, jars, torches)
    /// using stable Rigidbody manipulation and collision filtering.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(Collider))]
    [DisallowMultipleComponent]
    public sealed class PickupableObject : MonoBehaviour, IInteractable
    {
#pragma warning disable CS0649
        [Header("Interaction Settings")]
        [Tooltip("Prompt displayed on HUD when targeting this object.")]
        [SerializeField] private string promptText = "Pick Up Rock";

        [Header("Audio Feedback (Optional)")]
        [Tooltip("Sound played upon pickup.")]
        [SerializeField] private AudioClip pickupSound;

        [Tooltip("Sound played upon throw/release.")]
        [SerializeField] private AudioClip throwSound;

        [Tooltip("AudioSource component for playback.")]
        [SerializeField] private AudioSource audioSource;

        [Header("Events")]
        [Tooltip("Event fired when the object is picked up.")]
        [SerializeField] private UnityEvent onPickedUp;

        [Tooltip("Event fired when the object is thrown or dropped.")]
        [SerializeField] private UnityEvent onReleased;
#pragma warning restore CS0649

        private Rigidbody _rigidbody;
        private Collider _collider;
        private Material _instancedMaterial;
        private Renderer _renderer;

        private bool _isHeld;
        private PlayerObjectHolder _currentHolder;
        private Transform _originalParent;
        private bool _originalKinematic;
        private bool _originalUseGravity;

        /// <summary>True if this object is currently held by a player.</summary>
        public bool IsHeld => _isHeld;

        /// <summary>Attached Rigidbody reference.</summary>
        public Rigidbody AttachedRigidbody => _rigidbody;

        /// <summary>Attached Collider reference.</summary>
        public Collider AttachedCollider => _collider;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _collider = GetComponent<Collider>();
            _originalParent = transform.parent;

            // Cache initial physics state
            _originalKinematic = _rigidbody.isKinematic;
            _originalUseGravity = _rigidbody.useGravity;

            // Instance material to protect shared project assets
            _renderer = GetComponent<Renderer>();
            if (_renderer != null)
            {
                _instancedMaterial = _renderer.material;
            }
        }

        private void OnDestroy()
        {
            if (_instancedMaterial != null)
            {
                Destroy(_instancedMaterial);
                _instancedMaterial = null;
            }

            // Notify holder if destroyed while held
            if (_isHeld && _currentHolder != null)
            {
                _currentHolder.ClearHeldObjectSilently();
            }
        }

        #region IInteractable Implementation

        public bool CanInteract(GameObject interactor)
        {
            if (_isHeld || !enabled || !gameObject.activeInHierarchy) return false;

            var holder = interactor.GetComponentInParent<PlayerObjectHolder>();
            return holder != null && !holder.IsHoldingObject;
        }

        public string GetPromptText()
        {
            return promptText;
        }

        public void Interact(GameObject interactor)
        {
            if (_isHeld) return;

            var holder = interactor.GetComponentInParent<PlayerObjectHolder>();
            if (holder != null && !holder.IsHoldingObject)
            {
                holder.PickupObject(this);
            }
        }

        #endregion

        #region Carry & Throw Handling

        /// <summary>
        /// Initiates the held state when picked up by PlayerObjectHolder.
        /// </summary>
        public void OnPickedUp(PlayerObjectHolder holder, Transform holdAnchor, Collider playerCollider)
        {
            _isHeld = true;
            _currentHolder = holder;

            // Cache and switch Rigidbody to kinematic while carried
            _originalKinematic = _rigidbody.isKinematic;
            _originalUseGravity = _rigidbody.useGravity;

            _rigidbody.isKinematic = true;
            _rigidbody.useGravity = false;
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;

            // Ignore collision with the carrying player to prevent self-collision physics glitches
            if (playerCollider != null && _collider != null)
            {
                Physics.IgnoreCollision(_collider, playerCollider, true);
            }

            // Parent to hold anchor
            if (holdAnchor != null)
            {
                transform.SetParent(holdAnchor, true);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
            }

            PlaySound(pickupSound);
            onPickedUp?.Invoke();
        }

        /// <summary>
        /// Releases the held object into dynamic world physics with an applied force vector.
        /// </summary>
        public void OnReleased(Vector3 impulseForce, Vector3 inheritVelocity, Collider playerCollider)
        {
            _isHeld = false;
            _currentHolder = null;

            // Unparent back to world
            transform.SetParent(_originalParent, true);

            // Restore dynamic physics
            _rigidbody.isKinematic = false;
            _rigidbody.useGravity = true;

            // Inherit player motion velocity
            _rigidbody.linearVelocity = inheritVelocity;

            // Apply throw impulse if specified
            if (impulseForce.sqrMagnitude > 0.01f)
            {
                _rigidbody.AddForce(impulseForce, ForceMode.Impulse);
                PlaySound(throwSound);
            }

            // Re-enable player collision after clearing the player's immediate bounds
            if (playerCollider != null && _collider != null && gameObject.activeInHierarchy)
            {
                StartCoroutine(RestoreCollisionRoutine(playerCollider, 0.35f));
            }

            onReleased?.Invoke();
        }

        private IEnumerator RestoreCollisionRoutine(Collider playerCollider, float delay)
        {
            yield return new WaitForSeconds(delay);

            if (_collider != null && playerCollider != null)
            {
                Physics.IgnoreCollision(_collider, playerCollider, false);
            }
        }

        private void PlaySound(AudioClip clip)
        {
            if (clip != null && audioSource != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }

        #endregion
    }
}

