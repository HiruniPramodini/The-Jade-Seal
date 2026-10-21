using System;
using UnityEngine;
using UnityEngine.Events;

namespace JadeSeal.FinalBoss
{
    /// <summary>
    /// Presentation bridge between the Boss AI state machine and the Animator / Audio / VFX systems.
    /// Supports Animator parameters, Animation Events, and UnityEvents, while providing safe fallbacks
    /// if an Animator is unassigned or specific parameters don't exist.
    /// </summary>
    public sealed class FinalBossPresentation : MonoBehaviour
    {
        [Header("Animator Reference")]
        [SerializeField] private Animator animator;

        [Header("Animator Parameter Names")]
        [SerializeField] private string speedParam = "Speed";
        [SerializeField] private string isGuardingParam = "IsGuarding";
        [SerializeField] private string isDeadParam = "IsDead";
        [SerializeField] private string castBasicTrigger = "CastBasic";
        [SerializeField] private string castFireballTrigger = "CastFireball";
        [SerializeField] private string stompTrigger = "Stomp";
        [SerializeField] private string hitTrigger = "Hit";

        [Header("Unity Events for Audio / Effects")]
        public UnityEvent onCastBasicStarted = new UnityEvent();
        public UnityEvent onCastFireballStarted = new UnityEvent();
        public UnityEvent onStompWindupStarted = new UnityEvent();
        public UnityEvent onStompImpact = new UnityEvent();
        public UnityEvent onGuardStarted = new UnityEvent();
        public UnityEvent onGuardEnded = new UnityEvent();
        public UnityEvent onHitTaken = new UnityEvent();
        public UnityEvent onDeath = new UnityEvent();

        public event Action OnShootBasicEvent;
        public event Action OnLaunchFireballEvent;
        public event Action OnStompImpactEvent;
        public event Action OnAttackCompletedEvent;
        public event Action OnGuardCompletedEvent;

        private int speedHash;
        private int isGuardingHash;
        private int isDeadHash;
        private int castBasicHash;
        private int castFireballHash;
        private int stompHash;
        private int hitHash;

        private void Awake()
        {
            if (!animator) animator = GetComponentInChildren<Animator>();

            speedHash = Animator.StringToHash(speedParam);
            isGuardingHash = Animator.StringToHash(isGuardingParam);
            isDeadHash = Animator.StringToHash(isDeadParam);
            castBasicHash = Animator.StringToHash(castBasicTrigger);
            castFireballHash = Animator.StringToHash(castFireballTrigger);
            stompHash = Animator.StringToHash(stompTrigger);
            hitHash = Animator.StringToHash(hitTrigger);
        }

        public void SetSpeed(float speed)
        {
            if (animator && HasParameter(speedHash)) animator.SetFloat(speedHash, speed);
        }

        public void TriggerBasicCast()
        {
            if (animator && HasParameter(castBasicHash)) animator.SetTrigger(castBasicHash);
            onCastBasicStarted.Invoke();
        }

        public void TriggerFireballCast()
        {
            if (animator && HasParameter(castFireballHash)) animator.SetTrigger(castFireballHash);
            onCastFireballStarted.Invoke();
        }

        public void TriggerStomp()
        {
            if (animator && HasParameter(stompHash)) animator.SetTrigger(stompHash);
            onStompWindupStarted.Invoke();
        }

        public void SetGuarding(bool guarding)
        {
            if (animator && HasParameter(isGuardingHash)) animator.SetBool(isGuardingHash, guarding);
            if (guarding) onGuardStarted.Invoke(); else onGuardEnded.Invoke();
        }

        public void TriggerHit()
        {
            if (animator && HasParameter(hitHash)) animator.SetTrigger(hitHash);
            onHitTaken.Invoke();
        }

        public void TriggerDeath()
        {
            if (animator && HasParameter(isDeadHash)) animator.SetBool(isDeadHash, true);
            onDeath.Invoke();
        }

        private bool HasParameter(int hash)
        {
            if (!animator) return false;
            foreach (var param in animator.parameters)
            {
                if (param.nameHash == hash) return true;
            }
            return false;
        }

        // ========================================================
        // Animation Event Callbacks
        // (Can be placed on Animation clips in the Unity Editor)
        // ========================================================

        public void AnimEvent_ShootBasic()
        {
            OnShootBasicEvent?.Invoke();
        }

        public void AnimEvent_LaunchFireball()
        {
            OnLaunchFireballEvent?.Invoke();
        }

        public void AnimEvent_StompImpact()
        {
            onStompImpact.Invoke();
            OnStompImpactEvent?.Invoke();
        }

        public void AnimEvent_AttackComplete()
        {
            OnAttackCompletedEvent?.Invoke();
        }

        public void AnimEvent_GuardComplete()
        {
            OnGuardCompletedEvent?.Invoke();
        }
    }
}
