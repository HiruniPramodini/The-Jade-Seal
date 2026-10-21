using UnityEngine;

namespace JadeSeal.Interaction
{
    /// <summary>
    /// Core interaction contract implemented by all interactable objects in The Jade Seal
    /// (doors, gates, keys, physical pickups, movable barricades, and inspectable objects).
    /// Decouples interactor components from specific world/puzzle logic.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>
        /// Evaluates whether the specified interactor GameObject is currently permitted to interact with this object.
        /// </summary>
        /// <param name="interactor">The GameObject initiating the interaction (typically the player).</param>
        /// <returns>True if the interaction can proceed; otherwise false.</returns>
        bool CanInteract(GameObject interactor);

        /// <summary>
        /// Contextual prompt text to be displayed on the player's HUD/reticle when targeting this object.
        /// (e.g., "Inspect Jade Relic", "Open Door", "Pick Up Jade Key").
        /// </summary>
        /// <returns>A localized or contextual prompt string.</returns>
        string GetPromptText();

        /// <summary>
        /// Executes the primary interaction logic when triggered by the interactor.
        /// </summary>
        /// <param name="interactor">The GameObject initiating the interaction (typically the player).</param>
        void Interact(GameObject interactor);
    }
}

