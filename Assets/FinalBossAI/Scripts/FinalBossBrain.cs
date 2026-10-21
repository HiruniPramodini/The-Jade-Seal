using System;
using UnityEngine;

namespace JadeSeal.FinalBoss
{
    /// <summary>
    /// Core State Machine for the Final Boss AI.
    /// Coordinates perception, navigation, combat, guarding, health, and animations.
    /// Manages states:
    /// - IdleCombatReady
    /// - TrackingPlayer
    /// - BasicMagicAttack
    /// - SpecialFireballAttack
    /// - StompWindup
    /// - StompImpact
    /// - Guarding
    /// - HitReaction
    /// - Death
    /// </summary>
    [RequireComponent(typeof(FinalBossHealth), typeof(FinalBossPerception), typeof(FinalBossCombat))]
    [RequireComponent(typeof(FinalBossNavigation), typeof(FinalBossGuard))]
    public sealed class FinalBossBrain : MonoBehaviour
    {
        [Header("Configuration")]
        public FinalBossSettings settings;

        [Header("Runtime State")]
        public BossState CurrentState { get; private set; } = BossState.IdleCombatReady;

        public string DebugStatus =>
            $"{CurrentState} | Dist={(perception != null ? perception.DistanceToPlayer.ToString("F1") : "-")}m | " +
            $"HP={(health != null ? health.CurrentHealth.ToString("F0") : "-")}/{(health != null ? health.MaxHealth.ToString("F0") : "-")} | " +
            $"Guard={(guard != null && guard.IsGuarding)} | BasicCD={combat?.BasicCooldownRemaining:F1}s | " +
            $"FireballCD={combat?.FireballCooldownRemaining:F1}s | StompCD={combat?.StompCooldownRemaining:F1}s";

        public event Action<BossState> StateChanged;

        private FinalBossHealth health;
        private FinalBossPerception perception;
        private FinalBossCombat combat;
        private FinalBossNavigation nav;
        private FinalBossGuard guard;
        private FinalBossPresentation presentation;

        private float stateEnteredTime;
        private float attackTimer;
        private float hitReactionEndTime;
        private float accumulatedDamageInWindow;
        private float lastDamageTime;
        private bool ownsSettingsInstance;

        private void Awake()
        {
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<FinalBossSettings>();
                ownsSettingsInstance = true;
            }

            health = GetComponent<FinalBossHealth>();
            perception = GetComponent<FinalBossPerception>();
            combat = GetComponent<FinalBossCombat>();
            nav = GetComponent<FinalBossNavigation>();
            guard = GetComponent<FinalBossGuard>();
            presentation = GetComponent<FinalBossPresentation>();

            health.BindSettings(settings);
            nav.ApplySettings(settings);

            // Health event bindings
            health.Damaged += OnDamaged;
            health.Died += OnDied;

            // Guard event bindings
            guard.GuardStarted += () => ChangeState(BossState.Guarding);
            guard.GuardEnded += () =>
            {
                if (CurrentState == BossState.Guarding) ChangeState(BossState.TrackingPlayer);
            };

            // Presentation animation event bindings
            if (presentation != null)
            {
                presentation.OnShootBasicEvent += HandleShootBasicAnimEvent;
                presentation.OnLaunchFireballEvent += HandleLaunchFireballAnimEvent;
                presentation.OnStompImpactEvent += HandleStompImpactAnimEvent;
                presentation.OnAttackCompletedEvent += HandleAttackCompletedAnimEvent;
                presentation.OnGuardCompletedEvent += () => guard.EndGuard();
            }
        }

        private void OnDestroy()
        {
            if (health != null)
            {
                health.Damaged -= OnDamaged;
                health.Died -= OnDied;
            }

            if (ownsSettingsInstance && settings != null)
            {
                Destroy(settings);
            }
        }

        private void Update()
        {
            if (health.IsDead || CurrentState == BossState.Death)
            {
                return;
            }

            perception.UpdatePerception(settings);

            // Execute active state
            switch (CurrentState)
            {
                case BossState.IdleCombatReady:
                    ExecuteIdleState();
                    break;

                case BossState.TrackingPlayer:
                    ExecuteTrackingState();
                    break;

                case BossState.BasicMagicAttack:
                    ExecuteBasicMagicState();
                    break;

                case BossState.SpecialFireballAttack:
                    ExecuteFireballState();
                    break;

                case BossState.StompWindup:
                    ExecuteStompWindupState();
                    break;

                case BossState.StompImpact:
                    ExecuteStompImpactState();
                    break;

                case BossState.Guarding:
                    ExecuteGuardingState();
                    break;

                case BossState.HitReaction:
                    ExecuteHitReactionState();
                    break;
            }

            // Sync navigation speed to presentation
            if (presentation != null)
            {
                presentation.SetSpeed(nav.CurrentSpeed);
            }
        }

        private void ChangeState(BossState newState)
        {
            if (CurrentState == newState || CurrentState == BossState.Death) return;

            CurrentState = newState;
            stateEnteredTime = Time.time;
            attackTimer = 0f;

            if (presentation != null)
            {
                presentation.SetGuarding(CurrentState == BossState.Guarding);
            }

            StateChanged?.Invoke(newState);
        }

        private void ExecuteIdleState()
        {
            nav.Stop();

            if (perception.HasValidTarget)
            {
                ChangeState(BossState.TrackingPlayer);
            }
        }

        private void ExecuteTrackingState()
        {
            if (!perception.HasValidTarget)
            {
                ChangeState(BossState.IdleCombatReady);
                return;
            }

            // Continuous rotation toward player inside active arena
            nav.RotateTowards(perception.AimPosition, settings.turnSpeed);

            float distance = perception.DistanceToPlayer;

            // 1. Attack Selection Priority
            // Melee stomp if player is within melee radius
            if (distance <= settings.meleeRange && combat.CanStomp)
            {
                StartStompAttack();
                return;
            }

            // Special Giant Fireball if eligible
            if (distance <= settings.combatRange && combat.CanCastFireball)
            {
                StartFireballAttack();
                return;
            }

            // Basic Magic Projectile if eligible
            if (distance <= settings.combatRange && combat.CanCastBasic)
            {
                StartBasicMagicAttack();
                return;
            }

            // 2. Tactical Movement & Spacing
            if (distance > settings.maxPreferredDistance)
            {
                // Move towards player
                nav.MoveTo(perception.LastKnownPosition, settings);
            }
            else if (distance < settings.minPreferredDistance && !combat.CanStomp)
            {
                // Reposition away slightly to maintain ranged casting distance
                Vector3 awayDir = (transform.position - perception.LastKnownPosition).normalized;
                awayDir.y = 0f;
                nav.MoveTo(transform.position + awayDir * 3f, settings);
            }
            else
            {
                nav.Stop();
            }
        }

        // ========================================================
        // Attack Execution & Coroutine/Timer Fail-safes
        // ========================================================

        private void StartBasicMagicAttack()
        {
            nav.Stop();
            ChangeState(BossState.BasicMagicAttack);
            if (presentation != null) presentation.TriggerBasicCast();
        }

        private void ExecuteBasicMagicState()
        {
            attackTimer += Time.deltaTime;
            nav.RotateTowards(perception.AimPosition, settings.turnSpeed * 1.5f);

            // Fail-safe casting if animation events are not wired
            if (attackTimer >= settings.basicCastDelay && attackTimer - Time.deltaTime < settings.basicCastDelay)
            {
                combat.FireBasicMagic(perception.AimPosition, settings);
            }

            // Attack recovery complete
            if (attackTimer >= settings.basicCastDelay + 0.5f)
            {
                ChangeState(BossState.TrackingPlayer);
            }
        }

        private void HandleShootBasicAnimEvent()
        {
            if (CurrentState == BossState.BasicMagicAttack)
            {
                combat.FireBasicMagic(perception.AimPosition, settings);
            }
        }

        private void StartFireballAttack()
        {
            nav.Stop();
            ChangeState(BossState.SpecialFireballAttack);
            if (presentation != null) presentation.TriggerFireballCast();
        }

        private void ExecuteFireballState()
        {
            attackTimer += Time.deltaTime;
            nav.RotateTowards(perception.AimPosition, settings.turnSpeed);

            // Fail-safe fireball launch
            if (attackTimer >= settings.fireballCastDelay && attackTimer - Time.deltaTime < settings.fireballCastDelay)
            {
                combat.LaunchFireball(perception.AimPosition, settings);
            }

            if (attackTimer >= settings.fireballCastDelay + 0.8f)
            {
                ChangeState(BossState.TrackingPlayer);
            }
        }

        private void HandleLaunchFireballAnimEvent()
        {
            if (CurrentState == BossState.SpecialFireballAttack)
            {
                combat.LaunchFireball(perception.AimPosition, settings);
            }
        }

        private void StartStompAttack()
        {
            nav.Stop();
            ChangeState(BossState.StompWindup);
            if (presentation != null) presentation.TriggerStomp();
        }

        private void ExecuteStompWindupState()
        {
            attackTimer += Time.deltaTime;

            // At moment of stomp impact
            if (attackTimer >= settings.stompWindupDuration)
            {
                ChangeState(BossState.StompImpact);
                combat.TriggerStompShockwave(settings);
            }
        }

        private void HandleStompImpactAnimEvent()
        {
            if (CurrentState == BossState.StompWindup)
            {
                ChangeState(BossState.StompImpact);
                combat.TriggerStompShockwave(settings);
            }
        }

        private void ExecuteStompImpactState()
        {
            attackTimer += Time.deltaTime;
            nav.Stop();

            if (attackTimer >= settings.stompRecoveryDuration)
            {
                ChangeState(BossState.TrackingPlayer);
            }
        }

        private void HandleAttackCompletedAnimEvent()
        {
            if (CurrentState == BossState.BasicMagicAttack ||
                CurrentState == BossState.SpecialFireballAttack ||
                CurrentState == BossState.StompImpact)
            {
                ChangeState(BossState.TrackingPlayer);
            }
        }

        private void ExecuteGuardingState()
        {
            nav.Stop();
            nav.RotateTowards(perception.AimPosition, settings.turnSpeed * 0.5f);

            // Guard state exit is handled by FinalBossGuard component / duration
        }

        private void ExecuteHitReactionState()
        {
            nav.Stop();
            if (Time.time >= hitReactionEndTime)
            {
                ChangeState(BossState.TrackingPlayer);
            }
        }

        // ========================================================
        // Hit & Death Handling
        // ========================================================

        private void OnDamaged(float originalDamage, float actualDamage, bool wasGuarded)
        {
            if (health.IsDead) return;

            lastDamageTime = Time.time;

            // 1. Tactical Auto-guard check
            if (!wasGuarded && guard.RecordHitAndCheckAutoGuard(settings))
            {
                return; // Entered guard!
            }

            // 2. Heavy hit flinch reaction check (only if not currently performing special attacks or guarding)
            accumulatedDamageInWindow += actualDamage;
            if (accumulatedDamageInWindow >= settings.hitReactionThreshold &&
                CurrentState != BossState.Guarding &&
                CurrentState != BossState.StompWindup &&
                CurrentState != BossState.SpecialFireballAttack)
            {
                accumulatedDamageInWindow = 0f;
                hitReactionEndTime = Time.time + 0.45f;
                ChangeState(BossState.HitReaction);
                if (presentation != null) presentation.TriggerHit();
            }
        }

        private void OnDied()
        {
            nav.Stop();
            guard.EndGuard();
            ChangeState(BossState.Death);

            if (presentation != null)
            {
                presentation.TriggerDeath();
            }

            enabled = false;
        }

        public void TriggerManualAttack(BossAttackType attackType)
        {
            switch (attackType)
            {
                case BossAttackType.BasicMagic:
                    StartBasicMagicAttack();
                    break;
                case BossAttackType.Fireball:
                    StartFireballAttack();
                    break;
                case BossAttackType.StompShockwave:
                    StartStompAttack();
                    break;
            }
        }

        public void ToggleManualGuard()
        {
            if (guard.IsGuarding)
            {
                guard.EndGuard();
            }
            else
            {
                guard.StartGuard(settings);
            }
        }
    }
}
