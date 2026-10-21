using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine.AI;
using System.Collections.Generic;
using JadeSeal.EnemyAI.Core;
using JadeSeal.EnemyAI.Sensory;
using JadeSeal.EnemyAI.Visuals;
using JadeSeal.EnemyAI.Combat;
using JadeSeal.EnemyAI.Common;
using JadeSeal.EnemyAI.RoyalGuard;
using JadeSeal.EnemyAI.BambooHunter;
using JadeSeal.EnemyAI.ClanMaster;

namespace JadeSeal.EnemyAI.Editor
{
    /// <summary>
    /// Comprehensive Editor tool for The Jade Seal multi-agent expansion.
    /// Manages Animator Controllers, Prefab generation, and Scene integration
    /// for all four enemy agents (Mountain Archer, Royal Guard, Bamboo Hunter, Clan Master).
    /// </summary>
    public static class MultiAgentSetupEditor
    {
        [MenuItem("The Jade Seal/AI/Setup Animator Controllers for All Agents", false, 1)]
        public static void CreateAllAnimators()
        {
            SetupController("Assets/JadeSeal/Characters/RoyalGuard/AC_RoyalGuard.controller", new[] { "Speed", "IsBlocking", "MeleeAttack", "Hit", "Death", "IsDead" });
            SetupController("Assets/JadeSeal/Characters/BambooHunter/AC_BambooHunter.controller", new[] { "Speed", "IsAiming", "Shoot", "Hit", "Death", "IsDead" });
            SetupController("Assets/JadeSeal/Characters/ClanMaster/AC_ClanMaster.controller", new[] { "Speed", "IsDefending", "IsRallying", "Cleave", "Hit", "Death", "IsDead" });
            Debug.Log("<color=#00FF99><b>[MultiAgentSetup]</b> Successfully generated and configured Animator Controllers for all 3 remaining agents!</color>");
        }

        private static void SetupController(string path, string[] parameters)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            }

            foreach (var p in parameters)
            {
                if (!HasParameter(controller, p))
                {
                    if (p.StartsWith("Is"))
                        controller.AddParameter(p, AnimatorControllerParameterType.Bool);
                    else if (p == "Speed")
                        controller.AddParameter(p, AnimatorControllerParameterType.Float);
                    else
                        controller.AddParameter(p, AnimatorControllerParameterType.Trigger);
                }
            }
        }

        private static bool HasParameter(AnimatorController controller, string name)
        {
            foreach (var p in controller.parameters)
            {
                if (p.name == name) return true;
            }
            return false;
        }

        [MenuItem("The Jade Seal/AI/Spawn All 4 Enemy Agents in Scene", false, 2)]
        public static void SpawnAllFourAgents()
        {
            CreateAllAnimators();

            Vector3 centerPos = Vector3.zero;
            Camera cam = Camera.main;
            if (cam != null) centerPos = cam.transform.position + cam.transform.forward * 12f;
            if (NavMesh.SamplePosition(centerPos, out NavMeshHit chHit, 15f, NavMesh.AllAreas)) centerPos = chHit.position;

            // 1. Mountain Archer (High ground / Mountain Pass)
            Vector3 archerPos = centerPos + new Vector3(-8f, 0, 10f);
            SpawnMountainArcher(archerPos);

            // 2. Royal Guard (Heavy Melee Defender at Gate)
            Vector3 guardPos = centerPos + new Vector3(8f, 0, 8f);
            SpawnRoyalGuard(guardPos);

            // 3. Bamboo Hunter (Stealthy Archer in Bamboo Grove)
            Vector3 hunterPos = centerPos + new Vector3(-10f, 0, -8f);
            SpawnBambooHunter(hunterPos);

            // 4. Clan Master (Elite Commander at Dais)
            Vector3 masterPos = centerPos + new Vector3(6f, 0, -10f);
            SpawnClanMaster(masterPos);

            // Spawn Player nearby
            SpawnUniversalPlayer(centerPos);

            Debug.Log("<color=#00FF99><b>[The Jade Seal]</b> Successfully spawned all 4 autonomous enemy agents into their individual mountain/forest zones!</color>");
        }

        [MenuItem("The Jade Seal/AI/Spawn Royal Guard (Hiruni)", false, 11)]
        public static GameObject SpawnRoyalGuard(Vector3 spawnPos = default)
        {
            if (spawnPos == default) spawnPos = GetSafeSpawnPosition(new Vector3(6f, 0, 6f));

            GameObject guard = new GameObject("Enemy_RoyalGuard_Agent");
            guard.transform.position = spawnPos;

            var nav = guard.AddComponent<NavMeshAgent>();
            nav.speed = 2.2f;
            nav.angularSpeed = 240f;
            nav.acceleration = 12f;

            var col = guard.AddComponent<CapsuleCollider>();
            col.radius = 0.5f;
            col.height = 1.95f;
            col.center = new Vector3(0, 0.98f, 0);

            guard.AddComponent<RoyalGuardHealth>();
            guard.AddComponent<EnemyPerception>();
            var agent = guard.AddComponent<RoyalGuardAgent>();
            var vis = guard.AddComponent<RoyalGuardVisualizer>();

            // Attach RoyalGuard.fbx model
            string modelPath = "Assets/JadeSeal/Characters/RoyalGuard/RoyalGuard.fbx";
            GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (modelPrefab != null)
            {
                GameObject modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab, guard.transform);
                modelInstance.name = "RoyalGuard_3DModel";
                vis.ApplyMaterialStyling();
            }

            // Waypoints
            agent.SetWaypoints(CreateWaypoints("RoyalGuard_Waypoints", spawnPos, 8f));

            Selection.activeGameObject = guard;
            Undo.RegisterCreatedObjectUndo(guard, "Spawn Royal Guard");
            return guard;
        }

        [MenuItem("The Jade Seal/AI/Spawn Bamboo Hunter (Keshika)", false, 12)]
        public static GameObject SpawnBambooHunter(Vector3 spawnPos = default)
        {
            if (spawnPos == default) spawnPos = GetSafeSpawnPosition(new Vector3(-8f, 0, -6f));

            GameObject hunter = new GameObject("Enemy_BambooHunter_Agent");
            hunter.transform.position = spawnPos;

            var nav = hunter.AddComponent<NavMeshAgent>();
            nav.speed = 2.4f;
            nav.angularSpeed = 260f;
            nav.acceleration = 14f;

            var col = hunter.AddComponent<CapsuleCollider>();
            col.radius = 0.45f;
            col.height = 1.85f;
            col.center = new Vector3(0, 0.92f, 0);

            hunter.AddComponent<BambooHunterHealth>();
            hunter.AddComponent<EnemyPerception>();
            var agent = hunter.AddComponent<BambooHunterAgent>();
            var vis = hunter.AddComponent<BambooHunterVisualizer>();

            string modelPath = "Assets/JadeSeal/Characters/BambooHunter/BambooHunter.fbx";
            GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (modelPrefab != null)
            {
                GameObject modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab, hunter.transform);
                modelInstance.name = "BambooHunter_3DModel";
                vis.ApplyMaterialStyling();
            }

            agent.SetWaypoints(CreateWaypoints("BambooHunter_Waypoints", spawnPos, 10f));

            Selection.activeGameObject = hunter;
            Undo.RegisterCreatedObjectUndo(hunter, "Spawn Bamboo Hunter");
            return hunter;
        }

        [MenuItem("The Jade Seal/AI/Spawn Clan Master (Thamod)", false, 13)]
        public static GameObject SpawnClanMaster(Vector3 spawnPos = default)
        {
            if (spawnPos == default) spawnPos = GetSafeSpawnPosition(new Vector3(4f, 0, -8f));

            GameObject master = new GameObject("Enemy_ClanMaster_Agent");
            master.transform.position = spawnPos;

            var nav = master.AddComponent<NavMeshAgent>();
            nav.speed = 2.0f;
            nav.angularSpeed = 200f;
            nav.acceleration = 10f;

            var col = master.AddComponent<CapsuleCollider>();
            col.radius = 0.52f;
            col.height = 2.0f;
            col.center = new Vector3(0, 1.0f, 0);

            master.AddComponent<ClanMasterHealth>();
            master.AddComponent<EnemyPerception>();
            var agent = master.AddComponent<ClanMasterAgent>();
            var vis = master.AddComponent<ClanMasterVisualizer>();

            string modelPath = "Assets/JadeSeal/Characters/ClanMaster/ClanMaster.fbx";
            GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (modelPrefab != null)
            {
                GameObject modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab, master.transform);
                modelInstance.name = "ClanMaster_3DModel";
                vis.ApplyMaterialStyling();
            }

            agent.SetWaypoints(CreateWaypoints("ClanMaster_Waypoints", spawnPos, 12f));

            Selection.activeGameObject = master;
            Undo.RegisterCreatedObjectUndo(master, "Spawn Clan Master");
            return master;
        }

        public static GameObject SpawnMountainArcher(Vector3 spawnPos = default)
        {
            if (spawnPos == default) spawnPos = GetSafeSpawnPosition(new Vector3(-6f, 0, 8f));

            GameObject archer = new GameObject("Enemy_MountainArcher_Agent");
            archer.transform.position = spawnPos;

            var nav = archer.AddComponent<NavMeshAgent>();
            nav.speed = 4.2f;
            var col = archer.AddComponent<CapsuleCollider>();
            col.radius = 0.45f;
            col.height = 1.9f;
            col.center = new Vector3(0, 0.95f, 0);

            archer.AddComponent<MountainArcherHealth>();
            archer.AddComponent<MountainArcherPerception>();
            var agent = archer.AddComponent<MountainArcherAgent>();
            var vis = archer.AddComponent<MountainArcherVisualizer>();

            string modelPath = "Assets/JadeSeal/Characters/MountainArcher/MountainArcher_Model.fbx";
            GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (modelPrefab != null)
            {
                GameObject modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab, archer.transform);
                modelInstance.name = "MountainArcher_3DModel";
                vis.BindNewCharacterModel(modelInstance);
            }

            agent.SetPatrolWaypoints(CreateWaypoints("MountainArcher_Waypoints", spawnPos, 8f));
            Undo.RegisterCreatedObjectUndo(archer, "Spawn Mountain Archer");
            return archer;
        }

        public static GameObject SpawnUniversalPlayer(Vector3 centerPos)
        {
            Vector3 spawnPos = centerPos;
            if (NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, 6f, NavMesh.AllAreas))
            {
                spawnPos = hit.position;
            }

            GameObject playerObj = GameObject.Find("Test_Player_Target");
            if (playerObj == null)
            {
                playerObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                playerObj.name = "Test_Player_Target";
                playerObj.tag = "Player";
                playerObj.GetComponent<Renderer>().material.color = Color.cyan;
            }

            playerObj.transform.position = spawnPos;
            var combat = playerObj.GetComponent<TestPlayerCombatTarget>();
            if (combat == null) combat = playerObj.AddComponent<TestPlayerCombatTarget>();

            Selection.activeGameObject = playerObj;
            SceneView.lastActiveSceneView?.FrameSelected();
            return playerObj;
        }

        private static List<Transform> CreateWaypoints(string groupName, Vector3 center, float radius)
        {
            GameObject group = new GameObject(groupName);
            group.transform.position = center;
            var list = new List<Transform>();

            for (int i = 0; i < 3; i++)
            {
                float angle = i * (360f / 3f) * Mathf.Deg2Rad;
                Vector3 pos = center + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;
                if (NavMesh.SamplePosition(pos, out NavMeshHit hit, 6f, NavMesh.AllAreas))
                {
                    pos = hit.position;
                }
                GameObject wp = new GameObject($"{groupName}_Pt{i + 1}");
                wp.transform.position = pos;
                wp.transform.SetParent(group.transform);
                list.Add(wp.transform);
            }

            Undo.RegisterCreatedObjectUndo(group, $"Create {groupName}");
            return list;
        }

        private static Vector3 GetSafeSpawnPosition(Vector3 offset)
        {
            Vector3 basePos = Vector3.zero;
            Camera cam = Camera.main;
            if (cam != null) basePos = cam.transform.position + cam.transform.forward * 8f;
            Vector3 candidate = basePos + offset;
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 10f, NavMesh.AllAreas))
            {
                return hit.position;
            }
            return candidate;
        }
    }
}
