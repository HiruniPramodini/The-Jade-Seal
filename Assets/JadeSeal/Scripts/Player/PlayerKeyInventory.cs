using System;
using UnityEngine;
using UnityEngine.Events;

namespace JadeSeal.Player
{
    /// <summary>
    /// Lightweight player inventory tracking key progression items, specifically the Jade Key.
    /// Attached to the Player root GameObject. Designed to be completely portable
    /// to future teammate player prefabs without global singletons or heavy dependencies.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerKeyInventory : MonoBehaviour
    {
#pragma warning disable CS0649
        [Header("Key Inventory State")]
        [Tooltip("True if the player currently possesses the ancient Jade Key.")]
        [SerializeField] private bool hasJadeKey = false;

        [Header("Events")]
        [Tooltip("Invoked when the Jade Key possession state changes.")]
        [SerializeField] private UnityEvent<bool> onJadeKeyChanged;
#pragma warning restore CS0649

        /// <summary>Returns true if the player currently possesses the Jade Key.</summary>
        public bool HasJadeKey => hasJadeKey;

        /// <summary>C# event invoked when Jade Key possession state changes.</summary>
        public event Action<bool> OnJadeKeyStateChanged;

        /// <summary>
        /// Adds the Jade Key to this player's inventory.
        /// </summary>
        public void AddJadeKey()
        {
            if (hasJadeKey) return;

            hasJadeKey = true;
            Debug.Log($"<color=#2ecc71><b>[PlayerKeyInventory]</b></color> '{name}' acquired the <b>Jade Key</b>!");

            onJadeKeyChanged?.Invoke(true);
            OnJadeKeyStateChanged?.Invoke(true);
        }

        /// <summary>
        /// Removes the Jade Key from this player's inventory (e.g. if consumed by an ancient seal).
        /// </summary>
        public void RemoveJadeKey()
        {
            if (!hasJadeKey) return;

            hasJadeKey = false;
            Debug.Log($"<color=#e67e22>[PlayerKeyInventory]</color> '{name}' used the <b>Jade Key</b>.");

            onJadeKeyChanged?.Invoke(false);
            OnJadeKeyStateChanged?.Invoke(false);
        }

        /// <summary>
        /// Explicitly sets the Jade Key possession state.
        /// </summary>
        public void SetJadeKey(bool state)
        {
            if (hasJadeKey == state) return;

            hasJadeKey = state;
            onJadeKeyChanged?.Invoke(state);
            OnJadeKeyStateChanged?.Invoke(state);
        }
    }
}
