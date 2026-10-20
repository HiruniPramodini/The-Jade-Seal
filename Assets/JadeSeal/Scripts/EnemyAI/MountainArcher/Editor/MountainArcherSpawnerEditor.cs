using UnityEngine;
using UnityEditor;
using UnityEngine.AI;
using JadeSeal.EnemyAI.Core;
using JadeSeal.EnemyAI.Sensory;
using JadeSeal.EnemyAI.Visuals;

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
            nav.stoppingDistance = 0.5f;
            nav.radius = 0.45f;
            nav.height = 1.9f;

            // Core AI Components
            archerObj.AddComponent<MountainArcherPerception>();
            archerObj.AddComponent<MountainArcherAgent>();
            archerObj.AddComponent<MountainArcherVisualizer>();

            // Setup Waypoints Group for Mountain Pass
            GameObject waypointGroup = new GameObject("MountainArcher_Waypoints");
            waypointGroup.transform.position = spawnPosition;
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
            }

            Selection.activeGameObject = archerObj;
            Undo.RegisterCreatedObjectUndo(archerObj, "Spawn Mountain Archer Agent");
            Undo.RegisterCreatedObjectUndo(waypointGroup, "Spawn Mountain Archer Waypoints");

            Debug.Log($"<color=#00FF99><b>[MountainArcher]</b> Successfully spawned Mountain Archer Agent at {spawnPosition} with NavMeshAgent, Perception, and Visualizer!</color>");
        }
    }
}
