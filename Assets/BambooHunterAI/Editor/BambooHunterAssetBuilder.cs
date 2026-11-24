using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace JadeSeal.BambooHunter.Editor
{
    /// <summary>
    /// Deterministic Asset & Scene Builder for the Bamboo Hunter AI module.
    /// Programmatically generates and verifies all production assets:
    /// 1. Reusable Bamboo Hunter Settings asset (Assets/BambooHunterAI/Settings/BambooHunterSettings_Default.asset)
    /// 2. Physical Ballistic Arrow Prefab (Assets/BambooHunterAI/Prefabs/BambooHunterArrow.prefab)
    /// 3. Autonomous Bamboo Hunter Enemy Prefab (Assets/BambooHunterAI/Prefabs/BambooHunterEnemy.prefab)
    /// 4. Isolated Self-Contained Demo Scene (Assets/BambooHunterAI/Scenes/BambooHunterDemoScene.unity)
    ///
    /// Ensures 100% boundary safety: all generated files are strictly confined to Assets/BambooHunterAI/.
    /// </summary>
    public static class BambooHunterAssetBuilder
    {
        public const string RootDir = "Assets/BambooHunterAI";
        public const string SettingsDir = RootDir + "/Settings";
        public const string PrefabsDir = RootDir + "/Prefabs";
        public const string ScenesDir = RootDir + "/Scenes";
        public const string MaterialsDir = RootDir + "/Materials";

        public const string SettingsPath = SettingsDir + "/BambooHunterSettings_Default.asset";
        public const string ArrowPrefabPath = PrefabsDir + "/BambooHunterArrow.prefab";
        public const string EnemyPrefabPath = PrefabsDir + "/BambooHunterEnemy.prefab";
        public const string ScenePath = ScenesDir + "/BambooHunterDemoScene.unity";

        public static BambooHunterSettings SettingsAsset { get; private set; }
        public static GameObject ArrowPrefab { get; private set; }
        public static GameObject EnemyPrefab { get; private set; }

        [MenuItem("Jade Seal/Bamboo Hunter/Build Prefabs and Scene")]
        public static void BuildAllMenu()
        {
            BuildAll();
        }

        [MenuItem("Jade Seal/Bamboo Hunter/Fix Scene Enemy Instance")]
        public static void FixCurrentSceneEnemyMenu()
        {
            FixCurrentSceneEnemy();
        }

        public static bool BuildAll()
        {
            Debug.Log("[BambooHunterAssetBuilder] Commencing deterministic build of Bamboo Hunter assets...");

            try
            {
                EnsureDirectories();
                var materials = CreateMaterials();
                SettingsAsset = CreateSettingsAsset();

                // 1. Create Arrow Prefab & establish AssetDatabase GUID immediately
                ArrowPrefab = CreateArrowPrefab(materials.arrow);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.ImportAsset(ArrowPrefabPath, ImportAssetOptions.ForceSynchronousImport);
                var loadedArrow = AssetDatabase.LoadAssetAtPath<GameObject>(ArrowPrefabPath) ?? ArrowPrefab;

                // 2. Create Enemy Prefab with valid Arrow Prefab asset GUID reference
                EnemyPrefab = CreateEnemyPrefab(SettingsAsset, loadedArrow, materials.hunter, materials.fletching);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.ImportAsset(EnemyPrefabPath, ImportAssetOptions.ForceSynchronousImport);
                var loadedEnemy = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath) ?? EnemyPrefab;

                // 3. Build Demo Scene
                bool sceneBuilt = CreateDemoScene(SettingsAsset, loadedEnemy, loadedArrow, materials);
                FixEnemyPrefabArrowReference();

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.ImportAsset(EnemyPrefabPath, ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceSynchronousImport);

                Debug.Log($"[BambooHunterAssetBuilder] Build completed successfully! Scene built: {sceneBuilt}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BambooHunterAssetBuilder] Build failed with exception: {ex}");
                return false;
            }
        }

        private static void FixEnemyPrefabArrowReference()
        {
            try
            {
                if (File.Exists(EnemyPrefabPath) && File.Exists(ArrowPrefabPath))
                {
                    string arrowMeta = ArrowPrefabPath + ".meta";
                    string arrowGuid = "334e1dfc630ca8c4db7193e4422bffe6";
                    if (File.Exists(arrowMeta))
                    {
                        string metaText = File.ReadAllText(arrowMeta);
                        int guidIdx = metaText.IndexOf("guid: ");
                        if (guidIdx >= 0)
                        {
                            arrowGuid = metaText.Substring(guidIdx + 6, 32).Trim();
                        }
                    }

                    string enemyYaml = File.ReadAllText(EnemyPrefabPath);
                    if (enemyYaml.Contains("arrowPrefab: {fileID: 0}"))
                    {
                        enemyYaml = enemyYaml.Replace(
                            "arrowPrefab: {fileID: 0}",
                            $"arrowPrefab: {{fileID: 2064410397690175322, guid: {arrowGuid}, type: 3}}"
                        );
                        File.WriteAllText(EnemyPrefabPath, enemyYaml);
                    }

                    if (File.Exists(ScenePath))
                    {
                        string sceneYaml = File.ReadAllText(ScenePath);
                        if (sceneYaml.Contains("arrowPrefab: {fileID: 0}"))
                        {
                            sceneYaml = sceneYaml.Replace(
                                "arrowPrefab: {fileID: 0}",
                                $"arrowPrefab: {{fileID: 2064410397690175322, guid: {arrowGuid}, type: 3}}"
                            );
                            File.WriteAllText(ScenePath, sceneYaml);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BambooHunterAssetBuilder] Note on arrow reference patching: {ex.Message}");
            }
        }

        private static void EnsureDirectories()
        {
            EnsureDir(SettingsDir);
            EnsureDir(PrefabsDir);
            EnsureDir(ScenesDir);
            EnsureDir(MaterialsDir);
        }

        private static void EnsureDir(string dir)
        {
            if (!AssetDatabase.IsValidFolder(dir))
            {
                string parent = Path.GetDirectoryName(dir).Replace("\\", "/");
                string leaf = Path.GetFileName(dir);
                if (!AssetDatabase.IsValidFolder(parent))
                {
                    EnsureDir(parent);
                }
                AssetDatabase.CreateFolder(parent, leaf);
            }
        }

        public struct MaterialSet
        {
            public Material hunter;
            public Material fletching;
            public Material arrow;
            public Material bambooTrunk;
            public Material playerDummy;
            public Material ground;
        }

        private static MaterialSet CreateMaterials()
        {
            var set = new MaterialSet();

            Shader standardShader = Shader.Find("Standard");
            if (standardShader == null) standardShader = Shader.Find("Universal Render Pipeline/Lit");
            if (standardShader == null) standardShader = Shader.Find("Diffuse");

            set.hunter = GetOrCreateMaterial("M_BambooHunter.mat", standardShader, new Color(0.18f, 0.42f, 0.22f));
            set.fletching = GetOrCreateMaterial("M_HunterHeadband.mat", standardShader, new Color(0.85f, 0.20f, 0.15f));
            set.arrow = GetOrCreateMaterial("M_ArrowWood.mat", standardShader, new Color(0.65f, 0.50f, 0.30f));
            set.bambooTrunk = GetOrCreateMaterial("M_BambooTrunk.mat", standardShader, new Color(0.32f, 0.58f, 0.28f));
            set.playerDummy = GetOrCreateMaterial("M_PlayerDummy.mat", standardShader, new Color(0.82f, 0.22f, 0.18f));
            set.ground = GetOrCreateMaterial("M_GroundForest.mat", standardShader, new Color(0.28f, 0.32f, 0.22f));

            return set;
        }

        private static Material GetOrCreateMaterial(string filename, Shader shader, Color color)
        {
            string path = $"{MaterialsDir}/{filename}";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { color = color };
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.color = color;
                EditorUtility.SetDirty(mat);
            }
            return mat;
        }

        public static BambooHunterSettings CreateSettingsAsset()
        {
            var settings = AssetDatabase.LoadAssetAtPath<BambooHunterSettings>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<BambooHunterSettings>();
                ConfigureDefaultSettings(settings);
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }
            else
            {
                ConfigureDefaultSettings(settings);
                EditorUtility.SetDirty(settings);
            }
            return settings;
        }

        private static void ConfigureDefaultSettings(BambooHunterSettings s)
        {
            s.detectionRange = 25f;
            s.fieldOfView = 110f;
            s.memoryDuration = 5f;
            s.aimLeadFactor = 0.35f;

            s.thinkInterval = 0.20f;
            s.switchMargin = 0.08f;
            s.minimumStateDuration = 0.50f;
            s.maximumStateDuration = 6.0f;

            s.attackRange = 20f;
            s.preferredMinRange = 7f;
            s.preferredMaxRange = 16f;
            s.bowDrawDuration = 1.0f;
            s.arrowSpeed = 32f;
            s.arrowDamage = 18f;
            s.quiverCapacity = 6;
            s.maxShotsBeforeReposition = 2;
            s.shotCooldownDuration = 0.8f;
            s.arrowLifetime = 6.0f;
            s.arrowGravityScale = 1.0f;
            s.quiverRestockDuration = 2.5f;
            s.minDrawRatioToShoot = 0.99f;
            s.trajectoryArcSamples = 8;

            s.coverSearchRadius = 18f;
            s.coverDistanceWeight = 1.0f;
            s.coverPathLengthWeight = 1.5f;
            s.coverOcclusionWeight = 2.5f;
            s.coverFlankWeight = 1.2f;

            s.maxHealth = 120f;
            s.retreatThreshold = 0.25f;
            s.recoveryPerSecond = 3.0f;
            s.recoveryDelay = 3.5f;
        }

        public static GameObject CreateArrowPrefab(Material arrowMat)
        {
            var root = new GameObject("BambooHunterArrow");

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 0.15f;
            rb.useGravity = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            var col = root.AddComponent<CapsuleCollider>();
            col.radius = 0.06f;
            col.height = 0.90f;
            col.direction = 2; // Z-axis / forward aligned
            col.center = new Vector3(0f, 0f, 0.40f);

            var arrow = root.AddComponent<BambooHunterArrow>();

            // Visual Shaft
            var shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shaft.name = "ArrowShaft";
            shaft.transform.SetParent(root.transform, false);
            shaft.transform.localPosition = new Vector3(0f, 0f, 0.40f);
            shaft.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            shaft.transform.localScale = new Vector3(0.04f, 0.40f, 0.04f);
            if (arrowMat != null) shaft.GetComponent<MeshRenderer>().sharedMaterial = arrowMat;
            UnityEngine.Object.DestroyImmediate(shaft.GetComponent<Collider>());

            // Visual Head
            var head = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            head.name = "ArrowHead";
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, 0f, 0.83f);
            head.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            head.transform.localScale = new Vector3(0.07f, 0.06f, 0.07f);
            if (arrowMat != null) head.GetComponent<MeshRenderer>().sharedMaterial = arrowMat;
            UnityEngine.Object.DestroyImmediate(head.GetComponent<Collider>());

            // Visual Fletching
            var fletching = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fletching.name = "ArrowFletching";
            fletching.transform.SetParent(root.transform, false);
            fletching.transform.localPosition = new Vector3(0f, 0f, 0.04f);
            fletching.transform.localScale = new Vector3(0.12f, 0.02f, 0.10f);
            if (arrowMat != null) fletching.GetComponent<MeshRenderer>().sharedMaterial = arrowMat;
            UnityEngine.Object.DestroyImmediate(fletching.GetComponent<Collider>());

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, ArrowPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);

            return prefab;
        }

        public static GameObject BuildEnemyHierarchy(
            BambooHunterSettings settings,
            GameObject arrowPrefab,
            Material hunterMat,
            Material fletchingMat)
        {
            var root = new GameObject("BambooHunterEnemy");
            root.tag = "Untagged";

            var agent = root.AddComponent<NavMeshAgent>();
            agent.speed = 3.5f;
            agent.angularSpeed = 180f;
            agent.acceleration = 8.0f;
            agent.stoppingDistance = 0.5f;
            agent.radius = 0.5f;
            agent.height = 2.0f;

            var col = root.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 1.0f, 0f);
            col.radius = 0.5f;
            col.height = 2.0f;

            // Anchors
            var eyeAnchor = new GameObject("EyeAnchor").transform;
            eyeAnchor.SetParent(root.transform, false);
            eyeAnchor.localPosition = new Vector3(0f, 1.65f, 0f);

            var bowAnchor = new GameObject("BowAnchor").transform;
            bowAnchor.SetParent(root.transform, false);
            bowAnchor.localPosition = new Vector3(0.30f, 1.25f, 0.45f);

            // Visual Model
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "HunterBodyVisual";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 1.0f, 0f);
            body.transform.localScale = new Vector3(0.85f, 1.0f, 0.85f);
            if (hunterMat != null) body.GetComponent<MeshRenderer>().sharedMaterial = hunterMat;
            UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());

            // Headband / Vision Visor
            var visor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visor.name = "HunterHeadband";
            visor.transform.SetParent(root.transform, false);
            visor.transform.localPosition = new Vector3(0f, 1.65f, 0.38f);
            visor.transform.localScale = new Vector3(0.55f, 0.12f, 0.15f);
            if (fletchingMat != null) visor.GetComponent<MeshRenderer>().sharedMaterial = fletchingMat;
            UnityEngine.Object.DestroyImmediate(visor.GetComponent<Collider>());

            // Bow Prop
            var bowVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bowVisual.name = "BowProp";
            bowVisual.transform.SetParent(root.transform, false);
            bowVisual.transform.localPosition = new Vector3(0.30f, 1.25f, 0.20f);
            bowVisual.transform.localRotation = Quaternion.Euler(15f, 0f, 0f);
            bowVisual.transform.localScale = new Vector3(0.06f, 0.65f, 0.08f);
            if (hunterMat != null) bowVisual.GetComponent<MeshRenderer>().sharedMaterial = hunterMat;
            UnityEngine.Object.DestroyImmediate(bowVisual.GetComponent<Collider>());

            // Components
            var health = root.AddComponent<BambooHunterHealth>();
            health.Settings = settings;

            var perception = root.AddComponent<BambooHunterPerception>();
            perception.Settings = settings;
            perception.EyeAnchor = eyeAnchor;
            perception.BowAnchor = bowAnchor;
            perception.TargetTag = "Player";
            perception.AutoAcquireByTag = true;

            var navigation = root.AddComponent<BambooHunterNavigation>();

            var coverSystem = root.AddComponent<BambooHunterCoverSystem>();

            var bow = root.AddComponent<BambooHunterBow>();
            bow.Settings = settings;
            bow.ArrowPrefab = arrowPrefab;
            bow.LaunchAnchor = bowAnchor;

            var brain = root.AddComponent<BambooHunterBrain>();
            brain.Initialize(settings, perception, navigation, coverSystem, bow, health);

            return root;
        }

        public static GameObject CreateEnemyPrefab(
            BambooHunterSettings settings,
            GameObject arrowPrefab,
            Material hunterMat,
            Material fletchingMat)
        {
            var root = BuildEnemyHierarchy(settings, arrowPrefab, hunterMat, fletchingMat);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, EnemyPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>
        /// Fail-safe component reconciler ensuring every expected AI component, anchor, and setting exists.
        /// </summary>
        public static void EnsureEnemyComponents(
            GameObject enemy,
            BambooHunterSettings settings,
            GameObject arrowPrefab,
            Material hunterMat = null,
            Material fletchingMat = null)
        {
            if (enemy == null) return;

            if (settings == null)
            {
                settings = AssetDatabase.LoadAssetAtPath<BambooHunterSettings>(SettingsPath);
            }
            if (arrowPrefab == null)
            {
                arrowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArrowPrefabPath);
            }

            var agent = enemy.GetComponent<NavMeshAgent>();
            if (agent == null)
            {
                agent = enemy.AddComponent<NavMeshAgent>();
                agent.speed = 3.5f;
                agent.angularSpeed = 180f;
                agent.acceleration = 8.0f;
                agent.stoppingDistance = 0.5f;
                agent.radius = 0.5f;
                agent.height = 2.0f;
            }

            var col = enemy.GetComponent<CapsuleCollider>();
            if (col == null)
            {
                col = enemy.AddComponent<CapsuleCollider>();
                col.center = new Vector3(0f, 1.0f, 0f);
                col.radius = 0.5f;
                col.height = 2.0f;
            }

            // Anchors
            var eyeAnchor = enemy.transform.Find("EyeAnchor");
            if (eyeAnchor == null)
            {
                var eyeGo = new GameObject("EyeAnchor");
                eyeGo.transform.SetParent(enemy.transform, false);
                eyeGo.transform.localPosition = new Vector3(0f, 1.65f, 0f);
                eyeAnchor = eyeGo.transform;
            }

            var bowAnchor = enemy.transform.Find("BowAnchor");
            if (bowAnchor == null)
            {
                var bowGo = new GameObject("BowAnchor");
                bowGo.transform.SetParent(enemy.transform, false);
                bowGo.transform.localPosition = new Vector3(0.30f, 1.25f, 0.45f);
                bowAnchor = bowGo.transform;
            }

            var health = enemy.GetComponent<BambooHunterHealth>();
            if (health == null) health = enemy.AddComponent<BambooHunterHealth>();
            if (health.Settings == null && settings != null) health.Settings = settings;

            var perception = enemy.GetComponent<BambooHunterPerception>();
            if (perception == null) perception = enemy.AddComponent<BambooHunterPerception>();
            if (perception.Settings == null && settings != null) perception.Settings = settings;
            perception.EyeAnchor = eyeAnchor;
            perception.BowAnchor = bowAnchor;
            perception.TargetTag = "Player";
            perception.AutoAcquireByTag = true;

            var navigation = enemy.GetComponent<BambooHunterNavigation>();
            if (navigation == null) navigation = enemy.AddComponent<BambooHunterNavigation>();

            var coverSystem = enemy.GetComponent<BambooHunterCoverSystem>();
            if (coverSystem == null) coverSystem = enemy.AddComponent<BambooHunterCoverSystem>();

            var bow = enemy.GetComponent<BambooHunterBow>();
            if (bow == null) bow = enemy.AddComponent<BambooHunterBow>();
            if (bow.Settings == null && settings != null) bow.Settings = settings;
            if (bow.ArrowPrefab == null && arrowPrefab != null) bow.ArrowPrefab = arrowPrefab;
            if (bow.LaunchAnchor == null) bow.LaunchAnchor = bowAnchor;

            var brain = enemy.GetComponent<BambooHunterBrain>();
            if (brain == null) brain = enemy.AddComponent<BambooHunterBrain>();
            brain.Initialize(settings, perception, navigation, coverSystem, bow, health);
        }

        public static bool CreateDemoScene(
            BambooHunterSettings settings,
            GameObject enemyPrefab,
            GameObject arrowPrefab,
            MaterialSet materials)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. Directional Light
            var lightGo = new GameObject("DirectionalLight");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.color = new Color(1.0f, 0.96f, 0.88f);
            lightGo.transform.rotation = Quaternion.Euler(42f, -35f, 0f);

            // 2. Tactical Overview Camera
            var camGo = new GameObject("TacticalOverviewCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 55f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 200f;
            camGo.transform.position = new Vector3(0f, 14.5f, -22f);
            camGo.transform.rotation = Quaternion.Euler(28f, 0f, 0f);
            camGo.AddComponent<AudioListener>();
            camGo.tag = "MainCamera";

            // 3. Walkable Ground (60m x 60m)
            var groundGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            groundGo.name = "GroundWalkableForest";
            groundGo.transform.position = new Vector3(0f, -0.5f, 0f);
            groundGo.transform.localScale = new Vector3(60f, 1f, 60f);
            if (materials.ground != null) groundGo.GetComponent<MeshRenderer>().sharedMaterial = materials.ground;
            GameObjectUtility.SetStaticEditorFlags(groundGo, StaticEditorFlags.NavigationStatic | StaticEditorFlags.ContributeGI);

            // 4. NavMesh Surface Baking
            BakeNavMeshForGround(groundGo);

            // 5. Patrol Waypoints
            var wpParent = new GameObject("PatrolWaypoints");
            var wp1 = CreateWaypoint("WP_North", new Vector3(-6f, 0f, 6f), wpParent.transform);
            var wp2 = CreateWaypoint("WP_East", new Vector3(12f, 0f, 0f), wpParent.transform);
            var wp3 = CreateWaypoint("WP_South", new Vector3(0f, 0f, -12f), wpParent.transform);
            var wp4 = CreateWaypoint("WP_West", new Vector3(-12f, 0f, 0f), wpParent.transform);
            Transform[] waypoints = { wp1, wp2, wp3, wp4 };

            // 6. Tactical Cover Anchors with Physical Bamboo Occluders (At least 3)
            var coverParent = new GameObject("TacticalCoverSystem");
            var coverPointA = CreateCoverCluster("CoverPoint_WestGrove", new Vector3(-9.5f, 0f, 2.0f), Vector3.right, materials.bambooTrunk, coverParent.transform);
            var coverPointB = CreateCoverCluster("CoverPoint_EastGrove", new Vector3(9.5f, 0f, 2.0f), Vector3.left, materials.bambooTrunk, coverParent.transform);
            var coverPointC = CreateCoverCluster("CoverPoint_SouthGrove", new Vector3(0f, 0f, -9.5f), Vector3.forward, materials.bambooTrunk, coverParent.transform);
            BambooHunterCoverPoint[] coverPoints = { coverPointA, coverPointB, coverPointC };

            // 7. Player Test Dummy
            var dummyGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            dummyGo.name = "PlayerTestDummy";
            dummyGo.tag = "Player";
            dummyGo.transform.position = new Vector3(0f, 1.0f, 8.5f);
            dummyGo.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            if (materials.playerDummy != null) dummyGo.GetComponent<MeshRenderer>().sharedMaterial = materials.playerDummy;

            var dummyHealth = dummyGo.AddComponent<BambooHunterPlayerHealthDemo>();
            dummyHealth.Initialize(100f);

            var dummyHead = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dummyHead.name = "HeadTarget";
            dummyHead.transform.SetParent(dummyGo.transform, false);
            dummyHead.transform.localPosition = new Vector3(0f, 0.65f, 0f);
            dummyHead.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
            if (materials.fletching != null) dummyHead.GetComponent<MeshRenderer>().sharedMaterial = materials.fletching;
            UnityEngine.Object.DestroyImmediate(dummyHead.GetComponent<Collider>());

            // 8. Bamboo Hunter Enemy Instance - Robust Instantiation with Full Prefab Connection
            GameObject enemyInstance = null;

            if (enemyPrefab == null)
            {
                enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
            }

            if (enemyPrefab != null)
            {
                enemyInstance = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefab);
            }

            if (enemyInstance == null && File.Exists(EnemyPrefabPath))
            {
                AssetDatabase.ImportAsset(EnemyPrefabPath, ImportAssetOptions.ForceSynchronousImport);
                var reloadedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
                if (reloadedPrefab != null)
                {
                    enemyInstance = (GameObject)PrefabUtility.InstantiatePrefab(reloadedPrefab);
                }
            }

            if (enemyInstance == null)
            {
                enemyInstance = BuildEnemyHierarchy(settings, arrowPrefab, materials.hunter, materials.fletching);
                if (File.Exists(EnemyPrefabPath))
                {
                    var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
                    if (prefabAsset != null)
                    {
                        PrefabUtility.ConnectGameObjectToPrefab(enemyInstance, prefabAsset);
                    }
                }
            }

            enemyInstance.name = "BambooHunterEnemy";
            enemyInstance.transform.position = new Vector3(0f, 0f, -4.0f);
            enemyInstance.transform.rotation = Quaternion.identity;

            // Ensure every component, anchor, collider, and setting is present
            EnsureEnemyComponents(enemyInstance, settings, arrowPrefab, materials.hunter, materials.fletching);

            // Wire scene-specific references on the enemy instance
            var nav = enemyInstance.GetComponent<BambooHunterNavigation>();
            if (nav != null) nav.SetWaypoints(waypoints);

            var cs = enemyInstance.GetComponent<BambooHunterCoverSystem>();
            if (cs != null) cs.SetCoverPoints(coverPoints);

            var perc = enemyInstance.GetComponent<BambooHunterPerception>();
            if (perc != null) perc.Target = dummyGo.transform;

            var brain = enemyInstance.GetComponent<BambooHunterBrain>();
            if (brain == null)
            {
                throw new InvalidOperationException("[BambooHunterAssetBuilder] CRITICAL: BambooHunterBrain is missing on enemy instance!");
            }

            // 9. Demo Manager (HUD & Play Mode Harness)
            var managerGo = new GameObject("BambooHunterDemoManager");
            var hud = managerGo.AddComponent<BambooHunterDemoHUD>();
            hud.HunterBrain = brain;
            hud.PlayerDummyHealth = dummyHealth;
            hud.PlayerDummyTransform = dummyGo.transform;

            var harness = managerGo.AddComponent<BambooHunterRuntimeHarness>();
            harness.HunterBrain = brain;
            harness.PlayerDummy = dummyHealth;
            harness.CoverSystem = cs;

            EditorUtility.SetDirty(enemyInstance);
            EditorUtility.SetDirty(managerGo);
            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
            return saved;
        }

        /// <summary>
        /// Interactive one-click repair tool to restore BambooHunterEnemy components in the currently active scene.
        /// </summary>
        public static void FixCurrentSceneEnemy()
        {
            var currentScene = EditorSceneManager.GetActiveScene();
            if (!currentScene.IsValid())
            {
                Debug.LogWarning("[BambooHunterAssetBuilder] No active scene open to fix.");
                return;
            }

            var settings = AssetDatabase.LoadAssetAtPath<BambooHunterSettings>(SettingsPath);
            var arrowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArrowPrefabPath);
            var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);

            var enemyGo = GameObject.Find("BambooHunterEnemy");
            if (enemyGo == null)
            {
                if (enemyPrefab != null)
                {
                    enemyGo = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefab);
                }
                else
                {
                    enemyGo = new GameObject("BambooHunterEnemy");
                }
                enemyGo.name = "BambooHunterEnemy";
                enemyGo.transform.position = new Vector3(0f, 0f, -4.0f);
                enemyGo.transform.rotation = Quaternion.identity;
            }

            EnsureEnemyComponents(enemyGo, settings, arrowPrefab, null, null);

            if (enemyPrefab != null && !PrefabUtility.IsPartOfAnyPrefab(enemyGo))
            {
                PrefabUtility.ConnectGameObjectToPrefab(enemyGo, enemyPrefab);
            }

            // Wire waypoints
            var wpParent = GameObject.Find("PatrolWaypoints");
            if (wpParent != null)
            {
                var nav = enemyGo.GetComponent<BambooHunterNavigation>();
                if (nav != null)
                {
                    var wps = new Transform[wpParent.transform.childCount];
                    for (int i = 0; i < wps.Length; i++) wps[i] = wpParent.transform.GetChild(i);
                    nav.SetWaypoints(wps);
                }
            }

            // Wire cover points
            var coverParent = GameObject.Find("TacticalCoverSystem");
            if (coverParent != null)
            {
                var cs = enemyGo.GetComponent<BambooHunterCoverSystem>();
                if (cs != null)
                {
                    cs.SetCoverPoints(coverParent.GetComponentsInChildren<BambooHunterCoverPoint>());
                }
            }

            // Wire target dummy
            var dummyGo = GameObject.Find("PlayerTestDummy");
            if (dummyGo != null)
            {
                var perc = enemyGo.GetComponent<BambooHunterPerception>();
                if (perc != null) perc.Target = dummyGo.transform;
            }

            // Wire DemoManager
            var managerGo = GameObject.Find("BambooHunterDemoManager");
            if (managerGo != null)
            {
                var brain = enemyGo.GetComponent<BambooHunterBrain>();
                var dummyHealth = dummyGo != null ? dummyGo.GetComponent<BambooHunterPlayerHealthDemo>() : null;
                var cs = enemyGo.GetComponent<BambooHunterCoverSystem>();

                var hud = managerGo.GetComponent<BambooHunterDemoHUD>();
                if (hud != null)
                {
                    hud.HunterBrain = brain;
                    hud.PlayerDummyHealth = dummyHealth;
                    if (dummyGo != null) hud.PlayerDummyTransform = dummyGo.transform;
                }

                var harness = managerGo.GetComponent<BambooHunterRuntimeHarness>();
                if (harness != null)
                {
                    harness.HunterBrain = brain;
                    harness.PlayerDummy = dummyHealth;
                    harness.CoverSystem = cs;
                }
            }

            EditorSceneManager.MarkSceneDirty(currentScene);
            EditorSceneManager.SaveScene(currentScene);
            Debug.Log("[BambooHunterAssetBuilder] Successfully repaired and saved BambooHunterEnemy in scene!");
        }

        private static Transform CreateWaypoint(string name, Vector3 pos, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            return go.transform;
        }

        private static BambooHunterCoverPoint CreateCoverCluster(
            string name,
            Vector3 centerPos,
            Vector3 faceDir,
            Material trunkMat,
            Transform parent)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = centerPos;
            root.transform.rotation = Quaternion.LookRotation(faceDir);

            // Create cluster of 5 bamboo trunk occluders with physics colliders in front of hide position
            Vector3[] trunkOffsets = {
                new Vector3(0f, 0f, 1.2f),
                new Vector3(-0.5f, 0f, 1.3f),
                new Vector3(0.5f, 0f, 1.3f),
                new Vector3(-0.25f, 0f, 1.0f),
                new Vector3(0.25f, 0f, 1.0f)
            };

            for (int i = 0; i < trunkOffsets.Length; i++)
            {
                var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                trunk.name = $"BambooTrunk_{i + 1}";
                trunk.transform.SetParent(root.transform, false);
                trunk.transform.localPosition = trunkOffsets[i] + Vector3.up * 2.25f;
                trunk.transform.localScale = new Vector3(0.35f, 2.25f, 0.35f);
                if (trunkMat != null) trunk.GetComponent<MeshRenderer>().sharedMaterial = trunkMat;

                // Ensure static for navigation obstacle and physics collision
                GameObjectUtility.SetStaticEditorFlags(trunk, StaticEditorFlags.NavigationStatic | StaticEditorFlags.ContributeGI);
            }

            var cp = root.AddComponent<BambooHunterCoverPoint>();
            Vector3 peekOffset = new Vector3(1.2f, 0f, 0.4f);

            cp.Initialize(
                peekLocalOffset: peekOffset,
                quality: 0.90f,
                radius: 2.0f,
                isVegetation: true
            );

            return cp;
        }

        private static void BakeNavMeshForGround(GameObject ground)
        {
            try
            {
                // Attempt NavMeshSurface bake if com.unity.ai.navigation is present
                Type surfaceType = Type.GetType("Unity.AI.Navigation.NavMeshSurface, Unity.AI.Navigation");
                if (surfaceType == null)
                {
                    surfaceType = Type.GetType("Unity.AI.Navigation.NavMeshSurface, Unity.AI.Navigation.Runtime");
                }

                if (surfaceType != null)
                {
                    Component surface = ground.AddComponent(surfaceType);
                    MethodInfo buildMethod = surfaceType.GetMethod("BuildNavMesh", BindingFlags.Public | BindingFlags.Instance);
                    if (buildMethod != null)
                    {
                        buildMethod.Invoke(surface, null);
                        Debug.Log("[BambooHunterAssetBuilder] NavMesh successfully baked via Unity.AI.Navigation.NavMeshSurface!");
                        return;
                    }
                }

                // Fallback to UnityEditor.AI.NavMeshBuilder
                UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
                Debug.Log("[BambooHunterAssetBuilder] NavMesh baked via UnityEditor.AI.NavMeshBuilder.BuildNavMesh()");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BambooHunterAssetBuilder] NavMesh baking fallback triggered: {ex.Message}");
                try
                {
                    UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
                }
                catch (Exception) { }
            }
        }
    }
}
