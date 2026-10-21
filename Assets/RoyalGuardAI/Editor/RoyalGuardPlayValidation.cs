using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
namespace JadeSeal.RoyalGuard.Editor
{
    // Executes real frames/physics in a fresh unsaved scene, never the team's world scene.
    [InitializeOnLoad]
    public static class RoyalGuardPlayValidation
    {
        const string Key = "RoyalGuard.PlayValidation";
        static int stage;
        static float started;
        static double deadline;
        static GameObject guard, target, wall;
        static RoyalGuardPerception sense;
        static RoyalGuardNavigation nav;
        static RoyalGuardCrossbow weapon;
        static RoyalGuardSettings settings;
        static float damage;
        static Vector3 initial;
        static readonly List<string> results = new List<string>();
        static RoyalGuardPlayValidation() { EditorApplication.update += Tick; }
        static void Check(bool condition, string name) { if (!condition) throw new Exception(name); results.Add("PASS " + name); }
        static void Set(object obj, string field, object value) => obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(obj, value);
        public static void Run()
        {
            RoyalGuardValidation.CreateAssets();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }
        static void Finish(int code, string error = null)
        {
            SessionState.SetBool(Key, false);
            File.WriteAllText("RoyalGuardPlayValidation.txt", string.Join("\n", results) + (error == null ? "\n" : "\nFAIL " + error));
            Debug.Log("ROYAL_GUARD_PLAY_VALIDATION_" + (code == 0 ? "PASS " + results.Count : "FAIL " + error));
            EditorApplication.Exit(code);
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            try
            {
                if (stage == 0)
                {
                    deadline = EditorApplication.timeSinceStartup + 45;
                    var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.transform.position = new Vector3(0, -.25f, 0); floor.transform.localScale = new Vector3(40, .5f, 40);
                    var source = new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box, size = floor.transform.localScale, transform = Matrix4x4.TRS(floor.transform.position, Quaternion.identity, Vector3.one), area = 0 };
                    var data = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0), new List<NavMeshBuildSource> { source }, new Bounds(Vector3.zero, new Vector3(45, 10, 45)), Vector3.zero, Quaternion.identity);
                    Check(data && NavMesh.AddNavMeshData(data).valid, "Runtime test NavMesh built and registered");
                    guard = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/RoyalGuardAI/Prefabs/RoyalGuard.prefab"));
                    guard.GetComponent<RoyalGuardBrain>().enabled = false;
                    sense = guard.GetComponent<RoyalGuardPerception>(); nav = guard.GetComponent<RoyalGuardNavigation>(); weapon = guard.GetComponent<RoyalGuardCrossbow>();
                    settings = ScriptableObject.CreateInstance<RoyalGuardSettings>();
                    target = GameObject.CreatePrimitive(PrimitiveType.Capsule); target.name = "DamageTarget"; target.layer = 2; target.transform.position = new Vector3(0, 0, 8);
                    target.GetComponent<CapsuleCollider>().center = Vector3.up;
                    target.AddComponent<RoyalGuardDamageAdapter>().receiveDamage.AddListener(value => damage += value);
                    sense.player = target.transform; sense.obstacles = 1;
                    started = Time.time; stage = 1;
                }
                else if (stage == 1 && Time.time > started + .2f)
                {
                    Check(nav.Ready, "Guard is bound to runtime NavMesh");
                    sense.Sample(settings); Check(sense.Visible, "Distance FOV and clear line acquire player");
                    target.transform.position = new Vector3(0, 0, 40); sense.Sample(settings); Check(!sense.Visible, "Out-of-range target rejected");
                    target.transform.position = new Vector3(0, 0, -8); sense.Sample(settings); Check(!sense.Visible, "Behind-guard target rejected by FOV");
                    target.transform.position = new Vector3(0, 0, 8);
                    wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.position = new Vector3(0, 1.5f, 4); wall.transform.localScale = new Vector3(4, 3, 1);
                    Physics.SyncTransforms(); sense.Sample(settings);
                    Check(!sense.Visible && sense.HasMemory(settings.memorySeconds) && sense.LastPosition.z == 8, "Wall blocks visibility while last position persists");
                    Check(!weapon.ClearShot(sense.AimPosition, sense.obstacles), "Muzzle obstruction prevents feasible shot");
                    wall.SetActive(false); Physics.SyncTransforms(); sense.Sample(settings);
                    Set(weapon, "ammunition", 1); Set(weapon, "magazineSize", 1); Set(weapon, "reloadSeconds", .2f);
                    Check(weapon.TryFire(sense.AimPosition, guard), "Crossbow spawns physical bolt");
                    Check(weapon.NeedsReload && !weapon.TryFire(sense.AimPosition, guard), "Empty magazine blocks duplicate shot");
                    weapon.StartReload(); Check(weapon.IsReloading && !weapon.CanFire, "Reload locks firing");
                    started = Time.time; stage = 2;
                }
                else if (stage == 2 && Time.time > started + 1)
                {
                    Check(!sense.HasMemory(.1f), "Last-seen memory expires with elapsed game time");
                    Check(Mathf.Approximately(damage, 15), "Real physics collision delivers exactly one 15HP hit");
                    Check(!weapon.IsReloading && !weapon.NeedsReload && weapon.CanFire, "Timed reload and cooldown complete");
                    Check(!nav.Validate(new Vector3(100, 0, 100), out _, out _), "Off-mesh destination rejected");
                    Check(nav.Validate(new Vector3(4, 0, 0), out _, out float length) && length > 0, "Reachable destination produces complete measured path");
                    initial = guard.transform.position; nav.Refresh(); Check(nav.Move(new Vector3(4, 0, 0), settings), "Validated destination accepted");
                    started = Time.time; stage = 3;
                }
                else if (stage == 3 && Time.time > started + 1.5f)
                {
                    Check(Vector3.Distance(initial, guard.transform.position) > 1, "NavMesh agent advances across actual frames");
                    nav.Stop(); target.SetActive(false); sense.Sample(settings); Check(!sense.Visible, "Disabled target cannot remain visible");
                    UnityEngine.Object.DestroyImmediate(target); sense.Sample(settings); Check(!sense.Visible, "Destroyed target handled without exception");
                    sense.Forget(); Check(!sense.HasMemory(settings.memorySeconds), "Explicit memory expiry returns no target memory");
                    var health = guard.GetComponent<RoyalGuardHealth>();
                    health.TakeDamage(150); health.Recover(100, .55f); Check(Mathf.Approximately(health.CurrentHealth, 99), "Recovery capped at configured ratio");
                    target = GameObject.CreatePrimitive(PrimitiveType.Capsule); target.layer = 2; target.transform.position = guard.transform.position + Vector3.forward * 8;
                    target.GetComponent<CapsuleCollider>().center = Vector3.up;
                    sense.player = target.transform; guard.transform.rotation = Quaternion.identity; Physics.SyncTransforms(); sense.Sample(settings);
                    var coverGo = new GameObject("TestCover"); coverGo.transform.position = guard.transform.position + Vector3.forward * 2;
                    var cover = coverGo.AddComponent<RoyalGuardCoverPoint>();
                    var peek = new GameObject("TestPeek").transform; peek.position = guard.transform.position + new Vector3(3, 0, 4); Set(cover, "peekPosition", peek);
                    wall.transform.position = guard.transform.position + new Vector3(0, 1.5f, 4); wall.SetActive(true); Physics.SyncTransforms();
                    var tactics = guard.GetComponent<RoyalGuardTactics>(); tactics.coverPoints = new[] { cover };
                    tactics.Select(nav, sense, settings, false);
                    Check(tactics.Selected == cover && tactics.Score > 0, "Occluded reachable hide with clear peek wins cover selection");
                    var other = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/RoyalGuardAI/Prefabs/RoyalGuard.prefab"), new Vector3(10, 0, 0), Quaternion.identity);
                    other.GetComponent<RoyalGuardBrain>().enabled = false;
                    Check(!cover.Reserve(other.GetComponent<RoyalGuardBrain>()), "Cover reservation excludes second guard");
                    UnityEngine.Object.DestroyImmediate(other);
                    cover.enabled = false; tactics.Select(nav, sense, settings, false);
                    Check(!tactics.Selected, "Disabled cover is dynamically released");
                    tactics.coverPoints = new RoyalGuardCoverPoint[0]; wall.SetActive(false); Physics.SyncTransforms();
                    Set(weapon, "magazineSize", 100); Set(weapon, "ammunition", 100);
                    guard.GetComponent<RoyalGuardBrain>().enabled = true;
                    started = Time.time; stage = 4;
                }
                else if (stage == 4 && Time.time > started + .6f)
                {
                    Check(guard.GetComponent<RoyalGuardBrain>().CurrentAction == RoyalGuardUtility.State.Attack, "Autonomous brain enters Attack with no cover and clear target");
                    guard.GetComponent<RoyalGuardHealth>().TakeDamage(60);
                    started = Time.time; stage = 5;
                }
                else if (stage == 5 && Time.time > started + .4f)
                {
                    Check(guard.GetComponent<RoyalGuardBrain>().CurrentAction == RoyalGuardUtility.State.Retreat, "Autonomous low-health transition enters Retreat");
                    var health = guard.GetComponent<RoyalGuardHealth>(); health.TakeDamage(1000);
                    Check(health.IsDead && !guard.GetComponent<RoyalGuardBrain>().enabled, "Lethal damage stops brain in Play Mode");
                    Finish(0);
                }
                if (EditorApplication.timeSinceStartup > deadline && stage > 0) Finish(1, "45-second frame advancement timeout");
            }
            catch (Exception error) { Debug.LogException(error); Finish(1, error.ToString()); }
        }
    }
}


