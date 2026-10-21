using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using JadeSeal.Player;

namespace JadeSeal.Interaction
{
    /// <summary>
    /// Type of movement executed when the door or gate transitions between open and closed states.
    /// </summary>
    public enum DoorMotionType
    {
        RotatingSwing,
        Sliding
    }

    /// <summary>
    /// Determines whether the swinging door opens toward a fixed angle or swings dynamically away from the player.
    /// </summary>
    public enum SwingDirectionMode
    {
        AwayFromPlayer,
        FixedAngle
    }

    /// <summary>
    /// Reusable door and gate controller implementing <see cref="IInteractable"/>.
    /// Smoothly transitions doors and gates between closed and open states via coroutine-driven interpolation.
    /// Supports single-leaf doors, paired/double gates, sliding portcullises, collision blocking,
    /// optional audio feedback, and preparation hooks for future Jade Key locking logic.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DoorInteraction : MonoBehaviour, IInteractable
    {
#pragma warning disable CS0649
        [Header("Motion Settings")]
        [Tooltip("Type of animation: RotatingSwing for hinged doors/gates; Sliding for portcullises/screens.")]
        [SerializeField] private DoorMotionType motionType = DoorMotionType.RotatingSwing;

        [Tooltip("How the swing direction is determined. AwayFromPlayer prevents doors from swinging into the player.")]
        [SerializeField] private SwingDirectionMode swingDirection = SwingDirectionMode.AwayFromPlayer;

        [Tooltip("Target rotation angle around the local Y-axis when open (in degrees).")]
        [SerializeField] private float openAngle = 90.0f;

        [Tooltip("Local translation offset when fully open (used when motionType is Sliding).")]
        [SerializeField] private Vector3 slideOffset = new Vector3(0f, 3.0f, 0f);

        [Tooltip("Duration of the opening or closing transition in seconds.")]
        [SerializeField] private float animationDuration = 1.0f;

        [Tooltip("If true, interacting while moving will smoothly reverse the door. If false, interactions are locked until motion completes.")]
        [SerializeField] private bool allowInterrupt = false;

        [Header("Hinge & Pivot References")]
        [Tooltip("Primary moving hinge transform. If null, this GameObject's transform is used.")]
        [SerializeField] private Transform primaryHinge;

        [Tooltip("Optional secondary hinge for paired double-gates. Rotates symmetrically in the opposite direction.")]
        [SerializeField] private Transform secondaryHinge;

        [Tooltip("Optional reference to paired door leaf controller to keep lock and animation state in sync.")]
        [SerializeField] private DoorInteraction pairedDoor;

        [Header("Initial State")]
        [Tooltip("Whether the door starts in the open state at scene launch.")]
        [SerializeField] private bool startOpen = false;

        [Header("Interaction Prompts")]
        [Tooltip("HUD prompt text when the door is closed and ready to be opened.")]
        [SerializeField] private string openPromptText = "Open Gate";

        [Tooltip("HUD prompt text when the door is open and ready to be closed.")]
        [SerializeField] private string closePromptText = "Close Gate";

        [Header("Audio Feedback (Optional)")]
        [Tooltip("AudioSource component for playback. If unassigned, audio is skipped silently.")]
        [SerializeField] private AudioSource audioSource;

        [Tooltip("Sound played when the door begins opening.")]
        [SerializeField] private AudioClip openAudioClip;

        [Tooltip("Sound played when the door begins closing.")]
        [SerializeField] private AudioClip closeAudioClip;

        [Tooltip("Sound played when an interaction attempt is made on a locked door.")]
        [SerializeField] private AudioClip lockedAudioClip;

        [Header("Lock Settings (Key & Progression Integration)")]
        [Tooltip("If true, the door cannot be opened until unlocked by a key or script.")]
        [SerializeField] private bool isLocked = false;

        [Tooltip("If true, a locked door checks the interactor's PlayerKeyInventory for the Jade Key on interaction.")]
        [SerializeField] private bool requiresJadeKey = true;

        [Tooltip("HUD prompt displayed when targeting a locked door without the required key.")]
        [SerializeField] private string lockedPromptText = "Locked (Requires Jade Key)";

        [Tooltip("HUD prompt displayed when targeting a locked door with the required Jade Key in hand.")]
        [SerializeField] private string unlockPromptText = "Unlock Gate with Jade Key";

        [Tooltip("Whether the Jade Key is consumed upon unlocking (default false for permanent key ring progression).")]
        [SerializeField] private bool consumeKeyOnUnlock = false;

        [Header("Events")]
        [Tooltip("Event fired when the door finishes opening (true) or closing (false).")]
        [SerializeField] private UnityEvent<bool> onStateChanged;
#pragma warning restore CS0649

        // State tracking
        private bool _isOpen;
        private bool _isAnimating;
        private Coroutine _animationCoroutine;
        private GameObject _lastCheckedInteractor;

        // Cached primary hinge transform values
        private Transform _targetPrimary;
        private Quaternion _primaryClosedRot;
        private Vector3 _primaryClosedPos;

        // Cached secondary hinge transform values
        private Quaternion _secondaryClosedRot;
        private Vector3 _secondaryClosedPos;

        /// <summary>True if the door is currently fully open or opening.</summary>
        public bool IsOpen => _isOpen;

        /// <summary>True if the door is currently in motion between states.</summary>
        public bool IsAnimating => _isAnimating;

        /// <summary>Lock status of the door.</summary>
        public bool IsLocked
        {
            get => isLocked;
            set => isLocked = value;
        }

        /// <summary>Whether unlocking this door requires the Jade Key.</summary>
        public bool RequiresJadeKey
        {
            get => requiresJadeKey;
            set => requiresJadeKey = value;
        }

        /// <summary>Event raised whenever the door completes a transition (true = open, false = closed).</summary>
        public event Action<bool> OnDoorStateChanged;

        private void Awake()
        {
            _targetPrimary = primaryHinge != null ? primaryHinge : transform;

            // Cache closed baseline transforms
            _primaryClosedRot = _targetPrimary.localRotation;
            _primaryClosedPos = _targetPrimary.localPosition;

            if (secondaryHinge != null)
            {
                _secondaryClosedRot = secondaryHinge.localRotation;
                _secondaryClosedPos = secondaryHinge.localPosition;
            }

            // Apply initial state
            _isOpen = startOpen;
            if (_isOpen)
            {
                ApplyImmediateState(true, null);
            }
        }

        private void OnDisable()
        {
            if (_animationCoroutine != null)
            {
                StopCoroutine(_animationCoroutine);
                _animationCoroutine = null;
            }
            _isAnimating = false;
        }

        #region IInteractable Implementation

        /// <summary>
        /// Evaluates whether the player can currently interact with the door.
        /// Returns false while animating if interruption is disabled.
        /// </summary>
        public bool CanInteract(GameObject interactor)
        {
            if (!enabled || !gameObject.activeInHierarchy) return false;
            if (pairedDoor != null) return pairedDoor.CanInteract(interactor);
            if (_isAnimating && !allowInterrupt) return false;

            _lastCheckedInteractor = interactor;
            return true;
        }

        /// <summary>
        /// Returns the contextual HUD prompt based on current lock state, Jade Key possession, and open/closed state.
        /// </summary>
        public string GetPromptText()
        {
            if (pairedDoor != null) return pairedDoor.GetPromptText();

            if (isLocked)
            {
                if (requiresJadeKey && _lastCheckedInteractor != null)
                {
                    var inventory = _lastCheckedInteractor.GetComponentInParent<PlayerKeyInventory>();
                    if (inventory != null && inventory.HasJadeKey)
                    {
                        return unlockPromptText;
                    }
                }

                return lockedPromptText;
            }

            return _isOpen ? closePromptText : openPromptText;
        }

        /// <summary>
        /// Executes interaction triggered by PlayerInteractor on E-key press.
        /// Automatically checks the player's PlayerKeyInventory if the door is locked.
        /// </summary>
        public void Interact(GameObject interactor)
        {
            if (pairedDoor != null)
            {
                pairedDoor.Interact(interactor);
                return;
            }

            if (isLocked)
            {
                if (requiresJadeKey && interactor != null)
                {
                    var inventory = interactor.GetComponentInParent<PlayerKeyInventory>();
                    if (inventory != null && inventory.HasJadeKey)
                    {
                        if (consumeKeyOnUnlock)
                        {
                            inventory.RemoveJadeKey();
                        }

                        Unlock();
                        Debug.Log($"<color=#2ecc71><b>[DoorInteraction]</b></color> '{name}' successfully unlocked with <b>Jade Key</b> by '{interactor.name}'!");
                        OpenDoor(interactor);
                        return;
                    }
                }

                PlayAudio(lockedAudioClip);
                Debug.Log($"<color=#e74c3c>[DoorInteraction]</color> '{name}' is locked. {lockedPromptText}");
                return;
            }

            ToggleDoor(interactor);
        }

        #endregion

        #region Door Control API

        /// <summary>
        /// Toggles the door between open and closed states.
        /// </summary>
        /// <param name="interactor">Optional interactor used for dynamic swing direction detection.</param>
        public void ToggleDoor(GameObject interactor = null)
        {
            if (_isOpen)
            {
                CloseDoor(interactor);
            }
            else
            {
                OpenDoor(interactor);
            }
        }

        /// <summary>
        /// Smoothly opens the door.
        /// </summary>
        public void OpenDoor(GameObject interactor = null)
        {
            if (_isOpen && !_isAnimating) return;
            if (_isAnimating && !allowInterrupt) return;

            StartDoorTransition(true, interactor);
        }

        /// <summary>
        /// Smoothly closes the door.
        /// </summary>
        public void CloseDoor(GameObject interactor = null)
        {
            if (!_isOpen && !_isAnimating) return;
            if (_isAnimating && !allowInterrupt) return;

            StartDoorTransition(false, interactor);
        }

        /// <summary>
        /// Unlocks the door, allowing normal interaction. Intended for Task 5 key integration.
        /// </summary>
        public void Unlock()
        {
            isLocked = false;
            if (pairedDoor != null && pairedDoor.isLocked)
            {
                pairedDoor.Unlock();
            }
            Debug.Log($"<color=#2ecc71>[DoorInteraction]</color> '{name}' unlocked.");
        }

        /// <summary>
        /// Links this door to another door controller (e.g., paired double gate leaf).
        /// </summary>
        public void SetPairedDoor(DoorInteraction other)
        {
            pairedDoor = other;
        }

        /// <summary>
        /// Locks the door. If open, begins closing transition.
        /// </summary>
        public void Lock()
        {
            isLocked = true;
            if (_isOpen)
            {
                CloseDoor();
            }
            Debug.Log($"<color=#e67e22>[DoorInteraction]</color> '{name}' locked.");
        }

        /// <summary>
        /// Explicit unlock attempt passing a key item or PlayerKeyInventory.
        /// </summary>
        public bool TryUnlock(object keyItem = null)
        {
            if (!isLocked) return true;

            if (keyItem is PlayerKeyInventory inventory && inventory.HasJadeKey)
            {
                if (consumeKeyOnUnlock)
                {
                    inventory.RemoveJadeKey();
                }

                Unlock();
                return true;
            }

            if (!requiresJadeKey)
            {
                Unlock();
                return true;
            }

            return false;
        }

        #endregion

        #region Animation & Math

        private void StartDoorTransition(bool targetOpen, GameObject interactor)
        {
            if (_animationCoroutine != null)
            {
                StopCoroutine(_animationCoroutine);
            }

            _isOpen = targetOpen;
            PlayAudio(_isOpen ? openAudioClip : closeAudioClip);

            // Compute target rotations & positions
            CalculateTargets(targetOpen, interactor,
                out Quaternion targetPrimaryRot, out Vector3 targetPrimaryPos,
                out Quaternion targetSecondaryRot, out Vector3 targetSecondaryPos);

            _animationCoroutine = StartCoroutine(AnimateTransition(
                targetPrimaryRot, targetPrimaryPos,
                targetSecondaryRot, targetSecondaryPos));

            Debug.Log($"<color=#3498db>[DoorInteraction]</color> '{name}' {(_isOpen ? "Opening" : "Closing")} (Duration: {animationDuration:F1}s).");
        }

        private void CalculateTargets(bool targetOpen, GameObject interactor,
            out Quaternion primaryRot, out Vector3 primaryPos,
            out Quaternion secondaryRot, out Vector3 secondaryPos)
        {
            if (motionType == DoorMotionType.RotatingSwing)
            {
                primaryPos = _primaryClosedPos;
                secondaryPos = _secondaryClosedPos;

                if (!targetOpen)
                {
                    primaryRot = _primaryClosedRot;
                    secondaryRot = _secondaryClosedRot;
                    return;
                }

                // Determine swing sign: dynamic away from player or fixed
                float swingSign = 1.0f;
                if (swingDirection == SwingDirectionMode.AwayFromPlayer && interactor != null)
                {
                    Vector3 toPlayer = (interactor.transform.position - _targetPrimary.position).normalized;
                    float dot = Vector3.Dot(_targetPrimary.forward, toPlayer);
                    // If player is in front (dot > 0), swing inward/backward (-1), else outward (+1)
                    swingSign = dot > 0f ? -1.0f : 1.0f;
                }

                primaryRot = _primaryClosedRot * Quaternion.Euler(0f, openAngle * swingSign, 0f);

                // Secondary hinge swings opposite to mirror primary gate
                if (secondaryHinge != null)
                {
                    secondaryRot = _secondaryClosedRot * Quaternion.Euler(0f, -openAngle * swingSign, 0f);
                }
                else
                {
                    secondaryRot = _secondaryClosedRot;
                }
            }
            else // Sliding
            {
                primaryRot = _primaryClosedRot;
                secondaryRot = _secondaryClosedRot;

                if (!targetOpen)
                {
                    primaryPos = _primaryClosedPos;
                    secondaryPos = _secondaryClosedPos;
                }
                else
                {
                    primaryPos = _primaryClosedPos + slideOffset;
                    // Secondary sliding gate mirrors horizontal offset if provided
                    Vector3 secondaryOffset = new Vector3(-slideOffset.x, slideOffset.y, slideOffset.z);
                    secondaryPos = _secondaryClosedPos + secondaryOffset;
                }
            }
        }

        private IEnumerator AnimateTransition(
            Quaternion targetPrimaryRot, Vector3 targetPrimaryPos,
            Quaternion targetSecondaryRot, Vector3 targetSecondaryPos)
        {
            _isAnimating = true;

            Quaternion startPrimaryRot = _targetPrimary.localRotation;
            Vector3 startPrimaryPos = _targetPrimary.localPosition;

            Quaternion startSecondaryRot = secondaryHinge != null ? secondaryHinge.localRotation : Quaternion.identity;
            Vector3 startSecondaryPos = secondaryHinge != null ? secondaryHinge.localPosition : Vector3.zero;

            float elapsed = 0f;
            float duration = Mathf.Max(animationDuration, 0.05f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // SmoothStep creates a natural ease-in, ease-out mechanical motion
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                if (motionType == DoorMotionType.RotatingSwing)
                {
                    _targetPrimary.localRotation = Quaternion.Slerp(startPrimaryRot, targetPrimaryRot, smoothT);
                    if (secondaryHinge != null)
                    {
                        secondaryHinge.localRotation = Quaternion.Slerp(startSecondaryRot, targetSecondaryRot, smoothT);
                    }
                }
                else // Sliding
                {
                    _targetPrimary.localPosition = Vector3.Lerp(startPrimaryPos, targetPrimaryPos, smoothT);
                    if (secondaryHinge != null)
                    {
                        secondaryHinge.localPosition = Vector3.Lerp(startSecondaryPos, targetSecondaryPos, smoothT);
                    }
                }

                yield return null;
            }

            // Snap accurately to final target values
            if (motionType == DoorMotionType.RotatingSwing)
            {
                _targetPrimary.localRotation = targetPrimaryRot;
                if (secondaryHinge != null)
                {
                    secondaryHinge.localRotation = targetSecondaryRot;
                }
            }
            else
            {
                _targetPrimary.localPosition = targetPrimaryPos;
                if (secondaryHinge != null)
                {
                    secondaryHinge.localPosition = targetSecondaryPos;
                }
            }

            _isAnimating = false;
            _animationCoroutine = null;

            onStateChanged?.Invoke(_isOpen);
            OnDoorStateChanged?.Invoke(_isOpen);
        }

        private void ApplyImmediateState(bool open, GameObject interactor)
        {
            CalculateTargets(open, interactor,
                out Quaternion targetPrimaryRot, out Vector3 targetPrimaryPos,
                out Quaternion targetSecondaryRot, out Vector3 targetSecondaryPos);

            if (motionType == DoorMotionType.RotatingSwing)
            {
                _targetPrimary.localRotation = targetPrimaryRot;
                if (secondaryHinge != null) secondaryHinge.localRotation = targetSecondaryRot;
            }
            else
            {
                _targetPrimary.localPosition = targetPrimaryPos;
                if (secondaryHinge != null) secondaryHinge.localPosition = targetSecondaryPos;
            }
        }

        private void PlayAudio(AudioClip clip)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }

        #endregion

        #region Editor Gizmos

        private void OnDrawGizmosSelected()
        {
            Transform pivot = primaryHinge != null ? primaryHinge : transform;
            Gizmos.color = _isOpen ? Color.green : new Color(0.85f, 0.35f, 0.1f);
            Gizmos.DrawWireSphere(pivot.position, 0.2f);

            if (motionType == DoorMotionType.RotatingSwing)
            {
                Gizmos.DrawRay(pivot.position, pivot.forward * 1.5f);
            }
            else
            {
                Gizmos.DrawLine(pivot.position, pivot.position + pivot.TransformDirection(slideOffset));
            }
        }

        #endregion
    }
}

