using System;
using UnityEngine;

namespace JadeSeal.BambooHunter
{
    /// <summary>
    /// Autonomous Agent Coordinator for the Bamboo Hunter AI.
    /// Unifies the continuous Utility AI scoring engine with a discrete Finite State Machine (FSM).
    /// Orchestrates perception gathering, rate-limited utility arbitration with hysteresis,
    /// and deterministic state execution across patrol, investigate, aim, shoot, reposition, retreat, and dead.
    /// </summary>
    [AddComponentMenu("Jade Seal/Bamboo Hunter/Bamboo Hunter Brain")]
    public sealed class BambooHunterBrain : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private BambooHunterSettings settings;

        [Header("Subsystem References")]
        [SerializeField] private BambooHunterPerception perception;
        [SerializeField] private BambooHunterNavigation navigation;
        [SerializeField] private BambooHunterCoverSystem coverSystem;
        [SerializeField] private BambooHunterBow bow;
        [SerializeField] private BambooHunterHealth health;

        [Header("Runtime Tactical State (Read Only)")]
        [SerializeField] private BambooHunterState currentState = BambooHunterState.Patrol;
        [SerializeField] private BambooHunterTacticalIntent currentIntent = BambooHunterTacticalIntent.PatrolGrove;
        [SerializeField] private float stateElapsedTime = 0f;
        [SerializeField] private bool hasTargetVisibility;
        [SerializeField] private float targetDistance;
        [SerializeField] private Vector3 currentDestination;
        [SerializeField] private BambooHunterUtilityScores utilityScores;
        [SerializeField] private float thinkTimer = 0f;

        private BambooHunterDecisionContext lastContext;
        private bool isInitialized = false;

        #region Events

        /// <summary>Fired on every state transition. Parameters: previousState, newState.</summary>
        public event Action<BambooHunterState, BambooHunterState> OnStateChanged;

        /// <summary>Fired on every utility evaluation cycle. Parameters: context, scores.</summary>
        public event Action<BambooHunterDecisionContext, BambooHunterUtilityScores> OnDecisionCycle;

        #endregion

        #region Public Properties

        /// <summary>Current active FSM execution state.</summary>
        public BambooHunterState CurrentState => currentState;

        /// <summary>High-level tactical intent corresponding to current state.</summary>
        public BambooHunterTacticalIntent CurrentIntent => currentIntent;

        /// <summary>Time in seconds spent continuously in the current state.</summary>
        public float StateElapsedTime => stateElapsedTime;

        /// <summary>Most recently calculated normalized utility scores for all candidate states.</summary>
        public BambooHunterUtilityScores UtilityScores => utilityScores;

        /// <summary>Most recent sensory snapshot evaluated by the decision engine.</summary>
        public BambooHunterDecisionContext LastDecisionContext => lastContext;

        /// <summary>Configured ScriptableObject settings.</summary>
        public BambooHunterSettings Settings
        {
            get => settings;
            set => settings = value;
        }

        public BambooHunterPerception Perception => perception;
        public BambooHunterNavigation Navigation => navigation;
        public BambooHunterCoverSystem CoverSystem => coverSystem;
        public BambooHunterBow Bow => bow;
        public BambooHunterHealth Health => health;

        #endregion

        private void Awake()
        {
            EnsureComponents();
        }

        private void Start()
        {
            if (!isInitialized)
            {
                Initialize(settings);
            }
        }

        private void EnsureComponents()
        {
            if (perception == null) perception = GetComponent<BambooHunterPerception>();
            if (navigation == null) navigation = GetComponent<BambooHunterNavigation>();
            if (coverSystem == null) coverSystem = GetComponent<BambooHunterCoverSystem>();
            if (bow == null) bow = GetComponent<BambooHunterBow>();
            if (health == null) health = GetComponent<BambooHunterHealth>();
        }

        /// <summary>
        /// Explicit initialization helper for runtime and unit test setups.
        /// </summary>
        public void Initialize(
            BambooHunterSettings config,
            BambooHunterPerception p = null,
            BambooHunterNavigation n = null,
            BambooHunterCoverSystem cs = null,
            BambooHunterBow b = null,
            BambooHunterHealth h = null)
        {
            settings = config;
            if (p != null) perception = p;
            if (n != null) navigation = n;
            if (cs != null) coverSystem = cs;
            if (b != null) bow = b;
            if (h != null) health = h;

            EnsureComponents();

            // Wire death notification for immediate safety override
            if (health != null)
            {
                health.OnDied -= HandleHealthDied;
                health.OnDied += HandleHealthDied;
            }

            stateElapsedTime = 0f;
            thinkTimer = 0f;
            isInitialized = true;

            TransitionTo(BambooHunterState.Patrol, force: true);
        }

        private void OnDestroy()
        {
            if (health != null)
            {
                health.OnDied -= HandleHealthDied;
            }
        }

        private void HandleHealthDied()
        {
            TransitionTo(BambooHunterState.Dead, force: true);
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>
        /// Deterministic execution tick advancing state execution, timing, and rate-limited decision cycles.
        /// Can be driven by Update() or called directly by unit tests.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime <= 0f) return;

            // Terminal death state halts all cognitive loops
            if (currentState == BambooHunterState.Dead)
            {
                return;
            }

            stateElapsedTime += deltaTime;

            // Update active state continuous execution
            ExecuteStateUpdate(deltaTime);

            // Rate-limited utility decision cycle
            thinkTimer += deltaTime;
            float interval = settings ? settings.thinkInterval : 0.20f;
            if (thinkTimer >= interval)
            {
                thinkTimer = 0f;
                RunDecisionCycle();
            }

            // Update debug inspection fields
            if (perception != null)
            {
                hasTargetVisibility = perception.CanSeeTarget;
                targetDistance = perception.TargetDistance;
            }
            if (navigation != null)
            {
                currentDestination = navigation.CurrentDestination;
            }
        }

        /// <summary>
        /// Constructs the sensory context snapshot, evaluates candidate utilities,
        /// and applies hysteresis arbitration to select the optimal state.
        /// </summary>
        public void RunDecisionCycle()
        {
            if (currentState == BambooHunterState.Dead) return;

            // 0. Refresh perception sampling
            if (perception != null)
            {
                perception.Sample(settings);
            }

            // 1. Gather sensory context snapshot from all subsystems
            lastContext = BuildContext();

            // 2. Compute normalized utility scores for all candidate states
            utilityScores = BambooHunterUtility.EvaluateAll(lastContext, settings);

            // 3. Arbitrate with hysteresis, dwell time, and invariant safety overrides
            BambooHunterState nextState = BambooHunterUtility.Choose(lastContext, currentState, stateElapsedTime, settings);

            OnDecisionCycle?.Invoke(lastContext, utilityScores);

            // 4. Transition if an alternate state has dethroned the active state
            if (nextState != currentState)
            {
                TransitionTo(nextState);
            }
        }

        /// <summary>
        /// Compiles live outputs from all active subsystems into a unified decision context.
        /// </summary>
        public BambooHunterDecisionContext BuildContext()
        {
            var context = new BambooHunterDecisionContext();

            if (health != null)
            {
                health.PopulateDecisionContext(ref context);
            }

            if (perception != null)
            {
                perception.PopulateDecisionContext(ref context, settings);
            }

            if (bow != null)
            {
                bow.PopulateDecisionContext(ref context);
            }

            if (coverSystem != null)
            {
                coverSystem.PopulateDecisionContext(ref context);
            }

            context.Sanitize();
            return context;
        }

        /// <summary>
        /// Switches active state, executing exit logic on the outgoing state and entry logic on the incoming state.
        /// </summary>
        public void TransitionTo(BambooHunterState nextState, bool force = false)
        {
            if (!force && nextState == currentState) return;

            // Invariant: Once dead, cannot transition to any other state
            if (currentState == BambooHunterState.Dead && nextState != BambooHunterState.Dead) return;

            ExitState(currentState, nextState);

            BambooHunterState previousState = currentState;
            currentState = nextState;
            currentIntent = MapStateToIntent(nextState);
            stateElapsedTime = 0f;

            EnterState(nextState, previousState);

            OnStateChanged?.Invoke(previousState, nextState);
        }

        #region FSM State Execution (Entry / Update / Exit)

        private void EnterState(BambooHunterState state, BambooHunterState fromState)
        {
            switch (state)
            {
                case BambooHunterState.Patrol:
                    if (bow != null) bow.CancelDraw();
                    if (coverSystem != null) coverSystem.ReleaseCover();
                    if (navigation != null) navigation.MoveToPatrol(settings);
                    break;

                case BambooHunterState.Investigate:
                    if (bow != null) bow.CancelDraw();
                    if (coverSystem != null) coverSystem.ReleaseCover();
                    Vector3 lastSeen = perception != null ? perception.LastKnownPosition : transform.position;
                    if (navigation != null) navigation.MoveToInvestigate(lastSeen, settings);
                    break;

                case BambooHunterState.Aim:
                    if (navigation != null) navigation.StopForAim();
                    Transform target = perception != null ? perception.Target : null;
                    if (bow != null && target != null)
                    {
                        bow.BeginDraw(target);
                    }
                    break;

                case BambooHunterState.Shoot:
                    // Instantaneous arrow release
                    Transform shootTarget = perception != null ? perception.Target : null;
                    if (bow != null && shootTarget != null)
                    {
                        bow.TryShoot(shootTarget);
                    }
                    break;

                case BambooHunterState.Reposition:
                    if (bow != null) bow.CancelDraw();
                    if (coverSystem != null && coverSystem.EvaluateBestCover(navigation, perception, settings, defensiveOnly: false))
                    {
                        if (navigation != null && coverSystem.SelectedCover != null)
                        {
                            navigation.MoveToReposition(coverSystem.SelectedCover.HidePosition, settings);
                        }
                    }
                    else if (navigation != null)
                    {
                        // Fallback when no cover is available:
                        // If a threat is known, flank or backpedal away from threat rather than advancing into it!
                        if (perception != null && (perception.CanSeeTarget || perception.HasTargetMemory(settings ? settings.memoryDuration : 5f)))
                        {
                            Vector3 threatPos = perception.CanSeeTarget ? perception.TargetAimPosition : perception.LastKnownPosition;
                            Vector3 awayDir = (transform.position - threatPos);
                            awayDir.y = 0f;
                            if (awayDir.sqrMagnitude < 0.001f)
                            {
                                awayDir = -transform.forward;
                            }
                            else
                            {
                                awayDir.Normalize();
                            }

                            float safeDistance = settings ? settings.preferredMinRange : 7.0f;
                            Vector3 repositionPos = transform.position + awayDir * safeDistance;
                            navigation.MoveToReposition(repositionPos, settings);
                        }
                        else
                        {
                            // Unalerted fallback: continue patrol
                            navigation.MoveToPatrol(settings);
                        }
                    }
                    break;

                case BambooHunterState.Retreat:
                    if (bow != null) bow.CancelDraw();
                    if (coverSystem != null && coverSystem.EvaluateBestCover(navigation, perception, settings, defensiveOnly: true))
                    {
                        if (navigation != null && coverSystem.SelectedCover != null)
                        {
                            navigation.MoveToRetreat(coverSystem.SelectedCover.HidePosition, settings);
                        }
                    }
                    else if (navigation != null)
                    {
                        // Fallback: retreat directly away from threat
                        Vector3 retreatThreatPos = (perception != null && perception.CanSeeTarget)
                            ? perception.TargetAimPosition
                            : (perception != null ? perception.LastKnownPosition : transform.position);
                        Vector3 fleeDir = (transform.position - retreatThreatPos).normalized;
                        if (fleeDir.sqrMagnitude < 0.001f) fleeDir = -transform.forward;
                        navigation.MoveToRetreat(transform.position + fleeDir * 12f, settings);
                    }
                    break;

                case BambooHunterState.Dead:
                    if (bow != null) bow.CancelDraw();
                    if (navigation != null) navigation.StopForDeath();
                    if (coverSystem != null) coverSystem.ReleaseCover();
                    break;
            }
        }

        private void ExecuteStateUpdate(float deltaTime)
        {
            switch (currentState)
            {
                case BambooHunterState.Patrol:
                    // Waypoint patrol progression handled by navigation component
                    break;

                case BambooHunterState.Investigate:
                    // Orient toward investigation target position to maintain sensory FOV coverage
                    if (perception != null && perception.HasTargetMemory(settings ? settings.memoryDuration : 5f))
                    {
                        Vector3 lookDir = perception.LastKnownPosition - transform.position;
                        lookDir.y = 0f;
                        if (lookDir.sqrMagnitude > 0.001f)
                        {
                            Quaternion targetRot = Quaternion.LookRotation(lookDir.normalized);
                            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, deltaTime * 5.0f);
                        }
                    }
                    break;

                case BambooHunterState.Aim:
                    // Smooth tracking rotation toward target
                    if (perception != null && perception.Target != null)
                    {
                        Vector3 targetAim = perception.TargetAimPosition;
                        Vector3 dir = targetAim - transform.position;
                        dir.y = 0f;
                        if (dir.sqrMagnitude > 0.001f)
                        {
                            Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
                            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, deltaTime * 8.0f);
                        }
                    }

                    // Advance bow tension
                    if (bow != null)
                    {
                        bow.UpdateDraw(deltaTime);
                    }
                    break;

                case BambooHunterState.Shoot:
                    // Shoot released upon entry; subsequent tick immediately arbitrates next state
                    break;

                case BambooHunterState.Reposition:
                    // Check arrival at cover
                    if (coverSystem != null && coverSystem.IsAtCover)
                    {
                        if (bow != null) bow.ResetConsecutiveShots();

                        // Concealed health recovery if wounded
                        bool isConcealed = coverSystem.SelectedCover != null && coverSystem.SelectedCover.IsVegetationCover;
                        if (health != null)
                        {
                            health.UpdateRegeneration(deltaTime, isConcealed);
                        }
                    }

                    // Handle path failure or stuck navigation
                    if (navigation != null && navigation.IsStuck && coverSystem != null)
                    {
                        if (coverSystem.SelectedCover != null)
                        {
                            coverSystem.RejectCover(coverSystem.SelectedCover);
                        }
                    }
                    break;

                case BambooHunterState.Retreat:
                    // Concealed health regeneration while in safe retreat
                    if (coverSystem != null && coverSystem.IsAtCover)
                    {
                        bool isConcealed = coverSystem.SelectedCover != null && coverSystem.SelectedCover.IsVegetationCover;
                        if (health != null)
                        {
                            health.UpdateRegeneration(deltaTime, isConcealed);
                        }
                    }
                    break;

                case BambooHunterState.Dead:
                    // Terminal
                    break;
            }
        }

        private void ExitState(BambooHunterState state, BambooHunterState nextState)
        {
            switch (state)
            {
                case BambooHunterState.Aim:
                    // Cancel draw if transitioning anywhere other than Shoot
                    if (nextState != BambooHunterState.Shoot && bow != null)
                    {
                        bow.CancelDraw();
                    }
                    break;

                default:
                    break;
            }
        }

        #endregion

        /// <summary>
        /// Maps an operational FSM state to its corresponding high-level tactical intent.
        /// </summary>
        public static BambooHunterTacticalIntent MapStateToIntent(BambooHunterState state)
        {
            switch (state)
            {
                case BambooHunterState.Patrol: return BambooHunterTacticalIntent.PatrolGrove;
                case BambooHunterState.Investigate: return BambooHunterTacticalIntent.InvestigateLastSeen;
                case BambooHunterState.Aim: return BambooHunterTacticalIntent.PrepareShot;
                case BambooHunterState.Shoot: return BambooHunterTacticalIntent.ReleaseArrow;
                case BambooHunterState.Reposition: return BambooHunterTacticalIntent.RelocateToVegetationCover;
                case BambooHunterState.Retreat: return BambooHunterTacticalIntent.EmergencyRetreat;
                case BambooHunterState.Dead: return BambooHunterTacticalIntent.CeaseActions;
                default: return BambooHunterTacticalIntent.PatrolGrove;
            }
        }
    }
}
