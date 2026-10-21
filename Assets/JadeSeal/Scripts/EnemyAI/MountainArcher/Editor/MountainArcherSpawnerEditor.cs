using UnityEngine;
using UnityEditor;
using UnityEngine.AI;
using JadeSeal.EnemyAI.Core;
using JadeSeal.EnemyAI.Sensory;
using JadeSeal.EnemyAI.Visuals;
using JadeSeal.EnemyAI.Combat;

namespace JadeSeal.EnemyAI.Editor
{
    /// <summary>
    /// Editor utility to spawn and configure the Mountain Archer AI Agent with a single click.
    /// Places the agent into the scene with properly calibrated NavMeshAgent, Perception, and Visualizer.
    /// </summary>
    public static class MountainArcherSpawnerEditor
    {
        [MenuItem("The Jade Seal/AI/Spawn Mountain Archer in Scene", false, 10)]
        public static void SpawnArcher()
        {
            // Try to find Mountain Pass anchor or scene center
            Vector3 spawnPosition = Vector3.zero;
            var mountainPassAnchor = GameObject.Find("MOUNTAIN_PASS");
            if (mountainPassAnchor == null) mountainPassAnchor = GameObject.Find("Enemy_01_Bamboo_Hunter");
            if (mountainPassAnchor != null)
            {
                spawnPosition = mountainPassAnchor.transform.position + Vector3.right * 4.0f;
            }
            else if (SceneView.lastActiveSceneView != null)
            {
                spawnPosition = SceneView.lastActiveSceneView.camera.transform.position + SceneView.lastActiveSceneView.camera.transform.forward * 8.0f;
            }

            // Snap to NavMesh
            if (NavMesh.SamplePosition(spawnPosition, out NavMeshHit hit, 10.0f, NavMesh.AllAreas))
            {
                spawnPosition = hit.position;
            }

            // Create Agent GameObject
            GameObject archerObj = new GameObject("Enemy_MountainArcher_Agent");
            archerObj.transform.position = spawnPosition;

            // NavMeshAgent configuration
            var nav = archerObj.AddComponent<NavMeshAgent>();
            nav.speed = 4.2f;
            nav.angularSpeed = 240f;
            nav.acceleration = 12f;
            // Capsule Collider for hit detection
            var col = archerObj.AddComponent<CapsuleCollider>();
            col.radius = 0.45f;
            col.height = 1.9f;
            col.center = new Vector3(0, 0.95f, 0);

            // Core AI & Combat Components
            archerObj.AddComponent<MountainArcherHealth>();
            archerObj.AddComponent<MountainArcherPerception>();
            var agent = archerObj.AddComponent<MountainArcherAgent>();
            archerObj.AddComponent<MountainArcherVisualizer>();

            // Try to load downloaded 3D Archer Model
            AttachModelToArcher(archerObj);

            // Setup Waypoints Group for Mountain Pass
            GameObject waypointGroup = new GameObject("MountainArcher_Waypoints");
            waypointGroup.transform.position = spawnPosition;
            var waypointsList = new System.Collections.Generic.List<Transform>();

            for (int i = 0; i < 3; i++)
            {
                float angle = i * (360f / 3f) * Mathf.Deg2Rad;
                Vector3 wpPos = spawnPosition + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 8f;
                if (NavMesh.SamplePosition(wpPos, out NavMeshHit wpHit, 5f, NavMesh.AllAreas))
                {
                    wpPos = wpHit.position;
                }
                GameObject wp = new GameObject($"Waypoint_{i + 1}");
                wp.transform.position = wpPos;
                wp.transform.SetParent(waypointGroup.transform);
                waypointsList.Add(wp.transform);
            }

            // Assign patrol waypoints directly to agent
            agent.SetPatrolWaypoints(waypointsList);

            // Ensure Main Camera looks at Archer in Game View
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                mainCam.transform.position = spawnPosition + Vector3.back * 6f + Vector3.up * 3f;
                mainCam.transform.LookAt(spawnPosition + Vector3.up * 1.2f);
            }

            Selection.activeGameObject = archerObj;
            SceneView.lastActiveSceneView?.FrameSelected();

            Undo.RegisterCreatedObjectUndo(archerObj, "Spawn Mountain Archer Agent");
            Undo.RegisterCreatedObjectUndo(waypointGroup, "Spawn Mountain Archer Waypoints");

            Debug.Log($"<color=#00FF99><b>[MountainArcher]</b> Successfully spawned Mountain Archer Agent with 3D character model at {spawnPosition}!</color>");
        }

        [MenuItem("The Jade Seal/AI/Attach Downloaded 3D Model to Archer", false, 11)]
        public static void AttachModelToExistingArcher()
        {
            var archer = Object.FindFirstObjectByType<MountainArcherAgent>();
            if (archer == null)
            {
                Debug.LogWarning("[MountainArcher] No Mountain Archer Agent found in scene. Click 'Spawn Mountain Archer in Scene' first.");
                return;
            }

            AttachModelToArcher(archer.gameObject);
            Debug.Log("<color=#00FF99><b>[MountainArcher]</b> Replaced capsule with 3D Archer Model successfully!</color>");
        }

        private static void AttachModelToArcher(GameObject archerObj)
        {
            // Remove existing prototype capsule if present
            Transform oldVisuals = archerObj.transform.Find("ArcherPrototypeVisuals");
            if (oldVisuals != null)
            {
                Object.DestroyImmediate(oldVisuals.gameObject);
            }

            // Remove previous model if already attached
            Transform oldModel = archerObj.transform.Find("MountainArcher_3DModel");
            if (oldModel != null)
            {
                Object.DestroyImmediate(oldModel.gameObject);
            }

            // Load and instantiate friend's official MA.fbx model
            string modelPath = "Assets/JadeSeal/Characters/MountainArcher/MA.fbx";
            if (!System.IO.File.Exists(modelPath) && !System.IO.File.Exists(Application.dataPath + "/JadeSeal/Characters/MountainArcher/MA.fbx"))
            {
                modelPath = "Assets/JadeSeal/Characters/MountainArcher/MountainArcher_Model.fbx";
            }

            GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);

            if (modelPrefab != null)
            {
                GameObject modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab, archerObj.transform);
                modelInstance.name = "MountainArcher_3DModel";
                modelInstance.transform.localPosition = Vector3.zero;
                modelInstance.transform.localRotation = Quaternion.identity;
                modelInstance.transform.localScale = Vector3.one;

                // Strip out Blender rogue lights exported inside FBX
                foreach (var l in modelInstance.GetComponentsInChildren<Light>(true))
                {
                    Object.DestroyImmediate(l.gameObject);
                }

                // Inform Visualizer to bind Animator and clean up any old visuals
                var visualizer = archerObj.GetComponent<MountainArcherVisualizer>();
                if (visualizer != null)
                {
                    visualizer.BindNewCharacterModel(modelInstance);
                }

                Debug.Log($"<color=#00FF99><b>[MountainArcher]</b> Successfully attached official 3D model '{modelPrefab.name}' to {archerObj.name}!</color>");
            }
            else
            {
                Debug.LogWarning($"[MountainArcher] 3D Model not found at path: {modelPath}");
            }
        }

        [MenuItem("The Jade Seal/AI/Spawn Interactive Test Player (WASD + Combat)", false, 12)]
        public static void SpawnInteractiveTestPlayer()
        {
            var archer = Object.FindFirstObjectByType<MountainArcherAgent>();
            Vector3 spawnPos = (archer != null) ? archer.transform.position + archer.transform.forward * 12f : Vector3.zero;

            if (NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, 8f, NavMesh.AllAreas))
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
            combat.SetArcherReference(archer);

            Selection.activeGameObject = playerObj;
            SceneView.lastActiveSceneView?.FrameSelected();

            Undo.RegisterCreatedObjectUndo(playerObj, "Spawn Interactive Test Player");
            Debug.Log($"<color=#00FF99><b>[TestPlayer]</b> Spawned interactive player at {spawnPos}! Use WASD to move and Left-Click to attack!</color>");
        }
    }
}
