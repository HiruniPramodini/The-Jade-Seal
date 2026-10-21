using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
namespace JadeSeal.RoyalGuard.Editor
{
    public static class RoyalGuardValidation
    {
        const string Root = "Assets/RoyalGuardAI";
        static readonly List<string> results = new List<string>();
        static void Check(bool condition, string name) { if (!condition) throw new Exception(name); results.Add("PASS " + name); }
        [MenuItem("Jade Seal/Royal Guard/Create or update placeholder prefabs")]
        public static void CreateAssets()
        {
            Directory.CreateDirectory(Root + "/Prefabs");
            var settings = AssetDatabase.LoadAssetAtPath<RoyalGuardSettings>(Root + "/DefaultSettings.asset");
            if (!settings) { settings = ScriptableObject.CreateInstance<RoyalGuardSettings>(); AssetDatabase.CreateAsset(settings, Root + "/DefaultSettings.asset"); }
            var bolt = GameObject.CreatePrimitive(PrimitiveType.Cube); bolt.name = "RoyalGuardBolt_PLACEHOLDER";
            bolt.transform.localScale = new Vector3(.045f, .045f, .4f);
            var rb = bolt.AddComponent<Rigidbody>(); rb.useGravity = false; rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            bolt.AddComponent<RoyalGuardProjectile>();
            var boltAsset = PrefabUtility.SaveAsPrefabAsset(bolt, Root + "/Prefabs/RoyalGuardBolt.prefab"); UnityEngine.Object.DestroyImmediate(bolt);
            var guard = new GameObject("RoyalGuard_PLACEHOLDER");
            var agent = guard.AddComponent<NavMeshAgent>(); agent.speed = 3.5f; agent.height = 2; agent.radius = .4f; agent.stoppingDistance = .35f;
            guard.layer = 2;
            guard.AddComponent<RoyalGuardHealth>();
            var weapon = guard.AddComponent<RoyalGuardCrossbow>();
            var brain = guard.AddComponent<RoyalGuardBrain>(); brain.settings = settings;
            guard.AddComponent<RoyalGuardPresentation>();
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule); body.name = "TemporaryVisual_ReplaceWithLicensedModel";
            body.layer = 2; body.transform.SetParent(guard.transform, false); body.transform.localPosition = Vector3.up;
            body.transform.localScale = new Vector3(.8f, 1, .8f);
            // Keep a collider for the team weapon's damage raycast; no dynamic body fights the agent.
            var eyes = new GameObject("Eyes").transform; eyes.SetParent(guard.transform, false); eyes.localPosition = Vector3.up * 1.65f;
            guard.GetComponent<RoyalGuardPerception>().eyes = eyes;
            var muzzle = new GameObject("Muzzle").transform; muzzle.SetParent(guard.transform, false); muzzle.localPosition = new Vector3(.25f, 1.4f, .7f);
            var bow = GameObject.CreatePrimitive(PrimitiveType.Cube); bow.name = "Crossbow_PLACEHOLDER"; bow.transform.SetParent(muzzle, false); bow.transform.localScale = new Vector3(.7f, .08f, .15f); UnityEngine.Object.DestroyImmediate(bow.GetComponent<Collider>());
            var serialized = new SerializedObject(weapon);
            serialized.FindProperty("muzzle").objectReferenceValue = muzzle;
            serialized.FindProperty("boltPrefab").objectReferenceValue = boltAsset.GetComponent<RoyalGuardProjectile>(); serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(guard, Root + "/Prefabs/RoyalGuard.prefab"); UnityEngine.Object.DestroyImmediate(guard);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        }
        [MenuItem("Jade Seal/Royal Guard/Run deterministic checks")]
        public static void Run()
        {
            try
            {
                CreateAssets();
                var s = ScriptableObject.CreateInstance<RoyalGuardSettings>();
                var c = new RoyalGuardUtility.Context();
                Check(RoyalGuardUtility.Choose(c, RoyalGuardUtility.State.Attack, 1, s) == RoyalGuardUtility.State.Patrol, "Missing target invalidates Attack");
                c.memory = true;
                Check(RoyalGuardUtility.Choose(c, RoyalGuardUtility.State.Patrol, 1, s) == RoyalGuardUtility.State.Investigate, "Lost visible target investigates memory");
                c.lowHealth = true;
                Check(RoyalGuardUtility.Choose(c, RoyalGuardUtility.State.Attack, 0, s) == RoyalGuardUtility.State.Retreat, "Emergency retreat bypasses dwell even without cover");
                c.lowHealth = false; c.reload = true;
                Check(RoyalGuardUtility.Choose(c, RoyalGuardUtility.State.Patrol, 0, s) == RoyalGuardUtility.State.Reload, "Reload bypasses dwell");
                c.reload = false; c.visible = true; c.inRange = true; c.firingLine = true;
                Check(RoyalGuardUtility.Choose(c, RoyalGuardUtility.State.Attack, 2, s) == RoyalGuardUtility.State.Attack, "No-cover ranged fallback remains feasible");
                c.firingLine = false;
                Check(RoyalGuardUtility.Choose(c, RoyalGuardUtility.State.Attack, 0, s) == RoyalGuardUtility.State.Reposition, "Obstruction invalidates attack immediately");
                c.firingLine = true; c.cover = true; c.atHide = true;
                Check(RoyalGuardUtility.Choose(c, RoyalGuardUtility.State.SeekCover, 2, s) == RoyalGuardUtility.State.Reposition, "At hide position moves toward peek");
                c.atHide = false; c.atPeek = true;
                Check(RoyalGuardUtility.Choose(c, RoyalGuardUtility.State.Reposition, 2, s) == RoyalGuardUtility.State.Attack, "Peek arrival permits Attack");
                c.atPeek = false; c.atHide = true;
                Check(RoyalGuardUtility.Choose(c, RoyalGuardUtility.State.SeekCover, 0, s) == RoyalGuardUtility.State.Reposition, "Invalid current action bypasses minimum dwell");
                c.atHide = false; c.atPeek = false; c.firingLine = false; c.coverScore = .9f;
                Check(RoyalGuardUtility.Choose(c, RoyalGuardUtility.State.Reposition, 1, s) == RoyalGuardUtility.State.Reposition, "Hysteresis retains feasible state against small advantage");
                Check(RoyalGuardUtility.Choose(c, RoyalGuardUtility.State.Reposition, 9, s) == RoyalGuardUtility.State.SeekCover, "Maximum duration releases score margin");
                s.switchMargin = .01f;
                Check(RoyalGuardUtility.Choose(c, RoyalGuardUtility.State.Reposition, .1f, s) == RoyalGuardUtility.State.Reposition, "Minimum dwell holds feasible state");
                Check(RoyalGuardUtility.Choose(c, RoyalGuardUtility.State.Reposition, 1, s) == RoyalGuardUtility.State.SeekCover, "Stronger challenger wins after dwell");
                float good = RoyalGuardUtility.CoverScore(2, 3, 1, 0, 1, s);
                float exposed = RoyalGuardUtility.CoverScore(2, 3, 1, 1, 1, s);
                Check(good > exposed && good <= 1 && exposed >= 0, "Cover rewards occlusion with bounded score");
                Check(good > RoyalGuardUtility.CoverScore(2, 30, 1, 0, 1, s), "Path length penalizes detours");
                Check(good > RoyalGuardUtility.CoverScore(2, 3, 1, 0, 0, s), "Cover rewards tactical firing opportunity");
                s.distanceWeight = s.pathWeight = s.qualityWeight = s.exposureWeight = s.firingWeight = 0;
                Check(RoyalGuardUtility.CoverScore(0, 0, 1, 0, 1, s) == 0, "Zero weight sum is finite");
                UnityEngine.Object.DestroyImmediate(s);
                var go = new GameObject("HealthTest"); var health = go.AddComponent<RoyalGuardHealth>(); go.SendMessage("Awake");
                int deaths = 0; health.Died += () => deaths++;
                health.TakeDamage(20); Check(Mathf.Approximately(health.CurrentHealth, 160), "Damage decrements durable health");
                health.TakeDamage(-10); Check(Mathf.Approximately(health.CurrentHealth, 160), "Negative damage ignored");
                health.TakeDamage(1000); health.TakeDamage(1); health.Recover(1000, 1);
                Check(health.IsDead && deaths == 1 && health.CurrentHealth == 0, "Death fires once and cannot heal");
                UnityEngine.Object.DestroyImmediate(go);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/RoyalGuard.prefab");
                Check(prefab && prefab.GetComponent<RoyalGuardBrain>().settings && prefab.GetComponent<RoyalGuardNavigation>(), "Placeholder prefab has settings and navigation");
                File.WriteAllLines("RoyalGuardValidation.txt", results);
                Debug.Log("ROYAL_GUARD_VALIDATION_PASS " + results.Count);
            }
            catch (Exception error) { Debug.LogException(error); File.WriteAllText("RoyalGuardValidation.txt", string.Join("\n", results) + "\nFAIL " + error); if (Application.isBatchMode) EditorApplication.Exit(1); throw; }
        }
    }
}


