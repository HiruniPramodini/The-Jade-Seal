using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using JadeSeal.Interaction;

namespace JadeSeal.Player
{
    /// <summary>
    /// Player object carrying and throwing component.
    /// Manages picking up a single <see cref="PickupableObject"/>, anchoring it in front of the camera,
    /// preventing wall clipping, and launching it with physics impulse forces using the New Input System.
    ///
    /// CONTROLS (Configurable):
    /// - Pick Up: [E] (via PlayerInteractor)
    /// - Throw:   [G] (or Gamepad Right Trigger)
    /// - Drop:    [Q] (gentle release without force)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerObjectHolder : MonoBehaviour
    {
#pragma warning disable CS0649
        [Header("Hold Positioning")]
        [Tooltip("Child transform used as the hold anchor. If null, automatically generated on the camera.")]
        [SerializeField] private Transform holdAnchor;

        [Tooltip("Camera reference. If unassigned, automatically resolved in children or Camera.main.")]
        [SerializeField] private Camera playerCamera;

        [Tooltip("Local offset from camera position where the object is held.")]
        [SerializeField] private Vector3 holdOffset = new Vector3(0.35f, -0.25f, 1.15f);

        [Tooltip("If true, performs a sphere-cast to pull the held object back if the player faces a wall.")]
        [SerializeField] private bool preventWallClipping = true;

        [Tooltip("Collision layers considered walls/obstacles for anti-clipping.")]
        [SerializeField] private LayerMask clippingLayers = ~0;

        [Header("Throw & Drop Physics")]
        [Tooltip("Forward impulse force applied when throwing.")]
        [SerializeField] private float throwForce = 15.0f;

        [Tooltip("Upward arc impulse to provide natural ballistic trajectory.")]
        [SerializeField] private float upwardArcForce = 2.0f;

        [Tooltip("Cooldown period in seconds after throwing before another object can be picked up.")]
        [SerializeField] private float throwCooldown = 0.25f;

        [Header("Input Bindings (New Input System)")]
        [Tooltip("Keyboard key used to throw the held object.")]
        [SerializeField] private Key throwKey = Key.G;

        [Tooltip("Keyboard key used to gently drop the held object without throwing.")]
        [SerializeField] private Key dropKey = Key.Q;

        [Tooltip("Optional: Allow throwing with Left Mouse Button if not conflicted with combat.")]
        [SerializeField] private bool allowLeftMouseThrow = false;

        [Header("HUD Feedback")]
        [Tooltip("Show on-screen control hint when holding an object.")]
        [SerializeField] private bool showHeldHUD = true;
#pragma warning restore CS0649

        private PickupableObject _heldObject;
        private Collider _playerCollider;
        private CharacterController _characterController;
        private float _lastThrowTime = -10f;

        /// <summary>True if currently carrying a pickupable object.</summary>
        public bool IsHoldingObject => _heldObject != null;

        /// <summary>Reference to the currently carried object, or null.</summary>
        public PickupableObject HeldObject => _heldObject;

        private void Awake()
        {
            _playerCollider = GetComponent<Collider>();
            _characterController = GetComponent<CharacterController>();

            ResolveCameraAndHoldAnchor();
        }

        private void Update()
        {
            if (playerCamera == null || holdAnchor == null)
            {
                ResolveCameraAndHoldAnchor();
            }

            UpdateHoldAnchorPosition();
            HandleThrowInput();
        }

        private void ResolveCameraAndHoldAnchor()
        {
            if (playerCamera == null)
            {
                playerCamera = GetComponentInChildren<Camera>();
                if (playerCamera == null) playerCamera = Camera.main;
            }

            if (holdAnchor == null && playerCamera != null)
            {
                // Check if existing child hold point exists
                var existingAnchor = playerCamera.transform.Find("Hold_Anchor");
                if (existingAnchor != null)
                {
                    holdAnchor = existingAnchor;
                }
                else
                {
                    var anchorGo = new GameObject("Hold_Anchor");
                    anchorGo.transform.SetParent(playerCamera.transform, false);
                    anchorGo.transform.localPosition = holdOffset;
                    holdAnchor = anchorGo.transform;
                }
            }
        }

        private void UpdateHoldAnchorPosition()
        {
            if (playerCamera == null || holdAnchor == null) return;

            Vector3 camPos = playerCamera.transform.position;
            Vector3 targetWorldPos = camPos + playerCamera.transform.TransformDirection(holdOffset);

            if (preventWallClipping)
            {
                Vector3 toTarget = targetWorldPos - camPos;
                float targetDist = toTarget.magnitude;
                Vector3 dir = toTarget.normalized;

                // SphereCast from camera along the hold vector
                if (Physics.SphereCast(camPos, 0.15f, dir, out RaycastHit hit, targetDist, clippingLayers, QueryTriggerInteraction.Ignore))
                {
                    // Pull back slightly from the obstruction
                    float safeDistance = Mathf.Max(hit.distance - 0.1f, 0.35f);
                    holdAnchor.position = camPos + dir * safeDistance;
                }
                else
                {
                    holdAnchor.position = targetWorldPos;
                }
            }
            else
            {
                holdAnchor.position = targetWorldPos;
            }

            holdAnchor.rotation = playerCamera.transform.rotation;
        }

        private void HandleThrowInput()
        {
            if (_heldObject == null) return;

#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            var gamepad = Gamepad.current;

            // Throw check: [G] key, or Left Mouse if enabled, or Gamepad Right Trigger
            bool throwPressed = false;
            if (keyboard != null && keyboard[throwKey].wasPressedThisFrame) throwPressed = true;
            if (allowLeftMouseThrow && mouse != null && mouse.leftButton.wasPressedThisFrame) throwPressed = true;
            if (gamepad != null && (gamepad.rightTrigger.wasPressedThisFrame || gamepad.buttonWest.wasPressedThisFrame)) throwPressed = true;

            if (throwPressed)
            {
                ThrowHeldObject();
                return;
            }

            // Drop check: [Q] key or Gamepad East
            bool dropPressed = false;
            if (keyboard != null && keyboard[dropKey].wasPressedThisFrame) dropPressed = true;
            if (gamepad != null && gamepad.buttonEast.wasPressedThisFrame) dropPressed = true;

            if (dropPressed)
            {
                DropHeldObject();
            }
#endif
        }

        /// <summary>
        /// Picks up the specified object, anchoring it to the player's hold anchor.
        /// </summary>
        public void PickupObject(PickupableObject pickupable)
        {
            if (pickupable == null || _heldObject != null) return;
            if (Time.time - _lastThrowTime < throwCooldown) return;

            _heldObject = pickupable;

            Collider activeCol = _playerCollider != null ? _playerCollider : _characterController;
            _heldObject.OnPickedUp(this, holdAnchor, activeCol);

            Debug.Log($"<color=#2ecc71><b>[PlayerObjectHolder]</b></color> Picked up <b>{_heldObject.name}</b>. Press [G] to throw, [Q] to drop.");
        }

        /// <summary>
        /// Launches the carried object forward with ballistic impulse force.
        /// </summary>
        public void ThrowHeldObject()
        {
            if (_heldObject == null) return;

            Vector3 camForward = playerCamera != null ? playerCamera.transform.forward : transform.forward;
            Vector3 impulse = camForward * throwForce + Vector3.up * upwardArcForce;

            Vector3 inheritVel = Vector3.zero;
            if (_characterController != null)
            {
                inheritVel = _characterController.velocity;
            }

            Collider activeCol = _playerCollider != null ? _playerCollider : _characterController;
            var objToThrow = _heldObject;
            _heldObject = null;
            _lastThrowTime = Time.time;

            objToThrow.OnReleased(impulse, inheritVel, activeCol);
            Debug.Log($"<color=#3498db><b>[PlayerObjectHolder]</b></color> Threw <b>{objToThrow.name}</b> (Force: {throwForce:F1}).");
        }

        /// <summary>
        /// Gently drops the carried object without throwing force.
        /// </summary>
        public void DropHeldObject()
        {
            if (_heldObject == null) return;

            Vector3 inheritVel = Vector3.zero;
            if (_characterController != null)
            {
                inheritVel = _characterController.velocity * 0.5f;
            }

            Collider activeCol = _playerCollider != null ? _playerCollider : _characterController;
            var objToDrop = _heldObject;
            _heldObject = null;
            _lastThrowTime = Time.time;

            objToDrop.OnReleased(Vector3.zero, inheritVel, activeCol);
            Debug.Log($"<color=#e67e22><b>[PlayerObjectHolder]</b></color> Dropped <b>{objToDrop.name}</b>.");
        }

        /// <summary>
        /// Internal safeguard if the held object is destroyed while held.
        /// </summary>
        public void ClearHeldObjectSilently()
        {
            _heldObject = null;
        }

        private void OnGUI()
        {
            if (!showHeldHUD || _heldObject == null) return;

            float cx = Screen.width * 0.5f;
            float cy = Screen.height - 70f;

            string hudText = $"<b>Holding:</b> {_heldObject.name}    |    <b>[{throwKey}]</b> Throw    |    <b>[{dropKey}]</b> Drop";

            GUIStyle style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                richText = true
            };
            style.normal.textColor = Color.white;

            Vector2 size = style.CalcSize(new GUIContent(hudText));
            float boxW = size.x + 30f;
            float boxH = size.y + 10f;
            Rect boxRect = new Rect(cx - boxW * 0.5f, cy, boxW, boxH);

            Color prev = GUI.color;
            GUI.color = new Color(0.08f, 0.12f, 0.16f, 0.88f);
            GUI.Box(boxRect, GUIContent.none);

            GUI.color = Color.white;
            GUI.Label(boxRect, hudText, style);
            GUI.color = prev;
        }
    }
}

