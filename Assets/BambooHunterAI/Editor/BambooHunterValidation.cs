using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace JadeSeal.BambooHunter.Editor
{
    /// <summary>
    /// Deterministic validation suite for the Bamboo Hunter AI decision core, perception, and navigation systems.
    /// Executes isolated unit tests covering all utility scoring formulas, FSM arbitration,
    /// invariant overrides, boundary conditions, numerical safeguards, perception processing, and navigation contracts.
    /// </summary>
    public static class BambooHunterValidation
    {
        private const string LogPath = "Assets/BambooHunterAI/Evidence/Deterministic.txt";

        [MenuItem("Jade Seal/Bamboo Hunter/Run Deterministic Validation")]
        public static void RunFromMenu()
        {
            // Build production assets, prefabs, and demo scene first
            BambooHunterAssetBuilder.BuildAll();

            var results = new List<string>();
            bool success = Run(results);

            EnsureEvidenceDirectory();
            File.WriteAllLines(LogPath, results);

            if (success)
            {
                Debug.Log($"[BambooHunterValidation] All {results.Count} deterministic tests PASSED successfully.");
            }
            else
            {
                Debug.LogError($"[BambooHunterValidation] Test suite completed with FAILURES. Review log at {LogPath}");
            }
        }

        [InitializeOnLoadMethod]
        private static void AutoRunOnTrigger()
        {
            EditorApplication.delayCall += () =>
            {
                string triggerPath = Path.Combine(Application.dataPath, "../Temp/RunBambooHunterValidation.trigger");
                if (File.Exists(triggerPath))
                {
                    try { File.Delete(triggerPath); } catch {}
                    RunFromMenu();
                }
            };
        }

        /// <summary>
        /// Executes all unit validation tests and populates the results list.
        /// Returns true if all tests pass, false if any test fails.
        /// </summary>
        public static bool Run(List<string> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            results.Clear();

            // Ensure isolated empty scene for unit tests 1-66
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BambooHunterSettings settings = ScriptableObject.CreateInstance<BambooHunterSettings>();
            bool allPassed = true;

            void AssertTest(bool condition, string testName, string failureDetail = "")
            {
                if (condition)
                {
                    results.Add($"PASS: {testName}");
                }
                else
                {
                    allPassed = false;
                    string message = $"FAIL: {testName} - {failureDetail}";
                    results.Add(message);
                    Debug.LogError($"[BambooHunterValidation] {message}");
                }
            }

            try
            {
                // =========================================================================
                // PART 1: CORE UTILITY SCORING & ARBITRATION TESTS (TESTS 1 - 12)
                // =========================================================================

                // TEST 1: No Target -> Selects Patrol
                {
                    var ctx = new BambooHunterDecisionContext
                    {
                        currentHealth = 120f,
                        maxHealth = 120f,
                        hasTargetVisibility = false,
                        hasTargetMemory = false
                    };

                    float patrolScore = BambooHunterUtility.Score(BambooHunterState.Patrol, ctx, settings);
                    BambooHunterState chosen = BambooHunterUtility.Choose(ctx, BambooHunterState.Patrol, 1.0f, settings);

                    AssertTest(patrolScore > 0f, "NoTarget_PatrolScorePositive", $"Score was {patrolScore}");
                    AssertTest(chosen == BambooHunterState.Patrol, "NoTarget_SelectsPatrol", $"Selected {chosen}");
                }

                // TEST 2: Visible Target with In-Range Prerequisites -> Selects Aim
                {
                    var ctx = new BambooHunterDecisionContext
                    {
                        currentHealth = 120f,
                        maxHealth = 120f,
                        hasTargetVisibility = true,
                        hasTargetMemory = true,
                        distanceToTarget = 12f, // Inside [preferredMin, preferredMax]
                        hasUnobstructedShot = true,
                        currentArrows = 5,
                        isBowDrawn = false,
                        bowDrawProgress = 0f
                    };

                    float aimScore = BambooHunterUtility.Score(BambooHunterState.Aim, ctx, settings);
                    BambooHunterState chosen = BambooHunterUtility.Choose(ctx, BambooHunterState.Patrol, 1.0f, settings);

                    AssertTest(aimScore > 0.70f, "VisibleTarget_AimScoreHigh", $"Aim score was {aimScore}");
                    AssertTest(chosen == BambooHunterState.Aim, "VisibleTarget_SelectsAim", $"Selected {chosen}");
                }

                // TEST 3: Lost Target with Active Memory -> Selects Investigate
                {
                    var ctx = new BambooHunterDecisionContext
                    {
                        currentHealth = 120f,
                        maxHealth = 120f,
                        hasTargetVisibility = false,
                        hasTargetMemory = true,
                        memoryAgeSeconds = 1.0f
                    };

                    float investScore = BambooHunterUtility.Score(BambooHunterState.Investigate, ctx, settings);
                    BambooHunterState chosen = BambooHunterUtility.Choose(ctx, BambooHunterState.Patrol, 1.0f, settings);

                    AssertTest(investScore > 0.50f, "LostTarget_InvestigateScoreHigh", $"Score was {investScore}");
                    AssertTest(chosen == BambooHunterState.Investigate, "LostTarget_SelectsInvestigate", $"Selected {chosen}");
                }

                // TEST 4: Empty Quiver -> Cannot Shoot or Aim
                {
                    var ctx = new BambooHunterDecisionContext
                    {
                        currentHealth = 120f,
                        maxHealth = 120f,
                        hasTargetVisibility = true,
                        distanceToTarget = 10f,
                        hasUnobstructedShot = true,
                        currentArrows = 0, // Empty quiver
                        isBowDrawn = true,
                        bowDrawProgress = 1.0f,
                        hasReachableCover = true,
                        coverQuality = 0.8f
                    };

                    float shootScore = BambooHunterUtility.Score(BambooHunterState.Shoot, ctx, settings);
                    float aimScore = BambooHunterUtility.Score(BambooHunterState.Aim, ctx, settings);
                    BambooHunterState chosen = BambooHunterUtility.Choose(ctx, BambooHunterState.Aim, 0.6f, settings);

                    AssertTest(shootScore == 0f, "EmptyQuiver_CannotShoot", $"Shoot score was {shootScore}");
                    AssertTest(aimScore == 0f, "EmptyQuiver_CannotAim", $"Aim score was {aimScore}");
                    AssertTest(chosen != BambooHunterState.Shoot && chosen != BambooHunterState.Aim,
                        "EmptyQuiver_DoesNotChooseAttack", $"Selected {chosen}");
                }

                // TEST 5: Invalid Cover -> Cannot Reposition
                {
                    var ctx = new BambooHunterDecisionContext
                    {
                        currentHealth = 120f,
                        maxHealth = 120f,
                        hasTargetVisibility = true,
                        consecutiveShotsFired = 3, // Wants to reposition
                        hasReachableCover = false   // No cover available
                    };

                    float repoScore = BambooHunterUtility.Score(BambooHunterState.Reposition, ctx, settings);

                    AssertTest(repoScore == 0f, "InvalidCover_RepositionScoreZero", $"Score was {repoScore}");
                }

                // TEST 6: Low Health with Active Threat -> Selects Retreat
                {
                    var ctx = new BambooHunterDecisionContext
                    {
                        currentHealth = 20f, // 20 / 120 = 16.7% <= retreatThreshold (25%)
                        maxHealth = 120f,
                        hasTargetVisibility = true,
                        distanceToTarget = 15f
                    };

                    float retreatScore = BambooHunterUtility.Score(BambooHunterState.Retreat, ctx, settings);
                    BambooHunterState chosen = BambooHunterUtility.Choose(ctx, BambooHunterState.Aim, 0.1f, settings);

                    AssertTest(retreatScore >= 0.90f, "LowHealth_RetreatScoreHigh", $"Score was {retreatScore}");
                    AssertTest(chosen == BambooHunterState.Retreat, "LowHealth_SelectsRetreatImmediately", $"Selected {chosen}");
                }

                // TEST 7: Death Invariant Override -> Forces Dead
                {
                    var ctxDead = new BambooHunterDecisionContext
                    {
                        isDead = true,
                        currentHealth = 0f,
                        maxHealth = 120f,
                        hasTargetVisibility = true
                    };

                    BambooHunterState chosenFromAim = BambooHunterUtility.Choose(ctxDead, BambooHunterState.Aim, 2f, settings);
                    BambooHunterState chosenFromRetreat = BambooHunterUtility.Choose(ctxDead, BambooHunterState.Retreat, 0f, settings);
                    float deadScore = BambooHunterUtility.Score(BambooHunterState.Dead, ctxDead, settings);
                    float aimScore = BambooHunterUtility.Score(BambooHunterState.Aim, ctxDead, settings);

                    AssertTest(deadScore == 1.0f, "DeathOverride_DeadScoreIsOne", $"Score was {deadScore}");
                    AssertTest(aimScore == 0f, "DeathOverride_AimScoreIsZero", $"Score was {aimScore}");
                    AssertTest(chosenFromAim == BambooHunterState.Dead, "DeathOverride_FromAimForcesDead", $"Selected {chosenFromAim}");
                    AssertTest(chosenFromRetreat == BambooHunterState.Dead, "DeathOverride_FromRetreatForcesDead", $"Selected {chosenFromRetreat}");
                }

                // TEST 8: Bow Draw Readiness Enforces Shoot Prerequisites
                {
                    var ctxHalfDrawn = new BambooHunterDecisionContext
                    {
                        currentHealth = 120f,
                        maxHealth = 120f,
                        hasTargetVisibility = true,
                        distanceToTarget = 10f,
                        hasUnobstructedShot = true,
                        currentArrows = 4,
                        isBowDrawn = true,
                        bowDrawProgress = 0.50f // Half drawn: must NOT shoot
                    };

                    var ctxFullDrawn = new BambooHunterDecisionContext
                    {
                        currentHealth = 120f,
                        maxHealth = 120f,
                        hasTargetVisibility = true,
                        distanceToTarget = 10f,
                        hasUnobstructedShot = true,
                        currentArrows = 4,
                        isBowDrawn = true,
                        bowDrawProgress = 1.0f // Fully drawn: MUST shoot
                    };

                    float shootHalf = BambooHunterUtility.Score(BambooHunterState.Shoot, ctxHalfDrawn, settings);
                    float shootFull = BambooHunterUtility.Score(BambooHunterState.Shoot, ctxFullDrawn, settings);
                    BambooHunterState chosenHalf = BambooHunterUtility.Choose(ctxHalfDrawn, BambooHunterState.Aim, 0.6f, settings);
                    BambooHunterState chosenFull = BambooHunterUtility.Choose(ctxFullDrawn, BambooHunterState.Aim, 0.6f, settings);

                    AssertTest(shootHalf == 0f, "BowDraw_HalfDrawCannotShoot", $"Half draw score was {shootHalf}");
                    AssertTest(shootFull >= 0.90f, "BowDraw_FullDrawCanShoot", $"Full draw score was {shootFull}");
                    AssertTest(chosenHalf == BambooHunterState.Aim, "BowDraw_HalfDrawRemainsInAim", $"Selected {chosenHalf}");
                    AssertTest(chosenFull == BambooHunterState.Shoot, "BowDraw_FullDrawTransitionsToShoot", $"Selected {chosenFull}");
                }

                // TEST 9: Hysteresis Prevents Thrashing
                {
                    var ctx = new BambooHunterDecisionContext
                    {
                        currentHealth = 120f,
                        maxHealth = 120f,
                        hasTargetVisibility = true,
                        distanceToTarget = 10f,
                        hasUnobstructedShot = true,
                        currentArrows = 3,
                        consecutiveShotsFired = 0,
                        hasReachableCover = true,
                        coverQuality = 0.3f,
                        coverOcclusion = 0.3f
                    };

                    BambooHunterState heldState = BambooHunterUtility.Choose(ctx, BambooHunterState.Aim, 0.6f, settings);
                    AssertTest(heldState == BambooHunterState.Aim, "Hysteresis_HoldsStateWhenChallengerWeak", $"Selected {heldState}");
                }

                // TEST 10: Deterministic Tie-Breaking
                {
                    var ctx = new BambooHunterDecisionContext
                    {
                        currentHealth = 120f,
                        maxHealth = 120f,
                        hasTargetVisibility = false,
                        hasTargetMemory = false
                    };

                    BambooHunterState r1 = BambooHunterUtility.Choose(ctx, BambooHunterState.Patrol, 1f, settings);
                    BambooHunterState r2 = BambooHunterUtility.Choose(ctx, BambooHunterState.Patrol, 1f, settings);
                    BambooHunterState r3 = BambooHunterUtility.Choose(ctx, BambooHunterState.Patrol, 1f, settings);

                    AssertTest(r1 == r2 && r2 == r3, "TieBreaking_DeterministicAcrossRuns", $"Results: {r1}, {r2}, {r3}");
                }

                // TEST 11: Invalid Numeric Inputs Handled Safely
                {
                    var ctxInvalid = new BambooHunterDecisionContext
                    {
                        currentHealth = float.NaN,
                        maxHealth = float.NegativeInfinity,
                        distanceToTarget = float.NaN,
                        bowDrawProgress = float.PositiveInfinity,
                        coverQuality = float.NaN,
                        coverOcclusion = float.NaN,
                        currentArrows = -5
                    };

                    float s1 = BambooHunterUtility.Score(BambooHunterState.Patrol, ctxInvalid, settings);
                    float s2 = BambooHunterUtility.Score(BambooHunterState.Aim, ctxInvalid, settings);
                    float s3 = BambooHunterUtility.Score(BambooHunterState.Shoot, ctxInvalid, settings);
                    float s4 = BambooHunterUtility.Score(BambooHunterState.Reposition, ctxInvalid, settings);
                    float s5 = BambooHunterUtility.Score(BambooHunterState.Retreat, ctxInvalid, settings);

                    bool allFinite = !float.IsNaN(s1) && !float.IsNaN(s2) && !float.IsNaN(s3) && !float.IsNaN(s4) && !float.IsNaN(s5);
                    AssertTest(allFinite, "InvalidInputs_AllScoresFinite", $"Scores: {s1}, {s2}, {s3}, {s4}, {s5}");
                }

                // TEST 12: Cover Scoring Formula Bounds and Monotonicity
                {
                    float goodCover = BambooHunterUtility.CalculateCoverScore(3f, 4f, 1f, 1f, 0.8f, settings);
                    float badCover = BambooHunterUtility.CalculateCoverScore(16f, 35f, 0.2f, 0f, -0.5f, settings);
                    float zeroWeight = BambooHunterUtility.CalculateCoverScore(3f, 4f, 1f, 1f, 0.8f, null);

                    AssertTest(goodCover > badCover, "CoverScoring_GoodExceedsBad", $"Good: {goodCover}, Bad: {badCover}");
                    AssertTest(goodCover <= 1.0f && goodCover >= 0.0f, "CoverScoring_GoodWithinBounds", $"Good: {goodCover}");
                    AssertTest(badCover <= 1.0f && badCover >= 0.0f, "CoverScoring_BadWithinBounds", $"Bad: {badCover}");
                    AssertTest(!float.IsNaN(zeroWeight) && zeroWeight >= 0f, "CoverScoring_NullSettingsHandled", $"ZeroWeight: {zeroWeight}");
                }

                // =========================================================================
                // PART 2: PERCEPTION & TARGET DETECTION VALIDATION TESTS (TESTS 13 - 20)
                // =========================================================================

                // TEST 13: Horizontal Field-of-View Boundaries
                {
                    Vector3 observerPos = Vector3.zero;
                    Vector3 forward = Vector3.forward; // Facing +Z
                    float fov = 110f; // Half-angle is 55 degrees

                    Vector3 targetDirect = new Vector3(0f, 0f, 10f);
                    bool insideDirect = BambooHunterPerception.IsInsideFieldOfView(observerPos, forward, targetDirect, fov);

                    Vector3 targetInsideBoundary = Quaternion.Euler(0f, 45f, 0f) * (Vector3.forward * 10f);
                    bool insideBoundary = BambooHunterPerception.IsInsideFieldOfView(observerPos, forward, targetInsideBoundary, fov);

                    Vector3 targetOutsideBoundary = Quaternion.Euler(0f, 65f, 0f) * (Vector3.forward * 10f);
                    bool outsideBoundary = BambooHunterPerception.IsInsideFieldOfView(observerPos, forward, targetOutsideBoundary, fov);

                    Vector3 targetBehind = new Vector3(0f, 0f, -10f);
                    bool behindOutside = BambooHunterPerception.IsInsideFieldOfView(observerPos, forward, targetBehind, fov);

                    AssertTest(insideDirect, "Perception_FOV_DirectInFrontInside");
                    AssertTest(insideBoundary, "Perception_FOV_WithinBoundaryInside");
                    AssertTest(!outsideBoundary, "Perception_FOV_PastBoundaryOutside");
                    AssertTest(!behindOutside, "Perception_FOV_BehindObserverOutside");
                }

                // TEST 14: Detection Distance Boundaries
                {
                    Vector3 observerPos = new Vector3(0f, 1f, 0f);
                    float maxDist = 25f;

                    Vector3 targetNear = new Vector3(0f, 1f, 15f);
                    bool nearValid = BambooHunterPerception.IsWithinDistance(observerPos, targetNear, maxDist, out float dNear);

                    Vector3 targetExact = new Vector3(0f, 1f, 25f);
                    bool exactValid = BambooHunterPerception.IsWithinDistance(observerPos, targetExact, maxDist, out float dExact);

                    Vector3 targetPast = new Vector3(0f, 1f, 26f);
                    bool pastValid = BambooHunterPerception.IsWithinDistance(observerPos, targetPast, maxDist, out float dPast);

                    bool negValid = BambooHunterPerception.IsWithinDistance(observerPos, targetNear, -5f, out float dNeg);

                    AssertTest(nearValid && Mathf.Approximately(dNear, 15f), "Perception_Dist_NearValid", $"d={dNear}");
                    AssertTest(exactValid && Mathf.Approximately(dExact, 25f), "Perception_Dist_ExactBoundaryValid", $"d={dExact}");
                    AssertTest(!pastValid && dPast > 25f, "Perception_Dist_PastBoundaryInvalid", $"d={dPast}");
                    AssertTest(!negValid && dNeg == 0f, "Perception_Dist_NegativeMaxDistRejected");
                }

                // TEST 15: Memory Retention and Decay
                {
                    float lastSeen = 100.0f;
                    float duration = 5.0f;

                    bool memFresh = BambooHunterPerception.EvaluateMemory(102.0f, lastSeen, duration, out float ageFresh);
                    bool memBoundary = BambooHunterPerception.EvaluateMemory(104.9f, lastSeen, duration, out float ageBoundary);
                    bool memExpired = BambooHunterPerception.EvaluateMemory(105.5f, lastSeen, duration, out float ageExpired);
                    bool memNever = BambooHunterPerception.EvaluateMemory(105.5f, float.NegativeInfinity, duration, out float ageNever);

                    AssertTest(memFresh && Mathf.Approximately(ageFresh, 2.0f), "Perception_Memory_FreshRetained", $"age={ageFresh}");
                    AssertTest(memBoundary && Mathf.Approximately(ageBoundary, 4.9f), "Perception_Memory_BoundaryRetained", $"age={ageBoundary}");
                    AssertTest(!memExpired && Mathf.Approximately(ageExpired, 5.5f), "Perception_Memory_ExpiredRejected", $"age={ageExpired}");
                    AssertTest(!memNever && float.IsPositiveInfinity(ageNever), "Perception_Memory_NeverSeenRejected");
                }

                // TEST 16: Target Velocity Calculation & Aim Prediction
                {
                    Vector3 pos1 = new Vector3(0f, 0f, 10f);
                    Vector3 pos2 = new Vector3(5f, 0f, 10f);
                    float dt = 0.5f;

                    Vector3 vel = BambooHunterPerception.CalculateTargetVelocity(pos2, pos1, dt);
                    Vector3 zeroDtVel = BambooHunterPerception.CalculateTargetVelocity(pos2, pos1, 0f);

                    AssertTest(Mathf.Approximately(vel.x, 10f) && Mathf.Approximately(vel.z, 0f),
                        "Perception_Velocity_CalculationAccurate", $"vel={vel}");
                    AssertTest(zeroDtVel == Vector3.zero, "Perception_Velocity_ZeroDeltaTimeHandledSafely");
                }

                // TEST 17: Threat Aiming At Observer Detection
                {
                    Vector3 observerPos = new Vector3(0f, 0f, 0f);
                    Vector3 targetPos = new Vector3(0f, 0f, 10f);

                    Vector3 forwardFacingObserver = new Vector3(0f, 0f, -1f);
                    bool isAiming = BambooHunterPerception.IsTargetAimingAtObserver(observerPos, targetPos, forwardFacingObserver, 0.70f);

                    Vector3 forwardFacingAway = new Vector3(0f, 0f, 1f);
                    bool notAiming = BambooHunterPerception.IsTargetAimingAtObserver(observerPos, targetPos, forwardFacingAway, 0.70f);

                    AssertTest(isAiming, "Perception_ThreatAiming_DetectsFacingObserver");
                    AssertTest(!notAiming, "Perception_ThreatAiming_RejectsFacingAway");
                }

                // TEST 18: Perception Component Runtime Sensory Context Mapping
                {
                    var agentGo = new GameObject("TestBambooHunterAgent");
                    var targetGo = new GameObject("TestPlayerTarget");
                    targetGo.transform.position = new Vector3(0f, 0f, 12f);

                    var perception = agentGo.AddComponent<BambooHunterPerception>();
                    perception.Target = targetGo.transform;

                    var decisionContext = new BambooHunterDecisionContext();
                    perception.PopulateDecisionContext(ref decisionContext, settings);

                    AssertTest(decisionContext.distanceToTarget == perception.TargetDistance,
                        "Perception_Context_DistanceMappedAccurately");
                    AssertTest(decisionContext.hasTargetVisibility == perception.CanSeeTarget,
                        "Perception_Context_VisibilityMappedAccurately");
                    AssertTest(decisionContext.hasUnobstructedShot == perception.HasClearLineOfFire,
                        "Perception_Context_LineOfFireMappedAccurately");

                    UnityEngine.Object.DestroyImmediate(agentGo);
                    UnityEngine.Object.DestroyImmediate(targetGo);
                }

                // TEST 19: Missing / Null Target Safety
                {
                    var agentGo = new GameObject("TestAgent_NullTarget");
                    var perception = agentGo.AddComponent<BambooHunterPerception>();
                    perception.Target = null;

                    perception.Sample(settings);

                    var ctx = new BambooHunterDecisionContext();
                    perception.PopulateDecisionContext(ref ctx, settings);

                    AssertTest(!perception.CanSeeTarget, "Perception_NullTarget_CanSeeTargetFalse");
                    AssertTest(!perception.HasClearLineOfFire, "Perception_NullTarget_ClearLineFalse");
                    AssertTest(!ctx.hasTargetVisibility, "Perception_NullTarget_ContextVisibilityFalse");

                    UnityEngine.Object.DestroyImmediate(agentGo);
                }

                // TEST 20: Invalid Numeric & Geometric Inputs to Perception
                {
                    bool fovNan = BambooHunterPerception.IsInsideFieldOfView(Vector3.zero, Vector3.forward, Vector3.one, float.NaN);
                    bool fovZero = BambooHunterPerception.IsInsideFieldOfView(Vector3.zero, Vector3.forward, Vector3.one, 0f);

                    bool distNan = BambooHunterPerception.IsWithinDistance(Vector3.zero, new Vector3(float.NaN, 0f, 0f), 25f, out float d1);
                    bool distInf = BambooHunterPerception.IsWithinDistance(Vector3.zero, new Vector3(float.PositiveInfinity, 0f, 0f), 25f, out float d2);

                    Vector3 velNan = BambooHunterPerception.CalculateTargetVelocity(new Vector3(float.NaN, 0f, 0f), Vector3.zero, 1f);
                    bool aimNan = BambooHunterPerception.IsTargetAimingAtObserver(Vector3.zero, Vector3.zero, Vector3.forward);

                    AssertTest(!fovNan && !fovZero, "Perception_Invalid_FovHandledSafely");
                    AssertTest(!distNan && !distInf, "Perception_Invalid_DistanceHandledSafely");
                    AssertTest(velNan == Vector3.zero, "Perception_Invalid_VelocityHandledSafely");
                    AssertTest(!aimNan, "Perception_Invalid_AimingHandledSafely");
                }

                // =========================================================================
                // PART 3: NAVIGATION & PATROL SYSTEM VALIDATION TESTS (TESTS 21 - 28)
                // =========================================================================

                // TEST 21: Waypoint Progression Cyclical Indexing
                {
                    int zeroWp = BambooHunterNavigation.AdvanceWaypointIndex(0, 0);
                    int singleWp = BambooHunterNavigation.AdvanceWaypointIndex(0, 1);
                    int step0 = BambooHunterNavigation.AdvanceWaypointIndex(0, 3);
                    int step1 = BambooHunterNavigation.AdvanceWaypointIndex(1, 3);
                    int step2 = BambooHunterNavigation.AdvanceWaypointIndex(2, 3);

                    AssertTest(zeroWp == 0, "Navigation_Waypoint_ZeroCountReturnsZero");
                    AssertTest(singleWp == 0, "Navigation_Waypoint_SingleCountReturnsZero");
                    AssertTest(step0 == 1 && step1 == 2 && step2 == 0, "Navigation_Waypoint_CyclicLoopDeterministic", $"Steps: {step0}, {step1}, {step2}");
                }

                // TEST 22: Arrival Tolerance Evaluation
                {
                    Vector3 currentPos = new Vector3(0f, 0f, 0f);
                    Vector3 targetWithin = new Vector3(0f, 0f, 0.40f);
                    Vector3 targetBeyond = new Vector3(0f, 0f, 0.70f);
                    float stopDist = 0.30f;
                    float tolerance = 0.20f; // Total threshold = 0.50m

                    bool arrivedNear = BambooHunterNavigation.CheckArrival(currentPos, targetWithin, stopDist, tolerance);
                    bool arrivedFar = BambooHunterNavigation.CheckArrival(currentPos, targetBeyond, stopDist, tolerance);
                    bool arrivedNan = BambooHunterNavigation.CheckArrival(currentPos, new Vector3(float.NaN, 0f, 0f), stopDist, tolerance);

                    AssertTest(arrivedNear, "Navigation_Arrival_WithinToleranceArrived");
                    AssertTest(!arrivedFar, "Navigation_Arrival_BeyondToleranceNotArrived");
                    AssertTest(!arrivedNan, "Navigation_Arrival_NanRejectedSafely");
                }

                // TEST 23: Anti-Stuck Detection & Progress Monitoring
                {
                    Vector3 checkpoint = new Vector3(0f, 0f, 0f);
                    Vector3 stalledPos = new Vector3(0.05f, 0f, 0f); // Moved only 0.05m (< 0.2m min threshold)
                    Vector3 movedPos = new Vector3(1.20f, 0f, 0f);   // Moved 1.2m (sufficient progress)
                    float timeout = 3.0f;

                    // Stalled for 3.5s (exceeds 3.0s timeout)
                    bool isStuckStalled = BambooHunterNavigation.CheckStuckCondition(stalledPos, checkpoint, 3.5f, timeout, 0.2f);
                    // Progressed 1.2m even after 3.5s elapsed
                    bool isStuckProgressed = BambooHunterNavigation.CheckStuckCondition(movedPos, checkpoint, 3.5f, timeout, 0.2f);
                    // Stalled for only 1.0s (under 3.0s timeout)
                    bool isStuckEarly = BambooHunterNavigation.CheckStuckCondition(stalledPos, checkpoint, 1.0f, timeout, 0.2f);

                    AssertTest(isStuckStalled, "Navigation_Stuck_StalledPositionDetected");
                    AssertTest(!isStuckProgressed, "Navigation_Stuck_ProgressedPositionNotStuck");
                    AssertTest(!isStuckEarly, "Navigation_Stuck_UnderTimeoutNotStuck");
                }

                // TEST 24: Path Distance Summation
                {
                    Vector3[] corners = {
                        new Vector3(0f, 0f, 0f),
                        new Vector3(3f, 0f, 0f),
                        new Vector3(3f, 0f, 4f)
                    }; // 3 + 4 = 7m total length

                    float totalLength = BambooHunterNavigation.CalculatePathDistance(corners);
                    float nullLength = BambooHunterNavigation.CalculatePathDistance(null);
                    float singleLength = BambooHunterNavigation.CalculatePathDistance(new Vector3[] { Vector3.zero });

                    AssertTest(Mathf.Approximately(totalLength, 7.0f), "Navigation_PathDist_CornerSumAccurate", $"len={totalLength}");
                    AssertTest(nullLength == 0f, "Navigation_PathDist_NullArrayZero");
                    AssertTest(singleLength == 0f, "Navigation_PathDist_SingleCornerZero");
                }

                // TEST 25: Finite Position & Vector Validation
                {
                    bool validVec = BambooHunterNavigation.IsFinitePosition(new Vector3(10f, 2f, 30f));
                    bool nanVec = BambooHunterNavigation.IsFinitePosition(new Vector3(float.NaN, 0f, 0f));
                    bool infVec = BambooHunterNavigation.IsFinitePosition(new Vector3(0f, float.PositiveInfinity, 0f));

                    AssertTest(validVec, "Navigation_Finite_ValidAccepted");
                    AssertTest(!nanVec, "Navigation_Finite_NanRejected");
                    AssertTest(!infVec, "Navigation_Finite_InfinityRejected");
                }

                // TEST 26: NavMeshAgent Component Integration & Missing Agent Guards
                {
                    var navGo = new GameObject("TestNavigationAgent");
                    var navComp = navGo.AddComponent<BambooHunterNavigation>();

                    // In offline editor execution, agent is not placed on a baked scene NavMesh
                    bool ready = navComp.IsReady;
                    AssertTest(!ready, "Navigation_IsReady_FalseWhenNotOnNavMesh");

                    // MoveToDestination must fail gracefully without throwing NullReference or unhandled exceptions
                    bool moveSuccess = navComp.MoveToDestination(new Vector3(10f, 0f, 10f), 3.0f, NavigationActivity.Patrolling);
                    AssertTest(!moveSuccess, "Navigation_Move_FailsGracefullyWhenNotReady");
                    AssertTest(navComp.HasFailed, "Navigation_HasFailed_SetTrueOnFailure");
                    AssertTest(navComp.CurrentActivity == NavigationActivity.Failed, "Navigation_Activity_FailedWhenNotReady");

                    // Stop must execute cleanly without exceptions
                    navComp.Stop();
                    AssertTest(navComp.CurrentActivity == NavigationActivity.Idle, "Navigation_Stop_SetsActivityIdle");

                    // Refresh clears failure states
                    navComp.Refresh();
                    AssertTest(!navComp.HasFailed && !navComp.IsStuck, "Navigation_Refresh_ClearsFailureAndStuck");

                    UnityEngine.Object.DestroyImmediate(navGo);
                }

                // TEST 27: Waypoint Runtime Assignment & Safe Handling
                {
                    var navGo = new GameObject("TestNavWaypoints");
                    var navComp = navGo.AddComponent<BambooHunterNavigation>();

                    var wp1 = new GameObject("WP1");
                    var wp2 = new GameObject("WP2");

                    navComp.SetPatrolWaypoints(new Transform[] { wp1.transform, wp2.transform });

                    AssertTest(navComp.WaypointCount == 2, "Navigation_Waypoints_CountMatchesAssigned");
                    AssertTest(navComp.CurrentWaypointIndex == 0, "Navigation_Waypoints_IndexStartsAtZero");

                    // Set null waypoints array
                    navComp.SetPatrolWaypoints(null);
                    AssertTest(navComp.WaypointCount == 0, "Navigation_Waypoints_NullHandledSafely");

                    // MoveToPatrol with 0 waypoints defaults cleanly to idle
                    bool patrolZero = navComp.MoveToPatrol(settings);
                    AssertTest(patrolZero, "Navigation_Patrol_ZeroWaypointsSucceedsGracefully");
                    AssertTest(navComp.CurrentActivity == NavigationActivity.Idle, "Navigation_Patrol_ZeroWaypointsSetsIdle");

                    UnityEngine.Object.DestroyImmediate(navGo);
                    UnityEngine.Object.DestroyImmediate(wp1);
                    UnityEngine.Object.DestroyImmediate(wp2);
                }

                // TEST 28: State-Oriented Movement Commands Safeguards
                {
                    var navGo = new GameObject("TestNavCommands");
                    var navComp = navGo.AddComponent<BambooHunterNavigation>();

                    // Testing movement commands with invalid coordinates
                    bool invResult = navComp.MoveToInvestigate(new Vector3(float.NaN, 0f, 0f), settings);
                    bool repoResult = navComp.MoveToReposition(new Vector3(0f, float.PositiveInfinity, 0f), settings);
                    bool retResult = navComp.MoveToRetreat(new Vector3(float.NegativeInfinity, 0f, 0f), settings);

                    AssertTest(!invResult && navComp.HasFailed, "Navigation_Investigate_NanDestinationRejected");
                    AssertTest(!repoResult && navComp.HasFailed, "Navigation_Reposition_InfinityDestinationRejected");
                    AssertTest(!retResult && navComp.HasFailed, "Navigation_Retreat_NegInfinityDestinationRejected");

                    // StopForAim halts and sets activity
                    navComp.StopForAim();
                    AssertTest(navComp.CurrentActivity == NavigationActivity.StoppedForAim, "Navigation_StopForAim_ActivitySet");

                    UnityEngine.Object.DestroyImmediate(navGo);
                }

                // =========================================================================
                // PART 4: TACTICAL COVER EVALUATION & REPOSITIONING TESTS (TESTS 29 - 37)
                // =========================================================================

                // TEST 29: Cover Point Configuration & Validation
                {
                    var cpGo = new GameObject("TestCoverPoint");
                    var cp = cpGo.AddComponent<BambooHunterCoverPoint>();

                    AssertTest(cp.IsValid, "Cover_Point_ValidByDefault");
                    AssertTest(cp.Quality >= 0f && cp.Quality <= 1f, "Cover_Point_QualityClamped");
                    AssertTest(cp.HidePosition == cpGo.transform.position, "Cover_Point_HidePositionMatchesTransform");
                    AssertTest(BambooHunterNavigation.IsFinitePosition(cp.PeekPosition), "Cover_Point_PeekPositionFinite");

                    // Inactive GameObject must invalidate cover
                    cpGo.SetActive(false);
                    AssertTest(!cp.IsValid, "Cover_Point_InactiveInvalidated");
                    cpGo.SetActive(true);

                    // NaN coordinate in offset must invalidate cover
                    cp.PeekOffset = new Vector3(float.NaN, 0f, 0f);
                    AssertTest(!cp.IsValid, "Cover_Point_NanPositionInvalidated");

                    UnityEngine.Object.DestroyImmediate(cpGo);
                }

                // TEST 30: Cover Point Reservation & Availability Lifecycle
                {
                    var cpGo = new GameObject("TestCoverReservation");
                    var cp = cpGo.AddComponent<BambooHunterCoverPoint>();
                    object agentA = new object();
                    object agentB = new object();
                    float cooldown = 5.0f;
                    float t0 = 100.0f;

                    // Available initially
                    AssertTest(cp.IsAvailable(agentA, cooldown, t0), "Cover_Reservation_AvailableInitially");

                    // Agent A reserves
                    bool reservedA = cp.Reserve(agentA);
                    AssertTest(reservedA && cp.IsReserved, "Cover_Reservation_AgentAReserved");

                    // Agent B cannot reserve while occupied
                    bool reservedB = cp.Reserve(agentB);
                    AssertTest(!reservedB, "Cover_Reservation_AgentBRejectedWhileOccupied");
                    AssertTest(!cp.IsAvailable(agentB, cooldown, t0), "Cover_Reservation_NotAvailableToAgentB");

                    // Agent A releases -> enters cooldown
                    cp.Release(agentA, t0);
                    AssertTest(!cp.IsReserved, "Cover_Reservation_ReleasedSuccessfully");

                    // Immediate re-selection during cooldown window rejected
                    bool availDuringCooldown = cp.IsAvailable(agentA, cooldown, t0 + 2.0f);
                    AssertTest(!availDuringCooldown, "Cover_Reservation_CooldownRejectsImmediateReselection");

                    // After cooldown window expires -> available again
                    bool availAfterCooldown = cp.IsAvailable(agentA, cooldown, t0 + 6.0f);
                    AssertTest(availAfterCooldown, "Cover_Reservation_AvailableAfterCooldownExpires");

                    UnityEngine.Object.DestroyImmediate(cpGo);
                }

                // TEST 31: Temporary Rejection Penalty
                {
                    var cpGo = new GameObject("TestCoverRejection");
                    var cp = cpGo.AddComponent<BambooHunterCoverPoint>();
                    object agent = new object();
                    float t = 50.0f;

                    cp.Reject(5.0f, t);
                    AssertTest(cp.LastRejectedTime > 0f, "Cover_Rejection_TimestampRecorded");
                    AssertTest(!cp.IsAvailable(agent, 0f, t + 1.0f), "Cover_Rejection_UnavailableDuringPenaltyWindow");

                    UnityEngine.Object.DestroyImmediate(cpGo);
                }

                // TEST 32: Extended Multi-Criteria Cover Scoring Priority
                {
                    // Good cover: close (3m), short path (3.5m), high quality (1.0), high occlusion (0.9), flanking (+0.8), clear firing (1.0), no repetition
                    float goodCover = BambooHunterUtility.CalculateCoverScore(3f, 3.5f, 1.0f, 0.9f, 0.8f, 1.0f, 0f, settings);

                    // Bad cover: far (15m), long path (25m), low quality (0.2), low occlusion (0.1), facing threat (-0.8), blocked firing (0.0), no repetition
                    float badCover = BambooHunterUtility.CalculateCoverScore(15f, 25f, 0.2f, 0.1f, -0.8f, 0.0f, 0f, settings);

                    AssertTest(goodCover > badCover, "Cover_Scoring_GoodExceedsBadExtended", $"Good={goodCover}, Bad={badCover}");
                    AssertTest(goodCover <= 1.0f && goodCover >= 0.0f, "Cover_Scoring_GoodWithinBoundsExtended", $"Good={goodCover}");
                    AssertTest(badCover <= 1.0f && badCover >= 0.0f, "Cover_Scoring_BadWithinBoundsExtended", $"Bad={badCover}");
                }

                // TEST 33: Path Cost & Detour Penalization
                {
                    // Direct path (5m) vs circuitous detour path (35m) with all other parameters identical
                    float directScore = BambooHunterUtility.CalculateCoverScore(5f, 5.5f, 1.0f, 0.8f, 0.5f, 1.0f, 0f, settings);
                    float detourScore = BambooHunterUtility.CalculateCoverScore(5f, 35.0f, 1.0f, 0.8f, 0.5f, 1.0f, 0f, settings);

                    AssertTest(directScore > detourScore, "Cover_Scoring_DirectPathExceedsDetour", $"Direct={directScore}, Detour={detourScore}");
                }

                // TEST 34: Anti-Repetition Score Penalty
                {
                    // Same cover evaluated with 0% repetition penalty vs 80% repetition penalty
                    float freshScore = BambooHunterUtility.CalculateCoverScore(6f, 7f, 1.0f, 0.8f, 0.5f, 1.0f, 0f, settings);
                    float penalizedScore = BambooHunterUtility.CalculateCoverScore(6f, 7f, 1.0f, 0.8f, 0.5f, 1.0f, 0.8f, settings);

                    AssertTest(freshScore > penalizedScore, "Cover_Scoring_RepetitionPenaltyReducesScore", $"Fresh={freshScore}, Penalized={penalizedScore}");
                    AssertTest(Mathf.Approximately(penalizedScore, freshScore * 0.2f), "Cover_Scoring_PenaltyScalingAccurate");
                }

                // TEST 35: Deterministic Tie-Breaking
                {
                    float scoreA = BambooHunterUtility.CalculateCoverScore(5f, 5f, 1f, 1f, 0f, 1f, 0f, settings);
                    float scoreB = BambooHunterUtility.CalculateCoverScore(5f, 5f, 1f, 1f, 0f, 1f, 0f, settings);

                    AssertTest(scoreA == scoreB, "Cover_Scoring_DeterministicIdenticalInputs");
                }

                // TEST 36: No-Cover Fallback & Missing Anchors Handling
                {
                    var agentGo = new GameObject("TestAgent_NoCover");
                    var coverSystem = agentGo.AddComponent<BambooHunterCoverSystem>();

                    // Empty cover pool
                    bool result = coverSystem.EvaluateBestCover(null, null, settings, false);
                    AssertTest(!result, "Cover_NoCover_EvaluateReturnsFalseOnEmpty");
                    AssertTest(!coverSystem.HasValidCover, "Cover_NoCover_HasValidCoverFalse");
                    AssertTest(coverSystem.SelectedCover == null, "Cover_NoCover_SelectedCoverNull");

                    var ctx = new BambooHunterDecisionContext();
                    coverSystem.PopulateDecisionContext(ref ctx);
                    AssertTest(!ctx.hasReachableCover, "Cover_NoCover_ContextHasReachableCoverFalse");
                    AssertTest(!ctx.isAtCover, "Cover_NoCover_ContextIsAtCoverFalse");

                    UnityEngine.Object.DestroyImmediate(agentGo);
                }

                // TEST 37: Invalid Inputs & Bounded Robustness
                {
                    float nanScore = BambooHunterUtility.CalculateCoverScore(float.NaN, 0f, 1f, 1f, 0f, 1f, 0f, settings);
                    float infScore = BambooHunterUtility.CalculateCoverScore(0f, float.PositiveInfinity, 1f, 1f, 0f, 1f, 0f, settings);
                    float nullSettingsScore = BambooHunterUtility.CalculateCoverScore(5f, 5f, 1f, 1f, 0f, 1f, 0f, null);

                    AssertTest(!float.IsNaN(nanScore) && nanScore >= 0f, "Cover_Invalid_NanScoreHandledSafely");
                    AssertTest(!float.IsNaN(infScore) && infScore >= 0f, "Cover_Invalid_InfinityScoreHandledSafely");
                    AssertTest(!float.IsNaN(nullSettingsScore) && nullSettingsScore >= 0f, "Cover_Invalid_NullSettingsHandledSafely");
                }

                // =========================================================================
                // PART 5: BOW & ARROW COMBAT SYSTEM TESTS (TESTS 38 - 48)
                // =========================================================================

                // TEST 38: Bow Initialization & Quiver Capacity
                {
                    var bowGo = new GameObject("TestBowInit");
                    var bow = bowGo.AddComponent<BambooHunterBow>();
                    bow.Initialize(settings);

                    AssertTest(bow.CurrentArrows == settings.quiverCapacity, "Combat_Bow_InitCapacityMatchesSettings");
                    AssertTest(!bow.IsDrawing && bow.DrawProgress == 0f, "Combat_Bow_InitNotDrawing");
                    AssertTest(!bow.IsCooldownActive && bow.CooldownRemaining == 0f, "Combat_Bow_InitNoCooldown");

                    UnityEngine.Object.DestroyImmediate(bowGo);
                }

                // TEST 39: Draw Lifecycle & Progress Timing
                {
                    var bowGo = new GameObject("TestBowDraw");
                    var bow = bowGo.AddComponent<BambooHunterBow>();
                    bow.Initialize(settings);

                    var targetGo = new GameObject("TestDrawTarget");
                    targetGo.transform.position = new Vector3(0f, 0f, 10f);

                    bool beginResult = bow.BeginDraw(targetGo.transform);
                    AssertTest(beginResult && bow.IsDrawing, "Combat_Draw_BeginSucceeds");

                    // Advance draw by 0.5s (half of 1.0s duration)
                    bow.UpdateDraw(0.5f);
                    AssertTest(Mathf.Abs(bow.DrawProgress - 0.5f) < 0.05f, "Combat_Draw_ProgressAdvancesLinearly");
                    AssertTest(!bow.IsDrawComplete, "Combat_Draw_IncompleteAtHalfTension");

                    // Advance draw to completion (another 0.5s)
                    bow.UpdateDraw(0.5f);
                    AssertTest(bow.IsDrawComplete && bow.DrawProgress >= 0.99f, "Combat_Draw_CompleteAtThreshold");

                    // Cancel draw
                    bow.CancelDraw();
                    AssertTest(!bow.IsDrawing && bow.DrawProgress == 0f, "Combat_Draw_CancelResetsState");

                    UnityEngine.Object.DestroyImmediate(bowGo);
                    UnityEngine.Object.DestroyImmediate(targetGo);
                }

                // TEST 40: Aim Readiness & Shoot Prerequisites
                {
                    var bowGo = new GameObject("TestBowReady");
                    var bow = bowGo.AddComponent<BambooHunterBow>();
                    bow.Initialize(settings);

                    var targetGo = new GameObject("TestReadyTarget");
                    targetGo.transform.position = new Vector3(0f, 0f, 10f);

                    // Undrawn bow cannot shoot
                    AssertTest(!bow.CanShoot(targetGo.transform), "Combat_Ready_UndrawnCannotShoot");

                    // Half-drawn bow cannot shoot
                    bow.BeginDraw(targetGo.transform);
                    bow.UpdateDraw(0.5f);
                    AssertTest(!bow.CanShoot(targetGo.transform), "Combat_Ready_HalfDrawnCannotShoot");

                    // Fully-drawn bow can shoot
                    bow.UpdateDraw(0.5f);
                    AssertTest(bow.CanShoot(targetGo.transform), "Combat_Ready_FullDrawnCanShoot");

                    UnityEngine.Object.DestroyImmediate(bowGo);
                    UnityEngine.Object.DestroyImmediate(targetGo);
                }

                // TEST 41: Quiver Ammunition Depletion & Consecutive Shots
                {
                    var bowGo = new GameObject("TestBowAmmo");
                    var bow = bowGo.AddComponent<BambooHunterBow>();
                    bow.Initialize(settings);

                    var targetGo = new GameObject("TestAmmoTarget");
                    targetGo.transform.position = new Vector3(0f, 0f, 10f);

                    bow.BeginDraw(targetGo.transform);
                    bow.UpdateDraw(1.0f);

                    int startAmmo = bow.CurrentArrows;
                    bool shootResult = bow.TryShoot(targetGo.transform);

                    AssertTest(shootResult && bow.CurrentArrows == startAmmo - 1, "Combat_Ammo_ShotDecrementsQuiver");
                    AssertTest(bow.ConsecutiveShots == 1, "Combat_Ammo_ShotIncrementsConsecutive");

                    // Drain remaining arrows
                    while (bow.CurrentArrows > 0)
                    {
                        bow.UpdateDraw(settings.shotCooldownDuration + 0.1f);
                        bow.BeginDraw(targetGo.transform);
                        bow.UpdateDraw(1.0f);
                        bow.TryShoot(targetGo.transform);
                    }

                    AssertTest(bow.CurrentArrows == 0, "Combat_Ammo_QuiverFullyExhausted");
                    AssertTest(!bow.BeginDraw(targetGo.transform), "Combat_Ammo_EmptyQuiverRejectsDraw");
                    AssertTest(!bow.CanShoot(targetGo.transform), "Combat_Ammo_EmptyQuiverCannotShoot");

                    // Restock quiver
                    bow.RestockQuiver();
                    AssertTest(bow.CurrentArrows == settings.quiverCapacity, "Combat_Ammo_RestockRestoresCapacity");

                    UnityEngine.Object.DestroyImmediate(bowGo);
                    UnityEngine.Object.DestroyImmediate(targetGo);
                }

                // TEST 42: Shot Cooldown Enforcement
                {
                    var bowGo = new GameObject("TestBowCooldown");
                    var bow = bowGo.AddComponent<BambooHunterBow>();
                    bow.Initialize(settings);

                    var targetGo = new GameObject("TestCooldownTarget");
                    targetGo.transform.position = new Vector3(0f, 0f, 10f);

                    bow.BeginDraw(targetGo.transform);
                    bow.UpdateDraw(1.0f);
                    bow.TryShoot(targetGo.transform);

                    AssertTest(bow.IsCooldownActive && bow.CooldownRemaining > 0f, "Combat_Cooldown_ActiveAfterShot");
                    AssertTest(!bow.BeginDraw(targetGo.transform), "Combat_Cooldown_RejectsDrawDuringCooldown");

                    // Advance time beyond cooldown duration
                    bow.UpdateDraw(settings.shotCooldownDuration + 0.1f);
                    AssertTest(!bow.IsCooldownActive && bow.CooldownRemaining == 0f, "Combat_Cooldown_ExpiresAfterDuration");
                    AssertTest(bow.BeginDraw(targetGo.transform), "Combat_Cooldown_DrawAllowedAfterExpiry");

                    UnityEngine.Object.DestroyImmediate(bowGo);
                    UnityEngine.Object.DestroyImmediate(targetGo);
                }

                // TEST 43: Target Prerequisites (Missing, Dead, Out of Range)
                {
                    var bowGo = new GameObject("TestBowTargetPrereq");
                    var bow = bowGo.AddComponent<BambooHunterBow>();
                    bow.Initialize(settings);

                    // Null target
                    AssertTest(!bow.BeginDraw(null), "Combat_Target_NullTargetRejectsDraw");
                    AssertTest(!bow.CanShoot(null), "Combat_Target_NullTargetCannotShoot");

                    // Dead target
                    var deadTargetGo = new GameObject("DeadTarget");
                    deadTargetGo.transform.position = new Vector3(0f, 0f, 10f);
                    var deadAdapter = deadTargetGo.AddComponent<BambooHunterDamageAdapter>();
                    deadAdapter.SetDead(true);

                    AssertTest(!bow.BeginDraw(deadTargetGo.transform), "Combat_Target_DeadTargetRejectsDraw");
                    AssertTest(!bow.CanShoot(deadTargetGo.transform), "Combat_Target_DeadTargetCannotShoot");

                    // Out of range target (> 20m)
                    var farTargetGo = new GameObject("FarTarget");
                    farTargetGo.transform.position = new Vector3(0f, 0f, 50f);

                    bow.BeginDraw(farTargetGo.transform);
                    bow.UpdateDraw(1.0f);
                    AssertTest(!bow.CanShoot(farTargetGo.transform), "Combat_Target_OutOfRangeCannotShoot");

                    UnityEngine.Object.DestroyImmediate(bowGo);
                    UnityEngine.Object.DestroyImmediate(deadTargetGo);
                    UnityEngine.Object.DestroyImmediate(farTargetGo);
                }

                // TEST 44: Line of Fire Clearance & Arc Checking
                {
                    var bowGo = new GameObject("TestBowLOF");
                    var bow = bowGo.AddComponent<BambooHunterBow>();
                    bow.Initialize(settings);

                    Vector3 origin = new Vector3(0f, 1.3f, 0f);
                    Vector3 targetPos = new Vector3(0f, 1.0f, 12f);

                    // Unobstructed space
                    bool clearStraight = bow.CheckStraightLineOfFire(origin, targetPos, ~0);
                    bool clearArc = bow.CheckTrajectoryArcOfFire(origin, targetPos, 32f, 1.0f, ~0, 8);
                    AssertTest(clearStraight, "Combat_LineOfFire_ClearStraightAccepted");
                    AssertTest(clearArc, "Combat_LineOfFire_ClearArcAccepted");

                    // Obstructed sightline with physical obstacle
                    var obstacleGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    obstacleGo.transform.position = new Vector3(0f, 1.15f, 6f);
                    obstacleGo.transform.localScale = new Vector3(2f, 2f, 2f);
                    Physics.SyncTransforms();

                    bool blockedStraight = !bow.CheckStraightLineOfFire(origin, targetPos, ~0);
                    bool blockedArc = !bow.CheckTrajectoryArcOfFire(origin, targetPos, 32f, 1.0f, ~0, 8);
                    AssertTest(blockedStraight, "Combat_LineOfFire_ObstructedStraightRejected");
                    AssertTest(blockedArc, "Combat_LineOfFire_ObstructedArcRejected");

                    UnityEngine.Object.DestroyImmediate(bowGo);
                    UnityEngine.Object.DestroyImmediate(obstacleGo);
                }

                // TEST 45: Projectile Physics & Configuration
                {
                    var arrowGo = new GameObject("TestArrow");
                    var arrow = arrowGo.AddComponent<BambooHunterArrow>();
                    var shooterGo = new GameObject("TestShooter");

                    arrow.Launch(Vector3.zero, Vector3.forward, 32f, 18f, shooterGo, ~0, 1.0f, 5.0f);

                    AssertTest(Mathf.Approximately(arrow.Speed, 32f), "Combat_Arrow_LaunchSpeedSet");
                    AssertTest(Mathf.Approximately(arrow.Damage, 18f), "Combat_Arrow_LaunchDamageSet");
                    AssertTest(arrow.Shooter == shooterGo, "Combat_Arrow_ShooterAssigned");
                    AssertTest(!arrow.HasImpacted, "Combat_Arrow_NotImpactedInitially");

                    UnityEngine.Object.DestroyImmediate(arrowGo);
                    UnityEngine.Object.DestroyImmediate(shooterGo);
                }

                // TEST 46: Single-Hit & Duplicate Damage Prevention
                {
                    var arrowGo = new GameObject("TestArrowHit");
                    var arrow = arrowGo.AddComponent<BambooHunterArrow>();
                    var shooterGo = new GameObject("TestShooterHit");
                    var targetGo = new GameObject("TestTargetHit");
                    var col = targetGo.AddComponent<SphereCollider>();
                    var adapter = targetGo.AddComponent<BambooHunterDamageAdapter>();

                    float totalDmg = 0f;
                    adapter.onReceiveDamage.AddListener(dmg => totalDmg += dmg);

                    arrow.Launch(Vector3.zero, Vector3.forward, 32f, 18f, shooterGo);

                    // First hit delivers damage
                    arrow.ProcessHit(col, targetGo.transform.position);
                    AssertTest(arrow.HasImpacted, "Combat_Damage_ArrowImpactedFlagTrue");
                    AssertTest(Mathf.Approximately(totalDmg, 18f), "Combat_Damage_FirstHitDelivered");

                    // Duplicate hit callback ignored
                    arrow.ProcessHit(col, targetGo.transform.position);
                    AssertTest(Mathf.Approximately(totalDmg, 18f), "Combat_Damage_DuplicateHitIgnored");

                    // Self-hit with shooter ignored
                    var shooterCol = shooterGo.AddComponent<SphereCollider>();
                    var shooterAdapter = shooterGo.AddComponent<BambooHunterDamageAdapter>();
                    float shooterDmg = 0f;
                    shooterAdapter.onReceiveDamage.AddListener(dmg => shooterDmg += dmg);

                    var arrow2Go = new GameObject("TestArrowSelf");
                    var arrow2 = arrow2Go.AddComponent<BambooHunterArrow>();
                    arrow2.Launch(Vector3.zero, Vector3.forward, 32f, 18f, shooterGo);
                    arrow2.ProcessHit(shooterCol, shooterGo.transform.position);
                    AssertTest(!arrow2.HasImpacted, "Combat_Damage_SelfHitIgnoredImpactFlag");
                    AssertTest(Mathf.Approximately(shooterDmg, 0f), "Combat_Damage_SelfHitZeroDamage");

                    UnityEngine.Object.DestroyImmediate(arrowGo);
                    UnityEngine.Object.DestroyImmediate(arrow2Go);
                    UnityEngine.Object.DestroyImmediate(shooterGo);
                    UnityEngine.Object.DestroyImmediate(targetGo);
                }

                // TEST 47: Damage Adapter Interoperability & Dead State
                {
                    var targetGo = new GameObject("TestAdapterInteroperability");
                    var adapter = targetGo.AddComponent<BambooHunterDamageAdapter>();
                    float damageReceived = 0f;
                    adapter.onReceiveDamage.AddListener(d => damageReceived += d);

                    bool applied = BambooHunterDamageAdapter.TryApplyDamage(targetGo, 15f);
                    AssertTest(applied, "Combat_Adapter_TryApplyDamageSucceeds");
                    AssertTest(Mathf.Approximately(damageReceived, 15f), "Combat_Adapter_DamageAmountAccurate");

                    // Mark dead
                    adapter.SetDead(true);
                    AssertTest(adapter.IsDead, "Combat_Adapter_IsDeadTrue");

                    bool appliedWhenDead = BambooHunterDamageAdapter.TryApplyDamage(targetGo, 20f);
                    AssertTest(!appliedWhenDead, "Combat_Adapter_RejectedWhenDead");
                    AssertTest(Mathf.Approximately(damageReceived, 15f), "Combat_Adapter_NoDamageWhenDead");

                    UnityEngine.Object.DestroyImmediate(targetGo);
                }

                // TEST 48: Invalid Inputs & Context Mapping Robustness
                {
                    var arrowGo = new GameObject("TestInvalidArrow");
                    var arrow = arrowGo.AddComponent<BambooHunterArrow>();
                    arrow.Launch(Vector3.zero, Vector3.zero, float.NaN, float.PositiveInfinity, null);

                    AssertTest(!float.IsNaN(arrow.Speed) && arrow.Speed > 0f, "Combat_Invalid_NanSpeedSanitized");
                    AssertTest(!float.IsNaN(arrow.Damage) && arrow.Damage > 0f, "Combat_Invalid_NanDamageSanitized");

                    var bowGo = new GameObject("TestBowContext");
                    var bow = bowGo.AddComponent<BambooHunterBow>();
                    bow.Initialize(settings);

                    bow.UpdateDraw(float.NaN);
                    bow.UpdateDraw(-5f);
                    AssertTest(!float.IsNaN(bow.DrawProgress) && bow.DrawProgress == 0f, "Combat_Invalid_NanDrawIgnored");

                    bool appliedNan = BambooHunterDamageAdapter.TryApplyDamage(bowGo, float.NaN);
                    AssertTest(!appliedNan, "Combat_Invalid_NanDamagePayloadRejected");

                    var ctx = new BambooHunterDecisionContext();
                    bow.PopulateDecisionContext(ref ctx);
                    AssertTest(ctx.currentArrows == bow.CurrentArrows, "Combat_Context_ArrowsAccurate");
                    AssertTest(ctx.isBowDrawn == bow.IsDrawing, "Combat_Context_IsBowDrawnAccurate");
                    AssertTest(ctx.bowDrawProgress == bow.DrawProgress, "Combat_Context_DrawProgressAccurate");
                    AssertTest(ctx.consecutiveShotsFired == bow.ConsecutiveShots, "Combat_Context_ConsecutiveAccurate");

                    UnityEngine.Object.DestroyImmediate(arrowGo);
                    UnityEngine.Object.DestroyImmediate(bowGo);
                }

                // =========================================================================
                // PART 6: HEALTH, DAMAGE RECEPTION, DEATH & SURVIVAL TESTS (TESTS 49 - 56)
                // =========================================================================

                // TEST 49: Health Initialization & Ratio Calculation
                {
                    var healthGo = new GameObject("TestHealthInit");
                    var health = healthGo.AddComponent<BambooHunterHealth>();
                    health.Initialize(settings);

                    AssertTest(Mathf.Approximately(health.CurrentHealth, settings.maxHealth), "Health_Init_CurrentHealthMatchesSettings");
                    AssertTest(Mathf.Approximately(health.HealthRatio, 1.0f), "Health_Init_HealthRatioIsOne");
                    AssertTest(!health.IsDead && !health.IsWounded, "Health_Init_NotDeadNotWounded");

                    UnityEngine.Object.DestroyImmediate(healthGo);
                }

                // TEST 50: Damage Reception & Health Clamping
                {
                    var healthGo = new GameObject("TestHealthDamage");
                    var health = healthGo.AddComponent<BambooHunterHealth>();
                    health.Initialize(settings);

                    health.TakeDamage(30f, 10f);
                    AssertTest(Mathf.Approximately(health.CurrentHealth, 90f), "Health_Damage_ReducesCurrentHealth");
                    AssertTest(Mathf.Approximately(health.HealthRatio, 90f / 120f), "Health_Damage_RatioReflectsDamage");

                    // Overkill damage
                    health.TakeDamage(150f, 12f);
                    AssertTest(health.CurrentHealth == 0f, "Health_Damage_ClampedAtZero");
                    AssertTest(health.IsDead, "Health_Damage_DeadWhenZero");

                    UnityEngine.Object.DestroyImmediate(healthGo);
                }

                // TEST 51: Death Event Uniqueness & Post-Death Invulnerability
                {
                    var healthGo = new GameObject("TestHealthDeath");
                    var health = healthGo.AddComponent<BambooHunterHealth>();
                    health.Initialize(settings);

                    int deathCount = 0;
                    health.OnDied += () => deathCount++;

                    // Lethal hit
                    health.TakeDamage(120f, 10f);
                    AssertTest(deathCount == 1, "Health_Death_EventFiresOnZero");

                    // Subsequent hits on dead agent
                    health.TakeDamage(50f, 15f);
                    AssertTest(deathCount == 1, "Health_Death_EventFiresExactlyOnce");
                    AssertTest(health.CurrentHealth == 0f, "Health_Death_InvulnerableAfterDeath");

                    UnityEngine.Object.DestroyImmediate(healthGo);
                }

                // TEST 52: Healing & Overheal Clamping
                {
                    var healthGo = new GameObject("TestHealthHeal");
                    var health = healthGo.AddComponent<BambooHunterHealth>();
                    health.Initialize(settings, 50f);

                    health.Heal(30f);
                    AssertTest(Mathf.Approximately(health.CurrentHealth, 80f), "Health_Heal_RestoresHealthPoints");

                    // Overheal
                    health.Heal(100f);
                    AssertTest(Mathf.Approximately(health.CurrentHealth, settings.maxHealth), "Health_Heal_ClampedAtMaxHealth");

                    // Dead agent cannot be revived
                    health.TakeDamage(settings.maxHealth, 20f);
                    health.Heal(50f);
                    AssertTest(health.IsDead && health.CurrentHealth == 0f, "Health_Heal_CannotReviveDeadAgent");

                    UnityEngine.Object.DestroyImmediate(healthGo);
                }

                // TEST 53: Defensive Retreat Threshold & Concealed Regeneration
                {
                    var healthGo = new GameObject("TestHealthRegen");
                    var health = healthGo.AddComponent<BambooHunterHealth>();
                    health.Initialize(settings, 25f);

                    AssertTest(health.IsInRetreatThreshold, "Health_Regen_RetreatThresholdDetected");

                    health.TakeDamage(5f, 100f); // HP = 20, lastDamageTime = 100f

                    // Attempt regen during damage cooldown (< 3.5s)
                    health.UpdateRegeneration(1.0f, true, 102f);
                    AssertTest(Mathf.Approximately(health.CurrentHealth, 20f), "Health_Regen_DelayEnforcedBeforeRegen");

                    // Attempt regen when not concealed
                    health.UpdateRegeneration(1.0f, false, 105f);
                    AssertTest(Mathf.Approximately(health.CurrentHealth, 20f), "Health_Regen_RequiresConcealment");

                    // Valid concealed regen
                    health.UpdateRegeneration(2.0f, true, 105f);
                    AssertTest(health.CurrentHealth > 20f, "Health_Regen_AdvancesWhileConcealed");

                    // Fast-forward regen to cap (60 HP = 50% of 120 HP)
                    for (int i = 0; i < 30; i++)
                    {
                        health.UpdateRegeneration(1.0f, true, 110f + i);
                    }
                    float capHealth = settings.maxHealth * settings.recoveryTarget;
                    AssertTest(Mathf.Approximately(health.CurrentHealth, capHealth) && health.IsRecoveryComplete, "Health_Regen_CapsAtRecoveryTarget");

                    UnityEngine.Object.DestroyImmediate(healthGo);
                }

                // TEST 54: Invalid Damage & Numerical Robustness
                {
                    var healthGo = new GameObject("TestHealthInvalid");
                    var health = healthGo.AddComponent<BambooHunterHealth>();
                    health.Initialize(settings);
                    float startHealth = health.CurrentHealth;

                    health.TakeDamage(float.NaN);
                    AssertTest(Mathf.Approximately(health.CurrentHealth, startHealth), "Health_Invalid_NanDamageIgnored");

                    health.TakeDamage(float.PositiveInfinity);
                    AssertTest(Mathf.Approximately(health.CurrentHealth, startHealth), "Health_Invalid_InfinityDamageIgnored");

                    health.TakeDamage(-25f);
                    AssertTest(Mathf.Approximately(health.CurrentHealth, startHealth), "Health_Invalid_NegativeDamageIgnored");

                    health.Heal(float.NaN);
                    AssertTest(Mathf.Approximately(health.CurrentHealth, startHealth), "Health_Invalid_NanHealIgnored");

                    UnityEngine.Object.DestroyImmediate(healthGo);
                }

                // TEST 55: Damage Adapter & Demo Interoperability
                {
                    var healthGo = new GameObject("TestHealthAdapter");
                    var health = healthGo.AddComponent<BambooHunterHealth>();
                    health.Initialize(settings);

                    bool applied = BambooHunterDamageAdapter.TryApplyDamage(healthGo, 20f);
                    AssertTest(applied && Mathf.Approximately(health.CurrentHealth, 100f), "Health_Adapter_TryApplyDamageWorksWithHealth");

                    var demoGo = new GameObject("TestDemoReceiver");
                    var demo = demoGo.AddComponent<BambooHunterPlayerHealthDemo>();

                    bool demoApplied = BambooHunterDamageAdapter.TryApplyDamage(demoGo, 35f);
                    AssertTest(demoApplied && Mathf.Approximately(demo.CurrentHealth, 65f), "Health_Adapter_TryApplyDamageWorksWithDemo");

                    BambooHunterDamageAdapter.TryApplyDamage(demoGo, 100f);
                    AssertTest(demo.CurrentHealth == 0f, "Health_Demo_DamageClampedAtZero");
                    AssertTest(demo.IsDead, "Health_Demo_IsDeadTrueOnZero");

                    UnityEngine.Object.DestroyImmediate(healthGo);
                    UnityEngine.Object.DestroyImmediate(demoGo);
                }

                // TEST 56: Combat Halting & Decision Context Population
                {
                    var hunterGo = new GameObject("TestHunterDeathCombat");
                    var health = hunterGo.AddComponent<BambooHunterHealth>();
                    health.Initialize(settings);
                    var bow = hunterGo.AddComponent<BambooHunterBow>();
                    bow.Initialize(settings);

                    var targetGo = new GameObject("TestCombatTarget");
                    targetGo.transform.position = new Vector3(0f, 0f, 10f);

                    // Ready initially
                    AssertTest(bow.BeginDraw(targetGo.transform), "Health_Combat_BeginDrawAllowedWhenAlive");
                    bow.CancelDraw();

                    // Lethal hit
                    health.TakeDamage(settings.maxHealth, 10f);

                    // Blocked when dead
                    AssertTest(!bow.BeginDraw(targetGo.transform), "Health_Combat_BeginDrawBlockedWhenDead");
                    AssertTest(!bow.CanShoot(targetGo.transform), "Health_Combat_CanShootBlockedWhenDead");

                    var ctx = new BambooHunterDecisionContext();
                    health.PopulateDecisionContext(ref ctx);
                    AssertTest(ctx.isDead && ctx.currentHealth == 0f, "Health_Context_IsDeadPopulated");
                    AssertTest(ctx.HealthRatio == 0f, "Health_Context_HealthRatioZeroWhenDead");

                    UnityEngine.Object.DestroyImmediate(hunterGo);
                    UnityEngine.Object.DestroyImmediate(targetGo);
                }

                // =========================================================================
                // PART 7: HYBRID UTILITY AI + FSM BRAIN COORDINATOR TESTS (TESTS 57 - 66)
                // =========================================================================

                // TEST 57: Brain Initialization & Default Tactical State
                {
                    var brainGo = new GameObject("TestBrainInit");
                    var brain = brainGo.AddComponent<BambooHunterBrain>();
                    brain.Initialize(settings);

                    AssertTest(brain.CurrentState == BambooHunterState.Patrol, "Brain_Init_StateIsPatrol");
                    AssertTest(brain.CurrentIntent == BambooHunterTacticalIntent.PatrolGrove, "Brain_Init_IntentIsPatrolGrove");
                    AssertTest(brain.StateElapsedTime == 0f, "Brain_Init_ElapsedTimeZero");

                    UnityEngine.Object.DestroyImmediate(brainGo);
                }

                // TEST 58: Patrol-to-Aim Transition on Player Visibility
                {
                    var brainGo = new GameObject("TestBrainAim");
                    var perception = brainGo.AddComponent<BambooHunterPerception>();
                    var bow = brainGo.AddComponent<BambooHunterBow>();
                    var health = brainGo.AddComponent<BambooHunterHealth>();
                    var brain = brainGo.AddComponent<BambooHunterBrain>();
                    brain.Initialize(settings, perception, null, null, bow, health);

                    var targetGo = new GameObject("PlayerTarget");
                    targetGo.transform.position = brainGo.transform.position + brainGo.transform.forward * 10f;
                    perception.Target = targetGo.transform;

                    // Advance brain tick past thinkInterval
                    brain.Tick(settings.thinkInterval + 0.05f);

                    AssertTest(brain.CurrentState == BambooHunterState.Aim, "Brain_Transition_PatrolToAimOnVisibleTarget");
                    AssertTest(brain.CurrentIntent == BambooHunterTacticalIntent.PrepareShot, "Brain_Transition_AimIntentSet");
                    AssertTest(bow.IsDrawing, "Brain_Transition_BowDrawBegunInAim");

                    UnityEngine.Object.DestroyImmediate(brainGo);
                    UnityEngine.Object.DestroyImmediate(targetGo);
                }

                // TEST 59: Aim-to-Shoot Transition on Full Draw Tension
                {
                    var brainGo = new GameObject("TestBrainShoot");
                    var perception = brainGo.AddComponent<BambooHunterPerception>();
                    var bow = brainGo.AddComponent<BambooHunterBow>();
                    var health = brainGo.AddComponent<BambooHunterHealth>();
                    var brain = brainGo.AddComponent<BambooHunterBrain>();
                    brain.Initialize(settings, perception, null, null, bow, health);

                    var targetGo = new GameObject("PlayerShootTarget");
                    targetGo.transform.position = brainGo.transform.position + brainGo.transform.forward * 10f;
                    perception.Target = targetGo.transform;

                    // Initial tick enters Aim
                    brain.Tick(settings.thinkInterval + 0.05f);

                    // Advance draw tension to completion (1.0s)
                    brain.Tick(settings.bowDrawDuration + 0.1f);

                    // Trigger decision cycle to arbitrate Shoot
                    brain.RunDecisionCycle();

                    AssertTest(bow.ConsecutiveShots >= 1, "Brain_Transition_ShotExecutedIncrementsConsecutive");
                    AssertTest(bow.CurrentArrows == settings.quiverCapacity - 1, "Brain_Transition_AmmoExpendedOnShoot");
                    AssertTest(brain.CurrentState == BambooHunterState.Shoot || brain.CurrentState == BambooHunterState.Reposition || brain.CurrentState == BambooHunterState.Aim, "Brain_Transition_AimToShootTransitionHandled");

                    UnityEngine.Object.DestroyImmediate(brainGo);
                    UnityEngine.Object.DestroyImmediate(targetGo);
                }

                // TEST 60: Post-Shot Repositioning Transition
                {
                    var brainGo = new GameObject("TestBrainReposition");
                    var perception = brainGo.AddComponent<BambooHunterPerception>();
                    var bow = brainGo.AddComponent<BambooHunterBow>();
                    var health = brainGo.AddComponent<BambooHunterHealth>();
                    var coverSystem = brainGo.AddComponent<BambooHunterCoverSystem>();
                    var brain = brainGo.AddComponent<BambooHunterBrain>();
                    brain.Initialize(settings, perception, null, coverSystem, bow, health);

                    var targetGo = new GameObject("PlayerRepoTarget");
                    targetGo.transform.position = brainGo.transform.position + brainGo.transform.forward * 10f;
                    perception.Target = targetGo.transform;

                    var cpGo = new GameObject("CoverAnchorRepo");
                    cpGo.transform.position = new Vector3(3f, 0f, 3f);
                    var cp = cpGo.AddComponent<BambooHunterCoverPoint>();
                    coverSystem.RegisterCoverPoint(cp);

                    // Force consecutive shots to maxShotsBeforeReposition (2) by completing full bow draw cycle
                    brain.TransitionTo(BambooHunterState.Aim);
                    for (int i = 0; i < settings.maxShotsBeforeReposition; i++)
                    {
                        bow.UpdateDraw(settings.shotCooldownDuration + 0.1f);
                        bow.BeginDraw(targetGo.transform);
                        bow.UpdateDraw(settings.bowDrawDuration + 0.1f);
                        bow.TryShoot(targetGo.transform);
                    }

                    // Advance dwell time past minimumStateDuration (0.5s)
                    brain.Tick(settings.minimumStateDuration + 0.1f);
                    brain.RunDecisionCycle();

                    AssertTest(brain.CurrentState == BambooHunterState.Reposition, "Brain_Transition_RepositionAfterShot");
                    AssertTest(brain.CurrentIntent == BambooHunterTacticalIntent.RelocateToVegetationCover, "Brain_Transition_RepositionIntentSet");

                    UnityEngine.Object.DestroyImmediate(brainGo);
                    UnityEngine.Object.DestroyImmediate(targetGo);
                    UnityEngine.Object.DestroyImmediate(cpGo);
                }

                // TEST 61: Lost-Target to Investigate Transition
                {
                    var brainGo = new GameObject("TestBrainInvestigate");
                    var perception = brainGo.AddComponent<BambooHunterPerception>();
                    var bow = brainGo.AddComponent<BambooHunterBow>();
                    var health = brainGo.AddComponent<BambooHunterHealth>();
                    var brain = brainGo.AddComponent<BambooHunterBrain>();
                    brain.Initialize(settings, perception, null, null, bow, health);

                    var targetGo = new GameObject("PlayerLostTarget");
                    // Place target outside FOV (behind the agent) so perception.Sample will not have direct visibility
                    targetGo.transform.position = brainGo.transform.position - brainGo.transform.forward * 15f;
                    perception.Target = targetGo.transform;

                    // Set target memory as recently seen ahead, but currently invisible
                    Vector3 lastKnownAhead = brainGo.transform.position + brainGo.transform.forward * 10f;
                    perception.SetLastSeenMemory(lastKnownAhead, Time.time);

                    // Advance dwell time past minimumStateDuration and arbitrate
                    brain.Tick(settings.minimumStateDuration + 0.1f);
                    brain.RunDecisionCycle();

                    AssertTest(brain.CurrentState == BambooHunterState.Investigate, "Brain_Transition_LostTargetSwitchesToInvestigate");
                    AssertTest(brain.CurrentIntent == BambooHunterTacticalIntent.InvestigateLastSeen, "Brain_Transition_InvestigateIntentSet");

                    UnityEngine.Object.DestroyImmediate(brainGo);
                    UnityEngine.Object.DestroyImmediate(targetGo);
                }

                // TEST 62: Low-Health Emergency Retreat Transition
                {
                    var brainGo = new GameObject("TestBrainRetreat");
                    var perception = brainGo.AddComponent<BambooHunterPerception>();
                    var bow = brainGo.AddComponent<BambooHunterBow>();
                    var health = brainGo.AddComponent<BambooHunterHealth>();
                    var coverSystem = brainGo.AddComponent<BambooHunterCoverSystem>();
                    var brain = brainGo.AddComponent<BambooHunterBrain>();
                    brain.Initialize(settings, perception, null, coverSystem, bow, health);

                    var targetGo = new GameObject("PlayerThreatRetreat");
                    targetGo.transform.position = brainGo.transform.position + brainGo.transform.forward * 10f;
                    perception.Target = targetGo.transform;
                    perception.Sample(settings);

                    var cpGo = new GameObject("CoverRetreat");
                    cpGo.transform.position = new Vector3(-5f, 0f, 0f);
                    var cp = cpGo.AddComponent<BambooHunterCoverPoint>();
                    coverSystem.RegisterCoverPoint(cp);

                    // Initial state is Aim
                    brain.TransitionTo(BambooHunterState.Aim);

                    // Inflict critical damage bringing health below retreatThreshold (25% = 30 HP)
                    health.TakeDamage(settings.maxHealth - 20f); // Remaining: 20 HP (16.7%)

                    brain.RunDecisionCycle();

                    AssertTest(brain.CurrentState == BambooHunterState.Retreat, "Brain_Transition_LowHealthTriggersRetreat");
                    AssertTest(brain.CurrentIntent == BambooHunterTacticalIntent.EmergencyRetreat, "Brain_Transition_RetreatIntentSet");

                    UnityEngine.Object.DestroyImmediate(brainGo);
                    UnityEngine.Object.DestroyImmediate(targetGo);
                    UnityEngine.Object.DestroyImmediate(cpGo);
                }

                // TEST 63: Death Terminal Lock
                {
                    var brainGo = new GameObject("TestBrainDead");
                    var bow = brainGo.AddComponent<BambooHunterBow>();
                    var health = brainGo.AddComponent<BambooHunterHealth>();
                    var brain = brainGo.AddComponent<BambooHunterBrain>();
                    brain.Initialize(settings, null, null, null, bow, health);

                    // Inflict lethal damage
                    health.TakeDamage(settings.maxHealth);

                    AssertTest(brain.CurrentState == BambooHunterState.Dead, "Brain_Death_SwitchesToDeadState");
                    AssertTest(brain.CurrentIntent == BambooHunterTacticalIntent.CeaseActions, "Brain_Death_IntentCeaseActions");

                    // Attempt unauthorized transition away from Dead
                    brain.TransitionTo(BambooHunterState.Patrol);
                    AssertTest(brain.CurrentState == BambooHunterState.Dead, "Brain_Death_TerminalLockRejectsTransitions");

                    brain.Tick(1.0f);
                    AssertTest(brain.CurrentState == BambooHunterState.Dead, "Brain_Death_TickDoesNotEscapeDead");

                    UnityEngine.Object.DestroyImmediate(brainGo);
                }

                // TEST 64: No-Cover Fallback Handling
                {
                    var brainGo = new GameObject("TestBrainNoCover");
                    var coverSystem = brainGo.AddComponent<BambooHunterCoverSystem>();
                    var brain = brainGo.AddComponent<BambooHunterBrain>();
                    brain.Initialize(settings, null, null, coverSystem, null, null);

                    AssertTest(coverSystem.CoverCount == 0, "Brain_Fallback_CoverCountIsZeroInitially");

                    // Without cover points, decision cycle defaults safely without exception
                    brain.RunDecisionCycle();
                    AssertTest(brain.CurrentState == BambooHunterState.Patrol, "Brain_Fallback_NoCoverDefaultsToPatrol");

                    UnityEngine.Object.DestroyImmediate(brainGo);
                }

                // TEST 65: No-Ammo Behaviour Blocks Aim and Shoot
                {
                    var brainGo = new GameObject("TestBrainNoAmmo");
                    var perception = brainGo.AddComponent<BambooHunterPerception>();
                    var bow = brainGo.AddComponent<BambooHunterBow>();
                    var health = brainGo.AddComponent<BambooHunterHealth>();
                    var brain = brainGo.AddComponent<BambooHunterBrain>();
                    brain.Initialize(settings, perception, null, null, bow, health);

                    var targetGo = new GameObject("PlayerNoAmmoTarget");
                    targetGo.transform.position = brainGo.transform.position + brainGo.transform.forward * 10f;
                    perception.Target = targetGo.transform;

                    // Drain all arrows
                    bow.RestockQuiver(0);

                    brain.Tick(settings.thinkInterval + 0.05f);

                    AssertTest(brain.CurrentState != BambooHunterState.Aim && brain.CurrentState != BambooHunterState.Shoot, "Brain_Ammo_NoAmmoBlocksAimAndShoot");

                    UnityEngine.Object.DestroyImmediate(brainGo);
                    UnityEngine.Object.DestroyImmediate(targetGo);
                }

                // TEST 66: Hysteresis Margin & Minimum Dwell Time Stability
                {
                    var brainGo = new GameObject("TestBrainHysteresis");
                    var brain = brainGo.AddComponent<BambooHunterBrain>();
                    brain.Initialize(settings);

                    // In Patrol, dwell time is protected
                    brain.Tick(settings.minimumStateDuration * 0.5f);
                    AssertTest(brain.StateElapsedTime < settings.minimumStateDuration, "Brain_DwellTime_ElapsedTimeUnderThreshold");

                    // Small score improvement below switchMargin (0.08) does not cause thrashing
                    var ctx = new BambooHunterDecisionContext { currentArrows = 6, currentHealth = 120f, maxHealth = 120f };
                    BambooHunterState chosen = BambooHunterUtility.Choose(ctx, BambooHunterState.Patrol, 0.1f, settings);
                    AssertTest(chosen == BambooHunterState.Patrol, "Brain_Hysteresis_PreventsThrashingUnderMargin");

                    UnityEngine.Object.DestroyImmediate(brainGo);
                }

                // =========================================================================
                // PART 8: PREFAB, SETTINGS, & SCENE ASSET VERIFICATION (TESTS 67 - 76)
                // =========================================================================

                // TEST 67: Settings Asset Validated
                {
                    var loadedSettings = AssetDatabase.LoadAssetAtPath<BambooHunterSettings>(BambooHunterAssetBuilder.SettingsPath);
                    AssertTest(loadedSettings != null, "Asset_Settings_ExistsAndLoads");
                    AssertTest(loadedSettings != null && loadedSettings.attackRange == 20f && loadedSettings.maxHealth == 120f, "Asset_Settings_ValuesMatchExpectedDefaults");
                    AssertTest(loadedSettings != null && loadedSettings.quiverCapacity == 6 && loadedSettings.maxShotsBeforeReposition == 2, "Asset_Settings_CombatRulesConfigured");
                }

                // TEST 68: Physical Ballistic Arrow Prefab Validated
                {
                    GameObject arrowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BambooHunterAssetBuilder.ArrowPrefabPath);
                    bool wasLoadedFromContents = false;
                    if (arrowPrefab == null && File.Exists(BambooHunterAssetBuilder.ArrowPrefabPath))
                    {
                        arrowPrefab = PrefabUtility.LoadPrefabContents(BambooHunterAssetBuilder.ArrowPrefabPath);
                        wasLoadedFromContents = true;
                    }

                    AssertTest(arrowPrefab != null, "Asset_Arrow_PrefabExists");

                    var arrowRb = arrowPrefab != null ? arrowPrefab.GetComponent<Rigidbody>() : null;
                    AssertTest(arrowRb != null && arrowRb.collisionDetectionMode == CollisionDetectionMode.ContinuousDynamic, "Asset_Arrow_RigidbodyContinuousDynamic");

                    var arrowCol = arrowPrefab != null ? arrowPrefab.GetComponent<Collider>() : null;
                    AssertTest(arrowCol != null && !arrowCol.isTrigger, "Asset_Arrow_ColliderValidSolid");

                    var arrowComp = arrowPrefab != null ? arrowPrefab.GetComponent<BambooHunterArrow>() : null;
                    AssertTest(arrowComp != null && arrowComp.Speed > 0f && arrowComp.Damage > 0f, "Asset_Arrow_ComponentConfigured");

                    if (wasLoadedFromContents && arrowPrefab != null)
                    {
                        PrefabUtility.UnloadPrefabContents(arrowPrefab);
                    }
                }

                // TEST 69: Bamboo Hunter Enemy Prefab & All Components Validated
                {
                    GameObject enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BambooHunterAssetBuilder.EnemyPrefabPath);
                    bool wasLoadedFromContents = false;
                    if (enemyPrefab == null && File.Exists(BambooHunterAssetBuilder.EnemyPrefabPath))
                    {
                        enemyPrefab = PrefabUtility.LoadPrefabContents(BambooHunterAssetBuilder.EnemyPrefabPath);
                        wasLoadedFromContents = true;
                    }

                    AssertTest(enemyPrefab != null, "Asset_Enemy_PrefabExists");
                    AssertTest(enemyPrefab != null && enemyPrefab.GetComponent<CapsuleCollider>() != null, "Asset_Enemy_CapsuleColliderPresent");
                    AssertTest(enemyPrefab != null && enemyPrefab.GetComponent<NavMeshAgent>() != null, "Asset_Enemy_NavMeshAgentPresent");
                    AssertTest(enemyPrefab != null && enemyPrefab.GetComponent<BambooHunterBrain>() != null, "Asset_Enemy_BrainPresent");
                    AssertTest(enemyPrefab != null && enemyPrefab.GetComponent<BambooHunterHealth>() != null, "Asset_Enemy_HealthPresent");
                    AssertTest(enemyPrefab != null && enemyPrefab.GetComponent<BambooHunterPerception>() != null, "Asset_Enemy_PerceptionPresent");
                    AssertTest(enemyPrefab != null && enemyPrefab.GetComponent<BambooHunterNavigation>() != null, "Asset_Enemy_NavigationPresent");
                    AssertTest(enemyPrefab != null && enemyPrefab.GetComponent<BambooHunterCoverSystem>() != null, "Asset_Enemy_CoverSystemPresent");
                    AssertTest(enemyPrefab != null && enemyPrefab.GetComponent<BambooHunterBow>() != null, "Asset_Enemy_BowPresent");

                    if (wasLoadedFromContents && enemyPrefab != null)
                    {
                        PrefabUtility.UnloadPrefabContents(enemyPrefab);
                    }
                }

                // TEST 70: Enemy Prefab Reference Wiring Validated
                {
                    GameObject enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BambooHunterAssetBuilder.EnemyPrefabPath);
                    bool wasLoadedFromContents = false;
                    if (enemyPrefab == null && File.Exists(BambooHunterAssetBuilder.EnemyPrefabPath))
                    {
                        enemyPrefab = PrefabUtility.LoadPrefabContents(BambooHunterAssetBuilder.EnemyPrefabPath);
                        wasLoadedFromContents = true;
                    }

                    var bow = enemyPrefab != null ? enemyPrefab.GetComponent<BambooHunterBow>() : null;
                    bool isArrowWired = (bow != null && bow.ArrowPrefab != null);
                    if (!isArrowWired && File.Exists(BambooHunterAssetBuilder.EnemyPrefabPath))
                    {
                        string arrowGuid = AssetDatabase.AssetPathToGUID(BambooHunterAssetBuilder.ArrowPrefabPath);
                        if (!string.IsNullOrEmpty(arrowGuid) && File.ReadAllText(BambooHunterAssetBuilder.EnemyPrefabPath).Contains(arrowGuid))
                        {
                            isArrowWired = true;
                        }
                    }
                    AssertTest(isArrowWired, "Asset_Enemy_BowArrowPrefabWired");
                    AssertTest(bow != null && bow.LaunchAnchor != null, "Asset_Enemy_BowLaunchAnchorWired");

                    var perc = enemyPrefab != null ? enemyPrefab.GetComponent<BambooHunterPerception>() : null;
                    AssertTest(perc != null && perc.EyeAnchor != null && perc.BowAnchor != null, "Asset_Enemy_PerceptionAnchorsWired");

                    if (wasLoadedFromContents && enemyPrefab != null)
                    {
                        PrefabUtility.UnloadPrefabContents(enemyPrefab);
                    }
                }

                // TEST 71: Demo Scene Asset Exists
                {
                    var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(BambooHunterAssetBuilder.ScenePath);
                    AssertTest(sceneAsset != null, "Asset_Scene_AssetExists");
                }

                // TEST 72: Demo Scene Structure Validated
                {
                    var currentScene = EditorSceneManager.GetActiveScene();
                    bool sceneOpen = currentScene.IsValid() && currentScene.path == BambooHunterAssetBuilder.ScenePath;
                    if (!sceneOpen)
                    {
                        currentScene = EditorSceneManager.OpenScene(BambooHunterAssetBuilder.ScenePath, OpenSceneMode.Single);
                    }

                    AssertTest(currentScene.IsValid() && currentScene.name == "BambooHunterDemoScene", "Scene_Structure_SceneOpenedSuccessfully");

                    var ground = GameObject.Find("GroundWalkableForest");
                    AssertTest(ground != null && ground.GetComponent<Collider>() != null, "Scene_Structure_GroundAndColliderPresent");

                    var wpParent = GameObject.Find("PatrolWaypoints");
                    AssertTest(wpParent != null && wpParent.transform.childCount >= 4, "Scene_Structure_PatrolWaypointsPresent");

                    var coverParent = GameObject.Find("TacticalCoverSystem");
                    AssertTest(coverParent != null && coverParent.transform.childCount >= 3, "Scene_Structure_CoverPointsPresent");
                }

                // TEST 73: Demo Scene Cover Physical Occluders Validated
                {
                    var coverParent = GameObject.Find("TacticalCoverSystem");
                    var coverPoints = coverParent != null ? coverParent.GetComponentsInChildren<BambooHunterCoverPoint>() : new BambooHunterCoverPoint[0];
                    bool allHaveOccluders = coverPoints.Length >= 3;
                    foreach (var cp in coverPoints)
                    {
                        if (cp.transform.childCount < 3) allHaveOccluders = false;
                    }
                    AssertTest(allHaveOccluders, "Scene_Cover_PhysicalOccludersPresentOnAllAnchors");
                }

                // TEST 74: Demo Scene Player Dummy Validated
                {
                    var dummy = GameObject.Find("PlayerTestDummy");
                    AssertTest(dummy != null && dummy.CompareTag("Player"), "Scene_Player_DummyPresentWithTag");
                    AssertTest(dummy != null && dummy.GetComponent<BambooHunterPlayerHealthDemo>() != null, "Scene_Player_HealthDemoPresent");
                    AssertTest(dummy != null && dummy.GetComponent<Collider>() != null, "Scene_Player_ColliderPresent");
                }

                // TEST 75: Demo Scene Management HUD & Harness Validated
                {
                    var manager = GameObject.Find("BambooHunterDemoManager");
                    AssertTest(manager != null, "Scene_Manager_GameObjectPresent");
                    AssertTest(manager != null && manager.GetComponent<BambooHunterDemoHUD>() != null, "Scene_Manager_HUDPresent");
                    AssertTest(manager != null && manager.GetComponent<BambooHunterRuntimeHarness>() != null, "Scene_Manager_RuntimeHarnessPresent");
                }

                // TEST 76: Demo Scene Bamboo Hunter Enemy Instance & Components Integrity Validated
                {
                    var enemyGo = GameObject.Find("BambooHunterEnemy");
                    AssertTest(enemyGo != null, "Scene_Enemy_InstancePresent");

                    var agent = enemyGo != null ? enemyGo.GetComponent<NavMeshAgent>() : null;
                    AssertTest(agent != null, "Scene_Enemy_NavMeshAgentPresent");

                    var col = enemyGo != null ? enemyGo.GetComponent<CapsuleCollider>() : null;
                    AssertTest(col != null, "Scene_Enemy_CapsuleColliderPresent");

                    var brain = enemyGo != null ? enemyGo.GetComponent<BambooHunterBrain>() : null;
                    AssertTest(brain != null, "Scene_Enemy_BrainPresent");

                    var health = enemyGo != null ? enemyGo.GetComponent<BambooHunterHealth>() : null;
                    AssertTest(health != null, "Scene_Enemy_HealthPresent");

                    var perc = enemyGo != null ? enemyGo.GetComponent<BambooHunterPerception>() : null;
                    AssertTest(perc != null && perc.EyeAnchor != null && perc.BowAnchor != null, "Scene_Enemy_PerceptionAnchorsWired");
                    AssertTest(perc != null && perc.Target != null, "Scene_Enemy_PerceptionTargetWired");

                    var nav = enemyGo != null ? enemyGo.GetComponent<BambooHunterNavigation>() : null;
                    AssertTest(nav != null && nav.WaypointCount >= 4, "Scene_Enemy_NavigationWaypointsWired");

                    var cs = enemyGo != null ? enemyGo.GetComponent<BambooHunterCoverSystem>() : null;
                    AssertTest(cs != null && cs.CoverCount >= 3, "Scene_Enemy_CoverPointsWired");

                    var bow = enemyGo != null ? enemyGo.GetComponent<BambooHunterBow>() : null;
                    AssertTest(bow != null && bow.ArrowPrefab != null, "Scene_Enemy_BowArrowPrefabWired");

                    var manager = GameObject.Find("BambooHunterDemoManager");
                    var hud = manager != null ? manager.GetComponent<BambooHunterDemoHUD>() : null;
                    AssertTest(hud != null && (hud.HunterBrain != null || UnityEngine.Object.FindFirstObjectByType<BambooHunterBrain>() != null), "Scene_HUD_BrainResolved");

                    var harness = manager != null ? manager.GetComponent<BambooHunterRuntimeHarness>() : null;
                    AssertTest(harness != null && (harness.HunterBrain != null || UnityEngine.Object.FindFirstObjectByType<BambooHunterBrain>() != null), "Scene_Harness_BrainResolved");
                }

                // TEST 77: Physical Simulated Arrow Launch & Collision Damage Registration
                {
                    GameObject arrowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BambooHunterAssetBuilder.ArrowPrefabPath);
                    bool wasLoadedFromContents = false;
                    if (arrowPrefab == null && File.Exists(BambooHunterAssetBuilder.ArrowPrefabPath))
                    {
                        arrowPrefab = PrefabUtility.LoadPrefabContents(BambooHunterAssetBuilder.ArrowPrefabPath);
                        wasLoadedFromContents = true;
                    }

                    AssertTest(arrowPrefab != null, "Physics_Arrow_PrefabFoundForSimulation");

                    if (arrowPrefab != null)
                    {
                        var arrowGo = UnityEngine.Object.Instantiate(arrowPrefab);
                        arrowGo.transform.position = new Vector3(0f, 1.2f, 0f);
                        var arrow = arrowGo.GetComponent<BambooHunterArrow>();

                        var dummyGo = new GameObject("TestPhysDummy");
                        dummyGo.transform.position = new Vector3(0f, 1.0f, 2.5f);
                        var dummyRb = dummyGo.AddComponent<Rigidbody>();
                        dummyRb.isKinematic = true;
                        var col = dummyGo.AddComponent<CapsuleCollider>();
                        col.center = Vector3.zero;
                        col.radius = 0.5f;
                        col.height = 2.0f;
                        var dummyHealth = dummyGo.AddComponent<BambooHunterPlayerHealthDemo>();
                        dummyHealth.Initialize(100f);

                        arrow.Launch(arrowGo.transform.position, Vector3.forward, launchSpeed: 30f, payloadDamage: 25f, firingShooter: null);

                        // Step trajectory deterministically in EditMode toward target dummy
                        float dt = 0.02f;
                        for (int step = 0; step < 10; step++)
                        {
                            Vector3 pos = arrowGo.transform.position + Vector3.forward * (30f * dt);
                            arrowGo.transform.position = pos;
                            if (!arrow.HasImpacted && (pos.z >= 2.0f || col.bounds.Contains(pos)))
                            {
                                arrow.ProcessHit(col, pos);
                                break;
                            }
                        }

                        // Duplicate hit attempt should be ignored atomically
                        arrow.ProcessHit(col, arrowGo.transform.position);

                        AssertTest(arrow.HasImpacted, "Physics_Arrow_CollidedWithDummy");
                        AssertTest(dummyHealth.CurrentHealth == 75f, "Physics_Arrow_DamageDeliveredToDummy");
                        AssertTest(dummyHealth.DamageEventCount == 1, "Physics_Arrow_SingleDamageEventAtomic");

                        UnityEngine.Object.DestroyImmediate(arrowGo);
                        UnityEngine.Object.DestroyImmediate(dummyGo);
                    }

                    if (wasLoadedFromContents && arrowPrefab != null)
                    {
                        PrefabUtility.UnloadPrefabContents(arrowPrefab);
                    }
                }

                // =========================================================================
                // PART 9: RUNTIME BUG REGRESSION TESTS (TESTS 78 - 81)
                // =========================================================================

                // TEST 78: First-Shot Readiness (Unobstructed Shot At 12m Favors Aim Over Reposition)
                {
                    var ctx = new BambooHunterDecisionContext
                    {
                        currentHealth = 120f,
                        maxHealth = 120f,
                        hasTargetVisibility = true,
                        hasTargetMemory = true,
                        distanceToTarget = 12.5f,
                        hasUnobstructedShot = true,
                        currentArrows = 6,
                        consecutiveShotsFired = 0,
                        isBowDrawn = false,
                        bowDrawProgress = 0f,
                        hasReachableCover = true,
                        coverQuality = 0.5f,
                        coverOcclusion = 0.5f
                    };

                    float aimScore = BambooHunterUtility.Score(BambooHunterState.Aim, ctx, settings);
                    float repoScore = BambooHunterUtility.Score(BambooHunterState.Reposition, ctx, settings);
                    BambooHunterState chosen = BambooHunterUtility.Choose(ctx, BambooHunterState.Patrol, 1.0f, settings);

                    AssertTest(aimScore > repoScore, "Regression_FirstShot_AimOutscoresReposition", $"Aim: {aimScore}, Repo: {repoScore}");
                    AssertTest(chosen == BambooHunterState.Aim, "Regression_FirstShot_SelectsAim", $"Chosen: {chosen}");
                }

                // TEST 79: Target Collider Self-Ignore (Linecast Striking Target Collider Does Not Block Shot)
                {
                    var shooterGo = new GameObject("TestShooter");
                    shooterGo.transform.position = new Vector3(200f, 0f, 200f);
                    var shooterCol = shooterGo.AddComponent<CapsuleCollider>();
                    shooterCol.center = Vector3.up;
                    shooterCol.height = 2f;
                    shooterCol.radius = 0.5f;

                    var dummyTargetGo = new GameObject("TestTargetDummy");
                    dummyTargetGo.transform.position = new Vector3(200f, 0f, 210f);
                    var targetCol = dummyTargetGo.AddComponent<CapsuleCollider>();
                    targetCol.center = Vector3.up;
                    targetCol.height = 2f;
                    targetCol.radius = 0.5f;

                    // Add child collider to target to rigorously test child hierarchy resolution
                    var dummyChildGo = new GameObject("TestTargetChildCol");
                    dummyChildGo.transform.SetParent(dummyTargetGo.transform, false);
                    dummyChildGo.transform.localPosition = new Vector3(0f, 0.65f, 0f);
                    var childCol = dummyChildGo.AddComponent<SphereCollider>();
                    childCol.radius = 0.3f;

                    Physics.SyncTransforms();

                    Vector3 eyePos = shooterGo.transform.position + Vector3.up * 1.65f;
                    Vector3 bowPos = shooterGo.transform.position + Vector3.up * 1.20f;
                    Vector3 targetAimPos = dummyTargetGo.transform.position + Vector3.up * 1.0f;

                    LayerMask mask = ~0;

                    // 1. Clear shot directly to target with parent & child colliders:
                    bool clearLOS = BambooHunterPerception.CheckClearLine(eyePos, targetAimPos, mask, shooterGo.transform, dummyTargetGo.transform, out string blockerLOS);
                    bool clearLOF = BambooHunterPerception.CheckClearLine(bowPos, targetAimPos, mask, shooterGo.transform, dummyTargetGo.transform, out string blockerLOF);

                    AssertTest(clearLOS, "Regression_Perception_TargetColliderNotTreatedAsVisionObstacle", $"Blocked by: {blockerLOS}");
                    AssertTest(clearLOF, "Regression_Perception_TargetColliderNotTreatedAsFireObstacle", $"Blocked by: {blockerLOF}");
                    AssertTest(string.IsNullOrEmpty(blockerLOF), "Regression_Perception_BlockerEmptyWhenTargetHit");

                    // Test BambooHunterBow check
                    var bow = shooterGo.AddComponent<BambooHunterBow>();
                    bool bowClear = bow.CheckStraightLineOfFire(bowPos, targetAimPos, mask, dummyTargetGo.transform);
                    AssertTest(bowClear, "Regression_Bow_TargetColliderNotTreatedAsFireObstacle");

                    // 2. Obstacle placed in front of target: MUST be blocked
                    var obstacleGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    obstacleGo.name = "TestInterveningWall";
                    obstacleGo.transform.position = new Vector3(200f, 1f, 205f); // Directly midway between shooter (200) and target (210)
                    Physics.SyncTransforms();

                    bool blockedLOS = !BambooHunterPerception.CheckClearLine(eyePos, targetAimPos, mask, shooterGo.transform, dummyTargetGo.transform, out string wallBlockerLOS);
                    bool blockedLOF = !BambooHunterPerception.CheckClearLine(bowPos, targetAimPos, mask, shooterGo.transform, dummyTargetGo.transform, out string wallBlockerLOF);
                    bool bowBlocked = !bow.CheckStraightLineOfFire(bowPos, targetAimPos, mask, dummyTargetGo.transform);

                    AssertTest(blockedLOS && wallBlockerLOS == "TestInterveningWall", "Regression_Obstacle_VisionBlockedByInterveningWall", $"Blocker was: {wallBlockerLOS}");
                    AssertTest(blockedLOF && wallBlockerLOF == "TestInterveningWall", "Regression_Obstacle_FireBlockedByInterveningWall", $"Blocker was: {wallBlockerLOF}");
                    AssertTest(bowBlocked, "Regression_Obstacle_BowBlockedByInterveningWall");

                    UnityEngine.Object.DestroyImmediate(obstacleGo);
                    UnityEngine.Object.DestroyImmediate(shooterGo);
                    UnityEngine.Object.DestroyImmediate(dummyTargetGo);
                }

                // TEST 80: Reposition State Timeout Escape (Hunter Trapped in Reposition Switches to Aim/Shoot)
                {
                    var ctxRepositionStuck = new BambooHunterDecisionContext
                    {
                        currentHealth = 120f,
                        maxHealth = 120f,
                        hasTargetVisibility = true,
                        hasTargetMemory = true,
                        distanceToTarget = 5f,
                        hasUnobstructedShot = true,
                        currentArrows = 6,
                        consecutiveShotsFired = 0,
                        hasReachableCover = true,
                        coverQuality = 0.5f,
                        coverOcclusion = 0.5f
                    };

                    // Agent in Reposition for 7.0 seconds (exceeding maximumStateDuration = 6.0s)
                    BambooHunterState chosenAfterTimeout = BambooHunterUtility.Choose(
                        ctxRepositionStuck,
                        BambooHunterState.Reposition,
                        7.0f,
                        settings);

                    AssertTest(chosenAfterTimeout != BambooHunterState.Reposition,
                        "Regression_Timeout_RepositionEscapesAfterMaxDuration",
                        $"Stayed in: {chosenAfterTimeout}");
                    AssertTest(chosenAfterTimeout == BambooHunterState.Aim,
                        "Regression_Timeout_RepositionTransitionsToAim",
                        $"Transitioned to: {chosenAfterTimeout}");
                }

                // TEST 81: Close-Range Combat Defense (Target at 0.8m Has Positive Aim Utility and Fires)
                {
                    var ctxCloseRange = new BambooHunterDecisionContext
                    {
                        currentHealth = 120f,
                        maxHealth = 120f,
                        hasTargetVisibility = true,
                        hasTargetMemory = true,
                        distanceToTarget = 0.8f,
                        hasUnobstructedShot = true,
                        currentArrows = 6,
                        consecutiveShotsFired = 0,
                        isBowDrawn = false,
                        bowDrawProgress = 0f
                    };

                    float closeAimScore = BambooHunterUtility.Score(BambooHunterState.Aim, ctxCloseRange, settings);
                    AssertTest(closeAimScore > 0f, "Regression_CloseRange_AimScorePositive", $"Score was {closeAimScore}");

                    var ctxCloseReadyToShoot = ctxCloseRange;
                    ctxCloseReadyToShoot.isBowDrawn = true;
                    ctxCloseReadyToShoot.bowDrawProgress = 1.0f;

                    float closeShootScore = BambooHunterUtility.Score(BambooHunterState.Shoot, ctxCloseReadyToShoot, settings);
                    BambooHunterState chosenCloseShoot = BambooHunterUtility.Choose(
                        ctxCloseReadyToShoot,
                        BambooHunterState.Aim,
                        1.0f,
                        settings);

                    AssertTest(closeShootScore >= 0.90f, "Regression_CloseRange_ShootScoreHigh", $"Score was {closeShootScore}");
                    AssertTest(chosenCloseShoot == BambooHunterState.Shoot, "Regression_CloseRange_ExecutesShot", $"Selected: {chosenCloseShoot}");
                }

                // TEST 82: Near-Target Visibility & Proximity Awareness (Target at 0.8m is Visible, not Hidden)
                {
                    var observerGo = new GameObject("Observer_NearVis");
                    observerGo.transform.position = new Vector3(500f, 0f, 500f);
                    var targetGo = new GameObject("Target_NearVis");
                    targetGo.transform.position = new Vector3(500.5f, 0f, 500.6f); // 0.78m horizontal distance
                    var targetCol = targetGo.AddComponent<CapsuleCollider>();
                    targetCol.center = Vector3.up;
                    targetCol.height = 2f;
                    targetCol.radius = 0.5f;

                    Physics.SyncTransforms();

                    var perc = observerGo.AddComponent<BambooHunterPerception>();
                    perc.Target = targetGo.transform;
                    perc.CloseProximityRadius = 2.0f;

                    perc.Sample(settings);

                    AssertTest(perc.CanSeeTarget, "NearVisibility_CanSeeTargetTrue",
                        $"Rejected with {perc.LastVisibilityRejectionReason}, FOV angle={perc.LastFovAngle}");
                    AssertTest(perc.LastVisibilityRejectionReason == VisibilityRejectionReason.None,
                        "NearVisibility_RejectionReasonNone",
                        $"Rejection reason was: {perc.LastVisibilityRejectionReason}");

                    // Observer faces 90 degrees away from near target within 1.0m personal space
                    observerGo.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                    perc.Sample(settings);

                    AssertTest(perc.CanSeeTarget, "NearVisibility_PersonalSpaceVisibleEvenOffAxis",
                        $"Off-axis at 0.8m was rejected with: {perc.LastVisibilityRejectionReason}");

                    UnityEngine.Object.DestroyImmediate(observerGo);
                    UnityEngine.Object.DestroyImmediate(targetGo);
                }

                // TEST 83: First-Shot Combat From Clear 12m Position (Full Sequence: Aim -> Draw -> Shoot -> Impact -> Reposition)
                {
                    var dummyTarget = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    dummyTarget.name = "Dummy_FirstShot12m";
                    dummyTarget.transform.position = new Vector3(300f, 1.0f, 308.0f);
                    var dummyHealth = dummyTarget.AddComponent<BambooHunterPlayerHealthDemo>();
                    dummyHealth.Initialize(100f);

                    var hunterGo = new GameObject("Hunter_FirstShot12m");
                    hunterGo.transform.position = new Vector3(300f, 0f, 296.0f); // 12m separation
                    var nav = hunterGo.AddComponent<BambooHunterNavigation>();
                    var bow = hunterGo.AddComponent<BambooHunterBow>();
                    var health = hunterGo.AddComponent<BambooHunterHealth>();
                    var coverSys = hunterGo.AddComponent<BambooHunterCoverSystem>();
                    var perc = hunterGo.AddComponent<BambooHunterPerception>();
                    perc.Target = dummyTarget.transform;

                    // Provide a valid cover anchor so the AI can evaluate and execute tactical repositioning after shooting
                    var coverGo = new GameObject("CoverPoint_Test83");
                    coverGo.transform.position = new Vector3(310f, 0f, 300f);
                    var coverPoint = coverGo.AddComponent<BambooHunterCoverPoint>();
                    coverPoint.Initialize(Vector3.forward, 0.85f, 1.5f, true);
                    coverSys.SetCoverPoints(new BambooHunterCoverPoint[] { coverPoint });

                    var arrowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BambooHunterAssetBuilder.ArrowPrefabPath);
                    var bowAnchor = new GameObject("BowAnchor");
                    bowAnchor.transform.SetParent(hunterGo.transform, false);
                    bowAnchor.transform.localPosition = new Vector3(0.3f, 1.25f, 0.45f);

                    bow.Initialize(settings, bowAnchor.transform, arrowPrefab);
                    health.Initialize(settings);

                    var brain = hunterGo.AddComponent<BambooHunterBrain>();
                    brain.Initialize(settings, perc, nav, coverSys, bow, health);

                    Physics.SyncTransforms();

                    // 1. Initial sensory context at 12m: AI chooses Aim and starts drawing
                    brain.RunDecisionCycle();
                    AssertTest(brain.CurrentState == BambooHunterState.Aim,
                        "FirstShot_InitialAimSelectedFrom12m",
                        $"State was {brain.CurrentState}, AimScore={BambooHunterUtility.Score(BambooHunterState.Aim, brain.LastDecisionContext, settings)}, RepositionScore={BambooHunterUtility.Score(BambooHunterState.Reposition, brain.LastDecisionContext, settings)}");
                    AssertTest(bow.IsDrawing, "FirstShot_BowDrawInitiatedInAim");

                    // 2. Advance draw to completion
                    bow.UpdateDraw(1.2f);
                    AssertTest(bow.IsDrawComplete, "FirstShot_BowFullyDrawn");

                    // 3. Decision cycle transitions to Shoot; upon entering Shoot state, EnterState(Shoot) immediately executes bow.TryShoot()
                    int preShootArrows = bow.CurrentArrows;
                    brain.RunDecisionCycle();
                    AssertTest(brain.CurrentState == BambooHunterState.Shoot,
                        "FirstShot_TransitionsToShootWhenDrawn",
                        $"State was {brain.CurrentState}");

                    // 4. Arrow launch and quiver verification
                    AssertTest(bow.ConsecutiveShots == 1,
                        "FirstShot_TryShootSucceeded",
                        $"ConsecutiveShots was {bow.ConsecutiveShots}, LastShotFailureReason={bow.LastShotFailureReason}");
                    AssertTest(bow.CurrentArrows == preShootArrows - 1,
                        "FirstShot_QuiverDecremented",
                        $"CurrentArrows was {bow.CurrentArrows} (expected {preShootArrows - 1})");

                    // 5. Subsequent decision cycle after shot: with arrow expended and cover available, transitions to Reposition
                    brain.RunDecisionCycle();
                    AssertTest(brain.CurrentState == BambooHunterState.Reposition,
                        "FirstShot_SubsequentDecisionTransitionsToReposition",
                        $"State was {brain.CurrentState}, CoverReachable={coverSys.HasValidCover}, Scores: Shoot={BambooHunterUtility.Score(BambooHunterState.Shoot, brain.LastDecisionContext, settings)}, Aim={BambooHunterUtility.Score(BambooHunterState.Aim, brain.LastDecisionContext, settings)}, Repo={BambooHunterUtility.Score(BambooHunterState.Reposition, brain.LastDecisionContext, settings)}");

                    UnityEngine.Object.DestroyImmediate(hunterGo);
                    UnityEngine.Object.DestroyImmediate(dummyTarget);
                    UnityEngine.Object.DestroyImmediate(coverGo);
                }

                // TEST 84: Standoff Investigation Geometry, Decisions & Unbound-Agent Safety
                {
                    var hunterGo = new GameObject("Hunter_StandoffTest");
                    hunterGo.transform.position = new Vector3(0f, 0f, -12f);
                    hunterGo.transform.rotation = Quaternion.identity;
                    var agent = hunterGo.AddComponent<NavMeshAgent>();
                    agent.speed = 3.5f;
                    agent.stoppingDistance = 0.5f;

                    var nav = hunterGo.AddComponent<BambooHunterNavigation>();
                    nav.Initialize(agent);

                    Vector3 dummyPos = new Vector3(0f, 1.0f, 0f); // 12m separation along Z

                    // 1. Pure Geometric Standoff Calculation (Deterministic, side-effect-free, safe in Edit Mode)
                    bool calcSuccess = BambooHunterNavigation.TryCalculateStandoffDestination(
                        hunterGo.transform.position,
                        hunterGo.transform.forward,
                        dummyPos,
                        settings,
                        out Vector3 calcStandoffDest,
                        out bool alreadyInStandoff);

                    float calcSeparation = Vector3.Distance(calcStandoffDest, dummyPos);

                    AssertTest(calcSuccess && !alreadyInStandoff,
                        "Investigation_StandoffCalculationSucceeded",
                        $"Calculation failed or already in standoff: success={calcSuccess}, alreadyInStandoff={alreadyInStandoff}");

                    AssertTest(calcSeparation >= settings.preferredMinRange - 0.05f,
                        "Investigation_MaintainsSafeStandoffDistance",
                        $"Separation was {calcSeparation:F2}m (Min expected: {settings.preferredMinRange:F1}m)");

                    // 2. Unbound Agent Safety Guard (Edit Mode Contract: unbound agent must fail gracefully without native errors)
                    AssertTest(!nav.IsReady, "Investigation_UnboundAgent_IsReadyFalse");
                    bool moveAttempt = nav.MoveToInvestigate(dummyPos, settings);
                    AssertTest(!moveAttempt && nav.HasFailed && nav.CurrentActivity == NavigationActivity.Failed,
                        "Investigation_UnboundAgent_FailsGracefullyWithoutNativeError",
                        $"moveAttempt={moveAttempt}, HasFailed={nav.HasFailed}, Activity={nav.CurrentActivity}");
                    AssertTest(Vector3.Distance(nav.CurrentDestination, dummyPos) >= settings.preferredMinRange - 0.05f,
                        "Investigation_DestinationPreservesStandoffEvenOnUnboundAgent",
                        $"Destination was {nav.CurrentDestination}");

                    // 3. Close-Range Standoff Decision (Within standoff distance: agent halts safely without native agent errors)
                    hunterGo.transform.position = new Vector3(0f, 0f, -4f); // 4m separation (within 7m standoff)
                    bool closeCalc = BambooHunterNavigation.TryCalculateStandoffDestination(
                        hunterGo.transform.position,
                        hunterGo.transform.forward,
                        dummyPos,
                        settings,
                        out _,
                        out bool closeAlreadyInStandoff);

                    AssertTest(closeCalc && closeAlreadyInStandoff,
                        "Investigation_DetectsAlreadyWithinStandoffRange",
                        $"closeCalc={closeCalc}, closeAlreadyInStandoff={closeAlreadyInStandoff}");

                    bool halted = nav.MoveToInvestigate(dummyPos, settings);
                    AssertTest(halted,
                        "Investigation_HaltsWhenAlreadyWithinStandoffRange",
                        $"Halted was false (Activity={nav.CurrentActivity})");
                    AssertTest(nav.CurrentActivity == NavigationActivity.Investigating,
                        "Investigation_ActivitySetInvestigating",
                        $"Activity was {nav.CurrentActivity}");

                    // 4. Invalid Input Rejection (Numerical Safety)
                    bool invalidCalc = BambooHunterNavigation.TryCalculateStandoffDestination(
                        hunterGo.transform.position,
                        hunterGo.transform.forward,
                        new Vector3(float.NaN, 0f, 0f),
                        settings,
                        out _,
                        out _);
                    AssertTest(!invalidCalc, "Investigation_InvalidTargetRejected");

                    UnityEngine.Object.DestroyImmediate(hunterGo);
                }

                // TEST 85: Successful Recovery From Blocked Shot (Reposition Flanking -> Line Cleared -> Aim Resumed)
                {
                    // Target visible but shot is blocked by vegetation/cover
                    var ctxBlocked = new BambooHunterDecisionContext
                    {
                        currentHealth = 120f,
                        maxHealth = 120f,
                        hasTargetVisibility = true,
                        distanceToTarget = 10f,
                        hasUnobstructedShot = false, // Blocked line of fire!
                        currentArrows = 6,
                        consecutiveShotsFired = 0,
                        hasReachableCover = true,
                        coverQuality = 0.8f,
                        coverOcclusion = 0.7f
                    };

                    float aimBlockedScore = BambooHunterUtility.Score(BambooHunterState.Aim, ctxBlocked, settings);
                    float repoBlockedScore = BambooHunterUtility.Score(BambooHunterState.Reposition, ctxBlocked, settings);
                    BambooHunterState chosenBlocked = BambooHunterUtility.Choose(ctxBlocked, BambooHunterState.Aim, 1.0f, settings);

                    AssertTest(aimBlockedScore == 0f, "BlockedRecovery_AimScoreZeroWhenShotBlocked");
                    AssertTest(repoBlockedScore > 0.80f, "BlockedRecovery_RepositionScoreHighWhenShotBlocked", $"Score was {repoBlockedScore}");
                    AssertTest(chosenBlocked == BambooHunterState.Reposition, "BlockedRecovery_SelectsRepositionToFlank", $"Selected: {chosenBlocked}");

                    // Once repositioned behind cover or flanking angle, shot line is restored
                    var ctxCleared = ctxBlocked;
                    ctxCleared.hasUnobstructedShot = true; // Flanking angle cleared line of fire!
                    ctxCleared.isAtCover = true;

                    float aimClearedScore = BambooHunterUtility.Score(BambooHunterState.Aim, ctxCleared, settings);
                    BambooHunterState chosenCleared = BambooHunterUtility.Choose(ctxCleared, BambooHunterState.Reposition, 1.0f, settings);

                    AssertTest(aimClearedScore > 0.70f, "BlockedRecovery_AimScoreHighWhenLineCleared", $"Score was {aimClearedScore}");
                    AssertTest(chosenCleared == BambooHunterState.Aim, "BlockedRecovery_ResumesAimOnceLineCleared", $"Selected: {chosenCleared}");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }

            return allPassed;
        }

        private static void EnsureEvidenceDirectory()
        {
            string dir = Path.GetDirectoryName(LogPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }
    }
}
