using UnityEngine;
using JadeSeal.Interaction;

namespace JadeSeal.Player
{
    /// <summary>
    /// Modular CharacterController push component.
    /// Detects physical collisions between the player and <see cref="PushableBarricade"/> objects,
    /// transferring appropriate horizontal pushing forces via <see cref="OnControllerColliderHit"/>.
    ///
    /// TEAMMATE INTEGRATION GUIDE:
    /// When the final teammate Player Rig / Prefab is imported, attach this component
    /// directly to the root Player GameObject (alongside the CharacterController).
    /// It automatically detects pushable crates and stone barricades without requiring
    /// any custom movement code modifications.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public sealed class CharacterControllerPush : MonoBehaviour
    {
#pragma warning disable CS0649
        [Header("Push Settings")]
        [Tooltip("Force magnitude applied to pushable barricades upon contact.")]
        [SerializeField] private float pushStrength = 400.0f;

        [Tooltip("Minimum horizontal character velocity required before push force is applied.")]
        [SerializeField] private float minPushVelocity = 0.1f;
#pragma warning restore CS0649

        private CharacterController _characterController;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            // Ignore collisions underneath the player's feet (e.g. ground, stairs, stepping on objects)
            if (hit.moveDirection.y < -0.3f)
            {
                return;
            }

            // Only push objects explicitly marked with the PushableBarricade component
            var pushable = hit.collider.GetComponentInParent<PushableBarricade>();
            if (pushable == null || !pushable.CanBePushed)
            {
                return;
            }

            // Verify the character has sufficient horizontal movement towards the object
            Vector3 horizontalMove = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z);
            if (horizontalMove.sqrMagnitude < minPushVelocity * minPushVelocity)
            {
                return;
            }

            // Transfer force to the barricade
            pushable.ApplyPush(hit.moveDirection, hit.point, pushStrength);
        }
    }
}

