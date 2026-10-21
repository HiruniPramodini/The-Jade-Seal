using System;
using UnityEngine;
using JadeSeal.Player;

namespace JadeSeal.Interaction
{
    /// <summary>
    /// Collectible Jade Key item implementing <see cref="IInteractable"/>.
    /// When targeted by PlayerInteractor and E is pressed, adds the Jade Key
    /// to the player's <see cref="PlayerKeyInventory"/> and hides itself from the world.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public sealed class JadeKeyPickup : MonoBehaviour, IInteractable
    {
#pragma warning disable CS0649
        [Header("Interaction Settings")]
        [Tooltip("Prompt displayed on HUD when targeting the Jade Key.")]
        [SerializeField] private string promptText = "Pick Up Jade Key";

        [Tooltip("Whether to destroy the GameObject on pickup (true) or deactivate it (false).")]
        [SerializeField] private bool destroyOnPickup = false;

        [Header("Idle Animation")]
        [Tooltip("Whether the key slowly rotates and bobs in place.")]
        [SerializeField] private bool enableIdleAnimation = true;

        [SerializeField] private float rotationSpeed = 60.0f;
        [SerializeField] private float bobHeight = 0.08f;
        [SerializeField] private float bobFrequency = 2.0f;

        [Header("Audio Feedback (Optional)")]
        [Tooltip("Audio clip played when the key is picked up.")]
        [SerializeField] private AudioClip pickupAudioClip;
#pragma warning restore CS0649

        private bool _isCollected = false;
        private Vector3 _startPosition;
        private Material _instancedMaterial;
        private Renderer _renderer;

        /// <summary>True if this key has already been picked up.</summary>
        public bool IsCollected => _isCollected;

        private void Awake()
        {
            _startPosition = transform.position;

            // Instance material to ensure project shared materials are never mutated
            _renderer = GetComponent<Renderer>();
            if (_renderer != null)
            {
                _instancedMaterial = _renderer.material;
            }
        }

        private void Update()
        {
            if (!enableIdleAnimation || _isCollected) return;

            // Gentle rotation around Y
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);

            // Subtle vertical oscillation
            float newY = _startPosition.y + Mathf.Sin(Time.time * bobFrequency) * bobHeight;
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);
        }

        private void OnDestroy()
        {
            if (_instancedMaterial != null)
            {
                Destroy(_instancedMaterial);
                _instancedMaterial = null;
            }
        }

        #region IInteractable Implementation

        public bool CanInteract(GameObject interactor)
        {
            if (_isCollected || !enabled || !gameObject.activeInHierarchy) return false;

            // Ensure the interactor has a PlayerKeyInventory component available
            var inventory = interactor.GetComponentInParent<PlayerKeyInventory>();
            return inventory != null && !inventory.HasJadeKey;
        }

        public string GetPromptText()
        {
            return promptText;
        }

        public void Interact(GameObject interactor)
        {
            if (_isCollected) return;

            var inventory = interactor.GetComponentInParent<PlayerKeyInventory>();
            if (inventory == null)
            {
                Debug.LogWarning($"<color=#e67e22>[JadeKeyPickup]</color> '{interactor.name}' attempted to collect '{name}', but possesses no {nameof(PlayerKeyInventory)} component!");
                return;
            }

            // Flag as collected immediately to prevent duplicate triggers
            _isCollected = true;

            // Add key to inventory
            inventory.AddJadeKey();

            // Play audio if provided
            if (pickupAudioClip != null)
            {
                AudioSource.PlayClipAtPoint(pickupAudioClip, transform.position);
            }

            Debug.Log($"<color=#2ecc71><b>[JadeKeyPickup]</b></color> '{interactor.name}' successfully collected <b>{name}</b>!");

            // Hide or destroy
            if (destroyOnPickup)
            {
                Destroy(gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        #endregion
    }
}

