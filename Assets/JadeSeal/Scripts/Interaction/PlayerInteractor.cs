using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace JadeSeal.Interaction
{
    /// <summary>
    /// Central player interaction component.
    /// Performs camera-forward raycasts to detect objects implementing <see cref="IInteractable"/>.
    /// Handles user input via the Unity New Input System and displays an on-screen reticle/prompt.
    ///
    /// NOTE ON INPUT HANDLING:
    /// The shared InputSystem_Actions asset has a "Hold" interaction on the Player/Interact action.
    /// To support single E-key taps for opening doors, picking up items, and inspecting objects without
    /// altering the shared input asset, this component listens to the action's .started callback, which fires
    /// immediately upon the key/button being pressed down. Safeguards prevent duplicate firings within the same frame.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerInteractor : MonoBehaviour
    {
#pragma warning disable CS0649
        [Header("Detection Settings")]
        [Tooltip("Maximum raycast distance for detecting interactables (in meters).")]
        [SerializeField] private float raycastRange = 3.0f;

        [Tooltip("Collision layers tested for interactable objects.")]
        [SerializeField] private LayerMask interactableLayers = ~0;

        [Tooltip("Whether the raycast detects Trigger colliders as well as physical colliders.")]
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;

        [Header("Camera Reference")]
        [Tooltip("The camera used for forward raycasting. If null, automatically resolves to Camera.main or child camera.")]
        [SerializeField] private Camera playerCamera;

        [Header("New Input System")]
        [Tooltip("Reference to the InputSystem_Actions asset. If unassigned, attempts to find active Player/Interact or falls back to direct device reading.")]
        [SerializeField] private InputActionAsset inputActionsAsset;

        [Tooltip("Action map name within the Input Action Asset.")]
        [SerializeField] private string actionMapName = "Player";

        [Tooltip("Action name for interaction within the map.")]
        [SerializeField] private string interactActionName = "Interact";

        [Header("UI Feedback")]
        [Tooltip("Whether to render an immediate on-screen prompt and reticle using IMGUI.")]
        [SerializeField] private bool showOnScreenPrompt = true;

        [Tooltip("Reticle crosshair color.")]
        [SerializeField] private Color reticleColor = new Color(1f, 1f, 1f, 0.6f);

        [Tooltip("Reticle color when targeting a valid interactable.")]
        [SerializeField] private Color activeReticleColor = new Color(0.2f, 1f, 0.5f, 0.95f);
#pragma warning restore CS0649

        // State tracking
        private IInteractable _currentInteractable;
        private GameObject _currentHitObject;
        private int _lastInteractionFrame = -1;
        private bool _isActionSubscribed;

#if ENABLE_INPUT_SYSTEM
        private InputAction _interactAction;
#endif

        /// <summary>Currently focused interactable object, or null if nothing valid is targeted.</summary>
        public IInteractable CurrentInteractable => _currentInteractable;

        /// <summary>GameObject associated with the currently focused interactable.</summary>
        public GameObject CurrentHitObject => _currentHitObject;

        private void Awake()
        {
            ResolveCameraReference();
            InitializeInputActions();
        }

        private void OnEnable()
        {
            SubscribeToInput();
        }

        private void OnDisable()
        {
            UnsubscribeFromInput();
            _currentInteractable = null;
            _currentHitObject = null;
        }

        private void Update()
        {
            if (playerCamera == null)
            {
                ResolveCameraReference();
            }

            PerformRaycastDetection();
            CheckDirectInputFallback();
        }

        private void ResolveCameraReference()
        {
            if (playerCamera != null) return;

            // 1. Check children
            playerCamera = GetComponentInChildren<Camera>();

            // 2. Check main camera
            if (playerCamera == null)
            {
                playerCamera = Camera.main;
            }

            // 3. Check parent
            if (playerCamera == null)
            {
                playerCamera = GetComponentInParent<Camera>();
            }
        }

        private void InitializeInputActions()
        {
#if ENABLE_INPUT_SYSTEM
            if (inputActionsAsset != null)
            {
                var map = inputActionsAsset.FindActionMap(actionMapName, false);
                if (map != null)
                {
                    _interactAction = map.FindAction(interactActionName, false);
                }
                else
                {
                    _interactAction = inputActionsAsset.FindAction($"{actionMapName}/{interactActionName}", false);
                }
            }
#endif
        }

        private void SubscribeToInput()
        {
#if ENABLE_INPUT_SYSTEM
            if (_interactAction != null && !_isActionSubscribed)
            {
                // Subscribing to started enables single-tap trigger on press down, bypassing the Hold duration requirement.
                _interactAction.started += OnInteractStarted;
                _interactAction.Enable();
                _isActionSubscribed = true;
            }
#endif
        }

        private void UnsubscribeFromInput()
        {
#if ENABLE_INPUT_SYSTEM
            if (_interactAction != null && _isActionSubscribed)
            {
                _interactAction.started -= OnInteractStarted;
                _isActionSubscribed = false;
            }
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private void OnInteractStarted(InputAction.CallbackContext context)
        {
            ExecuteInteraction();
        }
#endif

        private void CheckDirectInputFallback()
        {
#if ENABLE_INPUT_SYSTEM
            // Fallback for isolated testing when InputActionAsset reference is not wired
            if (!_isActionSubscribed)
            {
                var keyboard = Keyboard.current;
                if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
                {
                    ExecuteInteraction();
                    return;
                }

                var gamepad = Gamepad.current;
                if (gamepad != null && (gamepad.buttonNorth.wasPressedThisFrame || gamepad.buttonEast.wasPressedThisFrame))
                {
                    ExecuteInteraction();
                }
            }
#endif
        }

        private void PerformRaycastDetection()
        {
            if (playerCamera == null)
            {
                _currentInteractable = null;
                _currentHitObject = null;
                return;
            }

            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, raycastRange, interactableLayers, triggerInteraction))
            {
                // Support IInteractable on the hit collider or any of its parents
                var interactable = hit.collider.GetComponentInParent<IInteractable>();
                if (interactable != null && interactable.CanInteract(gameObject))
                {
                    _currentInteractable = interactable;
                    _currentHitObject = (interactable as Component)?.gameObject ?? hit.collider.gameObject;
                    return;
                }
            }

            _currentInteractable = null;
            _currentHitObject = null;
        }

        /// <summary>
        /// Triggers interaction on the currently focused interactable.
        /// Includes debouncing safeguards to prevent duplicate interaction events on the same frame.
        /// </summary>
        public void ExecuteInteraction()
        {
            // Safeguard: Prevent duplicate execution on the same frame
            if (Time.frameCount == _lastInteractionFrame)
            {
                return;
            }

            _lastInteractionFrame = Time.frameCount;

            if (_currentInteractable != null && _currentInteractable.CanInteract(gameObject))
            {
                _currentInteractable.Interact(gameObject);
            }
        }

        private void OnGUI()
        {
            if (!showOnScreenPrompt) return;

            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;

            bool hasTarget = _currentInteractable != null && _currentInteractable.CanInteract(gameObject);

            // Draw center reticle dot
            Color prevColor = GUI.color;
            GUI.color = hasTarget ? activeReticleColor : reticleColor;
            float reticleSize = hasTarget ? 8f : 5f;
            GUI.Box(new Rect(cx - reticleSize * 0.5f, cy - reticleSize * 0.5f, reticleSize, reticleSize), GUIContent.none);

            // Draw prompt text when targeting a valid interactable
            if (hasTarget)
            {
                string prompt = _currentInteractable.GetPromptText();
                if (!string.IsNullOrEmpty(prompt))
                {
                    string fullPrompt = $"[E]  {prompt}";

                    GUIStyle promptStyle = new GUIStyle(GUI.skin.box)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 15,
                        fontStyle = FontStyle.Bold
                    };
                    promptStyle.normal.textColor = Color.white;

                    Vector2 textSize = promptStyle.CalcSize(new GUIContent(fullPrompt));
                    float boxWidth = Mathf.Max(textSize.x + 24f, 180f);
                    float boxHeight = textSize.y + 12f;
                    Rect promptRect = new Rect(cx - boxWidth * 0.5f, cy + 24f, boxWidth, boxHeight);

                    // Semi-transparent dark background
                    GUI.color = new Color(0.08f, 0.12f, 0.15f, 0.85f);
                    GUI.Box(promptRect, GUIContent.none);

                    // White text
                    GUI.color = Color.white;
                    GUI.Label(promptRect, fullPrompt, promptStyle);
                }
            }

            GUI.color = prevColor;
        }

        private void OnDrawGizmosSelected()
        {
            Camera cam = playerCamera != null ? playerCamera : GetComponentInChildren<Camera>();
            if (cam == null) cam = Camera.main;
            if (cam == null) return;

            Gizmos.color = _currentInteractable != null ? Color.green : Color.yellow;
            Gizmos.DrawRay(cam.transform.position, cam.transform.forward * raycastRange);
        }
    }
}
