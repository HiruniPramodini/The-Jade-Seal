using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace JadeSeal.MountainArcher.Editor
{
    public static class MountainArcherValidation
    {
        private const string Root = "Assets/MountainArcherAI";
        private static readonly List<string> results = new List<string>();

        private static void Check(bool condition, string name)
        {
            if (!condition) throw new Exception(name);
            results.Add("PASS " + name);
        }

        [MenuItem("Jade Seal/Mountain Archer/Create or update placeholder prefabs", false, 1)]
        public static void CreateAssets()
        {
            Directory.CreateDirectory(Root + "/Prefabs");
            var settings = AssetDatabase.LoadAssetAtPath<MountainArcherSettings>(Root + "/DefaultSettings.asset");
            if (!settings)
            {
                settings = ScriptableObject.CreateInstance<MountainArcherSettings>();
                AssetDatabase.CreateAsset(settings, Root + "/DefaultSettings.asset");
            }

            // Create Mountain Archer Arrow Prefab
            var arrow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            arrow.name = "MountainArcherArrow_PLACEHOLDER";
            arrow.layer = 2;
            arrow.transform.localScale = new Vector3(.03f, .3f, .03f);
            arrow.transform.localRotation = Quaternion.Euler(90f, 0, 0);

            var rb = arrow.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            arrow.AddComponent<MountainArcherArrow>();

            var arrowAsset = PrefabUtility.SaveAsPrefabAsset(arrow, Root + "/Prefabs/MountainArcherArrow.prefab");
            UnityEngine.Object.DestroyImmediate(arrow);

            // Create Mountain Archer Prefab
            var archer = new GameObject("MountainArcher_PLACEHOLDER");
            archer.layer = 2;

            var agent = archer.AddComponent<NavMeshAgent>();
            agent.speed = 6.0f; // Agile, faster mobility than Royal Guard (3.5) and Bamboo Hunter (5.2)
            agent.acceleration = 16f;
            agent.angularSpeed = 360f;
            agent.height = 1.9f;
            agent.radius = .45f;
            agent.stoppingDistance = .35f;

            var health = archer.AddComponent<MountainArcherHealth>(); // 120 HP (Bamboo 95 < Mountain 120 < Royal Guard 180)
            var weapon = archer.AddComponent<MountainArcherBow>();
            var brain = archer.AddComponent<MountainArcherBrain>();
            brain.settings = settings;

            archer.AddComponent<MountainArcherPresentation>();

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "TemporaryVisual_MountainArcher";
            body.layer = 2;
            body.transform.SetParent(archer.transform, false);
            body.transform.localPosition = Vector3.up;
            body.transform.localScale = new Vector3(.75f, .95f, .75f);

            var eyes = new GameObject("Eyes").transform;
            eyes.SetParent(archer.transform, false);
            eyes.localPosition = Vector3.up * 1.65f;
            archer.GetComponent<MountainArcherPerception>().eyes = eyes;

            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(archer.transform, false);
            muzzle.localPosition = new Vector3(.25f, 1.35f, .7f);

            var bow = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bow.name = "Longbow_PLACEHOLDER";
            bow.transform.SetParent(muzzle, false);
            bow.transform.localScale = new Vector3(.06f, .85f, .12f);
            UnityEngine.Object.DestroyImmediate(bow.GetComponent<Collider>());

            var serialized = new SerializedObject(weapon);
            serialized.FindProperty("muzzle").objectReferenceValue = muzzle;
            serialized.FindProperty("arrowPrefab").objectReferenceValue = arrowAsset.GetComponent<MountainArcherArrow>();
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(archer, Root + "/Prefabs/MountainArcher.prefab");
            UnityEngine.Object.DestroyImmediate(archer);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=#00FF99>[MountainArcherValidation]</color> Successfully created Mountain Archer prefabs and settings!");
        }

        [MenuItem("Jade Seal/Mountain Archer/Run deterministic checks", false, 2)]
        public static void Run()
        {
            try
            {
                CreateAssets();
                var s = ScriptableObject.CreateInstance<MountainArcherSettings>();
                var c = new MountainArcherUtility.Context();

                Check(MountainArcherUtility.Choose(c, MountainArcherUtility.State.Attack, 1, s) == MountainArcherUtility.State.Patrol, "Missing target invalidates Attack");
                c.memory = true;
                Check(MountainArcherUtility.Choose(c, MountainArcherUtility.State.Patrol, 1, s) == MountainArcherUtility.State.Investigate, "Lost visible target investigates memory");
                c.lowHealth = true;
                Check(MountainArcherUtility.Choose(c, MountainArcherUtility.State.Attack, 0, s) == MountainArcherUtility.State.Retreat, "Emergency retreat bypasses dwell even without cover");
                c.lowHealth = false;
                c.reload = true;
                Check(MountainArcherUtility.Choose(c, MountainArcherUtility.State.Patrol, 0, s) == MountainArcherUtility.State.Reload, "Quiver restock bypasses dwell");

                Debug.Log($"<color=#00FF99>[MountainArcherValidation]</color> All deterministic tests PASSED! ({results.Count} checks)");
            }
            catch (Exception ex)
            {
                Debug.LogError($"<color=#FF3333>[MountainArcherValidation]</color> Test failed: {ex.Message}");
            }
        }
    }
}
