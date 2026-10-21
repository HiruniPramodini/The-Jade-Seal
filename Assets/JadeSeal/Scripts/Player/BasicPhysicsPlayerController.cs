using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace JadeSeal.Player
{
    /// <summary>
    /// Minimal first-person CharacterController player designed exclusively for isolated
    /// sandbox physics and interaction testing.
    ///
    /// REPLACEMENT GUIDE FOR TEAMMATES:
    /// When the final Player Prefab and character rig are imported,
    /// this temporary sandbox controller should be removed or disabled.
    /// To migrate interaction capabilities to the final player rig:
    ///   1. Attach <see cref="Interaction.PlayerInteractor"/> to the final Player Camera or Player Root.
    ///   2. Ensure the Player GameObject has a Collider or CharacterController.
    ///   3. Assign the Player/Interact action in the PlayerInteractor inspector.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public sealed class BasicPhysicsPlayerController : MonoBehaviour
    {
#pragma warning disable CS0649
        [Header("Movement Settings")]
        [Tooltip("Horizontal movement speed in meters per second.")]
        [SerializeField] private float moveSpeed = 5.0f;

        [Tooltip("Downwards acceleration due to gravity in m/s^2.")]
        [SerializeField] private float gravity = -18.0f;

        [Tooltip("Small downward grounding velocity to keep the character firmly grounded on slopes.")]
        [SerializeField] private float groundedDownwardForce = -2.0f;

        [Header("Camera & Look Settings")]
        [Tooltip("First-person camera child. If unassigned, automatically looks in child transforms.")]
        [SerializeField] private Camera playerCamera;

        [Tooltip("Mouse look sensitivity multiplier.")]
        [SerializeField] private float mouseSensitivity = 0.12f;

        [Tooltip("Maximum upward vertical look angle in degrees.")]
        [SerializeField] private float maxPitchAngle = 80.0f;

        [Tooltip("Maximum downward vertical look angle in degrees.")]
        [SerializeField] private float minPitchAngle = -80.0f;

        [Header("New Input System")]
        [Tooltip("Reference to the InputSystem_Actions asset.")]
        [SerializeField] private InputActionAsset inputActionsAsset;

        [SerializeField] private string moveActionName = "Player/Move";
        [SerializeField] private string lookActionName = "Player/Look";
#pragma warning restore CS0649

        private CharacterController _characterController;
        private float _verticalVelocity;
        private float _cameraPitch;

#if ENABLE_INPUT_SYSTEM
        private InputAction _moveAction;
        private InputAction _lookAction;
#endif

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            ResolveCamera();
            InitializeInputActions();
        }

        private void Start()
        {
            LockCursor(true);
        }

        private void OnEnable()
        {
#if ENABLE_INPUT_SYSTEM
            _moveAction?.Enable();
            _lookAction?.Enable();
#endif
        }

        private void OnDisable()
        {
#if ENABLE_INPUT_SYSTEM
            _moveAction?.Disable();
            _lookAction?.Disable();
#endif
        }

        private void Update()
        {
            HandleCursorLocking();
            HandleMouseLook();
            HandleMovement();
        }

        private void ResolveCamera()
        {
            if (playerCamera == null)
            {
                playerCamera = GetComponentInChildren<Camera>();
            }
        }

        private void InitializeInputActions()
        {
#if ENABLE_INPUT_SYSTEM
            if (inputActionsAsset != null)
            {
                _moveAction = inputActionsAsset.FindAction(moveActionName, false);
                _lookAction = inputActionsAsset.FindAction(lookActionName, false);
            }
#endif
        }

        private void HandleCursorLocking()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;

            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                LockCursor(false);
            }

            if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
            {
                LockCursor(true);
            }
#endif
        }

        private void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void HandleMouseLook()
        {
            if (Cursor.lockState != CursorLockMode.Locked) return;

            Vector2 lookInput = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
            if (_lookAction != null)
            {
                lookInput = _lookAction.ReadValue<Vector2>();
            }
            else if (Mouse.current != null)
            {
                lookInput = Mouse.current.delta.ReadValue();
            }
#endif

            float mouseX = lookInput.x * mouseSensitivity;
            float mouseY = lookInput.y * mouseSensitivity;

            // Horizontal rotation on the player body
            transform.Rotate(Vector3.up * mouseX);

            // Vertical rotation (pitch) on the child camera
            if (playerCamera != null)
            {
                _cameraPitch = Mathf.Clamp(_cameraPitch - mouseY, minPitchAngle, maxPitchAngle);
                playerCamera.transform.localRotation = Quaternion.Euler(_cameraPitch, 0f, 0f);
            }
        }

        private void HandleMovement()
        {
            Vector2 moveInput = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
            if (_moveAction != null)
            {
                moveInput = _moveAction.ReadValue<Vector2>();
            }
            else if (Keyboard.current != null)
            {
                var k = Keyboard.current;
                float x = (k.dKey.isPressed ? 1f : 0f) - (k.aKey.isPressed ? 1f : 0f);
                float y = (k.wKey.isPressed ? 1f : 0f) - (k.sKey.isPressed ? 1f : 0f);
                moveInput = new Vector2(x, y);
            }
#endif

            if (moveInput.sqrMagnitude > 1f)
            {
                moveInput.Normalize();
            }

            Vector3 moveDirection = transform.forward * moveInput.y + transform.right * moveInput.x;
            Vector3 horizontalMotion = moveDirection * moveSpeed;

            // Gravity & Vertical Motion
            if (_characterController.isGrounded)
            {
                if (_verticalVelocity < 0f)
                {
                    _verticalVelocity = groundedDownwardForce;
                }
            }
            else
            {
                _verticalVelocity += gravity * Time.deltaTime;
            }

            Vector3 totalVelocity = horizontalMotion + Vector3.up * _verticalVelocity;
            _characterController.Move(totalVelocity * Time.deltaTime);
        }
    }
}
