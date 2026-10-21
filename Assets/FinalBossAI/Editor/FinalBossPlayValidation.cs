using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace JadeSeal.FinalBoss.Editor
{
    [InitializeOnLoad]
    public static class FinalBossPlayValidation
    {
        const string Key = "FinalBoss.PlayValidation";
        static int stage;
        static float started;
        static GameObject boss, target, wall;
        static FinalBossPerception sense;
        static FinalBossNavigation nav;
        static FinalBossCombat combat;
        static FinalBossGuard guard;
        static FinalBossHealth health;
        static FinalBossSettings settings;
        static float totalPlayerDamageReceived;
        static readonly List<string> results = new List<string>();

        static FinalBossPlayValidation()
        {
            EditorApplication.update += Tick;
        }

        static void Check(bool condition, string name)
        {
            if (!condition) throw new Exception(name);
            results.Add("PASS " + name);
        }

        public static void Run()
        {
            FinalBossValidation.CreateAssets();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }

        static void Finish(int code, string error = null)
        {
            SessionState.SetBool(Key, false);
            File.WriteAllText("FinalBossPlayValidation.txt", string.Join("\n", results) + (error == null ? "\n" : "\nFAIL " + error));
            Debug.Log("FINAL_BOSS_PLAY_VALIDATION_" + (code == 0 ? "PASS " + results.Count : "FAIL " + error));
            EditorApplication.Exit(code);
        }

        static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;

            try
            {
                if (stage == 0)
                {
                    // 1. Build runtime test arena NavMesh
                    var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    floor.transform.position = new Vector3(0, -0.25f, 0);
                    floor.transform.localScale = new Vector3(60, 0.5f, 60);

                    var source = new NavMeshBuildSource
                    {
                        shape = NavMeshBuildSourceShape.Box,
                        size = floor.transform.localScale,
                        transform = Matrix4x4.TRS(floor.transform.position, Quaternion.identity, Vector3.one),
                        area = 0
                    };

                    var data = NavMeshBuilder.BuildNavMeshData(
                        NavMesh.GetSettingsByID(0),
                        new List<NavMeshBuildSource> { source },
                        new Bounds(Vector3.zero, new Vector3(65, 10, 65)),
                        Vector3.zero,
                        Quaternion.identity);

                    Check(data && NavMesh.AddNavMeshData(data).valid, "Runtime test NavMesh built and registered");

                    // 2. Instantiate Final Boss
                    boss = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/RoyalGuardAI/Prefabs/FinalBoss.prefab"));
                    var brain = boss.GetComponent<FinalBossBrain>();
                    brain.enabled = false; // Manual phase-by-phase verification

                    sense = boss.GetComponent<FinalBossPerception>();
                    nav = boss.GetComponent<FinalBossNavigation>();
                    combat = boss.GetComponent<FinalBossCombat>();
                    guard = boss.GetComponent<FinalBossGuard>();
                    health = boss.GetComponent<FinalBossHealth>();
                    settings = ScriptableObject.CreateInstance<FinalBossSettings>();

                    // 3. Create Player target with damage adapter
                    target = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    target.name = "PlayerTestTarget";
                    target.layer = 2;
                    target.transform.position = new Vector3(0, 1f, 8f);
                    var adapter = target.AddComponent<FinalBossDamageAdapter>();
                    adapter.onReceiveDamage.AddListener(val => totalPlayerDamageReceived += val);

                    sense.player = target.transform;
                    sense.EncounterActive = true;

                    started = Time.time;
                    stage = 1;
                }
                else if (stage == 1 && Time.time > started + 0.25f)
                {
                    Check(nav.IsReady, "Boss NavMeshAgent successfully bound to runtime NavMesh");

                    // Test continuous arena awareness & behind-cover tracking
                    sense.UpdatePerception(settings);
                    Check(sense.PlayerInArena && sense.HasValidTarget, "Boss recognizes player inside active arena");

                    wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wall.transform.position = new Vector3(0, 1.5f, 4f);
                    wall.transform.localScale = new Vector3(6, 3, 1);
                    Physics.SyncTransforms();

                    sense.UpdatePerception(settings);
                    Check(sense.PlayerInArena, "Boss retains magical player awareness even when player is behind cover");

                    wall.SetActive(false);
                    Physics.SyncTransforms();

                    // Test Attack 1: Basic Magic Projectile
                    float dmgBefore = totalPlayerDamageReceived;
                    Check(combat.CanCastBasic, "Basic magic is ready to cast");
                    combat.FireBasicMagic(sense.AimPosition, settings);
                    Check(!combat.CanCastBasic, "Basic magic enters cooldown immediately after fire");

                    started = Time.time;
                    stage = 2;
                }
                else if (stage == 2 && Time.time > started + 0.8f)
                {
                    Check(totalPlayerDamageReceived > 0f, "Basic magic projectile delivered damage to player target");

                    // Test Attack 2: Giant Fireball
                    float dmgBefore = totalPlayerDamageReceived;
                    Check(combat.CanCastFireball, "Giant fireball is ready to launch");
                    combat.LaunchFireball(sense.AimPosition, settings);
                    Check(!combat.CanCastFireball, "Fireball enters cooldown immediately after launch");

                    started = Time.time;
                    stage = 3;
                }
                else if (stage == 3 && Time.time > started + 0.9f)
                {
                    Check(totalPlayerDamageReceived > 50f, "Giant fireball delivered high-impact direct/splash damage");

                    // Test Attack 3: Magical Stomp and Shockwave
                    Check(combat.CanStomp, "Stomp shockwave is ready to trigger");
                    combat.TriggerStompShockwave(settings);
                    Check(!combat.CanStomp, "Stomp enters cooldown immediately after trigger");

                    started = Time.time;
                    stage = 4;
                }
                else if (stage == 4 && Time.time > started + 0.9f)
                {
                    Check(totalPlayerDamageReceived > 90f, "Stomp shockwave expanded and damaged player target");

                    // Test Boss Guarding & Damage Mitigation
                    Check(health.CurrentHealth >= 1400f, "Boss possesses massive durability health pool (>= 1400 HP)");
                    float hpBefore = health.CurrentHealth;

                    guard.StartGuard(settings);
                    Check(guard.IsGuarding && health.IsGuarding, "Boss entered defensive guarding state");

                    health.TakeDamage(100f);
                    float actualTaken = hpBefore - health.CurrentHealth;
                    Check(Mathf.Approximately(actualTaken, 25f), "Guarding reduced incoming damage by 75% (took 25 instead of 100)");

                    guard.EndGuard();
                    Check(!guard.IsGuarding, "Guarding state ended cleanly");

                    // Test Terminal Death
                    int diedCalls = 0;
                    health.Died += () => diedCalls++;
                    health.TakeDamage(99999f);

                    Check(health.IsDead && health.CurrentHealth == 0f, "Boss died upon receiving lethal damage");
                    Check(diedCalls == 1, "Died event fired strictly once on fatal blow");

                    Finish(0);
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                Finish(1, ex.ToString());
            }
        }
    }
}
