using System;
using UnityEngine;

namespace JadeSeal.BambooHunter
{
    /// <summary>
    /// Explicit, inspector-configurable vegetation cover anchor for the Bamboo Hunter AI.
    ///
    /// ARCHITECTURE & DESIGN CONTRACTS:
    /// 1. Dual Anchor Geometry: Defines a HidePosition (concealment behind vegetation/obstacle)
    ///    and an optional PeekPosition (step-out location for clear archery firing line).
    /// 2. Modular Scene Independence: Authored as standalone prefabs or inspector-assigned nodes.
    ///    Does NOT require modifying shared world geometry or scenes.
    /// 3. Exposure Probing: Evaluates multi-height linecasts against obstacle layers to quantify
    ///    threat exposure without assuming visual bamboo trees have physics colliders.
    /// 4. Anti-Repetition & Reservation: Tracks occupancy and cooldown timestamps to prevent the
    ///    agent from repeatedly retreating to the exact same position in combat.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BambooHunterCoverPoint : MonoBehaviour
    {
        [Header("Anchor Positions")]
        [Tooltip("Optional transform for step-out peek position. If unassigned, HidePosition is used.")]
        [SerializeField] private Transform peekAnchor;

        [Tooltip("Local offset added to transform position if peekAnchor is unassigned.")]
        [SerializeField] private Vector3 peekOffset = new Vector3(0.8f, 0f, 0f);

        [Header("Tactical Properties")]
        [Tooltip("Authored cover quality rating normalized in [0.0, 1.0].")]
        [Range(0f, 1f)] [SerializeField] private float coverQuality = 1.0f;

        [Tooltip("Whether this cover point represents vegetation concealment rather than solid stone/architecture.")]
        [SerializeField] private bool isVegetationCover = true;

        [Tooltip("Estimated protection radius around the hide position in meters.")]
        [Min(0.2f)] [SerializeField] private float protectionRadius = 0.8f;

        [Header("Runtime State (Read Only)")]
        [SerializeField] private bool isReserved;
        [SerializeField] private float lastOccupiedTime = float.NegativeInfinity;
        [SerializeField] private float lastRejectedTime = float.NegativeInfinity;

        private object currentOccupant;

        #region Public Properties

        /// <summary>World position where the agent conceals itself from threat sightlines.</summary>
        public Vector3 HidePosition => transform.position;

        /// <summary>Local offset added to transform position if peekAnchor is unassigned.</summary>
        public Vector3 PeekOffset
        {
            get => peekOffset;
            set => peekOffset = value;
        }

        /// <summary>World position where the agent steps out to acquire clean arrow firing lines.</summary>
        public Vector3 PeekPosition
        {
            get
            {
                if (peekAnchor != null) return peekAnchor.position;
                return transform.position + transform.TransformDirection(peekOffset);
            }
        }

        /// <summary>Authored forward facing vector indicating the direction of cover orientation.</summary>
        public Vector3 FacingDirection => transform.forward;

        /// <summary>Authored quality factor clamped in [0.0, 1.0].</summary>
        public float Quality => Mathf.Clamp01(coverQuality);

        /// <summary>True if this anchor is categorized as vegetation cover.</summary>
        public bool IsVegetationCover => isVegetationCover;

        /// <summary>Effective radius around the hide position considered protected.</summary>
        public float ProtectionRadius => Mathf.Max(0.1f, protectionRadius);

        /// <summary>
        /// Explicit initialization helper for runtime or editor setup.
        /// </summary>
        public void Initialize(Vector3 peekLocalOffset, float quality = 1.0f, float radius = 1.5f, bool isVegetation = true)
        {
            peekOffset = peekLocalOffset;
            coverQuality = Mathf.Clamp01(quality);
            protectionRadius = Mathf.Max(0.1f, radius);
            isVegetationCover = isVegetation;
        }

        /// <summary>True if this cover point is currently reserved by an agent.</summary>
        public bool IsReserved => isReserved;

        /// <summary>Timestamp when this cover point was last used or vacated.</summary>
        public float LastOccupiedTime => lastOccupiedTime;

        /// <summary>Timestamp when this cover point was rejected due to a failed path or exposure.</summary>
        public float LastRejectedTime => lastRejectedTime;

        #endregion

        #region Availability & Reservation

        /// <summary>
        /// Validates that this cover point has finite, valid world coordinates and is active.
        /// </summary>
        public bool IsValid
        {
            get
            {
                if (!isActiveAndEnabled) return false;
                Vector3 hide = HidePosition;
                Vector3 peek = PeekPosition;

                bool hideFinite = !float.IsNaN(hide.x) && !float.IsNaN(hide.y) && !float.IsNaN(hide.z) &&
                                  !float.IsInfinity(hide.x) && !float.IsInfinity(hide.y) && !float.IsInfinity(hide.z);

                bool peekFinite = !float.IsNaN(peek.x) && !float.IsNaN(peek.y) && !float.IsNaN(peek.z) &&
                                  !float.IsInfinity(peek.x) && !float.IsInfinity(peek.y) && !float.IsInfinity(peek.z);

                return hideFinite && peekFinite;
            }
        }

        /// <summary>
        /// Checks whether this cover point can be selected by the requester.
        /// Evaluates active reservation, cooldown timer, and temporary rejection window.
        /// </summary>
        public bool IsAvailable(object requester, float cooldownDuration, float currentTime)
        {
            if (!IsValid) return false;

            // If reserved by another entity, cannot select
            if (isReserved && currentOccupant != requester) return false;

            // Anti-repetition cooldown: if recently vacated, must wait before re-selecting
            if (cooldownDuration > 0f && lastOccupiedTime > 0f)
            {
                if ((currentTime - lastOccupiedTime) < cooldownDuration)
                {
                    return false;
                }
            }

            // Temporary rejection penalty (e.g. from failed navigation path)
            if (currentTime < lastRejectedTime)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Reserves this cover point for the specified agent.
        /// </summary>
        public bool Reserve(object requester)
        {
            if (isReserved && currentOccupant != requester) return false;

            isReserved = true;
            currentOccupant = requester;
            return true;
        }

        /// <summary>
        /// Releases this cover point, recording the timestamp for anti-repetition cooldown.
        /// Optional simulated currentTime parameter for testing.
        /// </summary>
        public void Release(object requester, float currentTime = -1f)
        {
            if (currentOccupant == requester || requester == null)
            {
                isReserved = false;
                currentOccupant = null;
                lastOccupiedTime = currentTime >= 0f ? currentTime : Time.time;
            }
        }

        /// <summary>
        /// Marks this cover point as temporarily rejected (e.g., path unreachable).
        /// Optional simulated currentTime parameter for testing.
        /// </summary>
        public void Reject(float penaltySeconds = 5.0f, float currentTime = -1f)
        {
            isReserved = false;
            currentOccupant = null;
            lastRejectedTime = (currentTime >= 0f ? currentTime : Time.time) + Mathf.Max(0.5f, penaltySeconds);
        }

        /// <summary>
        /// Calculates remaining cooldown time before this cover point becomes eligible again.
        /// </summary>
        public float CooldownRemaining(float cooldownDuration, float currentTime)
        {
            if (lastOccupiedTime <= 0f) return 0f;
            float elapsed = currentTime - lastOccupiedTime;
            return Mathf.Max(0f, cooldownDuration - elapsed);
        }

        #endregion

        #region Tactical Exposure Probing

        /// <summary>
        /// Evaluates physical line-of-sight exposure from a threat position to this hide position.
        /// Performs non-allocating vertical multi-probe linecasts (crouch, chest, head heights).
        /// Returns normalized exposure ratio in [0.0, 1.0] (0.0 = fully concealed, 1.0 = fully exposed).
        /// </summary>
        public float CalculateExposure(Vector3 threatAimPosition, LayerMask obstacleMask, int probeCount = 3)
        {
            if (probeCount <= 0) return 0f;

            Vector3 baseHide = HidePosition;
            int clearRays = 0;

            for (int i = 0; i < probeCount; i++)
            {
                // Vertical probe offsets: 0.5m (crouch), 1.0m (torso), 1.6m (head)
                float heightOffset = 0.5f + (1.1f / Mathf.Max(1, probeCount - 1)) * i;
                Vector3 probePoint = baseHide + Vector3.up * heightOffset;

                // If linecast does not hit an obstacle, the ray is clear (exposed to threat)
                if (!Physics.Linecast(threatAimPosition, probePoint, obstacleMask, QueryTriggerInteraction.Ignore))
                {
                    clearRays++;
                }
            }

            return (float)clearRays / probeCount;
        }

        #endregion

        #region Editor Visualization & Gizmos

        private void OnDrawGizmosSelected()
        {
            Vector3 hide = HidePosition;
            Vector3 peek = PeekPosition;

            // Hide point (Blue/Cyan)
            Gizmos.color = isReserved ? Color.red : (isVegetationCover ? new Color(0.2f, 0.9f, 0.3f) : Color.cyan);
            Gizmos.DrawWireSphere(hide, ProtectionRadius);

            // Peek point (Yellow)
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(peek, 0.25f);
            Gizmos.DrawLine(hide, peek);

            // Facing direction (Green arrow)
            Gizmos.color = Color.green;
            Gizmos.DrawRay(hide, FacingDirection * 1.5f);
        }

        #endregion
    }
}
