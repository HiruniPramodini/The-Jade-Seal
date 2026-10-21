using UnityEngine;

namespace JadeSeal.Interaction
{
    /// <summary>
    /// Test interactable object for verifying player interaction physics and input.
    /// Cycles through distinct colors using an instanced material when E is pressed,
    /// ensuring no shared project materials are modified.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class SimpleTestInteractable : MonoBehaviour, IInteractable
    {
        [Header("Interaction Settings")]
        [Tooltip("Contextual prompt text displayed when targeted.")]
        [SerializeField] private string promptActionText = "Inspect Ancient Jade Relic";

        [Tooltip("Whether this object is currently interactable.")]
        [SerializeField] private bool isInteractable = true;

        [Header("Visual Feedback")]
        [Tooltip("Palette of colors cycled upon interaction.")]
        [SerializeField] private Color[] colorPalette = new Color[]
        {
            new Color(0.18f, 0.80f, 0.44f), // Jade Green
            new Color(0.95f, 0.77f, 0.06f), // Imperial Gold
            new Color(0.91f, 0.30f, 0.24f), // Vermilion Red
            new Color(0.20f, 0.60f, 0.86f), // Celadon Cyan
            new Color(0.61f, 0.35f, 0.71f)  // Amethyst Purple
        };

        private Material _instancedMaterial;
        private Renderer _renderer;
        private int _currentColorIndex = 0;
        private int _interactionCount = 0;

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            if (_renderer != null)
            {
                // Accessing .material automatically instantiates a local copy unique to this GameObject,
                // preserving the shared project asset and preventing dirtying version control.
                _instancedMaterial = _renderer.material;
                if (colorPalette != null && colorPalette.Length > 0)
                {
                    _instancedMaterial.color = colorPalette[0];
                }
            }
        }

        private void OnDestroy()
        {
            // Clean up the dynamically instanced material to prevent memory leaks in Play Mode
            if (_instancedMaterial != null)
            {
                Destroy(_instancedMaterial);
                _instancedMaterial = null;
            }
        }

        public bool CanInteract(GameObject interactor)
        {
            return isInteractable && enabled;
        }

        public string GetPromptText()
        {
            return promptActionText;
        }

        public void Interact(GameObject interactor)
        {
            _interactionCount++;

            if (_renderer != null && _instancedMaterial != null && colorPalette != null && colorPalette.Length > 0)
            {
                _currentColorIndex = (_currentColorIndex + 1) % colorPalette.Length;
                Color newColor = colorPalette[_currentColorIndex];
                _instancedMaterial.color = newColor;

                Debug.Log($"<color=#2ecc71>[JadeSeal Interaction]</color> '{name}' successfully interacted by '{interactor.name}' " +
                          $"(Count: {_interactionCount}, New Color: #{ColorUtility.ToHtmlStringRGB(newColor)}).");
            }
            else
            {
                Debug.Log($"<color=#2ecc71>[JadeSeal Interaction]</color> '{name}' interacted by '{interactor.name}' (Count: {_interactionCount}).");
            }
        }

        /// <summary>Allows runtime toggling of interactable availability.</summary>
        public void SetInteractable(bool state)
        {
            isInteractable = state;
        }
    }
}

