using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace JadeSeal.BambooHunter
{
    /// <summary>
    /// Automated runtime verification harness for the Bamboo Hunter AI in Unity Play Mode.
    /// Orchestrates a comprehensive 7-phase scenario validating genuine physical and sensory behaviors:
    /// 1. Peaceful Waypoint Patrol
    /// 2. Target Acquisition & Aim (FOV / Range detection)
    /// 3. Physical Ballistic Arrow Launch & Damage Registration on Player Dummy
    /// 4. Post-Shot Repositioning behind Vegetation Cover
    /// 5. Line-of-Sight Loss & Investigation of Last-Known Position
    /// 6. Low-Health Defensive Retreat
    /// 7. Terminal Death State Invariant Enforcement
    ///
    /// Logs all verification results to Assets/BambooHunterAI/Evidence/RuntimePlayModeLog.txt.
    /// </summary>
    [AddComponentMenu("Jade Seal/Bamboo Hunter/Runtime Harness")]
    [DisallowMultipleComponent]
    public sealed class BambooHunterRuntimeHarness : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private BambooHunterBrain hunterBrain;
        [SerializeField] private BambooHunterPlayerHealthDemo playerDummy;
        [SerializeField] private BambooHunterCoverSystem coverSystem;

        [Header("Execution Mode")]
        [Tooltip("If true, automatically commences the 7-phase verification sequence on Start().")]
        [SerializeField] private bool autoRunOnStart = false;

        [Tooltip("Maximum total seconds allowed for the automated verification suite before timeout.")]
        [SerializeField] private float scenarioTimeout = 25.0f;

        [Header("Verification Results (Read Only)")]
        [SerializeField] private bool isRunning = false;
        [SerializeField] private bool isComplete = false;
        [SerializeField] private int passedPhases = 0;
        [SerializeField] private int totalPhases = 7;
        [SerializeField] private string currentPhaseName = "Idle";

        private readonly System.Text.StringBuilder logBuilder = new System.Text.StringBuilder();

        public bool IsRunning => isRunning;
        public bool IsComplete => isComplete;
        public int PassedPhases => passedPhases;
        public int TotalPhases => totalPhases;
        public string CurrentPhaseName => currentPhaseName;

        /// <summary>Direct reference to the active Bamboo Hunter enemy brain.</summary>
        public BambooHunterBrain HunterBrain
        {
            get => hunterBrain;
            set => hunterBrain = value;
        }

        /// <summary>Direct reference to the player dummy health demo component.</summary>
        public BambooHunterPlayerHealthDemo PlayerDummy
        {
            get => playerDummy;
            set => playerDummy = value;
        }

        /// <summary>Direct reference to the tactical cover evaluation system.</summary>
        public BambooHunterCoverSystem CoverSystem
        {
            get => coverSystem;
            set => coverSystem = value;
        }

        private void Start()
        {
            FindSceneReferences();
            if (autoRunOnStart)
            {
                StartCoroutine(RunVerificationScenario());
            }
        }

        public void FindSceneReferences()
        {
            if (hunterBrain == null) hunterBrain = FindFirstObjectByType<BambooHunterBrain>();
            if (playerDummy == null) playerDummy = FindFirstObjectByType<BambooHunterPlayerHealthDemo>();
            if (coverSystem == null) coverSystem = FindFirstObjectByType<BambooHunterCoverSystem>();
        }

        /// <summary>
        /// Public entry point to trigger the automated verification sequence at runtime.
        /// </summary>
        public void RunTestScenario()
        {
            if (isRunning) return;
            StartCoroutine(RunVerificationScenario());
        }

        public IEnumerator RunVerificationScenario()
        {
            isRunning = true;
            isComplete = false;
            passedPhases = 0;
            logBuilder.Clear();

            LogLine("=========================================================================");
            LogLine("THE JADE SEAL — BAMBOO HUNTER AI RUNTIME PLAY MODE VERIFICATION");
            LogLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss} | Unity Version: {Application.unityVersion}");
            LogLine("=========================================================================");

            FindSceneReferences();

            if (hunterBrain == null || playerDummy == null)
            {
                LogLine("FAIL: Missing essential scene references (HunterBrain or PlayerDummy).");
                FinalizeScenario(false);
                yield break;
            }

            // -----------------------------------------------------------------
            // PHASE 1: PEACEFUL PATROL (No Active Target)
            // -----------------------------------------------------------------
            currentPhaseName = "Phase 1: Peaceful Patrol";
            LogLine("\n--- [PHASE 1] Verifying Peaceful Waypoint Patrol ---");

            // Ensure dummy is temporarily hidden or placed far away
            playerDummy.gameObject.SetActive(false);
            hunterBrain.Health.ResetHealth();
            hunterBrain.Bow.RestockQuiver();
            hunterBrain.TransitionTo(BambooHunterState.Patrol, force: true);

            yield return new WaitForSeconds(1.2f);

            bool phase1Success = (hunterBrain.CurrentState == BambooHunterState.Patrol) &&
                                 (hunterBrain.CurrentIntent == BambooHunterTacticalIntent.PatrolGrove) &&
                                 !hunterBrain.Perception.CanSeeTarget;

            if (phase1Success)
            {
                passedPhases++;
                LogLine($"PASS [Phase 1]: Hunter is in Patrol state ({hunterBrain.CurrentIntent}), moving peacefully without target.");
            }
            else
            {
                LogLine($"FAIL [Phase 1]: Unexpected state={hunterBrain.CurrentState}, intent={hunterBrain.CurrentIntent}, visible={hunterBrain.Perception.CanSeeTarget}");
            }

            // -----------------------------------------------------------------
            // PHASE 2: TARGET ACQUISITION & AIM (Player Detection)
            // -----------------------------------------------------------------
            currentPhaseName = "Phase 2: Target Detection & Aim";
            LogLine("\n--- [PHASE 2] Verifying Target Acquisition & Bow Aim ---");

            // Unveil Player Dummy in Hunter's line of sight
            playerDummy.gameObject.SetActive(true);
            playerDummy.ResetHealth();
            hunterBrain.Perception.Target = playerDummy.transform;

            float waitTimer = 0f;
            bool detectedAndAiming = false;
            while (waitTimer < 3.0f)
            {
                yield return new WaitForSeconds(0.2f);
                waitTimer += 0.2f;

                if (hunterBrain.CurrentState == BambooHunterState.Aim && hunterBrain.Bow.IsDrawing)
                {
                    detectedAndAiming = true;
                    break;
                }
            }

            if (detectedAndAiming)
            {
                passedPhases++;
                LogLine($"PASS [Phase 2]: Player detected (Dist={hunterBrain.Perception.TargetDistance:F1}m). Transitioned to Aim, bow draw active ({hunterBrain.Bow.DrawProgress * 100f:F0}%).");
            }
            else
            {
                LogLine($"FAIL [Phase 2]: Failed to transition to Aim. Current state={hunterBrain.CurrentState}, visible={hunterBrain.Perception.CanSeeTarget}");
            }

            // -----------------------------------------------------------------
            // PHASE 3: PHYSICAL ARROW LAUNCH & DAMAGE REGISTRATION
            // -----------------------------------------------------------------
            currentPhaseName = "Phase 3: Arrow Launch & Physical Damage";
            LogLine("\n--- [PHASE 3] Verifying Physical Projectile Launch & Damage Registration ---");

            float dummyInitialHealth = playerDummy.CurrentHealth;
            float shotWaitTimer = 0f;
            bool damageRegistered = false;

            while (shotWaitTimer < 4.0f)
            {
                yield return new WaitForSeconds(0.25f);
                shotWaitTimer += 0.25f;

                if (playerDummy.CurrentHealth < dummyInitialHealth)
                {
                    damageRegistered = true;
                    break;
                }
            }

            if (damageRegistered)
            {
                passedPhases++;
                LogLine($"PASS [Phase 3]: Physical arrow successfully hit Player Dummy! Damage registered: {dummyInitialHealth} -> {playerDummy.CurrentHealth:F1} HP (Consecutive shots: {hunterBrain.Bow.ConsecutiveShots}).");
            }
            else
            {
                LogLine($"FAIL [Phase 3]: Dummy did not receive arrow damage within {shotWaitTimer:F1}s. Dummy HP={playerDummy.CurrentHealth}");
            }

            // -----------------------------------------------------------------
            // PHASE 4: TACTICAL REPOSITIONING BEHIND COVER
            // -----------------------------------------------------------------
            currentPhaseName = "Phase 4: Repositioning to Vegetation Cover";
            LogLine("\n--- [PHASE 4] Verifying Post-Shot Tactical Repositioning ---");

            // Allow AI to fire second shot if needed to reach maxShotsBeforeReposition (2)
            float repoWaitTimer = 0f;
            bool transitionedToReposition = false;

            while (repoWaitTimer < 4.5f)
            {
                yield return new WaitForSeconds(0.3f);
                repoWaitTimer += 0.3f;

                if (hunterBrain.CurrentState == BambooHunterState.Reposition)
                {
                    transitionedToReposition = true;
                    break;
                }
            }

            if (transitionedToReposition)
            {
                passedPhases++;
                string coverName = hunterBrain.CoverSystem.SelectedCover != null ? hunterBrain.CoverSystem.SelectedCover.name : "None";
                LogLine($"PASS [Phase 4]: Transitioned to Reposition after reaching shot threshold. Seeking cover anchor: {coverName}.");
            }
            else
            {
                LogLine($"FAIL [Phase 4]: Failed to enter Reposition. Current state={hunterBrain.CurrentState}, shots={hunterBrain.Bow.ConsecutiveShots}");
            }

            // -----------------------------------------------------------------
            // PHASE 5: LOS LOSS & INVESTIGATION
            // -----------------------------------------------------------------
            currentPhaseName = "Phase 5: Target Loss & Investigation";
            LogLine("\n--- [PHASE 5] Verifying Line-of-Sight Loss & Investigation ---");

            // Hide dummy to simulate player ducking behind thick bamboo
            playerDummy.gameObject.SetActive(false);

            float invWaitTimer = 0f;
            bool transitionedToInvestigate = false;

            while (invWaitTimer < 4.0f)
            {
                yield return new WaitForSeconds(0.25f);
                invWaitTimer += 0.25f;

                if (hunterBrain.CurrentState == BambooHunterState.Investigate)
                {
                    transitionedToInvestigate = true;
                    break;
                }
            }

            if (transitionedToInvestigate)
            {
                passedPhases++;
                LogLine($"PASS [Phase 5]: Direct LOS broken. Transitioned to Investigate ({hunterBrain.CurrentIntent}) targeting last known position.");
            }
            else
            {
                LogLine($"FAIL [Phase 5]: Did not transition to Investigate. Current state={hunterBrain.CurrentState}");
            }

            // -----------------------------------------------------------------
            // PHASE 6: LOW HEALTH DEFENSIVE RETREAT
            // -----------------------------------------------------------------
            currentPhaseName = "Phase 6: Defensive Retreat";
            LogLine("\n--- [PHASE 6] Verifying Low-Health Emergency Retreat ---");

            // Restore dummy threat and inflict critical damage on Hunter (<25% health)
            playerDummy.gameObject.SetActive(true);
            hunterBrain.Perception.Target = playerDummy.transform;
            float retreatDamage = hunterBrain.Health.MaxHealth * 0.80f; // Inflict 80% damage, leaving 20% (<25% threshold)
            hunterBrain.Health.TakeDamage(retreatDamage);

            yield return new WaitForSeconds(0.5f);

            bool transitionedToRetreat = (hunterBrain.CurrentState == BambooHunterState.Retreat);

            if (transitionedToRetreat)
            {
                passedPhases++;
                LogLine($"PASS [Phase 6]: Health critically reduced to {hunterBrain.Health.CurrentHealth:F0}/{hunterBrain.Health.MaxHealth:F0} HP ({hunterBrain.Health.HealthRatio * 100f:F0}%). Emergency Retreat triggered!");
            }
            else
            {
                LogLine($"FAIL [Phase 6]: Critical health did not trigger Retreat. State={hunterBrain.CurrentState}, HP={hunterBrain.Health.CurrentHealth}");
            }

            // -----------------------------------------------------------------
            // PHASE 7: TERMINAL DEATH INVARIANT
            // -----------------------------------------------------------------
            currentPhaseName = "Phase 7: Terminal Death";
            LogLine("\n--- [PHASE 7] Verifying Terminal Death State ---");

            // Inflict lethal hit
            hunterBrain.Health.TakeDamage(hunterBrain.Health.MaxHealth + 50f);

            yield return new WaitForSeconds(0.3f);

            bool deadStateSuccess = (hunterBrain.CurrentState == BambooHunterState.Dead) &&
                                    (hunterBrain.CurrentIntent == BambooHunterTacticalIntent.CeaseActions) &&
                                    hunterBrain.Health.IsDead;

            // Attempt unauthorized state escape
            hunterBrain.TransitionTo(BambooHunterState.Patrol);
            bool lockHeld = (hunterBrain.CurrentState == BambooHunterState.Dead);

            if (deadStateSuccess && lockHeld)
            {
                passedPhases++;
                LogLine($"PASS [Phase 7]: Agent transitioned to Dead. Terminal lock successfully rejected state override. Invariant satisfied.");
            }
            else
            {
                LogLine($"FAIL [Phase 7]: Death invariant failed. State={hunterBrain.CurrentState}, IsDead={hunterBrain.Health.IsDead}");
            }

            // -----------------------------------------------------------------
            // SCENARIO SUMMARY
            // -----------------------------------------------------------------
            FinalizeScenario(passedPhases == totalPhases);
        }

        private void FinalizeScenario(bool allPassed)
        {
            isRunning = false;
            isComplete = true;
            currentPhaseName = allPassed ? "Completed: ALL PHASES PASSED" : "Completed: WITH FAILURES";

            LogLine("\n=========================================================================");
            LogLine($"VERIFICATION SUMMARY: {passedPhases} / {totalPhases} PHASES PASSED");
            LogLine($"STATUS: {(allPassed ? "SUCCESS — ALL RUNTIME BEHAVIORS VERIFIED" : "FAILURE — SOME PHASES FAILED")}");
            LogLine("=========================================================================");

            SaveEvidenceLog();
        }

        private void LogLine(string line)
        {
            Debug.Log("[BambooHunterHarness] " + line);
            logBuilder.AppendLine(line);
        }

        private void SaveEvidenceLog()
        {
            try
            {
                string evidenceDir = Path.Combine(Application.dataPath, "BambooHunterAI", "Evidence");
                if (!Directory.Exists(evidenceDir)) Directory.CreateDirectory(evidenceDir);

                string filePath = Path.Combine(evidenceDir, "RuntimePlayModeLog.txt");
                File.WriteAllText(filePath, logBuilder.ToString());
                Debug.Log($"[BambooHunterHarness] Evidence saved to: {filePath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BambooHunterHarness] Failed to write evidence log: {ex.Message}");
            }
        }
    }
}
