using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;
using JadeSeal.EnemyAI.Core;
using JadeSeal.EnemyAI.Visuals;

namespace JadeSeal.EnemyAI.Editor
{
    public static class MountainArcherAnimationSetup
    {
        [MenuItem("The Jade Seal/AI/Setup Complete Animator and Fix Duplicate Agents", false, 15)]
        public static void SetupAndCleanScene()
        {
            // 1. Clean up duplicate agents in the scene, keep only 1
            var agents = Object.FindObjectsByType<MountainArcherAgent>(FindObjectsSortMode.None);
            if (agents.Length > 1)
            {
                Debug.Log($"<color=yellow>[SceneCleanup]</color> Found {agents.Length} agents. Cleaning duplicates...");
                for (int i = 1; i < agents.Length; i++)
                {
                    Undo.DestroyObjectImmediate(agents[i].gameObject);
                }
            }

            // Also clean duplicate waypoint groups
            var waypoints = GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            int wpCount = 0;
            foreach (var go in waypoints)
            {
                if (go.name == "MountainArcher_Waypoints")
                {
                    wpCount++;
                    if (wpCount > 1) Undo.DestroyObjectImmediate(go);
                }
            }

            // 2. Locate or create Animator Controller
            string controllerPath = "Assets/JadeSeal/Characters/MountainArcher/AC_MountainArcher.controller";
            AnimatorController animController = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);

            if (animController == null)
            {
                animController = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                Debug.Log($"<color=#00FF99>[AnimatorSetup]</color> Created new AnimatorController at: {controllerPath}");
            }

            // 3. Ensure required Animator parameters exist
            EnsureParameter(animController, "Speed", AnimatorControllerParameterType.Float);
            EnsureParameter(animController, "IsAiming", AnimatorControllerParameterType.Bool);
            EnsureParameter(animController, "IsKiting", AnimatorControllerParameterType.Bool);
            EnsureParameter(animController, "Shoot", AnimatorControllerParameterType.Trigger);
            EnsureParameter(animController, "Hit", AnimatorControllerParameterType.Trigger);
            EnsureParameter(animController, "Death", AnimatorControllerParameterType.Trigger);
            EnsureParameter(animController, "IsDead", AnimatorControllerParameterType.Bool);

            // 4. Find all available animation clips across FBX models
            List<AnimationClip> availableClips = new List<AnimationClip>();
            string[] fbxGuids = AssetDatabase.FindAssets("t:Model", new[] { "Assets/JadeSeal/Characters" });
            foreach (string guid in fbxGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
                foreach (Object obj in assets)
                {
                    if (obj is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                    {
                        availableClips.Add(clip);
                        Debug.Log($"[AnimationSetup] Found clip: '{clip.name}' in {path}");
                    }
                }
            }

            // 5. Build State Machine States
            var rootStateMachine = animController.layers[0].stateMachine;

            // Find or create Locomotion / Idle state
            AnimatorState idleState = FindOrCreateState(rootStateMachine, "Locomotion_Idle");
            AnimatorState aimState = FindOrCreateState(rootStateMachine, "Archery_Aim");
            AnimatorState deathState = FindOrCreateState(rootStateMachine, "Death");

            // Assign clips if available
            foreach (var clip in availableClips)
            {
                if (clip.name.ToLower().Contains("draw") || clip.name.ToLower().Contains("shoot") || clip.name.ToLower().Contains("aim"))
                {
                    aimState.motion = clip;
                }
                else if (idleState.motion == null)
                {
                    idleState.motion = clip;
                }
            }

            // Transitions: Idle -> Aim
            if (!HasTransition(idleState, aimState))
            {
                var trans = idleState.AddTransition(aimState);
                trans.AddCondition(AnimatorConditionMode.If, 0, "IsAiming");
                trans.hasExitTime = false;
                trans.duration = 0.2f;
            }

            // Transitions: Aim -> Idle
            if (!HasTransition(aimState, idleState))
            {
                var trans = aimState.AddTransition(idleState);
                trans.AddCondition(AnimatorConditionMode.IfNot, 0, "IsAiming");
                trans.hasExitTime = false;
                trans.duration = 0.25f;
            }

            // AnyState -> Death
            if (!HasTransition(rootStateMachine, deathState))
            {
                var trans = rootStateMachine.AddAnyStateTransition(deathState);
                trans.AddCondition(AnimatorConditionMode.If, 0, "IsDead");
                trans.hasExitTime = false;
                trans.duration = 0.15f;
            }

            // 6. Assign AnimatorController to the scene Archer
            var activeArcher = Object.FindFirstObjectByType<MountainArcherAgent>();
            if (activeArcher != null)
            {
                var animator = activeArcher.GetComponentInChildren<Animator>();
                if (animator == null)
                {
                    Transform modelChild = activeArcher.transform.Find("MountainArcher_3DModel");
                    if (modelChild != null) animator = modelChild.gameObject.AddComponent<Animator>();
                    else animator = activeArcher.gameObject.AddComponent<Animator>();
                }

                animator.runtimeAnimatorController = animController;
                animator.applyRootMotion = false; // NavMeshAgent drives translation!

                var visualizer = activeArcher.GetComponent<MountainArcherVisualizer>();
                if (visualizer != null) visualizer.ResolveAnimator();

                Debug.Log($"<color=#00FF99>[AnimationSetup] Successfully assigned {animController.name} to {activeArcher.name}'s Animator!</color>");
            }

            AssetDatabase.SaveAssets();
            Debug.Log("<color=#00FF99><b>[AnimationSetup] Completed! Scene cleaned and Animator Controller configured.</b></color>");
        }

        private static void EnsureParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
        {
            foreach (var p in controller.parameters)
            {
                if (p.name == name) return;
            }
            controller.AddParameter(name, type);
        }

        private static AnimatorState FindOrCreateState(AnimatorStateMachine sm, string name)
        {
            foreach (var childState in sm.states)
            {
                if (childState.state.name == name) return childState.state;
            }
            return sm.AddState(name);
        }

        private static bool HasTransition(AnimatorState from, AnimatorState to)
        {
            foreach (var t in from.transitions)
            {
                if (t.destinationState == to) return true;
            }
            return false;
        }

        private static bool HasTransition(AnimatorStateMachine sm, AnimatorState to)
        {
            foreach (var t in sm.anyStateTransitions)
            {
                if (t.destinationState == to) return true;
            }
            return false;
        }
    }
}
