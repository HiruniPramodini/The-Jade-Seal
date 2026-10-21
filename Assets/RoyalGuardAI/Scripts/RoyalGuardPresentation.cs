using UnityEngine;
using UnityEngine.Events;
namespace JadeSeal.RoyalGuard
{
    // Optional presentation bridge. Animator parameters are configured by the model integrator.
    [RequireComponent(typeof(RoyalGuardHealth), typeof(RoyalGuardBrain))]
    public sealed class RoyalGuardPresentation : MonoBehaviour
    {
        public UnityEvent hit = new UnityEvent();
        public UnityEvent death = new UnityEvent();
        public UnityEvent<int> stateChanged = new UnityEvent<int>();
        RoyalGuardHealth health;
        RoyalGuardBrain brain;
        void Awake() { health = GetComponent<RoyalGuardHealth>(); brain = GetComponent<RoyalGuardBrain>(); }
        void OnEnable() { health.Damaged += OnHit; health.Died += OnDeath; brain.StateChanged += OnState; }
        void OnDisable() { health.Damaged -= OnHit; health.Died -= OnDeath; brain.StateChanged -= OnState; }
        void OnHit(float damage) => hit.Invoke();
        void OnDeath() => death.Invoke();
        void OnState(RoyalGuardUtility.State state) => stateChanged.Invoke((int)state);
    }
}
