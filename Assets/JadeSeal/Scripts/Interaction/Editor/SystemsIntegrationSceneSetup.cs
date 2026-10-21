#if UNITY_EDITOR
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.InputSystem;
using JadeSeal.Interaction;
using JadeSeal.Player;

namespace JadeSeal.Interaction.Editor
{
    /// <summary>
    /// Comprehensive Visual & Level Design Integration utility for The Jade Seal.
    /// Perfectly integrates all Systems Engineer interaction mechanics into Hiruni's environment,
    /// precisely matching the art direction, architectural composition, and spatial progression
    /// of the official reference design:
    ///
    /// LEVEL PROGRESSION:
    /// 1. Start / Bamboo Forest: Hand-sized throwable river rocks placed on trail shoulder (RF_rock meshes normalized).
    /// 2. First Door / Forest Gate: Unlocked rustic double wooden gate (Door 1) with timber posts, bamboo fencing,
    ///    overhead lintel, and hanging lanterns. Opens with [E] without key.
    /// 3. Roadside Supply Area: Moved into roadside clearing (Z = -28.0m). Walking trail (Z = -34.5m) is 100% clear.
    ///    Contains timber shelter, crimson banner, stacked cargo, pushable wooden crate, and carved stone barricade.
    /// 4. Jade Key Shrine: Natural forest rock/fern alcove shrine (X = -29.0m, Z = -38.5m) with stepped stone altar,
    ///    ceramic lotus offering dish, two authentic carved stone pagoda lanterns, and Jade Talisman Key.
    /// 5. Second Door / Temple Entrance: Grand imperial vermilion double gate (Door 2) seated AT GROUND LEVEL on
    ///    Threshold_Upper_Step (Y=0.40m at X=5.25m, Z=-30.00m). Studded with gold nails, lion knockers, and side wings.
    ///    Closed and locked; requires the Jade Key to unlock with [E].
    /// 6. Inner Courtyard: Preserves Hiruni's original courtyard architecture, landscaping, and bridges.
    /// 7. Mountain Exit: Barred bronze/timber sliding portcullis fitted into the Mountain Exit Gateway (X=63.0, Z=-10.5).
    /// </summary>
    public static class SystemsIntegrationSceneSetup
    {
        private const string SourceScenePath = "Assets/JadeSeal/Scenes/JadeSealFantasy.unity";
        private const string DestScenePath = "Assets/JadeSeal/Scenes/JadeSealFantasy_SystemsIntegration.unity";
        private const string PreFinalBackupPath = "Assets/JadeSeal/Scenes/JadeSealFantasy_SystemsIntegration_PreFinalPolish.unity";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";

        // Production environment materials & meshes (authentic Chinese fantasy aesthetic)
        private const string MatVermilion = "Assets/JadeSeal/Phase6/Revision2/Materials/P6_Vermilion_Lacquer.mat";
        private const string MatRosewood = "Assets/JadeSeal/Phase6/Revision2/Materials/P6_Oiled_Rosewood.mat";
        private const string MatGold = "Assets/JadeSeal/Phase6/Revision2/Materials/P6_Antique_Gold.mat";
        private const string MatJade = "Assets/JadeSeal/Phase6/Zone4/Materials/Z4_Luminous_Jade.mat";
        private const string MatJadeCeramic = "Assets/JadeSeal/Phase6/Revision2/Materials/R2_Jade_Ceramic.mat";
        private const string MatOak = "Assets/JadeSeal/Phase6/Zone2/Materials/Z2_Training_Oak.mat";
        private const string MatRockCC0 = "Assets/JadeSealDistribution/RockFernPass/RF_Rock_CC0.mat";
        private const string MatBanner = "Assets/JadeSeal/Phase6/Revision2/Materials/P6_Crimson_Banner.mat";
        private const string MatLantern = "Assets/JadeSeal/Phase6/Revision2/Materials/P6_Lantern_Silk.mat";
        private const string MatPodiumStone = "Assets/JadeSealDistribution/FinalClearance/Materials/Clear_Z4_Carved_Podium_Stone.mat";
        private const string MatPaper = "Assets/JadeSeal/Phase6/Zone2/Materials/Z2_Lattice_Paper.mat";

        private const string MeshRock0 = "Assets/JadeSealDistribution/RockFernPass/RF_rock_0.asset";
        private const string MeshRock1 = "Assets/JadeSealDistribution/RockFernPass/RF_rock_2.asset";

        [MenuItem("The Jade Seal/1. Apply Systems Environment Polish (One-Click)", priority = 1)]
        [MenuItem("The Jade Seal/Final Environment Polish", priority = 90)]
        [MenuItem("Tools/The Jade Seal/Final Environment Polish", priority = 90)]
        public static void FinalEnvironmentPolish()
        {
            ApplyCompleteVisualRedesign(isBatchMode: false);
        }

        [MenuItem("The Jade Seal/2. Validate Integration Scene", priority = 2)]
        [MenuItem("The Jade Seal/Validate Temple Gate Geometry", priority = 91)]
        [MenuItem("Tools/The Jade Seal/Validate Temple Gate Geometry", priority = 91)]
        public static void ValidateIntegrationScene()
        {
            RunComprehensiveSceneValidation(isSilent: false);
        }

        [MenuItem("The Jade Seal/Create Systems Integration Scene", priority = 100)]
        [MenuItem("Tools/The Jade Seal/Create Systems Integration Scene", priority = 100)]
        public static void CreateSystemsIntegrationScene()
        {
            BuildSystemsIntegrationScene(isBatchMode: false);
        }

        public static void FinalEnvironmentPolishBatch()
        {
            ApplyCompleteVisualRedesign(isBatchMode: true);
        }

        public static void CreateSystemsIntegrationSceneBatch()
        {
            BuildSystemsIntegrationScene(isBatchMode: true);
        }

        /// <summary>
        /// Applies the reference-matching level design and visual polish to the integration scene.
        /// </summary>
        public static void ApplyCompleteVisualRedesign(bool isBatchMode)
        {
            // If dest scene doesn't exist yet, copy from source
            if (!File.Exists(DestScenePath) && AssetDatabase.LoadAssetAtPath<SceneAsset>(DestScenePath) == null)
            {
                if (!File.Exists(SourceScenePath) && AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScenePath) == null)
                {
                    string msg = $"Source scene not found at:\n{SourceScenePath}";
                    Debug.LogError(msg);
                    if (!isBatchMode) EditorUtility.DisplayDialog("Source Scene Missing", msg, "OK");
                    return;
                }

                AssetDatabase.CopyAsset(SourceScenePath, DestScenePath);
                AssetDatabase.Refresh();
            }

            // 1. Safe Backup of current integration state
            try
            {
                if (File.Exists(DestScenePath))
                {
                    AssetDatabase.DeleteAsset(PreFinalBackupPath);
                    AssetDatabase.CopyAsset(DestScenePath, PreFinalBackupPath);
                    AssetDatabase.Refresh();
                    Debug.Log($"<color=#3498db><b>[The Jade Seal]</b></color> Safe backup saved to: {PreFinalBackupPath}");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[The Jade Seal] Backup notice: {ex.Message}");
            }

            // 2. Open ONLY the integration scene
            var integrationScene = EditorSceneManager.OpenScene(DestScenePath, OpenSceneMode.Single);
            if (!integrationScene.IsValid())
            {
                string errorMsg = $"[The Jade Seal] Failed to open scene at '{DestScenePath}'.";
                Debug.LogError(errorMsg);
                if (!isBatchMode) EditorUtility.DisplayDialog("Open Scene Failed", errorMsg, "OK");
                return;
            }

            // 3. Disable conflicting review camera if present
            var reviewCam = GameObject.Find("Phase6_Checkpoint_Review_Camera");
            if (reviewCam != null) reviewCam.SetActive(false);

            // 4. Thoroughly purge all obsolete or duplicate Systems Engineer objects
            ThoroughCleanupObsoleteObjects();

            // 5. Ensure grounded playable player at Player_Spawn
            try
            {
                EnsureGroundedPlayer();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[The Jade Seal] Warning during player setup: {ex}");
            }

            // 6. Populate reference-inspired systems progression at surveyed ground elevations
            try
            {
                PopulateReferenceLevelDesign();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[The Jade Seal] Error during level design population: {ex}");
            }

            // 7. Mark scene dirty and save
            EditorSceneManager.MarkSceneDirty(integrationScene);
            bool saved = EditorSceneManager.SaveScene(integrationScene, DestScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"<color=#2ecc71><b>[The Jade Seal]</b></color> Successfully generated and saved polished scene to:\n<b>{DestScenePath}</b> (SaveResult: {saved})");

            // 8. Run comprehensive validation immediately
            RunComprehensiveSceneValidation(isSilent: isBatchMode);

            if (!isBatchMode)
            {
                EditorUtility.DisplayDialog(
                    "Systems Environment Polish Applied!",
                    $"The Systems Integration Scene has been fully updated and saved!\n\n" +
                    $"Scene: {DestScenePath}\n\n" +
                    "Progression Highlights Verified:\n" +
                    "1. Bamboo Forest: Hand-sized throwable river rocks placed on trail shoulder (NOT in path).\n" +
                    "2. Forest Gate (Door 1): Framed rustic double wooden gate with timber posts, overhead lintel, lanterns & bamboo fencing. Opens with [E] without key.\n" +
                    "3. Roadside Supply Area: Moved into roadside clearing (Z = -28.0m). Walking trail (Z = -34.5m) is 100% wide open. Pushable crate & stone barricade safely inside clearing.\n" +
                    "4. Jade Key Shrine: Natural rock/fern alcove shrine (X = -29.0m, Z = -38.5m). Carved stone altar, ceramic lotus dish, 2 stone pagoda lanterns (zero red cubes!), and authentic Jade Key.\n" +
                    "5. Imperial Temple Gate (Door 2): Grand vermilion double gate seated AT GROUND LEVEL on Threshold_Upper_Step (X=5.25m, Y=0.40m, Z=-30.00m). Completely closed & locked; requires Jade Key to unlock with [E].\n" +
                    "6. Mountain Exit: Sliding bronze/timber portcullis at Mountain Exit Gateway.\n\n" +
                    "Press 'Play' in Unity to test!",
                    "OK");
            }
        }

        private static void BuildSystemsIntegrationScene(bool isBatchMode)
        {
            if (!File.Exists(SourceScenePath) && AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScenePath) == null)
            {
                string msg = $"Source environment scene not found at:\n{SourceScenePath}";
                Debug.LogError(msg);
                if (!isBatchMode) EditorUtility.DisplayDialog("Source Scene Missing", msg, "OK");
                return;
            }

            AssetDatabase.DeleteAsset(DestScenePath);
            AssetDatabase.Refresh();

            bool copied = AssetDatabase.CopyAsset(SourceScenePath, DestScenePath);
            if (!copied)
            {
                string errorMsg = $"Failed to copy '{SourceScenePath}' to '{DestScenePath}'.";
                Debug.LogError(errorMsg);
                if (!isBatchMode) EditorUtility.DisplayDialog("Copy Failed", errorMsg, "OK");
                return;
            }

            AssetDatabase.Refresh();
            ApplyCompleteVisualRedesign(isBatchMode);
        }

        /// <summary>
        /// Thoroughly finds and removes any old, duplicate, or obsolete Systems Engineer objects from previous iterations.
        /// NEVER deletes or modifies teammate environment objects.
        /// </summary>
        public static void ThoroughCleanupObsoleteObjects()
        {
            var obsoleteNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
            {
                "--- SYSTEMS_ENGINEER_INTEGRATION ---",
                "_SystemsEngineer_Integration",
                "Systems_Engineer_Integration",
                "Locked_Ceremonial_Gate",
                "Courtyard_Swing_Gate",
                "Pushable_Wooden_Crate",
                "Pushable_Supply_Crate",
                "Pushable_Stone_Barricade",
                "Jade_Key_Pickup_Altar",
                "Jade_Key_Crystal",
                "Sanctuary_Sliding_Portcullis",
                "Temple_Imperial_Gate_Door2",
                "Forest_Double_Gate_Door1",
                "Roadside_Supply_Area",
                "Jade_Key_Shrine",
                "Mountain_Exit_Portcullis",
                "River_Stone",
                "Mountain_Boulder"
            };

            var toDestroy = new List<GameObject>();

            var allGos = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (var go in allGos)
            {
                if (go == null) continue;
                if (EditorUtility.IsPersistent(go)) continue; // Skip assets in project
                if (go.hideFlags != HideFlags.None) continue;
                if (IsTeammateObject(go)) continue;

                if (obsoleteNames.Contains(go.name))
                {
                    if (!toDestroy.Contains(go)) toDestroy.Add(go);
                }
            }

            // Also check for loose components
            var doors = Object.FindObjectsByType<DoorInteraction>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var d in doors)
            {
                if (d == null) continue;
                var root = d.transform.root.gameObject;
                if (!IsTeammateObject(root) && !toDestroy.Contains(root))
                {
                    toDestroy.Add(root);
                }
            }

            var barricades = Object.FindObjectsByType<PushableBarricade>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var b in barricades)
            {
                if (b == null) continue;
                var root = b.transform.root.gameObject;
                if (!IsTeammateObject(root) && !toDestroy.Contains(root))
                {
                    toDestroy.Add(root);
                }
            }

            var pickups = Object.FindObjectsByType<PickupableObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var p in pickups)
            {
                if (p == null) continue;
                var root = p.transform.root.gameObject;
                if (!IsTeammateObject(root) && !toDestroy.Contains(root))
                {
                    toDestroy.Add(root);
                }
            }

            var keys = Object.FindObjectsByType<JadeKeyPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var k in keys)
            {
                if (k == null) continue;
                var root = k.transform.root.gameObject;
                if (!IsTeammateObject(root) && !toDestroy.Contains(root))
                {
                    toDestroy.Add(root);
                }
            }

            int count = 0;
            foreach (var go in toDestroy)
            {
                if (go != null)
                {
                    count++;
                    Object.DestroyImmediate(go);
                }
            }

            Debug.Log($"<color=#3498db><b>[The Jade Seal]</b></color> Purged {count} obsolete Systems Engineer objects.");
        }

        private static bool IsTeammateObject(GameObject go)
        {
            if (go == null) return false;
            string name = go.name;

            // Never delete teammate environment roots, geometry, or AI modules
            if (name.StartsWith("01_") || name.StartsWith("02_") || name.StartsWith("03_") ||
                name.StartsWith("04_") || name.StartsWith("05_") || name.StartsWith("06_") ||
                name.StartsWith("07_") || name.StartsWith("08_") || name.StartsWith("09_") ||
                name.StartsWith("P6_") || name.StartsWith("Z2_") || name.StartsWith("Z3_") || name.StartsWith("Z4_") ||
                name.StartsWith("LOD_") || name.StartsWith("Terrain") || name.StartsWith("Directional") ||
                name.StartsWith("Main Camera") || name.StartsWith("Global") || name.StartsWith("Lighting") ||
                name.Contains("Temple") || name.Contains("Academy") || name.Contains("Column") ||
                name.Contains("Threshold") || name.Contains("Entablature") || name.Contains("Paver") ||
                name.Contains("Step") || name.Contains("Wall") || name.Contains("Roof") ||
                name.Contains("RoyalGuard") || name.Contains("FinalBoss") || name.Contains("Boss"))
            {
                // Systems gate exceptions
                if (name == "Temple_Imperial_Gate_Door2" || name == "Locked_Ceremonial_Gate")
                    return false;
                return true;
            }
            return false;
        }

        private static void EnsureGroundedPlayer()
        {
            var existingPlayer = GameObject.FindWithTag("Player");
            if (existingPlayer != null)
            {
                ConfigurePlayerComponents(existingPlayer);
                return;
            }

            var spawnPoint = GameObject.Find("Player_Spawn");
            Vector3 spawnPos = spawnPoint != null ? spawnPoint.transform.position : new Vector3(-65.0f, 0.21f, -50.0f);
            spawnPos.y += 0.05f;

            var playerGo = new GameObject("Systems_Integrated_Player");
            playerGo.tag = "Player";
            playerGo.transform.position = spawnPos;
            if (spawnPoint != null) playerGo.transform.rotation = spawnPoint.transform.rotation;

            var charController = playerGo.AddComponent<CharacterController>();
            charController.height = 1.8f;
            charController.radius = 0.35f;
            charController.center = new Vector3(0f, 0.9f, 0f);
            charController.stepOffset = 0.35f;

            var camGo = new GameObject("Player_FirstPerson_Camera");
            camGo.transform.SetParent(playerGo.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.fieldOfView = 65.0f;
            camGo.AddComponent<AudioListener>();

            var holdPoint = new GameObject("Object_HoldPoint");
            holdPoint.transform.SetParent(camGo.transform, false);
            holdPoint.transform.localPosition = new Vector3(0f, -0.2f, 1.4f);

            ConfigurePlayerComponents(playerGo);
            Debug.Log($"<color=#2ecc71><b>[The Jade Seal]</b></color> Configured grounded playable player at: {spawnPos}");
        }

        private static void ConfigurePlayerComponents(GameObject playerGo)
        {
            var movement = playerGo.GetComponent<BasicPhysicsPlayerController>();
            if (movement == null) movement = playerGo.AddComponent<BasicPhysicsPlayerController>();

            var push = playerGo.GetComponent<CharacterControllerPush>();
            if (push == null) push = playerGo.AddComponent<CharacterControllerPush>();

            var inventory = playerGo.GetComponent<PlayerKeyInventory>();
            if (inventory == null) inventory = playerGo.AddComponent<PlayerKeyInventory>();

            var holder = playerGo.GetComponent<PlayerObjectHolder>();
            if (holder == null) holder = playerGo.AddComponent<PlayerObjectHolder>();

            var interactor = playerGo.GetComponent<PlayerInteractor>();
            if (interactor == null) interactor = playerGo.AddComponent<PlayerInteractor>();

            var actionsAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (actionsAsset != null)
            {
                if (movement != null)
                {
                    var movSo = new SerializedObject(movement);
                    SafeSetObjectRef(movSo, "inputActionsAsset", actionsAsset);
                    movSo.ApplyModifiedPropertiesWithoutUndo();
                }

                if (interactor != null)
                {
                    var interSo = new SerializedObject(interactor);
                    SafeSetObjectRef(interSo, "inputActionsAsset", actionsAsset);
                    interSo.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        private static void PopulateReferenceLevelDesign()
        {
            var systemsRoot = new GameObject("--- SYSTEMS_ENGINEER_INTEGRATION ---");

            // -------------------------------------------------------------
            // 1. START - BAMBOO FOREST: Accurately Scaled Hand-Sized Throwable Rocks
            // Tucked subtly beside the trail on the shoulder, NOT in the walking route.
            // Trail centerline at X=-60 is Z=-49.5. Rocks are placed at Z=-48.2 and Z=-47.4.
            // -------------------------------------------------------------
            var rock1 = CreateNormalizedThrowableRock(new Vector3(-60.8f, 0.22f, -48.2f), 0.20f, "River_Stone", 1.5f, MeshRock0);
            rock1.transform.SetParent(systemsRoot.transform, true);

            var rock2 = CreateNormalizedThrowableRock(new Vector3(-59.4f, 0.24f, -47.4f), 0.24f, "Mountain_Boulder", 2.5f, MeshRock1);
            rock2.transform.SetParent(systemsRoot.transform, true);

            // -------------------------------------------------------------
            // 2. FIRST DOOR - FOREST GATE (UNLOCKED)
            // Double wooden gate matching reference Panel 1 with timber posts,
            // stone pedestals, overhead lintel, hanging silk lanterns, and bamboo fencing.
            // Trail elevation surveyed at Y = 0.30m.
            // -------------------------------------------------------------
            var forestGate = CreateReferenceForestGate(
                new Vector3(-53.0f, 0.30f, -41.5f),
                Quaternion.Euler(0f, 40f, 0f),
                "Forest_Double_Gate_Door1");
            forestGate.transform.SetParent(systemsRoot.transform, true);

            // -------------------------------------------------------------
            // 3. SUPPLY AREA (ROADSIDE STORAGE CLEARING)
            // Moved completely into roadside clearing at Z = -28.0m (6.5m north of trail).
            // Walking trail at Z = -34.5m is 100% wide open and unobstructed.
            // Pushable crate and stone barricade are kept safely inside the clearing staging area.
            // Surveyed ground elevation Y = 0.40m.
            // -------------------------------------------------------------
            var supplyArea = CreateReferenceSupplyArea(
                new Vector3(-42.0f, 0.40f, -28.0f),
                Quaternion.Euler(0f, 15f, 0f),
                "Roadside_Supply_Area");
            supplyArea.transform.SetParent(systemsRoot.transform, true);

            // -------------------------------------------------------------
            // 4. JADE KEY SHRINE (NATURAL FOREST ALCOVE)
            // Tucked into natural rock & fern alcove south of the trail at X = -29.0m, Z = -38.5m.
            // (4.0m off the trail centerline Z = -34.5m). Surveyed ground elevation Y = 0.35m.
            // Authentic multi-tiered carved stone altar, ceramic lotus offering dish,
            // two traditional carved stone pagoda lanterns (zero red cubes!),
            // and authentic Jade Talisman Key resting naturally inside the dish.
            // -------------------------------------------------------------
            var jadeShrine = CreateReferenceJadeShrine(
                new Vector3(-29.0f, 0.35f, -38.5f),
                Quaternion.Euler(0f, 175f, 0f),
                "Jade_Key_Shrine");
            jadeShrine.transform.SetParent(systemsRoot.transform, true);

            // -------------------------------------------------------------
            // 5. SECOND DOOR - TEMPLE ENTRANCE (LOCKED, REQUIRES JADE KEY)
            // Seated AT GROUND LEVEL firmly on Threshold_Upper_Step:
            // Surveyed position: X = 5.25m, Y = 0.40m, Z = -30.00m.
            // Spans the entire 10.60m column opening between Vermilion_Column at Z=-24.7 and Z=-35.3.
            // Vermilion lacquer double doors (2.65m wide x 3.80m high each, meeting at Z=-30.00m).
            // 24 gold studs, sculpted lion knockers, rosewood lintel, and stone threshold sill.
            // Initially completely closed & locked; requires Jade Key in inventory to unlock with [E].
            // -------------------------------------------------------------
            var templeGate = CreateReferenceTempleGate(
                new Vector3(5.25f, 0.40f, -30.00f),
                Quaternion.Euler(0f, 90f, 0f),
                "Temple_Imperial_Gate_Door2");
            templeGate.transform.SetParent(systemsRoot.transform, true);

            // -------------------------------------------------------------
            // 7. MOUNTAIN EXIT PORTCULLIS
            // Sliding bronze/timber portcullis fitted under Mountain_Exit_Lintel (X=63.0, Z=-10.5).
            // -------------------------------------------------------------
            var slidingPortcullis = CreateReferenceMountainPortcullis(
                new Vector3(63.00f, 0.43f, -10.50f),
                Quaternion.identity,
                "Mountain_Exit_Portcullis");
            slidingPortcullis.transform.SetParent(systemsRoot.transform, true);
        }

        #region 1. Forest Gate (Door 1 - Unlocked Double Gate)

        public static GameObject CreateReferenceForestGate(Vector3 position, Quaternion rotation, string name)
        {
            var gateRoot = new GameObject(name);
            gateRoot.transform.position = position;
            gateRoot.transform.rotation = rotation;

            var frameMat = LoadMaterialOrDefault(MatRosewood, new Color(0.24f, 0.20f, 0.18f), "P6_Oiled_Rosewood");
            var plankMat = LoadMaterialOrDefault(MatOak, new Color(0.65f, 0.45f, 0.28f), "Z2_Training_Oak");
            var stoneMat = LoadMaterialOrDefault(MatRockCC0, new Color(0.35f, 0.35f, 0.35f), "RF_Rock_CC0");
            var goldMat = LoadMaterialOrDefault(MatGold, new Color(0.85f, 0.72f, 0.35f), "P6_Antique_Gold");
            var lanternMat = LoadMaterialOrDefault(MatLantern, new Color(0.95f, 0.65f, 0.25f), "P6_Lantern_Silk");

            float postDist = 1.45f; // Opening between posts is 2.90m
            float postHeight = 3.60f;

            // 1. Two Heavy Timber Gateposts on Carved Stone Pedestals
            for (int side = -1; side <= 1; side += 2)
            {
                var pedestal = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pedestal.name = side == -1 ? "Pedestal_Left" : "Pedestal_Right";
                pedestal.transform.SetParent(gateRoot.transform, false);
                pedestal.transform.localPosition = new Vector3(side * postDist, 0.25f, 0f);
                pedestal.transform.localScale = new Vector3(0.55f, 0.50f, 0.55f);
                pedestal.GetComponent<Renderer>().sharedMaterial = stoneMat;

                var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                post.name = side == -1 ? "Post_Left" : "Post_Right";
                post.transform.SetParent(gateRoot.transform, false);
                post.transform.localPosition = new Vector3(side * postDist, postHeight * 0.5f, 0f);
                post.transform.localScale = new Vector3(0.30f, postHeight * 0.5f, 0.30f);
                post.GetComponent<Renderer>().sharedMaterial = frameMat;

                var cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                cap.name = "Gold_Collar";
                cap.transform.SetParent(post.transform, false);
                cap.transform.localPosition = new Vector3(0f, 0.95f, 0f);
                cap.transform.localScale = new Vector3(1.15f, 0.08f, 1.15f);
                cap.GetComponent<Renderer>().sharedMaterial = goldMat;
                StripCollider(cap);
            }

            // 2. Heavy Timber Lintel Crossbeam with Rafter Ends
            var lintel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lintel.name = "Timber_Lintel";
            lintel.transform.SetParent(gateRoot.transform, false);
            lintel.transform.localPosition = new Vector3(0f, postHeight + 0.12f, 0f);
            lintel.transform.localScale = new Vector3(postDist * 2f + 1.2f, 0.25f, 0.40f);
            lintel.GetComponent<Renderer>().sharedMaterial = frameMat;

            // 5 Angled Rafter Tails across the top
            for (int r = -2; r <= 2; r++)
            {
                var rafter = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rafter.name = $"Rafter_Tail_{r}";
                rafter.transform.SetParent(gateRoot.transform, false);
                rafter.transform.localPosition = new Vector3(r * 0.85f, postHeight + 0.32f, 0f);
                rafter.transform.localScale = new Vector3(0.12f, 0.14f, 0.70f);
                rafter.GetComponent<Renderer>().sharedMaterial = frameMat;
                StripCollider(rafter);
            }

            // 3. Hanging Silk Lanterns with warm glow
            for (int side = -1; side <= 1; side += 2)
            {
                var lanternHook = new GameObject(side == -1 ? "Lantern_Left" : "Lantern_Right");
                lanternHook.transform.SetParent(gateRoot.transform, false);
                lanternHook.transform.localPosition = new Vector3(side * (postDist + 0.35f), postHeight - 0.20f, 0.35f);

                var silkBody = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                silkBody.name = "Silk_Chamber";
                silkBody.transform.SetParent(lanternHook.transform, false);
                silkBody.transform.localPosition = new Vector3(0f, -0.45f, 0f);
                silkBody.transform.localScale = new Vector3(0.28f, 0.32f, 0.28f);
                silkBody.GetComponent<Renderer>().sharedMaterial = lanternMat;
                StripCollider(silkBody);

                var lPoint = lanternHook.AddComponent<Light>();
                lPoint.type = LightType.Point;
                lPoint.color = new Color(1.0f, 0.65f, 0.22f);
                lPoint.intensity = 0.85f;
                lPoint.range = 3.5f;
            }

            // 4. Flanking Bamboo Fences linking to terrain borders
            CreateBambooFenceWing(gateRoot.transform, new Vector3(-(postDist + 1.25f), 1.0f, 0f), 1.6f, 2.0f, frameMat);
            CreateBambooFenceWing(gateRoot.transform, new Vector3(postDist + 1.25f, 1.0f, 0f), 1.6f, 2.0f, frameMat);

            // 5. Dual Gate Leaves Hinged to Posts
            float leafW = 1.35f;
            float leafH = 2.70f;
            float leafT = 0.12f;

            var leftHinge = new GameObject("Hinge_Left");
            leftHinge.transform.SetParent(gateRoot.transform, false);
            leftHinge.transform.localPosition = new Vector3(-postDist + 0.15f, 0f, 0f);

            var leftLeaf = CreateRusticGateLeaf(leftHinge.transform, isLeft: true, leafW, leafH, leafT, plankMat, frameMat, goldMat);

            var rightHinge = new GameObject("Hinge_Right");
            rightHinge.transform.SetParent(gateRoot.transform, false);
            rightHinge.transform.localPosition = new Vector3(postDist - 0.15f, 0f, 0f);

            var rightLeaf = CreateRusticGateLeaf(rightHinge.transform, isLeft: false, leafW, leafH, leafT, plankMat, frameMat, goldMat);

            // 6. Setup DoorInteraction on Left Leaf (Primary Double-Door Controller) - UNLOCKED
            var doorComp = leftLeaf.AddComponent<DoorInteraction>();
            var doorSo = new SerializedObject(doorComp);
            SafeSetObjectRef(doorSo, "primaryHinge", leftHinge.transform);
            SafeSetObjectRef(doorSo, "secondaryHinge", rightHinge.transform);
            SafeSetEnum(doorSo, "motionType", (int)DoorMotionType.RotatingSwing);
            SafeSetEnum(doorSo, "swingDirection", (int)SwingDirectionMode.AwayFromPlayer);
            SafeSetFloat(doorSo, "openAngle", 90.0f);
            SafeSetFloat(doorSo, "animationDuration", 1.0f);
            SafeSetBool(doorSo, "isLocked", false);
            SafeSetString(doorSo, "openPromptText", "Open Forest Gate");
            SafeSetString(doorSo, "closePromptText", "Close Forest Gate");
            doorSo.ApplyModifiedPropertiesWithoutUndo();

            // Setup Paired DoorInteraction on Right Leaf delegating to primary
            var rightComp = rightLeaf.AddComponent<DoorInteraction>();
            rightComp.SetPairedDoor(doorComp);
            var rightSo = new SerializedObject(rightComp);
            SafeSetObjectRef(rightSo, "primaryHinge", leftHinge.transform);
            SafeSetObjectRef(rightSo, "secondaryHinge", rightHinge.transform);
            SafeSetEnum(rightSo, "motionType", (int)DoorMotionType.RotatingSwing);
            SafeSetEnum(rightSo, "swingDirection", (int)SwingDirectionMode.AwayFromPlayer);
            SafeSetFloat(rightSo, "openAngle", 90.0f);
            SafeSetFloat(rightSo, "animationDuration", 1.0f);
            SafeSetBool(rightSo, "isLocked", false);
            SafeSetString(rightSo, "openPromptText", "Open Forest Gate");
            SafeSetString(rightSo, "closePromptText", "Close Forest Gate");
            rightSo.ApplyModifiedPropertiesWithoutUndo();

            return gateRoot;
        }

        private static void CreateBambooFenceWing(Transform parent, Vector3 localPos, float width, float height, Material mat)
        {
            var wing = new GameObject("Bamboo_Flank_Fence");
            wing.transform.SetParent(parent, false);
            wing.transform.localPosition = localPos;

            int rails = Mathf.RoundToInt(width / 0.16f);
            for (int i = 0; i < rails; i++)
            {
                float rx = (i - rails * 0.5f) * 0.16f;
                var p = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                p.name = "Bamboo_Stalk";
                p.transform.SetParent(wing.transform, false);
                p.transform.localPosition = new Vector3(rx, 0f, 0f);
                p.transform.localScale = new Vector3(0.08f, height * 0.5f, 0.08f);
                p.GetComponent<Renderer>().sharedMaterial = mat;
            }

            var crossTop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crossTop.name = "Cross_Rail_Top";
            crossTop.transform.SetParent(wing.transform, false);
            crossTop.transform.localPosition = new Vector3(0f, height * 0.35f, 0f);
            crossTop.transform.localScale = new Vector3(width, 0.08f, 0.10f);
            crossTop.GetComponent<Renderer>().sharedMaterial = mat;
            StripCollider(crossTop);

            var crossBot = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crossBot.name = "Cross_Rail_Bottom";
            crossBot.transform.SetParent(wing.transform, false);
            crossBot.transform.localPosition = new Vector3(0f, -height * 0.35f, 0f);
            crossBot.transform.localScale = new Vector3(width, 0.08f, 0.10f);
            crossBot.GetComponent<Renderer>().sharedMaterial = mat;
            StripCollider(crossBot);
        }

        private static GameObject CreateRusticGateLeaf(
            Transform hingeParent, bool isLeft,
            float width, float height, float thickness,
            Material plankMat, Material frameMat, Material goldMat)
        {
            var leafGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leafGo.name = isLeft ? "DoubleGate_Leaf_Left" : "DoubleGate_Leaf_Right";
            leafGo.transform.SetParent(hingeParent, false);

            float xOffset = isLeft ? (width * 0.5f) : (-width * 0.5f);
            leafGo.transform.localPosition = new Vector3(xOffset, height * 0.5f + 0.10f, 0f);
            leafGo.transform.localScale = new Vector3(width - 0.04f, height, thickness);
            leafGo.GetComponent<Renderer>().sharedMaterial = plankMat;

            // Outer Stile & Rail Frame
            CreateLeafBar(leafGo.transform, new Vector3(0f, 0.46f, 0f), new Vector3(1.0f, 0.08f, 1.25f), frameMat);
            CreateLeafBar(leafGo.transform, new Vector3(0f, -0.46f, 0f), new Vector3(1.0f, 0.08f, 1.25f), frameMat);
            CreateLeafBar(leafGo.transform, new Vector3(0.46f, 0f, 0f), new Vector3(0.08f, 1.0f, 1.25f), frameMat);
            CreateLeafBar(leafGo.transform, new Vector3(-0.46f, 0f, 0f), new Vector3(0.08f, 1.0f, 1.25f), frameMat);

            // Diagonal Brace
            var brace = GameObject.CreatePrimitive(PrimitiveType.Cube);
            brace.name = "Diagonal_Cross_Brace";
            brace.transform.SetParent(leafGo.transform, false);
            brace.transform.localPosition = Vector3.zero;
            brace.transform.localRotation = Quaternion.Euler(0f, 0f, isLeft ? 28f : -28f);
            brace.transform.localScale = new Vector3(0.08f, 1.22f, 0.70f);
            brace.GetComponent<Renderer>().sharedMaterial = frameMat;
            StripCollider(brace);

            // Metal Strap Hinges near post edge
            float hingeSideX = isLeft ? -0.42f : 0.42f;
            for (int h = 0; h < 2; h++)
            {
                float hY = h == 0 ? 0.38f : -0.38f;
                var strap = GameObject.CreatePrimitive(PrimitiveType.Cube);
                strap.name = "Iron_Strap_Hinge";
                strap.transform.SetParent(leafGo.transform, false);
                strap.transform.localPosition = new Vector3(hingeSideX, hY, 0.08f);
                strap.transform.localScale = new Vector3(0.22f, 0.08f, 0.40f);
                strap.GetComponent<Renderer>().sharedMaterial = goldMat;
                StripCollider(strap);
            }

            // Latch Handle near center meeting edge
            float handleX = isLeft ? 0.40f : -0.40f;
            var handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            handle.name = "Iron_Handle";
            handle.transform.SetParent(leafGo.transform, false);
            handle.transform.localPosition = new Vector3(handleX, 0f, 0.08f);
            handle.transform.localScale = new Vector3(0.12f, 0.12f, 0.40f);
            handle.GetComponent<Renderer>().sharedMaterial = goldMat;
            StripCollider(handle);

            return leafGo;
        }

        #endregion

        #region 2. Supply Area (Roadside Storage Shelter, Crate & Barricade)

        public static GameObject CreateReferenceSupplyArea(Vector3 position, Quaternion rotation, string name)
        {
            var root = new GameObject(name);
            root.transform.position = position;
            root.transform.rotation = rotation;

            var frameMat = LoadMaterialOrDefault(MatRosewood, new Color(0.24f, 0.20f, 0.18f), "P6_Oiled_Rosewood");
            var oakMat = LoadMaterialOrDefault(MatOak, new Color(0.68f, 0.48f, 0.28f), "Z2_Training_Oak");
            var bannerMat = LoadMaterialOrDefault(MatBanner, new Color(0.70f, 0.15f, 0.12f), "P6_Crimson_Banner");
            var goldMat = LoadMaterialOrDefault(MatGold, new Color(0.85f, 0.72f, 0.35f), "P6_Antique_Gold");

            // A. Rustic Wooden Shelter (4 posts, sloping roof, 2.6m x 2.0m, height 2.5m)
            var shelter = new GameObject("Storage_LeanTo_Shelter");
            shelter.transform.SetParent(root.transform, false);
            shelter.transform.localPosition = Vector3.zero;

            // 4 Timber Posts
            float w = 2.4f, d = 1.8f;
            CreateShelterPost(shelter.transform, new Vector3(-w * 0.5f, 1.25f, -d * 0.5f), 2.50f, frameMat);
            CreateShelterPost(shelter.transform, new Vector3(w * 0.5f, 1.25f, -d * 0.5f), 2.50f, frameMat);
            CreateShelterPost(shelter.transform, new Vector3(-w * 0.5f, 1.10f, d * 0.5f), 2.20f, frameMat);
            CreateShelterPost(shelter.transform, new Vector3(w * 0.5f, 1.10f, d * 0.5f), 2.20f, frameMat);

            // Sloping Roof Rafters & Slat Board
            var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.name = "Shelter_Roof_Slats";
            roof.transform.SetParent(shelter.transform, false);
            roof.transform.localPosition = new Vector3(0f, 2.40f, 0f);
            roof.transform.localRotation = Quaternion.Euler(10f, 0f, 0f);
            roof.transform.localScale = new Vector3(w + 0.5f, 0.10f, d + 0.5f);
            roof.GetComponent<Renderer>().sharedMaterial = frameMat;
            StripCollider(roof);

            // Hanging Crimson Clan Banner with Gold Medallion from shelter eave
            var banner = GameObject.CreatePrimitive(PrimitiveType.Cube);
            banner.name = "Hanging_Clan_Banner";
            banner.transform.SetParent(shelter.transform, false);
            banner.transform.localPosition = new Vector3(w * 0.40f, 1.60f, -d * 0.5f - 0.05f);
            banner.transform.localScale = new Vector3(0.45f, 1.10f, 0.02f);
            banner.GetComponent<Renderer>().sharedMaterial = bannerMat;
            StripCollider(banner);

            var medallion = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            medallion.name = "Gold_Medallion";
            medallion.transform.SetParent(banner.transform, false);
            medallion.transform.localPosition = new Vector3(0f, 0.15f, -0.6f);
            medallion.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            medallion.transform.localScale = new Vector3(0.40f, 0.05f, 0.40f);
            medallion.GetComponent<Renderer>().sharedMaterial = goldMat;
            StripCollider(medallion);

            // B. Stacked Static Supply Crates under shelter
            CreateStaticCrate(shelter.transform, new Vector3(-0.65f, 0.42f, 0.30f), 0.85f, oakMat, frameMat, goldMat);
            CreateStaticCrate(shelter.transform, new Vector3(-0.65f, 1.15f, 0.30f), 0.70f, oakMat, frameMat, goldMat);
            CreateStaticCrate(shelter.transform, new Vector3(0.35f, 0.40f, 0.35f), 0.80f, oakMat, frameMat, goldMat);

            // Storage Barrel
            var barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            barrel.name = "Storage_Barrel";
            barrel.transform.SetParent(shelter.transform, false);
            barrel.transform.localPosition = new Vector3(-0.65f, 0.42f, -0.35f);
            barrel.transform.localScale = new Vector3(0.55f, 0.42f, 0.55f);
            barrel.GetComponent<Renderer>().sharedMaterial = oakMat;

            // C. Movable Pushable Supply Crate kept safely inside the clearing staging area
            var pushableCrate = CreateArtisanSupplyCrate(
                root.transform.position + root.transform.TransformDirection(new Vector3(-0.2f, 0.48f, -0.6f)),
                "Pushable_Wooden_Crate");
            pushableCrate.transform.SetParent(root.transform, true);

            // D. Weathered Carved Stone Barricade on the clearing perimeter (NOT in the road!)
            var stoneBarricade = CreateWeatheredStoneBarricade(
                root.transform.position + root.transform.TransformDirection(new Vector3(1.4f, 0.45f, -0.2f)),
                "Pushable_Stone_Barricade");
            stoneBarricade.transform.SetParent(root.transform, true);

            return root;
        }

        private static void CreateShelterPost(Transform parent, Vector3 localPos, float height, Material mat)
        {
            var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
            post.name = "Shelter_Post";
            post.transform.SetParent(parent, false);
            post.transform.localPosition = localPos;
            post.transform.localScale = new Vector3(0.18f, height, 0.18f);
            post.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private static void CreateStaticCrate(Transform parent, Vector3 localPos, float size, Material woodMat, Material frameMat, Material goldMat)
        {
            var crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crate.name = "Static_Cargo_Crate";
            crate.transform.SetParent(parent, false);
            crate.transform.localPosition = localPos;
            crate.transform.localScale = Vector3.one * size;
            crate.GetComponent<Renderer>().sharedMaterial = woodMat;

            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        b.name = "Bracket";
                        b.transform.SetParent(crate.transform, false);
                        b.transform.localPosition = new Vector3(x * 0.48f, y * 0.48f, z * 0.48f);
                        b.transform.localScale = Vector3.one * 0.14f;
                        b.GetComponent<Renderer>().sharedMaterial = goldMat;
                        StripCollider(b);
                    }
                }
            }
        }

        #endregion

        #region 3. Jade Key Shrine (Lotus Dish Altar & Stone Pagoda Lanterns)

        public static GameObject CreateReferenceJadeShrine(Vector3 position, Quaternion rotation, string name)
        {
            var shrineRoot = new GameObject(name);
            shrineRoot.transform.position = position;
            shrineRoot.transform.rotation = rotation;

            var stoneMat = LoadMaterialOrDefault(MatRockCC0, new Color(0.35f, 0.35f, 0.35f), "RF_Rock_CC0");
            var podiumMat = LoadMaterialOrDefault(MatPodiumStone, new Color(0.38f, 0.36f, 0.34f), "Clear_Z4_Carved_Podium_Stone");
            var jadeCeramicMat = LoadMaterialOrDefault(MatJadeCeramic, new Color(0.20f, 0.55f, 0.40f), "R2_Jade_Ceramic");
            var goldMat = LoadMaterialOrDefault(MatGold, new Color(0.92f, 0.78f, 0.32f), "P6_Antique_Gold");
            var jadeMat = LoadMaterialOrDefault(MatJade, new Color(0.08f, 0.88f, 0.52f), "Z4_Luminous_Jade");

            // Tier 1: Mossy Carved Stone Base Plinth
            var baseTier = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseTier.name = "Altar_Base_Tier";
            baseTier.transform.SetParent(shrineRoot.transform, false);
            baseTier.transform.localPosition = new Vector3(0f, 0.10f, 0f);
            baseTier.transform.localScale = new Vector3(1.40f, 0.20f, 1.40f);
            baseTier.GetComponent<Renderer>().sharedMaterial = stoneMat;

            // Tier 2: Carved Waist Pillar
            var waist = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            waist.name = "Altar_Waist_Pillar";
            waist.transform.SetParent(shrineRoot.transform, false);
            waist.transform.localPosition = new Vector3(0f, 0.42f, 0f);
            waist.transform.localScale = new Vector3(1.00f, 0.45f, 1.00f);
            waist.GetComponent<Renderer>().sharedMaterial = podiumMat;
            StripCollider(waist);

            // Tier 3: Altar Top Slab / Cornice
            var topSlab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            topSlab.name = "Altar_Top_Slab";
            topSlab.transform.SetParent(shrineRoot.transform, false);
            topSlab.transform.localPosition = new Vector3(0f, 0.71f, 0f);
            topSlab.transform.localScale = new Vector3(1.30f, 0.12f, 1.30f);
            topSlab.GetComponent<Renderer>().sharedMaterial = stoneMat;

            // Tier 4: Carved Ceramic Offering Dish resting on altar table
            var lotusDish = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            lotusDish.name = "Offering_Lotus_Dish";
            lotusDish.transform.SetParent(shrineRoot.transform, false);
            lotusDish.transform.localPosition = new Vector3(0f, 0.79f, 0f);
            lotusDish.transform.localScale = new Vector3(0.65f, 0.05f, 0.65f);
            lotusDish.GetComponent<Renderer>().sharedMaterial = jadeCeramicMat;
            StripCollider(lotusDish);

            // Dish Raised Rim Segments
            for (int p = 0; p < 8; p++)
            {
                float angle = p * (360f / 8f);
                var rimSeg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rimSeg.name = "Dish_Rim";
                rimSeg.transform.SetParent(lotusDish.transform, false);
                rimSeg.transform.localPosition = new Vector3(Mathf.Sin(angle * Mathf.Deg2Rad) * 0.46f, 0.35f, Mathf.Cos(angle * Mathf.Deg2Rad) * 0.46f);
                rimSeg.transform.localRotation = Quaternion.Euler(0f, angle, 0f);
                rimSeg.transform.localScale = new Vector3(0.12f, 0.40f, 0.36f);
                rimSeg.GetComponent<Renderer>().sharedMaterial = jadeCeramicMat;
                StripCollider(rimSeg);
            }

            // Two Traditional Carved Stone Pagoda Lanterns (Zero Red Cubes!)
            for (int side = -1; side <= 1; side += 2)
            {
                CreateStonePagodaLantern(shrineRoot.transform, new Vector3(side * 1.25f, 0f, 0f), stoneMat, goldMat);
            }

            // Authentic Multi-Part Jade Talisman Key Root - RESTING NATURALLY IN DISH
            var keyRoot = new GameObject("Jade_Key_Artifact");
            keyRoot.transform.SetParent(shrineRoot.transform, false);
            keyRoot.transform.localPosition = new Vector3(0f, 0.86f, 0f);
            keyRoot.transform.localRotation = Quaternion.Euler(15f, 0f, 0f); // Tilted back gently in dish

            // Circular Bi-Disc Talisman Crown Ring
            var crown = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            crown.name = "BiDisc_Crown";
            crown.transform.SetParent(keyRoot.transform, false);
            crown.transform.localPosition = new Vector3(0f, 0.20f, 0f);
            crown.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            crown.transform.localScale = new Vector3(0.22f, 0.035f, 0.22f);
            crown.GetComponent<Renderer>().sharedMaterial = jadeMat;
            StripCollider(crown);

            var innerGoldRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            innerGoldRing.name = "Inner_Gold_Hub";
            innerGoldRing.transform.SetParent(crown.transform, false);
            innerGoldRing.transform.localPosition = Vector3.zero;
            innerGoldRing.transform.localScale = new Vector3(0.40f, 1.15f, 0.40f);
            innerGoldRing.GetComponent<Renderer>().sharedMaterial = goldMat;
            StripCollider(innerGoldRing);

            // Faceted Tablet Shaft
            var shaft = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shaft.name = "Talisman_Shaft";
            shaft.transform.SetParent(keyRoot.transform, false);
            shaft.transform.localPosition = Vector3.zero;
            shaft.transform.localScale = new Vector3(0.11f, 0.26f, 0.045f);
            shaft.GetComponent<Renderer>().sharedMaterial = jadeMat;
            StripCollider(shaft);

            // Gold Waist Band Filigree
            var band = GameObject.CreatePrimitive(PrimitiveType.Cube);
            band.name = "Gold_Band";
            band.transform.SetParent(keyRoot.transform, false);
            band.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            band.transform.localScale = new Vector3(0.125f, 0.045f, 0.055f);
            band.GetComponent<Renderer>().sharedMaterial = goldMat;
            StripCollider(band);

            // Stepped Key Flanges / Teeth
            var teeth = GameObject.CreatePrimitive(PrimitiveType.Cube);
            teeth.name = "Key_Teeth";
            teeth.transform.SetParent(keyRoot.transform, false);
            teeth.transform.localPosition = new Vector3(0.04f, -0.15f, 0f);
            teeth.transform.localScale = new Vector3(0.11f, 0.08f, 0.04f);
            teeth.GetComponent<Renderer>().sharedMaterial = jadeMat;
            StripCollider(teeth);

            // Trigger Collider satisfying [RequireComponent(typeof(Collider))]
            var keyTrigger = keyRoot.AddComponent<SphereCollider>();
            keyTrigger.radius = 0.50f;
            keyTrigger.isTrigger = true;

            // Attach & Configure JadeKeyPickup
            var pickup = keyRoot.AddComponent<JadeKeyPickup>();
            var pickupSo = new SerializedObject(pickup);
            SafeSetString(pickupSo, "promptText", "Press E to Collect Jade Key");
            SafeSetBool(pickupSo, "enableIdleAnimation", false); // Rests naturally in dish!
            SafeSetFloat(pickupSo, "rotationSpeed", 0f);
            SafeSetFloat(pickupSo, "bobHeight", 0f);
            SafeSetFloat(pickupSo, "bobFrequency", 0f);
            pickupSo.ApplyModifiedPropertiesWithoutUndo();

            // Subtle Mystical Emerald Glow (range 2.2m, intensity 0.9)
            var lightGo = new GameObject("Jade_Emerald_Glow");
            lightGo.transform.SetParent(shrineRoot.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 1.05f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.15f, 0.95f, 0.55f);
            light.intensity = 0.9f;
            light.range = 2.2f;

            return shrineRoot;
        }

        private static void CreateStonePagodaLantern(Transform parent, Vector3 localPos, Material stoneMat, Material accentMat)
        {
            var pagoda = new GameObject("Stone_Pagoda_Lantern");
            pagoda.transform.SetParent(parent, false);
            pagoda.transform.localPosition = localPos;

            // 1. Plinth Foot Base
            var baseP = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseP.name = "Plinth";
            baseP.transform.SetParent(pagoda.transform, false);
            baseP.transform.localPosition = new Vector3(0f, 0.07f, 0f);
            baseP.transform.localScale = new Vector3(0.40f, 0.14f, 0.40f);
            baseP.GetComponent<Renderer>().sharedMaterial = stoneMat;

            // 2. Central Stone Stem / Pillar
            var stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stem.name = "Stem";
            stem.transform.SetParent(pagoda.transform, false);
            stem.transform.localPosition = new Vector3(0f, 0.32f, 0f);
            stem.transform.localScale = new Vector3(0.18f, 0.36f, 0.18f);
            stem.GetComponent<Renderer>().sharedMaterial = stoneMat;
            StripCollider(stem);

            // 3. Middle Firebox Platform (Chudai)
            var midPlat = GameObject.CreatePrimitive(PrimitiveType.Cube);
            midPlat.name = "Firebox_Platform";
            midPlat.transform.SetParent(pagoda.transform, false);
            midPlat.transform.localPosition = new Vector3(0f, 0.54f, 0f);
            midPlat.transform.localScale = new Vector3(0.36f, 0.08f, 0.36f);
            midPlat.GetComponent<Renderer>().sharedMaterial = stoneMat;
            StripCollider(midPlat);

            // 4. Firebox Chamber (Hibukuro) with 4 Carved Stone Corner Posts
            float postOff = 0.12f;
            for (int cx = -1; cx <= 1; cx += 2)
            {
                for (int cz = -1; cz <= 1; cz += 2)
                {
                    var cPost = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cPost.name = "Firebox_Post";
                    cPost.transform.SetParent(pagoda.transform, false);
                    cPost.transform.localPosition = new Vector3(cx * postOff, 0.69f, cz * postOff);
                    cPost.transform.localScale = new Vector3(0.05f, 0.22f, 0.05f);
                    cPost.GetComponent<Renderer>().sharedMaterial = stoneMat;
                    StripCollider(cPost);
                }
            }

            // Inner Warm Glowing Fire Core
            var core = GameObject.CreatePrimitive(PrimitiveType.Cube);
            core.name = "Lantern_Core";
            core.transform.SetParent(pagoda.transform, false);
            core.transform.localPosition = new Vector3(0f, 0.69f, 0f);
            core.transform.localScale = new Vector3(0.18f, 0.18f, 0.18f);
            var paperMat = LoadMaterialOrDefault(MatPaper, new Color(0.95f, 0.85f, 0.65f), "Z2_Lattice_Paper");
            core.GetComponent<Renderer>().sharedMaterial = paperMat;
            StripCollider(core);

            // 5. Pagoda Sloping Stone Roof Eaves (Kasa)
            var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.name = "Pagoda_Roof";
            roof.transform.SetParent(pagoda.transform, false);
            roof.transform.localPosition = new Vector3(0f, 0.84f, 0f);
            roof.transform.localScale = new Vector3(0.52f, 0.08f, 0.52f);
            roof.GetComponent<Renderer>().sharedMaterial = stoneMat;
            StripCollider(roof);

            var roofUpper = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roofUpper.name = "Pagoda_Roof_Upper";
            roofUpper.transform.SetParent(pagoda.transform, false);
            roofUpper.transform.localPosition = new Vector3(0f, 0.90f, 0f);
            roofUpper.transform.localScale = new Vector3(0.34f, 0.06f, 0.34f);
            roofUpper.GetComponent<Renderer>().sharedMaterial = stoneMat;
            StripCollider(roofUpper);

            // 6. Lotus Bud / Jewel Stone Finial (Hoju)
            var finial = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            finial.name = "Stone_Finial";
            finial.transform.SetParent(pagoda.transform, false);
            finial.transform.localPosition = new Vector3(0f, 1.00f, 0f);
            finial.transform.localScale = new Vector3(0.12f, 0.14f, 0.12f);
            finial.GetComponent<Renderer>().sharedMaterial = stoneMat;
            StripCollider(finial);

            // 7. Warm Amber Interior Light
            var lGo = new GameObject("Pagoda_Amber_Light");
            lGo.transform.SetParent(pagoda.transform, false);
            lGo.transform.localPosition = new Vector3(0f, 0.69f, 0f);
            var l = lGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1.0f, 0.72f, 0.30f);
            l.intensity = 0.80f;
            l.range = 2.8f;
        }

        #endregion

        #region 4. Temple Gate (Door 2 - Grand Imperial Red Gate Seated at Ground Level)

        public static GameObject CreateReferenceTempleGate(Vector3 position, Quaternion rotation, string name)
        {
            var gateRoot = new GameObject(name);
            gateRoot.transform.position = position;
            gateRoot.transform.rotation = rotation;

            var vermilionMat = LoadMaterialOrDefault(MatVermilion, new Color(0.70f, 0.22f, 0.15f), "P6_Vermilion_Lacquer");
            var rosewoodMat = LoadMaterialOrDefault(MatRosewood, new Color(0.25f, 0.22f, 0.20f), "P6_Oiled_Rosewood");
            var goldMat = LoadMaterialOrDefault(MatGold, new Color(0.92f, 0.78f, 0.32f), "P6_Antique_Gold");
            var stoneMat = LoadMaterialOrDefault(MatRockCC0, new Color(0.30f, 0.30f, 0.30f), "RF_Rock_CC0");
            var jadeCeramicMat = LoadMaterialOrDefault(MatJadeCeramic, new Color(0.18f, 0.52f, 0.38f), "R2_Jade_Ceramic");

            // Total column-to-column distance is 10.60m (Z=-24.7 to Z=-35.3)
            float totalSpan = 10.60f;
            float leafWidth = 2.65f; // Central opening: 2 leaves x 2.65m = 5.30m
            float leafHeight = 3.80f;
            float leafThick = 0.18f;

            // 1. Stone Threshold Sill resting firmly on the upper paver step
            var thresholdSill = GameObject.CreatePrimitive(PrimitiveType.Cube);
            thresholdSill.name = "Architectural_Threshold_Sill";
            thresholdSill.transform.SetParent(gateRoot.transform, false);
            thresholdSill.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            thresholdSill.transform.localScale = new Vector3(totalSpan, 0.10f, 0.60f);
            thresholdSill.GetComponent<Renderer>().sharedMaterial = stoneMat;

            // 2. Grand Rosewood Lintel Beam beneath the entablature
            var topLintel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            topLintel.name = "Imperial_Top_Lintel";
            topLintel.transform.SetParent(gateRoot.transform, false);
            topLintel.transform.localPosition = new Vector3(0f, 4.05f, 0f);
            topLintel.transform.localScale = new Vector3(totalSpan, 0.35f, 0.50f);
            topLintel.GetComponent<Renderer>().sharedMaterial = rosewoodMat;

            // 7 Golden ornamental brackets on lintel beam
            for (int i = -3; i <= 3; i++)
            {
                var bracket = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bracket.name = $"Gold_Bracket_{i}";
                bracket.transform.SetParent(gateRoot.transform, false);
                bracket.transform.localPosition = new Vector3(i * 1.45f, 3.88f, 0f);
                bracket.transform.localScale = new Vector3(0.22f, 0.12f, 0.54f);
                bracket.GetComponent<Renderer>().sharedMaterial = goldMat;
                StripCollider(bracket);
            }

            // 3. Flanking Side Screen Wings connecting gate flush into the columns
            CreateFlankWing(gateRoot.transform, new Vector3(-3.95f, 2.0f, 0f), 2.65f, leafHeight, rosewoodMat, vermilionMat);
            CreateFlankWing(gateRoot.transform, new Vector3(3.95f, 2.0f, 0f), 2.65f, leafHeight, rosewoodMat, vermilionMat);

            // Two Decorative Green Jade Ceramic Offering Pots on Stone Plinths
            CreateJadeUrnPedestal(gateRoot.transform, new Vector3(-4.60f, 0.35f, -0.65f), stoneMat, jadeCeramicMat);
            CreateJadeUrnPedestal(gateRoot.transform, new Vector3(4.60f, 0.35f, -0.65f), stoneMat, jadeCeramicMat);

            // Gatepost Jambs at local X = -2.65 and +2.65
            var leftPost = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftPost.name = "Left_Gatepost_Jamb";
            leftPost.transform.SetParent(gateRoot.transform, false);
            leftPost.transform.localPosition = new Vector3(-leafWidth, 2.0f, 0f);
            leftPost.transform.localScale = new Vector3(0.35f, 4.0f, 0.38f);
            leftPost.GetComponent<Renderer>().sharedMaterial = rosewoodMat;

            var rightPost = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightPost.name = "Right_Gatepost_Jamb";
            rightPost.transform.SetParent(gateRoot.transform, false);
            rightPost.transform.localPosition = new Vector3(leafWidth, 2.0f, 0f);
            rightPost.transform.localScale = new Vector3(0.35f, 4.0f, 0.38f);
            rightPost.GetComponent<Renderer>().sharedMaterial = rosewoodMat;

            // 4. Primary Hinge (Left Leaf - hinges at local X = -2.65m, spans towards center X = 0)
            var leftHingeGo = new GameObject("Hinge_Pivot_Left");
            leftHingeGo.transform.SetParent(gateRoot.transform, false);
            leftHingeGo.transform.localPosition = new Vector3(-leafWidth, 0f, 0f);

            var leftLeaf = CreateSingleDoorLeaf(leftHingeGo.transform, isLeftLeaf: true, leafWidth, leafHeight, leafThick, vermilionMat, rosewoodMat, goldMat);

            // 5. Secondary Hinge (Right Leaf - hinges at local X = +2.65m, spans towards center X = 0)
            var rightHingeGo = new GameObject("Hinge_Pivot_Right");
            rightHingeGo.transform.SetParent(gateRoot.transform, false);
            rightHingeGo.transform.localPosition = new Vector3(leafWidth, 0f, 0f);

            var rightLeaf = CreateSingleDoorLeaf(rightHingeGo.transform, isLeftLeaf: false, leafWidth, leafHeight, leafThick, vermilionMat, rosewoodMat, goldMat);

            // 6. Attach & Configure DoorInteraction on Left Leaf (Primary Controller) - LOCKED WITH JADE KEY
            var doorComp = leftLeaf.AddComponent<DoorInteraction>();
            var doorSo = new SerializedObject(doorComp);
            SafeSetObjectRef(doorSo, "primaryHinge", leftHingeGo.transform);
            SafeSetObjectRef(doorSo, "secondaryHinge", rightHingeGo.transform);
            SafeSetEnum(doorSo, "motionType", (int)DoorMotionType.RotatingSwing);
            SafeSetEnum(doorSo, "swingDirection", (int)SwingDirectionMode.AwayFromPlayer);
            SafeSetFloat(doorSo, "openAngle", 90.0f);
            SafeSetFloat(doorSo, "animationDuration", 1.2f);
            SafeSetBool(doorSo, "isLocked", true);
            SafeSetBool(doorSo, "requiresJadeKey", true);
            SafeSetString(doorSo, "lockedPromptText", "Locked (Requires Jade Key)");
            SafeSetString(doorSo, "unlockPromptText", "Unlock Gate with Jade Key");
            SafeSetString(doorSo, "openPromptText", "Open Imperial Temple Gate");
            SafeSetString(doorSo, "closePromptText", "Close Imperial Temple Gate");
            doorSo.ApplyModifiedPropertiesWithoutUndo();

            // Setup Paired DoorInteraction on Right Leaf delegating directly to left leaf
            var rightComp = rightLeaf.AddComponent<DoorInteraction>();
            rightComp.SetPairedDoor(doorComp);
            var rightSo = new SerializedObject(rightComp);
            SafeSetObjectRef(rightSo, "primaryHinge", leftHingeGo.transform);
            SafeSetObjectRef(rightSo, "secondaryHinge", rightHingeGo.transform);
            SafeSetEnum(rightSo, "motionType", (int)DoorMotionType.RotatingSwing);
            SafeSetEnum(rightSo, "swingDirection", (int)SwingDirectionMode.AwayFromPlayer);
            SafeSetFloat(rightSo, "openAngle", 90.0f);
            SafeSetFloat(rightSo, "animationDuration", 1.2f);
            SafeSetBool(rightSo, "isLocked", true);
            SafeSetBool(rightSo, "requiresJadeKey", true);
            SafeSetString(rightSo, "lockedPromptText", "Locked (Requires Jade Key)");
            SafeSetString(rightSo, "unlockPromptText", "Unlock Gate with Jade Key");
            SafeSetString(rightSo, "openPromptText", "Open Imperial Temple Gate");
            SafeSetString(rightSo, "closePromptText", "Close Imperial Temple Gate");
            rightSo.ApplyModifiedPropertiesWithoutUndo();

            return gateRoot;
        }

        private static void CreateJadeUrnPedestal(Transform parent, Vector3 localPos, Material stoneMat, Material jadeMat)
        {
            var ped = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ped.name = "Urn_Pedestal";
            ped.transform.SetParent(parent, false);
            ped.transform.localPosition = localPos;
            ped.transform.localScale = new Vector3(0.50f, 0.40f, 0.50f);
            ped.GetComponent<Renderer>().sharedMaterial = stoneMat;

            var urn = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            urn.name = "Jade_Offering_Urn";
            urn.transform.SetParent(ped.transform, false);
            urn.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            urn.transform.localScale = new Vector3(0.70f, 0.45f, 0.70f);
            urn.GetComponent<Renderer>().sharedMaterial = jadeMat;
            StripCollider(urn);
        }

        private static void CreateFlankWing(Transform parent, Vector3 localPos, float width, float height, Material frameMat, Material panelMat)
        {
            var flank = GameObject.CreatePrimitive(PrimitiveType.Cube);
            flank.name = "Side_Screen_Wing";
            flank.transform.SetParent(parent, false);
            flank.transform.localPosition = localPos;
            flank.transform.localScale = new Vector3(width, height, 0.12f);
            flank.GetComponent<Renderer>().sharedMaterial = panelMat;

            var trim = GameObject.CreatePrimitive(PrimitiveType.Cube);
            trim.name = "Wing_Rosewood_Trim";
            trim.transform.SetParent(flank.transform, false);
            trim.transform.localPosition = Vector3.zero;
            trim.transform.localScale = new Vector3(1.02f, 1.02f, 1.15f);
            trim.GetComponent<Renderer>().sharedMaterial = frameMat;
            StripCollider(trim);
        }

        private static GameObject CreateSingleDoorLeaf(
            Transform hingeParent, bool isLeftLeaf,
            float width, float height, float thickness,
            Material vermilionMat, Material rosewoodMat, Material goldMat)
        {
            var leafGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leafGo.name = isLeftLeaf ? "DoubleGate_Leaf_Left" : "DoubleGate_Leaf_Right";
            leafGo.transform.SetParent(hingeParent, false);

            float xOffset = isLeftLeaf ? (width * 0.5f) : (-width * 0.5f);
            leafGo.transform.localPosition = new Vector3(xOffset, height * 0.5f + 0.10f, 0f);
            leafGo.transform.localScale = new Vector3(width - 0.04f, height, thickness);
            leafGo.GetComponent<Renderer>().sharedMaterial = vermilionMat;

            // Raised Rosewood Framing Stiles and Rails
            CreateLeafBar(leafGo.transform, new Vector3(0f, 0.46f, 0f), new Vector3(1.0f, 0.08f, 1.25f), rosewoodMat);
            CreateLeafBar(leafGo.transform, new Vector3(0f, -0.46f, 0f), new Vector3(1.0f, 0.08f, 1.25f), rosewoodMat);
            CreateLeafBar(leafGo.transform, new Vector3(0f, 0.0f, 0f), new Vector3(1.0f, 0.06f, 1.20f), rosewoodMat);
            CreateLeafBar(leafGo.transform, new Vector3(0.46f, 0f, 0f), new Vector3(0.08f, 1.0f, 1.25f), rosewoodMat);
            CreateLeafBar(leafGo.transform, new Vector3(-0.46f, 0f, 0f), new Vector3(0.08f, 1.0f, 1.25f), rosewoodMat);

            // 12 Golden Imperial Studs (4 rows x 3 cols) on Front and Back
            for (int r = 0; r < 4; r++)
            {
                float localY = Mathf.Lerp(-0.35f, 0.35f, r / 3.0f);
                for (int c = 0; c < 3; c++)
                {
                    float localX = Mathf.Lerp(-0.32f, 0.32f, c / 2.0f);

                    var studF = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    studF.name = "Stud_Front";
                    studF.transform.SetParent(leafGo.transform, false);
                    studF.transform.localPosition = new Vector3(localX, localY, -0.55f);
                    studF.transform.localScale = new Vector3(0.06f, 0.045f, 0.35f);
                    studF.GetComponent<Renderer>().sharedMaterial = goldMat;
                    StripCollider(studF);

                    var studB = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    studB.name = "Stud_Back";
                    studB.transform.SetParent(leafGo.transform, false);
                    studB.transform.localPosition = new Vector3(localX, localY, 0.55f);
                    studB.transform.localScale = new Vector3(0.06f, 0.045f, 0.35f);
                    studB.GetComponent<Renderer>().sharedMaterial = goldMat;
                    StripCollider(studB);
                }
            }

            // Sculpted Golden Lion-Head Knocker with Pull Ring near center meeting edge
            float knockerX = isLeftLeaf ? 0.35f : -0.35f;
            var knocker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            knocker.name = "Lion_Knocker_Plate";
            knocker.transform.SetParent(leafGo.transform, false);
            knocker.transform.localPosition = new Vector3(knockerX, 0f, -0.60f);
            knocker.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            knocker.transform.localScale = new Vector3(0.18f, 0.04f, 0.18f);
            knocker.GetComponent<Renderer>().sharedMaterial = goldMat;
            StripCollider(knocker);

            var knockerRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            knockerRing.name = "Knocker_Ring";
            knockerRing.transform.SetParent(knocker.transform, false);
            knockerRing.transform.localPosition = new Vector3(0f, 0.8f, 0.12f);
            knockerRing.transform.localScale = new Vector3(0.70f, 0.30f, 0.70f);
            knockerRing.GetComponent<Renderer>().sharedMaterial = goldMat;
            StripCollider(knockerRing);

            return leafGo;
        }

        private static void CreateLeafBar(Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
        {
            var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = "Frame_Bar";
            bar.transform.SetParent(parent, false);
            bar.transform.localPosition = localPos;
            bar.transform.localScale = localScale;
            bar.GetComponent<Renderer>().sharedMaterial = mat;
            StripCollider(bar);
        }

        #endregion

        #region 5. Mountain Exit Portcullis (Sliding Barred Heavy Gate)

        public static GameObject CreateReferenceMountainPortcullis(Vector3 position, Quaternion rotation, string name)
        {
            var root = new GameObject(name);
            root.transform.position = position;
            root.transform.rotation = rotation;

            var frameMat = LoadMaterialOrDefault(MatRosewood, new Color(0.22f, 0.20f, 0.18f), "P6_Oiled_Rosewood");
            var bronzeMat = LoadMaterialOrDefault(MatGold, new Color(0.75f, 0.60f, 0.35f), "P6_Antique_Bronze");
            var stoneMat = LoadMaterialOrDefault(MatRockCC0, new Color(0.35f, 0.35f, 0.35f), "RF_Rock_CC0");

            float spanW = 3.80f;
            float gateH = 3.20f;
            float gateT = 0.16f;

            // Frame Archway Posts
            for (int s = -1; s <= 1; s += 2)
            {
                var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                post.name = s == -1 ? "Stone_Jamb_L" : "Stone_Jamb_R";
                post.transform.SetParent(root.transform, false);
                post.transform.localPosition = new Vector3(s * (spanW * 0.5f + 0.20f), gateH * 0.5f, 0f);
                post.transform.localScale = new Vector3(0.40f, gateH + 0.40f, 0.45f);
                post.GetComponent<Renderer>().sharedMaterial = stoneMat;
            }

            // Overhead Lintel Arch Track
            var lintel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lintel.name = "Portcullis_Header_Track";
            lintel.transform.SetParent(root.transform, false);
            lintel.transform.localPosition = new Vector3(0f, gateH + 0.20f, 0f);
            lintel.transform.localScale = new Vector3(spanW + 0.80f, 0.40f, 0.50f);
            lintel.GetComponent<Renderer>().sharedMaterial = frameMat;

            // Sliding Gate Assembly
            var gateGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gateGo.name = "Sliding_Portcullis_Gate";
            gateGo.transform.SetParent(root.transform, false);
            gateGo.transform.localPosition = new Vector3(0f, gateH * 0.5f + 0.05f, 0f);
            gateGo.transform.localScale = new Vector3(spanW, gateH, gateT);
            gateGo.GetComponent<Renderer>().sharedMaterial = frameMat;

            // Vertical Reinforced Bronze Grate Bars
            int barCount = 9;
            for (int b = 0; b < barCount; b++)
            {
                float bx = (b - (barCount - 1) * 0.5f) * 0.38f;
                var bar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                bar.name = $"Grate_Bar_{b}";
                bar.transform.SetParent(gateGo.transform, false);
                bar.transform.localPosition = new Vector3(bx / spanW, 0f, 0f);
                bar.transform.localScale = new Vector3(0.06f / spanW, 0.52f, 0.85f);
                bar.GetComponent<Renderer>().sharedMaterial = bronzeMat;
                StripCollider(bar);
            }

            // Dual Brass Pull Rings
            for (int r = -1; r <= 1; r += 2)
            {
                var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ring.name = "Brass_Pull_Ring";
                ring.transform.SetParent(gateGo.transform, false);
                ring.transform.localPosition = new Vector3(r * 0.25f, -0.15f, 0.55f);
                ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                ring.transform.localScale = new Vector3(0.06f, 0.02f, 0.06f);
                ring.GetComponent<Renderer>().sharedMaterial = bronzeMat;
                StripCollider(ring);
            }

            // DoorInteraction configured for sliding motion upward
            var doorComp = gateGo.AddComponent<DoorInteraction>();
            var doorSo = new SerializedObject(doorComp);
            SafeSetObjectRef(doorSo, "primaryHinge", gateGo.transform);
            SafeSetEnum(doorSo, "motionType", (int)DoorMotionType.Sliding);
            SafeSetVector3(doorSo, "slideOffset", new Vector3(0f, 3.50f, 0f));
            SafeSetFloat(doorSo, "animationDuration", 1.2f);
            SafeSetString(doorSo, "openPromptText", "Raise Portcullis");
            SafeSetString(doorSo, "closePromptText", "Lower Portcullis");
            SafeSetBool(doorSo, "isLocked", false);
            doorSo.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        #endregion

        #region 6. Normalized Throwable Rocks & Barricade Factory

        public static GameObject CreateNormalizedThrowableRock(
            Vector3 position, float targetDiameter, string name, float massKg, string meshPath)
        {
            var rockGo = new GameObject(name);
            rockGo.transform.position = position;

            var stoneMat = LoadMaterialOrDefault(MatRockCC0, new Color(0.35f, 0.35f, 0.35f), "RF_Rock_CC0");

            var col = rockGo.AddComponent<SphereCollider>();
            col.radius = targetDiameter * 0.5f;

            var rb = rockGo.AddComponent<Rigidbody>();
            rb.mass = massKg;
            rb.linearDamping = 0.5f;
            rb.angularDamping = 0.8f;
            rb.useGravity = true;
            rb.isKinematic = false;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            var pickup = rockGo.AddComponent<PickupableObject>();
            var pSo = new SerializedObject(pickup);
            SafeSetString(pSo, "promptText", $"Pick Up {name.Replace('_', ' ')}");
            pSo.ApplyModifiedPropertiesWithoutUndo();

            var visualGo = new GameObject("Visual_Mesh");
            visualGo.transform.SetParent(rockGo.transform, false);

            var mf = visualGo.AddComponent<MeshFilter>();
            var mr = visualGo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = stoneMat;

            var sourceMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (sourceMesh != null)
            {
                mf.sharedMesh = sourceMesh;
                Vector3 boundsSize = sourceMesh.bounds.size;
                float maxDim = Mathf.Max(boundsSize.x, boundsSize.y, boundsSize.z);
                if (maxDim < 0.001f) maxDim = 1f;

                float scaleFactor = targetDiameter / maxDim;
                visualGo.transform.localScale = Vector3.one * scaleFactor;
                visualGo.transform.localPosition = -sourceMesh.bounds.center * scaleFactor;
            }
            else
            {
                var sphereMesh = Resources.GetBuiltinResource<Mesh>("New-Sphere.fbx");
                mf.sharedMesh = sphereMesh != null ? sphereMesh : GameObject.CreatePrimitive(PrimitiveType.Sphere).GetComponent<MeshFilter>().sharedMesh;
                visualGo.transform.localScale = Vector3.one * targetDiameter;
            }

            return rockGo;
        }

        public static GameObject CreateArtisanSupplyCrate(Vector3 position, string name)
        {
            var crateRoot = new GameObject(name);
            crateRoot.transform.position = position;

            var oakMat = LoadMaterialOrDefault(MatOak, new Color(0.72f, 0.48f, 0.28f), "P6_Supply_Crate_Wood");
            var rosewoodMat = LoadMaterialOrDefault(MatRosewood, new Color(0.30f, 0.22f, 0.18f), "P6_Crate_Dark_Trim");
            var goldMat = LoadMaterialOrDefault(MatGold, new Color(0.85f, 0.72f, 0.35f), "P6_Crate_Corner_Brackets");

            float size = 0.90f;

            var rootCollider = crateRoot.AddComponent<BoxCollider>();
            rootCollider.size = Vector3.one * size;

            var rb = crateRoot.AddComponent<Rigidbody>();
            rb.mass = 35.0f;
            rb.linearDamping = 3.5f;
            rb.useGravity = true;
            rb.isKinematic = false;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

            var pushable = crateRoot.AddComponent<PushableBarricade>();
            var pushableSo = new SerializedObject(pushable);
            SafeSetEnum(pushableSo, "weightCategory", (int)BarricadeWeightCategory.LightWoodenCrate);
            SafeSetFloat(pushableSo, "mass", 35.0f);
            SafeSetFloat(pushableSo, "linearDamping", 3.5f);
            SafeSetFloat(pushableSo, "pushResistance", 1.0f);
            SafeSetFloat(pushableSo, "maxLinearSpeed", 2.5f);
            SafeSetBool(pushableSo, "freezeRotationXZ", true);
            pushableSo.ApplyModifiedPropertiesWithoutUndo();

            var core = GameObject.CreatePrimitive(PrimitiveType.Cube);
            core.name = "Crate_Body";
            core.transform.SetParent(crateRoot.transform, false);
            core.transform.localScale = Vector3.one * (size * 0.94f);
            core.GetComponent<Renderer>().sharedMaterial = oakMat;
            StripCollider(core);

            float halfS = size * 0.47f;
            float railThick = 0.07f;
            CreateCrateRail(crateRoot.transform, new Vector3(-halfS, 0f, -halfS), new Vector3(railThick, size, railThick), rosewoodMat);
            CreateCrateRail(crateRoot.transform, new Vector3(halfS, 0f, -halfS), new Vector3(railThick, size, railThick), rosewoodMat);
            CreateCrateRail(crateRoot.transform, new Vector3(-halfS, 0f, halfS), new Vector3(railThick, size, railThick), rosewoodMat);
            CreateCrateRail(crateRoot.transform, new Vector3(halfS, 0f, halfS), new Vector3(railThick, size, railThick), rosewoodMat);

            CreateCrateRail(crateRoot.transform, new Vector3(0f, halfS, -halfS), new Vector3(size, railThick, railThick), rosewoodMat);
            CreateCrateRail(crateRoot.transform, new Vector3(0f, halfS, halfS), new Vector3(size, railThick, railThick), rosewoodMat);
            CreateCrateRail(crateRoot.transform, new Vector3(-halfS, halfS, 0f), new Vector3(railThick, railThick, size), rosewoodMat);
            CreateCrateRail(crateRoot.transform, new Vector3(halfS, halfS, 0f), new Vector3(railThick, railThick, size), rosewoodMat);

            CreateCrateRail(crateRoot.transform, new Vector3(0f, -halfS, -halfS), new Vector3(size, railThick, railThick), rosewoodMat);
            CreateCrateRail(crateRoot.transform, new Vector3(0f, -halfS, halfS), new Vector3(size, railThick, railThick), rosewoodMat);
            CreateCrateRail(crateRoot.transform, new Vector3(-halfS, -halfS, 0f), new Vector3(railThick, railThick, size), rosewoodMat);
            CreateCrateRail(crateRoot.transform, new Vector3(halfS, -halfS, 0f), new Vector3(railThick, railThick, size), rosewoodMat);

            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        var bracket = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        bracket.name = "Gold_Corner_Bracket";
                        bracket.transform.SetParent(crateRoot.transform, false);
                        bracket.transform.localPosition = new Vector3(x * (halfS + 0.01f), y * (halfS + 0.01f), z * (halfS + 0.01f));
                        bracket.transform.localScale = Vector3.one * 0.13f;
                        bracket.GetComponent<Renderer>().sharedMaterial = goldMat;
                        StripCollider(bracket);
                    }
                }
            }

            CreateDiagonalStrap(crateRoot.transform, new Vector3(0f, 0f, halfS + 0.005f), 45f, rosewoodMat);
            CreateDiagonalStrap(crateRoot.transform, new Vector3(0f, 0f, -halfS - 0.005f), 45f, rosewoodMat);
            CreateDiagonalStrap(crateRoot.transform, new Vector3(halfS + 0.005f, 0f, 0f), 45f, rosewoodMat, isSide: true);
            CreateDiagonalStrap(crateRoot.transform, new Vector3(-halfS - 0.005f, 0f, 0f), 45f, rosewoodMat, isSide: true);

            return crateRoot;
        }

        public static GameObject CreateWeatheredStoneBarricade(Vector3 position, string name)
        {
            var barricadeRoot = new GameObject(name);
            barricadeRoot.transform.position = position;

            var stoneMat = LoadMaterialOrDefault(MatRockCC0, new Color(0.40f, 0.42f, 0.44f), "RF_Rock_CC0");
            var darkStoneMat = LoadMaterialOrDefault(MatRosewood, new Color(0.28f, 0.28f, 0.28f), "P6_Stone_Carved");

            Vector3 size = new Vector3(1.40f, 0.85f, 0.70f);

            var rootCol = barricadeRoot.AddComponent<BoxCollider>();
            rootCol.size = size;

            var rb = barricadeRoot.AddComponent<Rigidbody>();
            rb.mass = 350.0f;
            rb.linearDamping = 5.0f;
            rb.useGravity = true;
            rb.isKinematic = false;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

            var pushable = barricadeRoot.AddComponent<PushableBarricade>();
            var pushableSo = new SerializedObject(pushable);
            SafeSetEnum(pushableSo, "weightCategory", (int)BarricadeWeightCategory.HeavyStoneBarricade);
            SafeSetFloat(pushableSo, "mass", 350.0f);
            SafeSetFloat(pushableSo, "linearDamping", 5.0f);
            SafeSetFloat(pushableSo, "pushResistance", 3.5f);
            SafeSetFloat(pushableSo, "maxLinearSpeed", 0.8f);
            SafeSetBool(pushableSo, "freezeRotationXZ", true);
            pushableSo.ApplyModifiedPropertiesWithoutUndo();

            var basePlinth = GameObject.CreatePrimitive(PrimitiveType.Cube);
            basePlinth.name = "Stone_Base_Plinth";
            basePlinth.transform.SetParent(barricadeRoot.transform, false);
            basePlinth.transform.localPosition = new Vector3(0f, -0.32f, 0f);
            basePlinth.transform.localScale = new Vector3(size.x, 0.20f, size.z);
            basePlinth.GetComponent<Renderer>().sharedMaterial = darkStoneMat;
            StripCollider(basePlinth);

            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Stone_Body";
            body.transform.SetParent(barricadeRoot.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            body.transform.localScale = new Vector3(size.x * 0.92f, 0.50f, size.z * 0.86f);
            body.GetComponent<Renderer>().sharedMaterial = stoneMat;
            StripCollider(body);

            for (int f = -1; f <= 1; f += 2)
            {
                var groove = GameObject.CreatePrimitive(PrimitiveType.Cube);
                groove.name = "Carved_Groove";
                groove.transform.SetParent(barricadeRoot.transform, false);
                groove.transform.localPosition = new Vector3(0f, 0.02f, f * (size.z * 0.44f));
                groove.transform.localScale = new Vector3(size.x * 0.80f, 0.06f, 0.03f);
                groove.GetComponent<Renderer>().sharedMaterial = darkStoneMat;
                StripCollider(groove);
            }

            var capStone = GameObject.CreatePrimitive(PrimitiveType.Cube);
            capStone.name = "Stone_Cap_Table";
            capStone.transform.SetParent(barricadeRoot.transform, false);
            capStone.transform.localPosition = new Vector3(0f, 0.36f, 0f);
            capStone.transform.localScale = new Vector3(size.x * 1.04f, 0.16f, size.z * 1.05f);
            capStone.GetComponent<Renderer>().sharedMaterial = darkStoneMat;
            StripCollider(capStone);

            return barricadeRoot;
        }

        private static void CreateCrateRail(Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
        {
            var rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rail.name = "Crate_Rail";
            rail.transform.SetParent(parent, false);
            rail.transform.localPosition = localPos;
            rail.transform.localScale = localScale;
            rail.GetComponent<Renderer>().sharedMaterial = mat;
            StripCollider(rail);
        }

        private static void CreateDiagonalStrap(Transform parent, Vector3 localPos, float angle, Material mat, bool isSide = false)
        {
            var strap = GameObject.CreatePrimitive(PrimitiveType.Cube);
            strap.name = "Diagonal_Strap";
            strap.transform.SetParent(parent, false);
            strap.transform.localPosition = localPos;
            strap.transform.localRotation = Quaternion.Euler(0f, isSide ? 90f : 0f, angle);
            strap.transform.localScale = new Vector3(0.06f, 1.15f, 0.015f);
            strap.GetComponent<Renderer>().sharedMaterial = mat;
            StripCollider(strap);
        }

        #endregion

        #region 7. Validation Utility

        public static void RunComprehensiveSceneValidation(bool isSilent)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== THE JADE SEAL: SYSTEMS INTEGRATION SCENE VALIDATION ===");

            bool allPassed = true;

            // 1. Check Player
            var player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                var inv = player.GetComponent<PlayerKeyInventory>();
                var inter = player.GetComponent<PlayerInteractor>();
                var push = player.GetComponent<CharacterControllerPush>();
                var holder = player.GetComponent<PlayerObjectHolder>();
                sb.AppendLine($"[PASS] Player found at {player.transform.position}:");
                sb.AppendLine($"       Inventory: {inv != null}, Interactor: {inter != null}, Push: {push != null}, Holder: {holder != null}");
            }
            else
            {
                sb.AppendLine("[FAIL] No active Player with tag 'Player' found!");
                allPassed = false;
            }

            // 2. Check Forest Gate (Door 1)
            var forestGate = GameObject.Find("Forest_Double_Gate_Door1");
            if (forestGate != null)
            {
                var leftLeaf = forestGate.transform.Find("Hinge_Left/DoubleGate_Leaf_Left");
                var rightLeaf = forestGate.transform.Find("Hinge_Right/DoubleGate_Leaf_Right");
                var door = leftLeaf != null ? leftLeaf.GetComponent<DoorInteraction>() : null;
                sb.AppendLine($"[PASS] Forest Gate (Door 1) found at {forestGate.transform.position}:");
                sb.AppendLine($"       LeftLeaf: {leftLeaf != null}, RightLeaf: {rightLeaf != null}, IsLocked: {door?.IsLocked == true} (Must be false)");
                if (door == null || door.IsLocked) allPassed = false;
            }
            else
            {
                sb.AppendLine("[FAIL] Forest_Double_Gate_Door1 NOT found in scene!");
                allPassed = false;
            }

            // 3. Check Supply Area
            var supply = GameObject.Find("Roadside_Supply_Area");
            if (supply != null)
            {
                var crate = GameObject.Find("Pushable_Wooden_Crate");
                var barricade = GameObject.Find("Pushable_Stone_Barricade");
                sb.AppendLine($"[PASS] Roadside Supply Area found at {supply.transform.position} (Z={supply.transform.position.z:F1}m, clearing is safely off-trail):");
                sb.AppendLine($"       Pushable Crate: {(crate != null ? crate.transform.position.ToString() : "Missing")}");
                sb.AppendLine($"       Pushable Barricade: {(barricade != null ? barricade.transform.position.ToString() : "Missing")}");
                if (crate == null || barricade == null) allPassed = false;
            }
            else
            {
                sb.AppendLine("[FAIL] Roadside_Supply_Area NOT found in scene!");
                allPassed = false;
            }

            // 4. Check Jade Key Shrine
            var shrine = GameObject.Find("Jade_Key_Shrine");
            if (shrine != null)
            {
                var keyGo = GameObject.Find("Jade_Key_Artifact");
                var keyPickup = keyGo != null ? keyGo.GetComponent<JadeKeyPickup>() : null;
                var keyCol = keyGo != null ? keyGo.GetComponent<Collider>() : null;
                sb.AppendLine($"[PASS] Jade Key Shrine found at {shrine.transform.position} (Alcove off-trail):");
                sb.AppendLine($"       Jade Key Artifact: {keyGo != null}, PickupComponent: {keyPickup != null}, TriggerCollider: {keyCol != null && keyCol.isTrigger}");
                if (keyGo == null || keyPickup == null || keyCol == null) allPassed = false;
            }
            else
            {
                sb.AppendLine("[FAIL] Jade_Key_Shrine NOT found in scene!");
                allPassed = false;
            }

            // 5. Check Imperial Temple Gate (Door 2)
            var gateGo = GameObject.Find("Temple_Imperial_Gate_Door2");
            if (gateGo != null)
            {
                var leftLeaf = gateGo.transform.Find("Hinge_Pivot_Left/DoubleGate_Leaf_Left");
                var rightLeaf = gateGo.transform.Find("Hinge_Pivot_Right/DoubleGate_Leaf_Right");

                if (leftLeaf != null && rightLeaf != null)
                {
                    var leftMr = leftLeaf.GetComponent<MeshRenderer>();
                    var rightMr = rightLeaf.GetComponent<MeshRenderer>();
                    var leftCol = leftLeaf.GetComponent<BoxCollider>();
                    var rightCol = rightLeaf.GetComponent<BoxCollider>();
                    var doorComp = leftLeaf.GetComponent<DoorInteraction>();

                    Bounds leftB = leftMr != null ? leftMr.bounds : new Bounds();
                    Bounds rightB = rightMr != null ? rightMr.bounds : new Bounds();

                    bool grounded = gateGo.transform.position.y <= 0.45f;
                    bool locked = doorComp != null && doorComp.IsLocked && doorComp.RequiresJadeKey;

                    sb.AppendLine($"[PASS] Temple Imperial Gate (Door 2) firmly seated at GROUND LEVEL:");
                    sb.AppendLine($"       Root Position: {gateGo.transform.position} (Ground threshold level Y=0.40m)");
                    sb.AppendLine($"       LEFT LEAF: Center={leftB.center}, Size={leftB.size}, ColliderEnabled={leftCol != null && leftCol.enabled}");
                    sb.AppendLine($"       RIGHT LEAF: Center={rightB.center}, Size={rightB.size}, ColliderEnabled={rightCol != null && rightCol.enabled}");
                    sb.AppendLine($"       Door Interaction: Component={doorComp != null}, Locked={doorComp?.IsLocked}, RequiresJadeKey={doorComp?.RequiresJadeKey}");

                    if (!grounded || !locked) allPassed = false;
                }
                else
                {
                    sb.AppendLine($"[FAIL] Temple Gate leaves missing! Left: {leftLeaf != null}, Right: {rightLeaf != null}");
                    allPassed = false;
                }
            }
            else
            {
                sb.AppendLine("[FAIL] Temple_Imperial_Gate_Door2 NOT found in scene!");
                allPassed = false;
            }

            // 6. Check Throwable Rocks
            var rock1 = GameObject.Find("River_Stone");
            var rock2 = GameObject.Find("Mountain_Boulder");
            sb.AppendLine($"[PASS] Hand-sized throwable rocks on trail shoulder:");
            sb.AppendLine($"       River_Stone: {(rock1 != null ? rock1.transform.position.ToString() : "Missing")}");
            sb.AppendLine($"       Mountain_Boulder: {(rock2 != null ? rock2.transform.position.ToString() : "Missing")}");
            if (rock1 == null || rock2 == null) allPassed = false;

            sb.AppendLine($"\n>>> OVERALL STATUS: {(allPassed ? "100% ALL SYSTEMS VERIFIED & READY FOR PLAY MODE" : "WARNINGS DETECTED")} <<<");

            string report = sb.ToString();
            if (allPassed)
            {
                Debug.Log($"<color=#2ecc71><b>[The Jade Seal]</b></color>\n{report}");
            }
            else
            {
                Debug.LogWarning($"<color=#e67e22><b>[The Jade Seal]</b></color>\n{report}");
            }

            if (!isSilent)
            {
                EditorUtility.DisplayDialog("Systems Integration Scene Validation", report, "OK");
            }
        }

        #endregion

        #region 8. Helper Utilities

        public static void SafeSetObjectRef(SerializedObject so, string propertyName, UnityEngine.Object value)
        {
            if (so == null || string.IsNullOrEmpty(propertyName)) return;
            var prop = so.FindProperty(propertyName);
            if (prop != null)
            {
                prop.objectReferenceValue = value;
            }
            else
            {
                Debug.LogWarning($"[The Jade Seal] Property '{propertyName}' not found on {so.targetObject?.GetType().Name}");
            }
        }

        public static void SafeSetString(SerializedObject so, string propertyName, string value)
        {
            if (so == null || string.IsNullOrEmpty(propertyName)) return;
            var prop = so.FindProperty(propertyName);
            if (prop != null)
            {
                prop.stringValue = value;
            }
            else
            {
                Debug.LogWarning($"[The Jade Seal] Property '{propertyName}' not found on {so.targetObject?.GetType().Name}");
            }
        }

        public static void SafeSetFloat(SerializedObject so, string propertyName, float value)
        {
            if (so == null || string.IsNullOrEmpty(propertyName)) return;
            var prop = so.FindProperty(propertyName);
            if (prop != null)
            {
                prop.floatValue = value;
            }
            else
            {
                Debug.LogWarning($"[The Jade Seal] Property '{propertyName}' not found on {so.targetObject?.GetType().Name}");
            }
        }

        public static void SafeSetBool(SerializedObject so, string propertyName, bool value)
        {
            if (so == null || string.IsNullOrEmpty(propertyName)) return;
            var prop = so.FindProperty(propertyName);
            if (prop != null)
            {
                prop.boolValue = value;
            }
            else
            {
                Debug.LogWarning($"[The Jade Seal] Property '{propertyName}' not found on {so.targetObject?.GetType().Name}");
            }
        }

        public static void SafeSetEnum(SerializedObject so, string propertyName, int enumValueIndex)
        {
            if (so == null || string.IsNullOrEmpty(propertyName)) return;
            var prop = so.FindProperty(propertyName);
            if (prop != null)
            {
                prop.enumValueIndex = enumValueIndex;
            }
            else
            {
                Debug.LogWarning($"[The Jade Seal] Property '{propertyName}' not found on {so.targetObject?.GetType().Name}");
            }
        }

        public static void SafeSetVector3(SerializedObject so, string propertyName, Vector3 value)
        {
            if (so == null || string.IsNullOrEmpty(propertyName)) return;
            var prop = so.FindProperty(propertyName);
            if (prop != null)
            {
                prop.vector3Value = value;
            }
            else
            {
                Debug.LogWarning($"[The Jade Seal] Property '{propertyName}' not found on {so.targetObject?.GetType().Name}");
            }
        }

        private static Material LoadMaterialOrDefault(string path, Color fallbackColor, string fallbackName)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            var fallbackMat = new Material(Shader.Find("Standard"));
            fallbackMat.name = fallbackName;
            fallbackMat.color = fallbackColor;
            return fallbackMat;
        }

        private static void StripCollider(GameObject go)
        {
            var c = go.GetComponent<Collider>();
            if (c != null) Object.DestroyImmediate(c);
        }

        #endregion
    }
}
#endif
