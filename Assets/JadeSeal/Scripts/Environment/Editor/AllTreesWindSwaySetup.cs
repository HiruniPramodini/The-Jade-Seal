#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using JadeSeal.Environment;

namespace JadeSeal.Environment.Editor
{
    /// <summary>
    /// Safe, non-destructive Editor utility for applying GentleWindSway across all verified
    /// environment bamboo and pine trees in the Systems Integration scene.
    ///
    /// SAFETY & ELIGIBILITY GUARANTEES:
    /// 1. Operates ONLY on the active Systems Integration scene (JadeSealFantasy_SystemsIntegration.unity).
    /// 2. Explicitly refuses to run on the original teammate scene (JadeSealFantasy.unity).
    /// 3. Targets ONLY verified vegetation root GameObjects that carry an LODGroup component.
    /// 4. Explicitly ignores decorative fence pickets (Bamboo_Stalk), gates, buildings, waypoints,
    ///    enemy AI markers, and colliders.
    /// 5. Fully idempotent: does not duplicate GentleWindSway if already present; updates settings.
    /// 6. Configures zone-appropriate wind motion:
    ///    - Bamboo: 1.8 deg sway, 0.9 rad/s speed, 0.3 deg secondary harmonic
    ///    - Pine:   0.9 deg sway, 0.6 rad/s speed, 0.15 deg secondary harmonic
    /// 7. Full Undo support (Ctrl+Z) and marks the scene dirty without force-saving.
    /// 8. Displays confirmation dialog with exact tree counts before executing changes.
    /// 9. Provides a separate clean removal command for easy reversion.
    /// </summary>
    public static class AllTreesWindSwaySetup
    {
        private const string ExpectedSceneFilename = "JadeSealFantasy_SystemsIntegration.unity";
        private const string TeammateOriginalSceneFilename = "JadeSealFantasy.unity";

        // Bamboo sway parameters (calm natural mountain breeze)
        private const float BambooPrimarySway = 1.8f;
        private const float BambooWindSpeed = 0.9f;
        private const float BambooSecondarySway = 0.3f;

        // Pine sway parameters (subtle, dignified evergreen motion)
        private const float PinePrimarySway = 0.9f;
        private const float PineWindSpeed = 0.6f;
        private const float PineSecondarySway = 0.15f;

        [MenuItem("The Jade Seal/Apply Wind Sway to All Environment Trees", priority = 20)]
        [MenuItem("Tools/The Jade Seal/Apply Wind Sway to All Environment Trees", priority = 20)]
        public static void ApplyWindSwayToAllTrees()
        {
            // 1. Scene Validation
            if (!ValidateActiveScene(out var activeScene))
            {
                return;
            }

            // 2. Scan and categorize eligible trees
            ScanEligibleTrees(activeScene, out var eligibleBamboo, out var eligiblePine, out var excludedObjects);

            int totalEligible = eligibleBamboo.Count + eligiblePine.Count;
            if (totalEligible == 0)
            {
                EditorUtility.DisplayDialog(
                    "No Eligible Trees Found",
                    "No eligible bamboo or pine tree root GameObjects with LODGroup components were found in the active scene.",
                    "OK");
                return;
            }

            // 3. User Confirmation Dialog
            string promptMessage =
                $"Eligible Environment Trees Found: {totalEligible}\n\n" +
                $"• Bamboo trees (LOD roots): {eligibleBamboo.Count}\n" +
                $"  Settings: Sway {BambooPrimarySway}°, Speed {BambooWindSpeed} rad/s, SecSway {BambooSecondarySway}°\n\n" +
                $"• Black Pine trees (LOD roots): {eligiblePine.Count}\n" +
                $"  Settings: Sway {PinePrimarySway}°, Speed {PineWindSpeed} rad/s, SecSway {PineSecondarySway}°\n\n" +
                $"• Excluded non-tree objects: {excludedObjects.Count} (fence pickets, markers, colliders)\n\n" +
                $"Target Scene: {activeScene.name}\n\n" +
                $"Do you want to apply GentleWindSway to all {totalEligible} trees?";

            bool userConfirmed = EditorUtility.DisplayDialog(
                "Apply Wind Sway to All Environment Trees",
                promptMessage,
                "Apply Wind Sway",
                "Cancel");

            if (!userConfirmed)
            {
                Debug.Log("[The Jade Seal] Apply Wind Sway operation cancelled by user.");
                return;
            }

            // 4. Apply / Configure components
            int newlyAttachedCount = 0;
            int updatedCount = 0;

            Undo.SetCurrentGroupName("Apply Wind Sway to All Environment Trees");
            int undoGroup = Undo.GetCurrentGroup();

            // Process Bamboo
            foreach (var go in eligibleBamboo)
            {
                bool isNew = ConfigureTreeWindSway(go, BambooPrimarySway, BambooWindSpeed, BambooSecondarySway);
                if (isNew) newlyAttachedCount++;
                else updatedCount++;
            }

            // Process Pine
            foreach (var go in eligiblePine)
            {
                bool isNew = ConfigureTreeWindSway(go, PinePrimarySway, PineWindSpeed, PineSecondarySway);
                if (isNew) newlyAttachedCount++;
                else updatedCount++;
            }

            Undo.CollapseUndoOperations(undoGroup);

            // 5. Mark scene dirty (never force-save)
            EditorSceneManager.MarkSceneDirty(activeScene);

            // 6. Summary Report
            string completionReport =
                $"Wind Sway Application Complete!\n\n" +
                $"• Total Trees Processed: {totalEligible}\n" +
                $"  - Bamboo Roots: {eligibleBamboo.Count}\n" +
                $"  - Pine Roots: {eligiblePine.Count}\n" +
                $"• Newly Attached: {newlyAttachedCount}\n" +
                $"• Updated Existing: {updatedCount}\n" +
                $"• Excluded (Fence/Markers): {excludedObjects.Count}\n\n" +
                $"Scene has been marked dirty. Press 'Play' in Unity to see the natural wind across the forest!";

            EditorUtility.DisplayDialog("Wind Sway Applied", completionReport, "OK");
            Debug.Log($"<color=#2ecc71><b>[The Jade Seal]</b></color> Successfully applied GentleWindSway to {totalEligible} trees ({newlyAttachedCount} new, {updatedCount} updated).");
        }

        [MenuItem("The Jade Seal/Remove Wind Sway from All Environment Trees", priority = 21)]
        [MenuItem("Tools/The Jade Seal/Remove Wind Sway from All Environment Trees", priority = 21)]
        public static void RemoveWindSwayFromAllTrees()
        {
            if (!ValidateActiveScene(out var activeScene))
            {
                return;
            }

            ScanEligibleTrees(activeScene, out var eligibleBamboo, out var eligiblePine, out _);
            var allTrees = new List<GameObject>(eligibleBamboo);
            allTrees.AddRange(eligiblePine);

            var treesWithComponent = new List<GentleWindSway>();
            foreach (var go in allTrees)
            {
                var sway = go.GetComponent<GentleWindSway>();
                if (sway != null)
                {
                    treesWithComponent.Add(sway);
                }
            }

            if (treesWithComponent.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "No Wind Sway Found",
                    "None of the environment trees currently have a GentleWindSway component attached.",
                    "OK");
                return;
            }

            bool confirmRemove = EditorUtility.DisplayDialog(
                "Remove Wind Sway",
                $"Found {treesWithComponent.Count} trees with GentleWindSway components.\n\n" +
                $"Do you want to remove GentleWindSway from all of them?\n(This will restore their resting rotations).",
                "Remove",
                "Cancel");

            if (!confirmRemove)
            {
                Debug.Log("[The Jade Seal] Removal operation cancelled by user.");
                return;
            }

            Undo.SetCurrentGroupName("Remove Wind Sway from All Environment Trees");
            int undoGroup = Undo.GetCurrentGroup();

            int removedCount = 0;
            foreach (var sway in treesWithComponent)
            {
                if (sway != null)
                {
                    Undo.DestroyObjectImmediate(sway);
                    removedCount++;
                }
            }

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(activeScene);

            EditorUtility.DisplayDialog(
                "Wind Sway Removed",
                $"Successfully removed GentleWindSway from {removedCount} environment tree(s).\nScene marked dirty.",
                "OK");
            Debug.Log($"<color=#e67e22><b>[The Jade Seal]</b></color> Removed GentleWindSway from {removedCount} trees.");
        }

        /// <summary>
        /// Validates that the active scene is the Systems Integration scene.
        /// Strictly refuses to run on teammate original scenes.
        /// </summary>
        private static bool ValidateActiveScene(out UnityEngine.SceneManagement.Scene activeScene)
        {
            activeScene = EditorSceneManager.GetActiveScene();
            if (!activeScene.IsValid())
            {
                EditorUtility.DisplayDialog("Error", "No valid active scene loaded in Unity Editor.", "OK");
                return false;
            }

            string scenePath = activeScene.path;

            // Reject teammate original scene
            if (!string.IsNullOrEmpty(scenePath) && scenePath.EndsWith(TeammateOriginalSceneFilename, StringComparison.OrdinalIgnoreCase)
                && !scenePath.EndsWith(ExpectedSceneFilename, StringComparison.OrdinalIgnoreCase))
            {
                string msg = $"REFUSED OPERATION:\n\n" +
                             $"The currently open scene is the original teammate scene:\n'{scenePath}'\n\n" +
                             $"To protect teammate work, this tool will ONLY run on:\n'{ExpectedSceneFilename}'.\n\n" +
                             $"Please open the Systems Integration scene before running this command.";
                EditorUtility.DisplayDialog("Protected Teammate Scene", msg, "OK");
                Debug.LogError($"[The Jade Seal] Safety check failed: Refused to run on teammate scene '{scenePath}'.");
                return false;
            }

            // Must match Systems Integration scene
            if (string.IsNullOrEmpty(scenePath) || !scenePath.EndsWith(ExpectedSceneFilename, StringComparison.OrdinalIgnoreCase))
            {
                string msg = $"Active scene is not the Systems Integration scene.\n\n" +
                             $"Current: '{scenePath}'\n" +
                             $"Expected: '{ExpectedSceneFilename}'\n\n" +
                             $"Please open '{ExpectedSceneFilename}' before running.";
                EditorUtility.DisplayDialog("Incorrect Scene", msg, "OK");
                Debug.LogError($"[The Jade Seal] Safety check failed: Active scene '{scenePath}' is not '{ExpectedSceneFilename}'.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Scans the scene and identifies all verified tree roots carrying an LODGroup.
        /// Excludes decorative fences, pickets with colliders, waypoints, and AI markers.
        /// </summary>
        private static void ScanEligibleTrees(
            UnityEngine.SceneManagement.Scene scene,
            out List<GameObject> eligibleBamboo,
            out List<GameObject> eligiblePine,
            out List<string> excludedObjects)
        {
            eligibleBamboo = new List<GameObject>();
            eligiblePine = new List<GameObject>();
            excludedObjects = new List<string>();

            // Find all LODGroups in the scene
            var lodGroups = UnityEngine.Object.FindObjectsByType<LODGroup>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var lod in lodGroups)
            {
                if (lod == null) continue;
                var go = lod.gameObject;
                if (go.scene != scene) continue;

                string name = go.name;
                bool isBamboo = name.IndexOf("bamboo", StringComparison.OrdinalIgnoreCase) >= 0;
                bool isPine = name.IndexOf("pine", StringComparison.OrdinalIgnoreCase) >= 0;

                if (!isBamboo && !isPine) continue;

                // Safety checks: reject child LOD objects, colliders, or fence parts
                if (name.StartsWith("LOD_") || name.Contains("Stalk") || name.Contains("Fence"))
                {
                    excludedObjects.Add($"{name} (Excluded: Sub-part or fence)");
                    continue;
                }

                if (go.GetComponent<Collider>() != null)
                {
                    excludedObjects.Add($"{name} (Excluded: Carries physics collider)");
                    continue;
                }

                if (isBamboo)
                {
                    eligibleBamboo.Add(go);
                }
                else if (isPine)
                {
                    eligiblePine.Add(go);
                }
            }
        }

        /// <summary>
        /// Configures GentleWindSway with Undo registration and duplicate protection.
        /// </summary>
        private static bool ConfigureTreeWindSway(GameObject go, float primarySway, float speed, float secondarySway)
        {
            var sway = go.GetComponent<GentleWindSway>();
            bool isNew = false;

            if (sway == null)
            {
                sway = Undo.AddComponent<GentleWindSway>(go);
                isNew = true;
            }
            else
            {
                Undo.RecordObject(sway, "Configure GentleWindSway");
            }

            var so = new SerializedObject(sway);
            var propPrimary = so.FindProperty("primarySwayAngle");
            var propSpeed = so.FindProperty("windSpeed");
            var propSecondary = so.FindProperty("secondarySwayAngle");

            if (propPrimary != null) propPrimary.floatValue = primarySway;
            if (propSpeed != null) propSpeed.floatValue = speed;
            if (propSecondary != null) propSecondary.floatValue = secondarySway;

            so.ApplyModifiedProperties();
            return isNew;
        }
    }
}
#endif
