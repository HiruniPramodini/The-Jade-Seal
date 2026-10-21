#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using JadeSeal.Interaction;
using JadeSeal.Player;

namespace JadeSeal.Interaction.Editor
{
    /// <summary>
    /// Editor utility to generate the isolated Physics & Interaction Sandbox Scene
    /// with a single click. Fulfills Task 1 & Task 2 testing requirements without touching the
    /// production scene or generating unverified handcrafted YAML.
    /// </summary>
    public static class PhysicsTestSceneSetup
    {
        private const string SceneDirectory = "Assets/JadeSeal/Scenes";
        private const string ScenePath = "Assets/JadeSeal/Scenes/PhysicsInteraction_TestScene.unity";
        private const string InputActionsAssetPath = "Assets/InputSystem_Actions.inputactions";

        [MenuItem("The Jade Seal/Create Physics Interaction Test Scene", false, 10)]
        [MenuItem("Tools/The Jade Seal/Setup Physics Test Scene", false, 10)]
        public static void GenerateScene()
        {
            if (File.Exists(ScenePath))
            {
                bool overwrite = EditorUtility.DisplayDialog(
                    "Recreate Test Scene?",
                    $"A test scene already exists at:\n{ScenePath}\n\nDo you want to recreate it?",
                    "Recreate",
                    "Cancel");

                if (!overwrite) return;
            }

            BuildScene(isBatchMode: false);
        }

        [MenuItem("The Jade Seal/Add Door Props to Current Scene", false, 11)]
        [MenuItem("Tools/The Jade Seal/Add Door Props to Current Scene", false, 11)]
        public static void AddDoorPropsToCurrentScene()
        {
            var root = new GameObject("--- Interactive_Test_Props ---");
            Undo.RegisterCreatedObjectUndo(root, "Add Interactive Props");

            var swingGate = CreateSwingingGate(new Vector3(-3.5f, 0f, 4f), Quaternion.identity, "Courtyard_Swing_Gate", false, "");
            swingGate.transform.SetParent(root.transform, true);

            var slidingGate = CreateSlidingGate(new Vector3(3.5f, 0f, 4f), Quaternion.identity, "Sanctuary_Sliding_Portcullis");
            slidingGate.transform.SetParent(root.transform, true);

            var lockedGate = CreateSwingingGate(new Vector3(0f, 0f, 8.0f), Quaternion.identity, "Locked_Ceremonial_Gate", true, "Locked (Requires Jade Key)");
            lockedGate.transform.SetParent(root.transform, true);

            var keyProp = CreateJadeKeyProp(new Vector3(0f, 0f, 5.2f), Quaternion.identity, "Jade_Key_Pickup");
            keyProp.transform.SetParent(root.transform, true);

            var woodCrate = CreatePushableBarricade(new Vector3(-3.0f, 0.6f, 0.5f), new Vector3(1.2f, 1.2f, 1.2f), "Pushable_Wooden_Crate", BarricadeWeightCategory.LightWoodenCrate);
            woodCrate.transform.SetParent(root.transform, true);

            var stoneBarricade = CreatePushableBarricade(new Vector3(3.0f, 0.6f, 0.5f), new Vector3(1.8f, 1.2f, 1.0f), "Pushable_Stone_Barricade", BarricadeWeightCategory.HeavyStoneBarricade);
            stoneBarricade.transform.SetParent(root.transform, true);

            var rock1 = CreateThrowableRock(new Vector3(-1.4f, 0.25f, 1.8f), 0.2f, "River_Stone", new Color(0.45f, 0.48f, 0.50f));
            rock1.transform.SetParent(root.transform, true);

            var rock2 = CreateThrowableRock(new Vector3(1.4f, 0.3f, 1.8f), 0.28f, "Mountain_Boulder", new Color(0.35f, 0.32f, 0.30f));
            rock2.transform.SetParent(root.transform, true);

            // Ensure player in scene has key inventory, CharacterControllerPush, and PlayerObjectHolder
            var player = GameObject.Find("Sandbox_Player");
            if (player != null)
            {
                if (player.GetComponent<PlayerKeyInventory>() == null)
                    player.AddComponent<PlayerKeyInventory>();
                if (player.GetComponent<CharacterControllerPush>() == null)
                    player.AddComponent<CharacterControllerPush>();
                if (player.GetComponent<PlayerObjectHolder>() == null)
                    player.AddComponent<PlayerObjectHolder>();
            }

            Selection.activeGameObject = root;
            Debug.Log("<color=#2ecc71><b>[The Jade Seal]</b></color> Successfully added interactive Gates, Barricades, and Jade Key props to current scene!");
        }

        /// <summary>
        /// Batch-mode executable entry point that bypasses modal dialogs.
        /// </summary>
        public static void GenerateSceneBatch()
        {
            BuildScene(isBatchMode: true);
        }

        private static void BuildScene(bool isBatchMode)
        {
            // Ensure destination directory exists
            if (!Directory.Exists(SceneDirectory))
            {
                Directory.CreateDirectory(SceneDirectory);
                AssetDatabase.Refresh();
            }

            // Create fresh empty scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. Directional Light
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.88f);
            light.intensity = 1.15f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // 2. Ground Plane (50m x 50m)
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground_Plane_50x50";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(5f, 1f, 5f);
            var groundRenderer = ground.GetComponent<Renderer>();
            if (groundRenderer != null)
            {
                var groundMat = new Material(Shader.Find("Standard"))
                {
                    name = "TestGround_Material",
                    color = new Color(0.25f, 0.28f, 0.30f)
                };
                groundRenderer.sharedMaterial = groundMat;
            }

            // 3. Task 1 Test Interactables: Ancient Jade Relic & Chest
            var pedestal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pedestal.name = "Pedestal_Table";
            pedestal.transform.position = new Vector3(0f, 0.45f, 3.2f);
            pedestal.transform.localScale = new Vector3(1.2f, 0.9f, 1.2f);
            var pedRenderer = pedestal.GetComponent<Renderer>();
            if (pedRenderer != null)
            {
                var pedMat = new Material(Shader.Find("Standard"))
                {
                    name = "Pedestal_Material",
                    color = new Color(0.35f, 0.32f, 0.28f)
                };
                pedRenderer.sharedMaterial = pedMat;
            }

            var relic = GameObject.CreatePrimitive(PrimitiveType.Cube);
            relic.name = "Test_Interactable_Relic";
            relic.transform.position = new Vector3(0f, 1.2f, 3.2f);
            relic.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            relic.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
            var relicRenderer = relic.GetComponent<Renderer>();
            if (relicRenderer != null)
            {
                var relicMat = new Material(Shader.Find("Standard"))
                {
                    name = "Relic_Material",
                    color = new Color(0.18f, 0.80f, 0.44f) // Initial Jade Green
                };
                relicRenderer.sharedMaterial = relicMat;
            }
            relic.AddComponent<SimpleTestInteractable>();

            var chest = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chest.name = "Test_Interactable_Chest";
            chest.transform.position = new Vector3(2.5f, 0.5f, 2.5f);
            chest.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
            var chestRenderer = chest.GetComponent<Renderer>();
            if (chestRenderer != null)
            {
                var chestMat = new Material(Shader.Find("Standard"))
                {
                    name = "Chest_Material",
                    color = new Color(0.95f, 0.77f, 0.06f) // Initial Gold
                };
                chestRenderer.sharedMaterial = chestMat;
            }
            chest.AddComponent<SimpleTestInteractable>();

            // 4. Task 2, Task 3, & Task 5 Test Props: Courtyard Swing Gate, Sliding Portcullis, Locked Gate, Jade Key, and Pushable Barricades
            var propsGroup = new GameObject("--- Interactive_Test_Props ---");

            var swingGate = CreateSwingingGate(new Vector3(-3.5f, 0f, 4f), Quaternion.identity, "Courtyard_Swing_Gate", false, "");
            swingGate.transform.SetParent(propsGroup.transform, true);

            var slidingGate = CreateSlidingGate(new Vector3(3.5f, 0f, 4f), Quaternion.identity, "Sanctuary_Sliding_Portcullis");
            slidingGate.transform.SetParent(propsGroup.transform, true);

            var lockedGate = CreateSwingingGate(new Vector3(0f, 0f, 8.0f), Quaternion.identity, "Locked_Ceremonial_Gate", true, "Locked (Requires Jade Key)");
            lockedGate.transform.SetParent(propsGroup.transform, true);

            var jadeKey = CreateJadeKeyProp(new Vector3(0f, 0f, 5.2f), Quaternion.identity, "Jade_Key_Pickup");
            jadeKey.transform.SetParent(propsGroup.transform, true);

            // Task 3: Lightweight wooden crate and heavy stone barricade placed on open ground
            var woodCrate = CreatePushableBarricade(new Vector3(-3.0f, 0.6f, 0.5f), new Vector3(1.2f, 1.2f, 1.2f), "Pushable_Wooden_Crate", BarricadeWeightCategory.LightWoodenCrate);
            woodCrate.transform.SetParent(propsGroup.transform, true);

            var stoneBarricade = CreatePushableBarricade(new Vector3(3.0f, 0.6f, 0.5f), new Vector3(1.8f, 1.2f, 1.0f), "Pushable_Stone_Barricade", BarricadeWeightCategory.HeavyStoneBarricade);
            stoneBarricade.transform.SetParent(propsGroup.transform, true);

            // Task 4: Throwable pickup objects (River Stone and Mountain Boulder)
            var rock1 = CreateThrowableRock(new Vector3(-1.4f, 0.25f, 1.8f), 0.2f, "River_Stone", new Color(0.45f, 0.48f, 0.50f));
            rock1.transform.SetParent(propsGroup.transform, true);

            var rock2 = CreateThrowableRock(new Vector3(1.4f, 0.3f, 1.8f), 0.28f, "Mountain_Boulder", new Color(0.35f, 0.32f, 0.30f));
            rock2.transform.SetParent(propsGroup.transform, true);

            // 5. Load Shared Input Actions Asset
            var inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsAssetPath);
            if (inputActions == null)
            {
                Debug.LogWarning($"[PhysicsTestSceneSetup] Could not load InputActionAsset at {InputActionsAssetPath}. Falling back to direct device input.");
            }

            // 6. Player Setup
            var playerGo = new GameObject("Sandbox_Player");
            playerGo.transform.position = new Vector3(0f, 1.0f, 0f);

            var cc = playerGo.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.stepOffset = 0.3f;
            cc.slopeLimit = 45f;

            // Attach PlayerKeyInventory (stores key progression)
            playerGo.AddComponent<PlayerKeyInventory>();

            // Attach CharacterControllerPush (transfers force to PushableBarricade objects)
            playerGo.AddComponent<CharacterControllerPush>();

            // Player visual capsule proxy (collider removed so it does not interfere with CharacterController)
            var visualCapsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visualCapsule.name = "Visual_Capsule_Proxy";
            visualCapsule.transform.SetParent(playerGo.transform, false);
            visualCapsule.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            visualCapsule.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
            var proxyCollider = visualCapsule.GetComponent<Collider>();
            if (proxyCollider != null) Object.DestroyImmediate(proxyCollider);

            // Child First-Person Camera
            var camGo = new GameObject("Player_FirstPersonCamera");
            camGo.transform.SetParent(playerGo.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            var cam = camGo.AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.fieldOfView = 68f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            camGo.AddComponent<AudioListener>();

            // Hold Anchor for Pickup / Throw (Task 4)
            var holdAnchor = new GameObject("Hold_Anchor");
            holdAnchor.transform.SetParent(camGo.transform, false);
            holdAnchor.transform.localPosition = new Vector3(0.35f, -0.25f, 1.15f);

            // Wire BasicPhysicsPlayerController
            var controller = playerGo.AddComponent<BasicPhysicsPlayerController>();
            var controllerSo = new SerializedObject(controller);
            controllerSo.FindProperty("playerCamera").objectReferenceValue = cam;
            if (inputActions != null)
            {
                controllerSo.FindProperty("inputActionsAsset").objectReferenceValue = inputActions;
            }
            controllerSo.ApplyModifiedPropertiesWithoutUndo();

            // Wire PlayerInteractor
            var interactor = playerGo.AddComponent<PlayerInteractor>();
            var interactorSo = new SerializedObject(interactor);
            interactorSo.FindProperty("playerCamera").objectReferenceValue = cam;
            if (inputActions != null)
            {
                interactorSo.FindProperty("inputActionsAsset").objectReferenceValue = inputActions;
            }
            interactorSo.ApplyModifiedPropertiesWithoutUndo();

            // Wire PlayerObjectHolder (Task 4)
            var holder = playerGo.AddComponent<PlayerObjectHolder>();
            var holderSo = new SerializedObject(holder);
            holderSo.FindProperty("playerCamera").objectReferenceValue = cam;
            holderSo.FindProperty("holdAnchor").objectReferenceValue = holdAnchor.transform;
            holderSo.ApplyModifiedPropertiesWithoutUndo();

            // 7. Save Scene
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Debug.Log($"<color=#2ecc71><b>[The Jade Seal]</b></color> Successfully created test scene at:\n<b>{ScenePath}</b>\n" +
                      $"Press <b>Play</b> to test WASD movement, mouse look, reticle targeting, [E] relic interaction, swinging gates, sliding portcullises, and rock throwing!");

            if (!isBatchMode)
            {
                EditorUtility.DisplayDialog(
                    "Test Scene Ready!",
                    $"Physics & Interaction Test Scene created successfully!\n\nLocation: {ScenePath}\n\n" +
                    "1. Press 'Play' in Unity.\n" +
                    "2. Interact with Ancient Jade Relic & Chest ([E]).\n" +
                    "3. Test Courtyard Swing Gate & Sliding Portcullis ([E]).\n" +
                    "4. Pushable Wooden Crate & Stone Barricade: Walk into them to push.\n" +
                    "5. Pickup & Throw Objects: Approach River Stone or Mountain Boulder -> press [E] to Pick Up -> press [G] to Throw (or [Q] to Drop)!\n" +
                    "6. Approach Locked Ceremonial Gate -> verify 'Locked (Requires Jade Key)' prompt.\n" +
                    "7. Collect Jade Key on the altar ([E]) -> return to Locked Gate -> press [E] to unlock & pass through!",
                    "OK");
            }
        }

        /// <summary>
        /// Builds an authentic hinged courtyard gate with decorative stone posts, lintel, and movable wooden leaf.
        /// </summary>
        public static GameObject CreateSwingingGate(Vector3 position, Quaternion rotation, string name, bool isLocked, string lockedPrompt)
        {
            var gateRoot = new GameObject(name);
            gateRoot.transform.position = position;
            gateRoot.transform.rotation = rotation;

            var frameMat = new Material(Shader.Find("Standard"))
            {
                name = "Gate_Frame_Material",
                color = new Color(0.28f, 0.25f, 0.22f)
            };

            var woodMat = new Material(Shader.Find("Standard"))
            {
                name = isLocked ? "Gate_Vermilion_Locked_Material" : "Gate_Vermilion_Material",
                color = isLocked ? new Color(0.55f, 0.12f, 0.10f) : new Color(0.70f, 0.22f, 0.15f)
            };

            // Left Post
            var leftPost = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftPost.name = "Frame_Left_Post";
            leftPost.transform.SetParent(gateRoot.transform, false);
            leftPost.transform.localPosition = new Vector3(-0.95f, 1.3f, 0f);
            leftPost.transform.localScale = new Vector3(0.3f, 2.6f, 0.3f);
            leftPost.GetComponent<Renderer>().sharedMaterial = frameMat;

            // Right Post
            var rightPost = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightPost.name = "Frame_Right_Post";
            rightPost.transform.SetParent(gateRoot.transform, false);
            rightPost.transform.localPosition = new Vector3(0.95f, 1.3f, 0f);
            rightPost.transform.localScale = new Vector3(0.3f, 2.6f, 0.3f);
            rightPost.GetComponent<Renderer>().sharedMaterial = frameMat;

            // Top Lintel
            var lintel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lintel.name = "Frame_Top_Lintel";
            lintel.transform.SetParent(gateRoot.transform, false);
            lintel.transform.localPosition = new Vector3(0f, 2.75f, 0f);
            lintel.transform.localScale = new Vector3(2.2f, 0.3f, 0.35f);
            lintel.GetComponent<Renderer>().sharedMaterial = frameMat;

            // Hinge Pivot at left post edge
            var hingeGo = new GameObject("Hinge_Pivot");
            hingeGo.transform.SetParent(gateRoot.transform, false);
            hingeGo.transform.localPosition = new Vector3(-0.8f, 0f, 0f);

            // Door Leaf child of hinge pivot (offset so left edge aligns with hinge pivot)
            var doorLeaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
            doorLeaf.name = "Door_Leaf";
            doorLeaf.transform.SetParent(hingeGo.transform, false);
            doorLeaf.transform.localPosition = new Vector3(0.8f, 1.25f, 0f);
            doorLeaf.transform.localScale = new Vector3(1.6f, 2.4f, 0.12f);
            doorLeaf.GetComponent<Renderer>().sharedMaterial = woodMat;

            // Attach & Configure DoorInteraction component
            var doorComp = doorLeaf.AddComponent<DoorInteraction>();
            var doorSo = new SerializedObject(doorComp);
            doorSo.FindProperty("primaryHinge").objectReferenceValue = hingeGo.transform;
            doorSo.FindProperty("motionType").enumValueIndex = (int)DoorMotionType.RotatingSwing;
            doorSo.FindProperty("swingDirection").enumValueIndex = (int)SwingDirectionMode.AwayFromPlayer;
            doorSo.FindProperty("openAngle").floatValue = 90.0f;
            doorSo.FindProperty("animationDuration").floatValue = 1.0f;
            doorSo.FindProperty("isLocked").boolValue = isLocked;
            doorSo.FindProperty("requiresJadeKey").boolValue = isLocked;
            if (!string.IsNullOrEmpty(lockedPrompt))
            {
                doorSo.FindProperty("lockedPromptText").stringValue = lockedPrompt;
            }
            doorSo.FindProperty("unlockPromptText").stringValue = "Unlock Gate with Jade Key";
            doorSo.FindProperty("openPromptText").stringValue = "Open Gate";
            doorSo.FindProperty("closePromptText").stringValue = "Close Gate";
            doorSo.ApplyModifiedPropertiesWithoutUndo();

            return gateRoot;
        }

        /// <summary>
        /// Builds an ancient Jade Key altar with floating, oscillating collectible key.
        /// </summary>
        public static GameObject CreateJadeKeyProp(Vector3 position, Quaternion rotation, string name)
        {
            var keyRoot = new GameObject(name);
            keyRoot.transform.position = position;
            keyRoot.transform.rotation = rotation;

            var stoneMat = new Material(Shader.Find("Standard"))
            {
                name = "Key_Altar_Stone_Material",
                color = new Color(0.30f, 0.28f, 0.26f)
            };

            var jadeMat = new Material(Shader.Find("Standard"))
            {
                name = "Jade_Key_Material",
                color = new Color(0.08f, 0.85f, 0.50f) // Luminous Jade Green
            };

            // Altar pedestal stand
            var altar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            altar.name = "Key_Altar_Pedestal";
            altar.transform.SetParent(keyRoot.transform, false);
            altar.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            altar.transform.localScale = new Vector3(0.6f, 0.9f, 0.6f);
            altar.GetComponent<Renderer>().sharedMaterial = stoneMat;

            // Floating Jade Key Crystal
            var keyCrystal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            keyCrystal.name = "Jade_Key_Crystal";
            keyCrystal.transform.SetParent(keyRoot.transform, false);
            keyCrystal.transform.localPosition = new Vector3(0f, 1.15f, 0f);
            keyCrystal.transform.localScale = new Vector3(0.25f, 0.25f, 0.35f);
            keyCrystal.transform.localRotation = Quaternion.Euler(45f, 45f, 0f);
            keyCrystal.GetComponent<Renderer>().sharedMaterial = jadeMat;

            // Attach & Configure JadeKeyPickup
            var pickup = keyCrystal.AddComponent<JadeKeyPickup>();
            var pickupSo = new SerializedObject(pickup);
            pickupSo.FindProperty("promptText").stringValue = "Pick Up Jade Key";
            pickupSo.FindProperty("enableIdleAnimation").boolValue = true;
            pickupSo.FindProperty("rotationSpeed").floatValue = 50.0f;
            pickupSo.FindProperty("bobHeight").floatValue = 0.06f;
            pickupSo.FindProperty("bobFrequency").floatValue = 2.0f;
            pickupSo.ApplyModifiedPropertiesWithoutUndo();

            return keyRoot;
        }

        /// <summary>
        /// Builds an authentic sliding metal/timber portcullis gate that smoothly lifts upward.
        /// </summary>
        public static GameObject CreateSlidingGate(Vector3 position, Quaternion rotation, string name)
        {
            var gateRoot = new GameObject(name);
            gateRoot.transform.position = position;
            gateRoot.transform.rotation = rotation;

            var stoneMat = new Material(Shader.Find("Standard"))
            {
                name = "Portcullis_Stone_Material",
                color = new Color(0.32f, 0.34f, 0.36f)
            };

            var metalMat = new Material(Shader.Find("Standard"))
            {
                name = "Portcullis_Grille_Material",
                color = new Color(0.18f, 0.20f, 0.22f)
            };

            // Left Column
            var leftCol = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftCol.name = "Portcullis_Left_Column";
            leftCol.transform.SetParent(gateRoot.transform, false);
            leftCol.transform.localPosition = new Vector3(-1.1f, 1.8f, 0f);
            leftCol.transform.localScale = new Vector3(0.35f, 3.6f, 0.35f);
            leftCol.GetComponent<Renderer>().sharedMaterial = stoneMat;

            // Right Column
            var rightCol = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightCol.name = "Portcullis_Right_Column";
            rightCol.transform.SetParent(gateRoot.transform, false);
            rightCol.transform.localPosition = new Vector3(1.1f, 1.8f, 0f);
            rightCol.transform.localScale = new Vector3(0.35f, 3.6f, 0.35f);
            rightCol.GetComponent<Renderer>().sharedMaterial = stoneMat;

            // Top Arch Housing
            var topArch = GameObject.CreatePrimitive(PrimitiveType.Cube);
            topArch.name = "Portcullis_Top_Housing";
            topArch.transform.SetParent(gateRoot.transform, false);
            topArch.transform.localPosition = new Vector3(0f, 3.8f, 0f);
            topArch.transform.localScale = new Vector3(2.6f, 0.5f, 0.45f);
            topArch.GetComponent<Renderer>().sharedMaterial = stoneMat;

            // Sliding Grille Leaf
            var slidingLeaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slidingLeaf.name = "Portcullis_Sliding_Grille";
            slidingLeaf.transform.SetParent(gateRoot.transform, false);
            slidingLeaf.transform.localPosition = new Vector3(0f, 1.35f, 0f);
            slidingLeaf.transform.localScale = new Vector3(1.85f, 2.7f, 0.12f);
            slidingLeaf.GetComponent<Renderer>().sharedMaterial = metalMat;

            // Attach & Configure DoorInteraction component
            var doorComp = slidingLeaf.AddComponent<DoorInteraction>();
            var doorSo = new SerializedObject(doorComp);
            doorSo.FindProperty("primaryHinge").objectReferenceValue = slidingLeaf.transform;
            doorSo.FindProperty("motionType").enumValueIndex = (int)DoorMotionType.Sliding;
            doorSo.FindProperty("slideOffset").vector3Value = new Vector3(0f, 2.7f, 0f);
            doorSo.FindProperty("animationDuration").floatValue = 1.2f;
            doorSo.FindProperty("openPromptText").stringValue = "Raise Portcullis";
            doorSo.FindProperty("closePromptText").stringValue = "Lower Portcullis";
            doorSo.ApplyModifiedPropertiesWithoutUndo();

            return gateRoot;
        }

        /// <summary>
        /// Builds a physical pushable crate or stone block equipped with a non-kinematic Rigidbody and PushableBarricade.
        /// </summary>
        public static GameObject CreatePushableBarricade(Vector3 position, Vector3 size, string name, BarricadeWeightCategory category)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = position;
            go.transform.localScale = size;

            bool isWood = category == BarricadeWeightCategory.LightWoodenCrate;
            var mat = new Material(Shader.Find("Standard"))
            {
                name = isWood ? "Pushable_Wood_Material" : "Pushable_Stone_Material",
                color = isWood ? new Color(0.72f, 0.48f, 0.28f) : new Color(0.38f, 0.40f, 0.42f)
            };
            go.GetComponent<Renderer>().sharedMaterial = mat;

            // Non-kinematic Rigidbody with gravity and continuous collision
            var rb = go.AddComponent<Rigidbody>();
            rb.useGravity = true;
            rb.isKinematic = false;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            float setMass = isWood ? 35.0f : 350.0f;
            float setDamping = isWood ? 3.5f : 5.0f;
            float setResistance = isWood ? 1.0f : 3.5f;
            float setMaxSpeed = isWood ? 2.5f : 0.8f;

            rb.mass = setMass;
            rb.linearDamping = setDamping;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

            // PushableBarricade component
            var pushable = go.AddComponent<PushableBarricade>();
            var pushableSo = new SerializedObject(pushable);
            pushableSo.FindProperty("weightCategory").enumValueIndex = (int)category;
            pushableSo.FindProperty("mass").floatValue = setMass;
            pushableSo.FindProperty("linearDamping").floatValue = setDamping;
            pushableSo.FindProperty("pushResistance").floatValue = setResistance;
            pushableSo.FindProperty("maxLinearSpeed").floatValue = setMaxSpeed;
            pushableSo.FindProperty("freezeRotationXZ").boolValue = true;
            pushableSo.ApplyModifiedPropertiesWithoutUndo();

            return go;
        }

        /// <summary>
        /// Builds a physical pickupable rock with a sphere collider, dynamic Rigidbody, and PickupableObject component.
        /// </summary>
        public static GameObject CreateThrowableRock(Vector3 position, float radius, string name, Color rockColor)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.position = position;
            go.transform.localScale = Vector3.one * (radius * 2.0f);

            var mat = new Material(Shader.Find("Standard"))
            {
                name = $"{name}_Material",
                color = rockColor
            };
            go.GetComponent<Renderer>().sharedMaterial = mat;

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = radius > 0.25f ? 4.5f : 1.5f;
            rb.linearDamping = 0.5f;
            rb.angularDamping = 0.5f;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            var pickup = go.AddComponent<PickupableObject>();
            var pickupSo = new SerializedObject(pickup);
            pickupSo.FindProperty("promptText").stringValue = $"Pick Up {name.Replace('_', ' ')}";
            pickupSo.ApplyModifiedPropertiesWithoutUndo();

            return go;
        }
    }
}
#endif
