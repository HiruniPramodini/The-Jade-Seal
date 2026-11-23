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
    /// Safe, non-destructive Editor utility for initial testing of gentle wind sway on spawn-area bamboo.
    ///
    /// SAFETY GUARANTEES:
    /// 1. Operates ONLY on the active Systems Integration scene (JadeSealFantasy_SystemsIntegration.unity).
    /// 2. Explicitly refuses to run on the original teammate scene (JadeSealFantasy.unity).
    /// 3. Targets ONLY the four verified bamboo trees near Player_Spawn (-65, 0.205, -50).
    /// 4. Does not modify meshes, materials, LODGroup configurations, colliders, lighting, or NavMesh.
    /// 5. Fully idempotent: never duplicates GentleWindSway components if executed multiple times.
    /// 6. Full Undo support (Ctrl+Z) and marks the scene dirty without force-saving.
    /// 7. Never runs automatically on compile or editor launch.
    /// </summary>
    public static class BambooWindSwayTestSetup
    {
        private const string ExpectedSceneFilename = "JadeSealFantasy_SystemsIntegration.unity";
        private const string TeammateOriginalSceneFilename = "JadeSealFantasy.unity";

        private struct BambooTargetInfo
        {
            public readonly string Name;
            public readonly ulong TargetObjectId;
            public readonly Vector3 ExpectedPosition;
            public readonly float PositionTolerance;

            public BambooTargetInfo(string name, ulong targetObjectId, Vector3 expectedPosition, float tolerance = 0.20f)
            {
                Name = name;
                TargetObjectId = targetObjectId;
                ExpectedPosition = expectedPosition;
                PositionTolerance = tolerance;
            }
        }

        // The four verified bamboo trees closest to Player_Spawn (-65.0, 0.205, -50.0)
        private static readonly BambooTargetInfo[] VerifiedTargets = new BambooTargetInfo[]
        {
            // 1. Small Bamboo (~9.4m from spawn)
            new BambooTargetInfo(
                "P6_Tree_Bamboo_Small_Green_01",
                404735066UL,
                new Vector3(-56.157f, 2.870f, -53.133f)),

            // 2. Small Bamboo (~9.9m from spawn)
            new BambooTargetInfo(
                "P6_Tree_Bamboo_Small_Green_01",
                622522698UL,
                new Vector3(-57.028f, 3.994f, -55.853f)),

            // 3. Small Bamboo (~10.3m from spawn)
            new BambooTargetInfo(
                "P6_Tree_Bamboo_Small_Green_01",
                629037624UL,
                new Vector3(-55.636f, 3.440f, -54.365f)),

            // 4. Big Bamboo (~10.7m from spawn)
            new BambooTargetInfo(
                "P6_Tree_Bamboo_Big_Green_01",
                246080048UL,
                new Vector3(-55.873f, 3.926f, -55.616f))
        };

        [MenuItem("The Jade Seal/Test Wind Sway on Spawn Bamboo (4 Trees)", priority = 10)]
        [MenuItem("Tools/The Jade Seal/Test Wind Sway on Spawn Bamboo (4 Trees)", priority = 10)]
        public static void AttachWindSwayToSpawnBamboo()
        {
            // 1. Scene Validation
            if (!ValidateActiveScene(out var activeScene))
            {
                return;
            }

            int attachedCount = 0;
            int alreadyPresentCount = 0;
            var missingTargets = new List<string>();

            // 2. Locate and process the four verified bamboo roots
            for (int i = 0; i < VerifiedTargets.Length; i++)
            {
                var target = VerifiedTargets[i];
                var candidateGo = FindTargetBamboo(target);

                if (candidateGo == null)
                {
                    missingTargets.Add($"#{i + 1} {target.Name} (ID: {target.TargetObjectId}, Pos: {target.ExpectedPosition})");
                    continue;
                }

                // Check for duplicate component
                var existingSway = candidateGo.GetComponent<GentleWindSway>();
                if (existingSway != null)
                {
                    alreadyPresentCount++;
                    Debug.Log($"<color=#3498db><b>[The Jade Seal]</b></color> GentleWindSway already attached to: <b>{candidateGo.name}</b> (Pos: {candidateGo.transform.position})");
                    continue;
                }

                // Attach with full Undo registration
                Undo.AddComponent<GentleWindSway>(candidateGo);
                attachedCount++;
                Debug.Log($"<color=#2ecc71><b>[The Jade Seal]</b></color> Successfully attached GentleWindSway to: <b>{candidateGo.name}</b> (Pos: {candidateGo.transform.position})");
            }

            // 3. Status Reporting & Dirty State
            if (attachedCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
            }

            string report = $"Wind Sway Test Setup Complete:\n\n" +
                            $"- Attached to: {attachedCount} bamboo tree(s)\n" +
                            $"- Already had component: {alreadyPresentCount} tree(s)\n" +
                            $"- Missing / Not found: {missingTargets.Count} tree(s)\n\n" +
                            $"Scene has been marked dirty. Press 'Play' in Unity to observe the gentle sway!";

            if (missingTargets.Count > 0)
            {
                report += $"\n\nMissing targets:\n" + string.Join("\n", missingTargets);
                Debug.LogWarning($"[The Jade Seal] Some targets could not be located:\n" + string.Join("\n", missingTargets));
            }

            EditorUtility.DisplayDialog("Wind Sway Test Setup", report, "OK");
        }

        [MenuItem("The Jade Seal/Remove Wind Sway from Spawn Bamboo (4 Trees)", priority = 11)]
        [MenuItem("Tools/The Jade Seal/Remove Wind Sway from Spawn Bamboo (4 Trees)", priority = 11)]
        public static void RemoveWindSwayFromSpawnBamboo()
        {
            if (!ValidateActiveScene(out var activeScene))
            {
                return;
            }

            int removedCount = 0;
            for (int i = 0; i < VerifiedTargets.Length; i++)
            {
                var target = VerifiedTargets[i];
                var candidateGo = FindTargetBamboo(target);
                if (candidateGo == null) continue;

                var sway = candidateGo.GetComponent<GentleWindSway>();
                if (sway != null)
                {
                    Undo.DestroyObjectImmediate(sway);
                    removedCount++;
                    Debug.Log($"<color=#e67e22><b>[The Jade Seal]</b></color> Removed GentleWindSway from: <b>{candidateGo.name}</b>");
                }
            }

            if (removedCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
            }

            EditorUtility.DisplayDialog(
                "Wind Sway Removed",
                $"Removed GentleWindSway component from {removedCount} bamboo tree(s).\nScene marked dirty.",
                "OK");
        }

        /// <summary>
        /// Validates that the currently active scene is the Systems Integration scene,
        /// and strictly rejects running on teammate scenes.
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
        /// Locates a target bamboo tree by dual-matching its GlobalObjectId and its surveyed ground coordinates.
        /// </summary>
        private static GameObject FindTargetBamboo(BambooTargetInfo target)
        {
            // First search directly inside the approach root if available
            var approachRoot = GameObject.Find("01_Whispering_Bamboo_Approach");
            if (approachRoot != null)
            {
                var found = CheckTransformChildren(approachRoot.transform, target);
                if (found != null) return found;
            }

            // Fallback: search all root objects in the scene
            var activeScene = EditorSceneManager.GetActiveScene();
            var roots = activeScene.GetRootGameObjects();
            foreach (var root in roots)
            {
                var found = CheckTransformChildren(root.transform, target);
                if (found != null) return found;
            }

            return null;
        }

        private static GameObject CheckTransformChildren(Transform parent, BambooTargetInfo target)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == target.Name)
                {
                    // Check world position match
                    float dist = Vector3.Distance(child.position, target.ExpectedPosition);
                    if (dist <= target.PositionTolerance)
                    {
                        return child.gameObject;
                    }

                    // Check GlobalObjectId targetObjectId match
                    var gid = GlobalObjectId.GetGlobalObjectIdSlow(child.gameObject);
                    if (gid.targetObjectId == target.TargetObjectId)
                    {
                        return child.gameObject;
                    }
                }

                // Check deeper if needed
                if (child.childCount > 0 && child.name.StartsWith("01_"))
                {
                    var deep = CheckTransformChildren(child, target);
                    if (deep != null) return deep;
                }
            }

            return null;
        }
    }
}
#endif
