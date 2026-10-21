using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace JadeSeal.FinalBoss.Editor
{
    public static class FinalBossValidation
    {
        const string Root = "Assets/RoyalGuardAI";
        static readonly List<string> results = new List<string>();

        static void Check(bool condition, string name)
        {
            if (!condition) throw new Exception(name);
            results.Add("PASS " + name);
        }

        [MenuItem("Jade Seal/Final Boss/Create or update Final Boss prefabs")]
        public static void CreateAssets()
        {
            Directory.CreateDirectory(Root + "/Prefabs");

            // 1. Settings Asset
            var settings = AssetDatabase.LoadAssetAtPath<FinalBossSettings>(Root + "/FinalBossSettings.asset");
            if (!settings)
            {
                settings = ScriptableObject.CreateInstance<FinalBossSettings>();
                AssetDatabase.CreateAsset(settings, Root + "/FinalBossSettings.asset");
            }

            // 2. Attack 1: Magic Bolt Prefab
            var boltGO = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            boltGO.name = "BossMagicBolt_PREFAB";
            boltGO.transform.localScale = new Vector3(0.12f, 0.45f, 0.12f);
            var boltCol = boltGO.GetComponent<Collider>();
            boltCol.isTrigger = false;
            var boltRb = boltGO.AddComponent<Rigidbody>();
            boltRb.useGravity = false;
            boltRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            boltGO.AddComponent<BossMagicBolt>();
            var boltAsset = PrefabUtility.SaveAsPrefabAsset(boltGO, Root + "/Prefabs/BossMagicBolt.prefab");
            UnityEngine.Object.DestroyImmediate(boltGO);

            // 3. Attack 2: Giant Fireball Prefab
            var fireballGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fireballGO.name = "BossFireball_PREFAB";
            fireballGO.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
            var fbCol = fireballGO.GetComponent<Collider>();
            fbCol.isTrigger = false;
            var fbRb = fireballGO.AddComponent<Rigidbody>();
            fbRb.useGravity = false;
            fbRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            fireballGO.AddComponent<BossFireball>();
            var fireballAsset = PrefabUtility.SaveAsPrefabAsset(fireballGO, Root + "/Prefabs/BossFireball.prefab");
            UnityEngine.Object.DestroyImmediate(fireballGO);

            // 4. Attack 3: Ground Shockwave Prefab
            var shockwaveGO = new GameObject("BossShockwave_PREFAB");
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "VisualShockwaveRing";
            ring.transform.SetParent(shockwaveGO.transform, false);
            ring.transform.localScale = new Vector3(1f, 0.05f, 1f);
            UnityEngine.Object.DestroyImmediate(ring.GetComponent<Collider>());
            var shockComp = shockwaveGO.AddComponent<BossShockwave>();
            var serializedShock = new SerializedObject(shockComp);
            serializedShock.FindProperty("visualRing").objectReferenceValue = ring.transform;
            serializedShock.ApplyModifiedPropertiesWithoutUndo();
            var shockwaveAsset = PrefabUtility.SaveAsPrefabAsset(shockwaveGO, Root + "/Prefabs/BossShockwave.prefab");
            UnityEngine.Object.DestroyImmediate(shockwaveGO);

            // 5. Final Boss Prefab
            var bossGO = new GameObject("FinalBoss");
            bossGO.layer = 2; // Ignore raycast by default to prevent self-occlusion

            var agent = bossGO.AddComponent<NavMeshAgent>();
            agent.speed = settings.moveSpeed;
            agent.height = 2.6f;
            agent.radius = 0.6f;
            agent.stoppingDistance = settings.stoppingDistance;

            var health = bossGO.AddComponent<FinalBossHealth>();
            var serializedHealth = new SerializedObject(health);
            serializedHealth.FindProperty("settings").objectReferenceValue = settings;
            serializedHealth.FindProperty("overrideMaxHealth").floatValue = 1500f;
            serializedHealth.ApplyModifiedPropertiesWithoutUndo();

            var perception = bossGO.AddComponent<FinalBossPerception>();
            var combat = bossGO.AddComponent<FinalBossCombat>();
            var nav = bossGO.AddComponent<FinalBossNavigation>();
            var guard = bossGO.AddComponent<FinalBossGuard>();
            var presentation = bossGO.AddComponent<FinalBossPresentation>();
            var brain = bossGO.AddComponent<FinalBossBrain>();
            brain.settings = settings;

            // Sculpted Armor & Intimidating Visual Hierarchy
            var visualRoot = new GameObject("BossVisual_ArmoredJadeWarlord");
            visualRoot.transform.SetParent(bossGO.transform, false);

            // Heavy Body Capsule (Imposing frame, 2.5m tall)
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "ArmoredTorso";
            body.transform.SetParent(visualRoot.transform, false);
            body.transform.localPosition = new Vector3(0f, 1.3f, 0f);
            body.transform.localScale = new Vector3(1.1f, 1.25f, 0.9f);

            // Shoulder Pauldrons (Heavy armor)
            var leftPauldron = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftPauldron.name = "LeftShoulderArmor";
            leftPauldron.transform.SetParent(visualRoot.transform, false);
            leftPauldron.transform.localPosition = new Vector3(-0.75f, 2.05f, 0f);
            leftPauldron.transform.localScale = new Vector3(0.5f, 0.4f, 0.6f);
            leftPauldron.transform.localRotation = Quaternion.Euler(0f, 0f, 15f);
            UnityEngine.Object.DestroyImmediate(leftPauldron.GetComponent<Collider>());

            var rightPauldron = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightPauldron.name = "RightShoulderArmor";
            rightPauldron.transform.SetParent(visualRoot.transform, false);
            rightPauldron.transform.localPosition = new Vector3(0.75f, 2.05f, 0f);
            rightPauldron.transform.localScale = new Vector3(0.5f, 0.4f, 0.6f);
            rightPauldron.transform.localRotation = Quaternion.Euler(0f, 0f, -15f);
            UnityEngine.Object.DestroyImmediate(rightPauldron.GetComponent<Collider>());

            // Crown / Horns Ornament
            var crown = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crown.name = "WarlordCrown";
            crown.transform.SetParent(visualRoot.transform, false);
            crown.transform.localPosition = new Vector3(0f, 2.65f, 0.05f);
            crown.transform.localScale = new Vector3(0.55f, 0.25f, 0.45f);
            UnityEngine.Object.DestroyImmediate(crown.GetComponent<Collider>());

            // Guard Barrier Visual (Jade energy shield)
            var barrier = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            barrier.name = "JadeGuardBarrier";
            barrier.transform.SetParent(bossGO.transform, false);
            barrier.transform.localPosition = new Vector3(0f, 1.3f, 0.2f);
            barrier.transform.localScale = new Vector3(2.6f, 2.6f, 2.6f);
            UnityEngine.Object.DestroyImmediate(barrier.GetComponent<Collider>());
            barrier.SetActive(false);

            // Sockets
            var handSocket = new GameObject("Hand_MagicCastPoint").transform;
            handSocket.SetParent(bossGO.transform, false);
            handSocket.localPosition = new Vector3(0.6f, 1.6f, 0.9f);

            var fireballSocket = new GameObject("Overhead_FireballSocket").transform;
            fireballSocket.SetParent(bossGO.transform, false);
            fireballSocket.localPosition = new Vector3(0f, 2.8f, 0.5f);

            var footSocket = new GameObject("Foot_StompOrigin").transform;
            footSocket.SetParent(bossGO.transform, false);
            footSocket.localPosition = new Vector3(0f, 0.05f, 0.3f);

            // Wire Combat References
            var serializedCombat = new SerializedObject(combat);
            serializedCombat.FindProperty("magicCastPoint").objectReferenceValue = handSocket;
            serializedCombat.FindProperty("fireballCastPoint").objectReferenceValue = fireballSocket;
            serializedCombat.FindProperty("stompPoint").objectReferenceValue = footSocket;
            serializedCombat.FindProperty("magicBoltPrefab").objectReferenceValue = boltAsset.GetComponent<BossMagicBolt>();
            serializedCombat.FindProperty("fireballPrefab").objectReferenceValue = fireballAsset.GetComponent<BossFireball>();
            serializedCombat.FindProperty("shockwavePrefab").objectReferenceValue = shockwaveAsset.GetComponent<BossShockwave>();
            serializedCombat.ApplyModifiedPropertiesWithoutUndo();

            // Wire Guard Visual
            var serializedGuard = new SerializedObject(guard);
            serializedGuard.FindProperty("guardBarrierVisual").objectReferenceValue = barrier;
            serializedGuard.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(bossGO, Root + "/Prefabs/FinalBoss.prefab");
            UnityEngine.Object.DestroyImmediate(bossGO);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Jade Seal/Final Boss/Run deterministic checks")]
        public static void Run()
        {
            results.Clear();
            try
            {
                CreateAssets();

                // 1. Health Invariants & Substantial HP Verification
                var go = new GameObject("FinalBossHealthTest");
                var health = go.AddComponent<FinalBossHealth>();
                health.InitializeHealth();

                Check(health.MaxHealth >= 1000f, "Boss max health is substantially high (>= 1000 HP)");
                Check(Mathf.Approximately(health.CurrentHealth, health.MaxHealth), "Boss starts with full health");

                health.TakeDamage(100f);
                Check(Mathf.Approximately(health.CurrentHealth, health.MaxHealth - 100f), "Damage decrements health accurately");

                health.TakeDamage(-50f);
                Check(Mathf.Approximately(health.CurrentHealth, health.MaxHealth - 100f), "Negative damage is ignored");

                // 2. Guarding Damage Mitigation
                health.IsGuarding = true;
                float hpBeforeGuarded = health.CurrentHealth;
                health.TakeDamage(100f); // with 75% reduction, should take 25 dmg
                Check(Mathf.Approximately(health.CurrentHealth, hpBeforeGuarded - 25f), "Guarding reduces incoming damage by 75%");

                health.IsGuarding = false;

                // 3. Terminal Death Behavior
                int deathEvents = 0;
                health.Died += () => deathEvents++;
                health.TakeDamage(99999f);
                health.TakeDamage(500f);
                health.Heal(1000f);

                Check(health.IsDead && health.CurrentHealth == 0f, "Lethal damage clamps to zero and cannot heal");
                Check(deathEvents == 1, "Died event fires strictly once");
                UnityEngine.Object.DestroyImmediate(go);

                // 4. Combat Cooldown & Attack Selection
                var combatGO = new GameObject("CombatTest");
                var combat = combatGO.AddComponent<FinalBossCombat>();
                var boltPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/BossMagicBolt.prefab").GetComponent<BossMagicBolt>();
                var fireballPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/BossFireball.prefab").GetComponent<BossFireball>();
                var shockwavePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/BossShockwave.prefab").GetComponent<BossShockwave>();

                combat.BindPrefabs(boltPrefab, fireballPrefab, shockwavePrefab);
                combat.ResetCooldowns();

                Check(combat.CanCastBasic, "Basic magic ready after cooldown reset");
                Check(combat.CanCastFireball, "Fireball ready after cooldown reset");
                Check(combat.CanStomp, "Stomp ready after cooldown reset");

                var settings = ScriptableObject.CreateInstance<FinalBossSettings>();
                combat.FireBasicMagic(Vector3.forward * 10f, settings);
                Check(!combat.CanCastBasic, "Basic magic enters cooldown immediately after fire");

                combat.LaunchFireball(Vector3.forward * 10f, settings);
                Check(!combat.CanCastFireball, "Fireball enters cooldown immediately after launch");

                combat.TriggerStompShockwave(settings);
                Check(!combat.CanStomp, "Stomp enters cooldown immediately after impact");

                UnityEngine.Object.DestroyImmediate(combatGO);
                UnityEngine.Object.DestroyImmediate(settings);

                // 5. Prefab & Wiring Checks
                var bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/FinalBoss.prefab");
                Check(bossPrefab != null, "FinalBoss prefab exists");
                Check(bossPrefab.GetComponent<FinalBossBrain>() != null, "FinalBoss has Brain component");
                Check(bossPrefab.GetComponent<FinalBossHealth>() != null, "FinalBoss has Health component");
                Check(bossPrefab.GetComponent<FinalBossPerception>() != null, "FinalBoss has Perception component");
                Check(bossPrefab.GetComponent<FinalBossCombat>() != null, "FinalBoss has Combat component");
                Check(bossPrefab.GetComponent<FinalBossNavigation>() != null, "FinalBoss has Navigation component");
                Check(bossPrefab.GetComponent<FinalBossGuard>() != null, "FinalBoss has Guard component");
                Check(bossPrefab.GetComponent<NavMeshAgent>() != null, "FinalBoss has NavMeshAgent");

                File.WriteAllLines("FinalBossValidation.txt", results);
                Debug.Log("FINAL_BOSS_VALIDATION_PASS " + results.Count);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                File.WriteAllText("FinalBossValidation.txt", string.Join("\n", results) + "\nFAIL " + ex);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }
    }
}
