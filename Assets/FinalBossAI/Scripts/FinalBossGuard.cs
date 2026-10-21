using System;
using System.Collections.Generic;
using UnityEngine;

namespace JadeSeal.FinalBoss
{
    /// <summary>
    /// Controls the Boss's defensive guarding state.
    /// Reduces incoming damage, tracks guard cooldowns, handles guard hit accumulation,
    /// and activates defensive visual barriers.
    /// </summary>
    public sealed class FinalBossGuard : MonoBehaviour
    {
        [Header("Defense Visuals")]
        [Tooltip("Optional magical shield or barrier visual that is toggled during guard.")]
        [SerializeField] private GameObject guardBarrierVisual;

        private FinalBossHealth health;
        private float guardEndTime;
        private float nextGuardAvailableTime;
        private readonly List<float> recentHitTimestamps = new List<float>();

        public bool IsGuarding { get; private set; }
        public float GuardCooldownRemaining => Mathf.Max(0f, nextGuardAvailableTime - Time.time);

        public event Action GuardStarted;
        public event Action GuardEnded;

        private void Awake()
        {
            health = GetComponent<FinalBossHealth>();
            if (guardBarrierVisual != null) guardBarrierVisual.SetActive(false);
        }

        private void Update()
        {
            if (IsGuarding && Time.time >= guardEndTime)
            {
                EndGuard();
            }
        }

        public bool CanGuard(FinalBossSettings settings)
        {
            if (IsGuarding) return false;
            if (health != null && health.IsDead) return false;
            return Time.time >= nextGuardAvailableTime;
        }

        public bool StartGuard(FinalBossSettings settings)
        {
            if (!CanGuard(settings)) return false;

            IsGuarding = true;
            float duration = settings != null ? settings.guardDuration : 2.5f;
            float cooldown = settings != null ? settings.guardCooldown : 8f;

            guardEndTime = Time.time + duration;
            nextGuardAvailableTime = Time.time + cooldown;

            if (health != null) health.IsGuarding = true;
            if (guardBarrierVisual != null) guardBarrierVisual.SetActive(true);

            GuardStarted?.Invoke();
            return true;
        }

        public void EndGuard()
        {
            if (!IsGuarding) return;

            IsGuarding = false;
            if (health != null) health.IsGuarding = false;
            if (guardBarrierVisual != null) guardBarrierVisual.SetActive(false);

            GuardEnded?.Invoke();
        }

        /// <summary>
        /// Registers an incoming hit to evaluate whether to trigger tactical defensive guarding.
        /// </summary>
        public bool RecordHitAndCheckAutoGuard(FinalBossSettings settings)
        {
            if (IsGuarding || !CanGuard(settings)) return false;

            float window = settings != null ? settings.guardAccumulationWindow : 3f;
            int threshold = settings != null ? settings.guardHitTriggerCount : 3;

            recentHitTimestamps.Add(Time.time);
            recentHitTimestamps.RemoveAll(t => Time.time - t > window);

            if (recentHitTimestamps.Count >= threshold)
            {
                recentHitTimestamps.Clear();
                return StartGuard(settings);
            }

            return false;
        }

        public void ResetGuard()
        {
            EndGuard();
            nextGuardAvailableTime = 0f;
            recentHitTimestamps.Clear();
        }
    }
}
