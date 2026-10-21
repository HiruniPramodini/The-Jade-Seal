using System;
using UnityEngine;
using UnityEngine.Events;

namespace JadeSeal.MountainArcher
{
    public sealed class MountainArcherPresentation : MonoBehaviour
    {
        [Serializable] public sealed class StateEvent : UnityEvent<MountainArcherUtility.State> { }
        [Serializable] public sealed class FloatEvent : UnityEvent<float> { }

        public StateEvent onStateChanged;
        public FloatEvent onDamaged;
        public UnityEvent onDied;

        private MountainArcherBrain brain;
        private MountainArcherHealth health;

        private void Awake()
        {
            brain = GetComponent<MountainArcherBrain>();
            health = GetComponent<MountainArcherHealth>();

            if (brain) brain.StateChanged += HandleState;
            if (health)
            {
                health.Damaged += HandleDamaged;
                health.Died += HandleDied;
            }
        }

        private void OnDestroy()
        {
            if (brain) brain.StateChanged -= HandleState;
            if (health)
            {
                health.Damaged -= HandleDamaged;
                health.Died -= HandleDied;
            }
        }

        private void HandleState(MountainArcherUtility.State state) => onStateChanged?.Invoke(state);
        private void HandleDamaged(float d) => onDamaged?.Invoke(d);
        private void HandleDied() => onDied?.Invoke();
    }
}
