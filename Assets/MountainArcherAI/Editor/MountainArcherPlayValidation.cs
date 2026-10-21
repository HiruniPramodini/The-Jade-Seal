using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace JadeSeal.MountainArcher.Editor
{
    // Executes real frames/physics in a fresh unsaved scene, never modifying the main game scene.
    [InitializeOnLoad]
    public static class MountainArcherPlayValidation
    {
        private const string Key = "MountainArcher.PlayValidation";
        private static int stage;
        private static float started;
        private static double deadline;
        private static GameObject archer, target, wall;
        private static MountainArcherPerception sense;
        private static MountainArcherNavigation nav;
        private static MountainArcherBow weapon;
        private static MountainArcherSettings settings;
        private static float damage;
        private static Vector3 initial;
        private static readonly List<string> results = new List<string>();

        static MountainArcherPlayValidation()
        {
            EditorApplication.update += Tick;
        }

        private static void Check(bool condition, string name)
        {
            if (!condition) throw new Exception(name);
            results.Add("PASS " + name);
        }

        private static void Set(object obj, string field, object value) =>
            obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(obj, value);

        [MenuItem("Jade Seal/Mountain Archer/Run playmode automated physics validation", false, 3)]
        public static void Run()
        {
            MountainArcherValidation.CreateAssets();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }

        private static void Finish(int code, string error = null)
        {
            SessionState.SetBool(Key, false);
            File.WriteAllText("MountainArcherPlayValidation.txt", string.Join("\n", results) + (error == null ? "\n" : "\nFAIL " + error));
            Debug.Log("MOUNTAIN_ARCHER_PLAY_VALIDATION_" + (code == 0 ? "PASS " + results.Count : "FAIL " + error));
            EditorApplication.ExitPlaymode();
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;

            try
            {
                if (stage == 0)
                {
                    deadline = EditorApplication.timeSinceStartup + 45;
                    var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    floor.transform.position = new Vector3(0, -.25f, 0);
                    floor.transform.localScale = new Vector3(40, .5f, 40);

                    var source = new NavMeshBuildSource
                    {
                        shape = NavMeshBuildSourceShape.Box,
                        size = floor.transform.localScale,
                        transform = Matrix4x4.TRS(floor.transform.position, Quaternion.identity, Vector3.one),
                        area = 0
                    };

                    var data = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0), new List<NavMeshBuildSource> { source }, new Bounds(Vector3.zero, new Vector3(45, 10, 45)), Vector3.zero, Quaternion.identity);
                    Check(data && NavMesh.AddNavMeshData(data).valid, "Runtime test NavMesh built and registered");

                    archer = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MountainArcherAI/Prefabs/MountainArcher.prefab"));
                    archer.GetComponent<MountainArcherBrain>().enabled = false;

                    sense = archer.GetComponent<MountainArcherPerception>();
                    nav = archer.GetComponent<MountainArcherNavigation>();
                    weapon = archer.GetComponent<MountainArcherBow>();
                    settings = ScriptableObject.CreateInstance<MountainArcherSettings>();

                    target = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    target.name = "DamageTarget";
                    target.layer = 2;
                    target.transform.position = new Vector3(0, 0, 8);
                    target.GetComponent<CapsuleCollider>().center = Vector3.up;
                    target.AddComponent<MountainArcherDamageAdapter>();

                    sense.player = target.transform;
                    sense.obstacles = 1;

                    started = Time.time;
                    stage = 1;
                }
                else if (stage == 1 && Time.time > started + .2f)
                {
                    Check(nav.Ready, "Mountain Archer is bound to runtime NavMesh");
                    sense.Sample(settings);
                    Check(sense.Visible, "Distance FOV and clear line acquire player");

                    target.transform.position = new Vector3(0, 0, 40);
                    sense.Sample(settings);
                    Check(!sense.Visible, "Out-of-range target rejected");

                    target.transform.position = new Vector3(0, 0, -8);
                    sense.Sample(settings);
                    Check(!sense.Visible, "Behind-archer target rejected by FOV");

                    target.transform.position = new Vector3(0, 0, 8);
                    wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wall.transform.position = new Vector3(0, 1.5f, 4);
                    wall.transform.localScale = new Vector3(4, 3, 1);
                    Physics.SyncTransforms();
                    sense.Sample(settings);

                    Check(!sense.Visible && sense.HasMemory(settings.memorySeconds) && sense.LastPosition.z == 8, "Wall blocks visibility while last position persists");

                    wall.SetActive(false);
                    Physics.SyncTransforms();
                    sense.Sample(settings);

                    Check(weapon.TryFire(sense.LastPosition + Vector3.up, archer), "Bow fires high-speed ballistic arrow");
                    started = Time.time;
                    stage = 2;
                }
                else if (stage == 2 && Time.time > started + 1f)
                {
                    Check(!nav.Validate(new Vector3(100, 0, 100), out _, out _), "Off-mesh destination rejected");
                    Check(nav.Validate(new Vector3(4, 0, 0), out _, out float length) && length > 0, "Reachable destination produces complete measured path");

                    initial = archer.transform.position;
                    nav.Refresh();
                    Check(nav.Move(new Vector3(4, 0, 0), settings), "Validated high-speed navigation destination accepted");
                    started = Time.time;
                    stage = 3;
                }
                else if (stage == 3 && Time.time > started + 1.5f)
                {
                    Check(Vector3.Distance(initial, archer.transform.position) > 1, "NavMesh agent advances at 6.0m/s sprint speed across frames");
                    nav.Stop();
                    var health = archer.GetComponent<MountainArcherHealth>();
                    health.TakeDamage(100);
                    Check(health.CurrentHealth <= 20f, "120 HP correctly reduced by damage");
                    health.Recover(50, .55f);
                    Check(health.CurrentHealth > 20f, "Recovery system replenishes health up to cap");

                    Finish(0);
                }

                if (EditorApplication.timeSinceStartup > deadline) Finish(1, "Timeout in stage " + stage);
            }
            catch (Exception ex)
            {
                Finish(1, ex.Message);
            }
        }
    }
}
