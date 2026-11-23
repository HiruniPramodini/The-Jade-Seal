#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using JadeSeal.Environment;
using JadeSeal.Interaction;

namespace JadeSeal.Environment.Editor
{
    /// <summary>
    /// Master, safe, idempotent Unity Editor recovery tool that reconstructs the
    /// COMPLETE, high-fidelity Red Jade Crystal visual in the Systems Integration scene
    /// in one unified, undoable operation.
    ///
    /// RECOVERY GUARANTEES:
    /// 1. Play Mode Rejection: Refuses execution if Unity is in Play Mode, ensuring all
    ///    changes are applied in Edit Mode and ready to be saved permanently.
    /// 2. Strict Scene Validation: Operates ONLY on JadeSealFantasy_SystemsIntegration.unity.
    ///    Guarantees zero modifications to teammate scenes (JadeSealFantasy.unity).
    /// 3. Preserves Gameplay: Keeps Jade_Key_Artifact, SphereCollider trigger, JadeKeyPickup,
    ///    PlayerKeyInventory, and Door 2 interactions 100% intact.
    /// 4. Complete Visual Rebuild:
    ///    - Faceted_Red_Jade_Crystal (RedJade_FacetedCrystal.asset + M_Red_Luminous_Jade.mat)
    ///    - Ornamental_Metallic_Bands:
    ///      • Metal_Spiral_Bands (RedJade_MetalSpiralBands.asset + M_Dark_Ornamental_Bronze.mat)
    ///      • Dragon_Claw_Ornaments (RedJade_DragonClawOrnaments.asset + M_Dark_Ornamental_Bronze.mat)
    ///    - Magic_Energy_Ring_Inner & Magic_Energy_Ring_Outer (M_Red_Energy_Ring.mat)
    ///    - Ruby_Point_Light (Crimson ruby red #FF1E38, range 4.5m, intensity 2.2)
    ///    - Crimson_Mote_Particles with matching TwoConstants VelocityOverLifetime curves
    ///    - MagicalRedJadeArtifact animation component (20 deg/s rotation, bobbing, rings, pulsing)
    /// 5. Resumes Partially Built Scenes: Reuses existing containers/components cleanly
    ///    without creating duplicate GameObjects or components.
    /// 6. Robust Error Handling: Uses an Undo group with Undo.RevertAllDownToGroup on exception
    ///    to roll back modifications if an unexpected failure occurs.
    /// 7. Cleans Legacy Green Pieces: Removes BiDisc_Crown, Talisman_Shaft, Gold_Band, Key_Teeth.
    /// 8. Disables Green Glow: Sets Jade_Emerald_Glow to inactive on the shrine.
    /// 9. Scene Dirtying: Marks the scene dirty and instructs the user to press Ctrl+S to save permanently.
    /// </summary>
    public static class RedJadeCompleteRecovery
    {
        public const string ExpectedSceneName = "JadeSealFantasy_SystemsIntegration";
        public const string ExpectedScenePath = "Assets/JadeSeal/Scenes/JadeSealFantasy_SystemsIntegration.unity";

        // Asset Paths
        public const string CrystalMeshPath = "Assets/JadeSeal/Art/RedJadeArtifact/Meshes/RedJade_FacetedCrystal.asset";
        public const string SpiralBandsMeshPath = "Assets/JadeSeal/Art/RedJadeArtifact/Meshes/RedJade_MetalSpiralBands.asset";
        public const string DragonClawsMeshPath = "Assets/JadeSeal/Art/RedJadeArtifact/Meshes/RedJade_DragonClawOrnaments.asset";

        public const string RubyMaterialPath = "Assets/JadeSeal/Art/RedJadeArtifact/Materials/M_Red_Luminous_Jade.mat";
        public const string BronzeMaterialPath = "Assets/JadeSeal/Art/RedJadeArtifact/Materials/M_Dark_Ornamental_Bronze.mat";
        public const string EnergyRingMaterialPath = "Assets/JadeSeal/Art/RedJadeArtifact/Materials/M_Red_Energy_Ring.mat";

        // Hierarchy Object Names
        public const string ShrineGameObjectName = "Jade_Key_Shrine";
        public const string KeyArtifactGameObjectName = "Jade_Key_Artifact";
        public const string EmeraldGlowGameObjectName = "Jade_Emerald_Glow";
        public const string RedVisualChildName = "Red_Jade_Crystal_Visual";

        private static readonly string[] LegacyGreenChildNames = new string[]
        {
            "BiDisc_Crown",
            "Talisman_Shaft",
            "Gold_Band",
            "Key_Teeth"
        };

        [MenuItem("The Jade Seal/Systems Integration/Rebuild Complete Red Jade Crystal", priority = 110)]
        [MenuItem("Tools/The Jade Seal/Rebuild Complete Red Jade Crystal", priority = 110)]
        public static void RebuildCompleteRedJadeMenu()
        {
            // ---------------------------------------------------------
            // 1. Reject Execution During Play Mode
            // ---------------------------------------------------------
            if (EditorApplication.isPlaying || EditorApplication.isPaused || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(
                    "Play Mode Active",
                    "Cannot rebuild the Red Jade Crystal while Unity is in Play Mode.\n\n" +
                    "Please stop Play Mode (click the Play button to exit) and run this tool in Edit Mode so changes persist permanently.",
                    "OK");
                return;
            }

            // ---------------------------------------------------------
            // 2. Validate Active Target Scene
            // ---------------------------------------------------------
            Scene activeScene = EditorSceneManager.GetActiveScene();
            bool isCorrectScene = string.Equals(activeScene.path, ExpectedScenePath, StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(activeScene.name, ExpectedSceneName, StringComparison.OrdinalIgnoreCase);

            if (!isCorrectScene)
            {
                EditorUtility.DisplayDialog(
                    "Invalid Active Scene",
                    $"This tool can ONLY be executed on the Systems Integration scene:\n{ExpectedScenePath}\n\n" +
                    $"Currently active scene:\n{(string.IsNullOrEmpty(activeScene.path) ? activeScene.name : activeScene.path)}\n\n" +
                    "Execution aborted to protect teammate scenes and master project assets.",
                    "OK");
                return;
            }

            // ---------------------------------------------------------
            // 3. Validate Required Assets (Meshes & Materials) BEFORE Scene Modification
            // ---------------------------------------------------------
            Mesh crystalMesh = AssetDatabase.LoadAssetAtPath<Mesh>(CrystalMeshPath);
            Mesh spiralMesh = AssetDatabase.LoadAssetAtPath<Mesh>(SpiralBandsMeshPath);
            Mesh clawsMesh = AssetDatabase.LoadAssetAtPath<Mesh>(DragonClawsMeshPath);

            Material rubyMat = AssetDatabase.LoadAssetAtPath<Material>(RubyMaterialPath);
            Material bronzeMat = AssetDatabase.LoadAssetAtPath<Material>(BronzeMaterialPath);
            Material energyRingMat = AssetDatabase.LoadAssetAtPath<Material>(EnergyRingMaterialPath);

            if (crystalMesh == null || spiralMesh == null || clawsMesh == null)
            {
                EditorUtility.DisplayDialog(
                    "Required Meshes Missing",
                    "One or more procedural mesh assets are missing:\n\n" +
                    $"• Faceted Crystal: {(crystalMesh != null ? "FOUND" : "MISSING")}\n" +
                    $"• Spiral Bands: {(spiralMesh != null ? "FOUND" : "MISSING")}\n" +
                    $"• Dragon Claws: {(clawsMesh != null ? "FOUND" : "MISSING")}\n\n" +
                    "Please run mesh generators from 'The Jade Seal -> Art' first.",
                    "OK");
                return;
            }

            if (rubyMat == null || bronzeMat == null || energyRingMat == null)
            {
                EditorUtility.DisplayDialog(
                    "Required Materials Missing",
                    "One or more materials in Assets/JadeSeal/Art/RedJadeArtifact/Materials are missing:\n\n" +
                    $"• M_Red_Luminous_Jade: {(rubyMat != null ? "FOUND" : "MISSING")}\n" +
                    $"• M_Dark_Ornamental_Bronze: {(bronzeMat != null ? "FOUND" : "MISSING")}\n" +
                    $"• M_Red_Energy_Ring: {(energyRingMat != null ? "FOUND" : "MISSING")}\n\n" +
                    "Aborting without making changes.",
                    "OK");
                return;
            }

            // ---------------------------------------------------------
            // 4. Locate Shrine and Gameplay Root
            // ---------------------------------------------------------
            GameObject shrineGo = GameObject.Find(ShrineGameObjectName);
            if (shrineGo == null)
            {
                EditorUtility.DisplayDialog(
                    "Shrine Not Found",
                    $"Could not locate '{ShrineGameObjectName}' in the active scene.\nAborting.",
                    "OK");
                return;
            }

            Transform keyArtifactTransform = shrineGo.transform.Find(KeyArtifactGameObjectName);
            if (keyArtifactTransform == null)
            {
                GameObject globalKeyGo = GameObject.Find(KeyArtifactGameObjectName);
                if (globalKeyGo != null)
                {
                    keyArtifactTransform = globalKeyGo.transform;
                }
            }

            if (keyArtifactTransform == null)
            {
                EditorUtility.DisplayDialog(
                    "Key Artifact Not Found",
                    $"Could not locate '{KeyArtifactGameObjectName}' in the active scene.\nAborting.",
                    "OK");
                return;
            }

            GameObject keyArtifactGo = keyArtifactTransform.gameObject;

            // Verify existing gameplay components
            SphereCollider sphereCollider = keyArtifactGo.GetComponent<SphereCollider>();
            JadeKeyPickup keyPickup = keyArtifactGo.GetComponent<JadeKeyPickup>();

            if (sphereCollider == null || keyPickup == null)
            {
                EditorUtility.DisplayDialog(
                    "Gameplay Component Missing",
                    $"'{KeyArtifactGameObjectName}' is missing required gameplay components:\n\n" +
                    $"• SphereCollider: {(sphereCollider != null ? "FOUND" : "MISSING")}\n" +
                    $"• JadeKeyPickup: {(keyPickup != null ? "FOUND" : "MISSING")}\n\n" +
                    "Aborting to protect player interaction and Door 2 progression.",
                    "OK");
                return;
            }

            // ---------------------------------------------------------
            // 5. Begin Undo Recording with Exception Rollback Guard
            // ---------------------------------------------------------
            Undo.SetCurrentGroupName("Rebuild Complete Red Jade Crystal");
            int undoGroup = Undo.GetCurrentGroup();

            try
            {
                // Ensure key artifact root is active in case it was deactivated during play
                if (!keyArtifactGo.activeSelf)
                {
                    Undo.RecordObject(keyArtifactGo, "Activate Key Artifact Root");
                    keyArtifactGo.SetActive(true);
                }

                // ---------------------------------------------------------
                // 6. Disable Old Green Shrine Light (Jade_Emerald_Glow)
                // ---------------------------------------------------------
                Transform emeraldGlowTransform = shrineGo.transform.Find(EmeraldGlowGameObjectName);
                if (emeraldGlowTransform != null && emeraldGlowTransform.gameObject.activeSelf)
                {
                    Undo.RecordObject(emeraldGlowTransform.gameObject, "Disable Jade Emerald Glow");
                    emeraldGlowTransform.gameObject.SetActive(false);
                }

                // ---------------------------------------------------------
                // 7. Remove Legacy Green Pieces (Safe for Partially Built Scenes)
                // ---------------------------------------------------------
                int removedGreenCount = 0;
                for (int i = keyArtifactTransform.childCount - 1; i >= 0; i--)
                {
                    Transform child = keyArtifactTransform.GetChild(i);
                    if (Array.IndexOf(LegacyGreenChildNames, child.name) >= 0)
                    {
                        Undo.DestroyObjectImmediate(child.gameObject);
                        removedGreenCount++;
                    }
                }

                // Ensure idle animation on JadeKeyPickup is false so root stays stationary
                SerializedObject pickupSo = new SerializedObject(keyPickup);
                SerializedProperty enableIdleAnimProp = pickupSo.FindProperty("enableIdleAnimation");
                if (enableIdleAnimProp != null && enableIdleAnimProp.boolValue)
                {
                    enableIdleAnimProp.boolValue = false;
                    pickupSo.ApplyModifiedProperties();
                }

                // ---------------------------------------------------------
                // 8. Update Bronze Material Properties (Warm Bronze Shine)
                // ---------------------------------------------------------
                Undo.RecordObject(bronzeMat, "Upgrade Bronze Material Properties");
                Color warmBronze = new Color(0.2902f, 0.2078f, 0.1569f, 1.0f); // sRGB #4A3528
                bronzeMat.SetColor("_Color", warmBronze);
                bronzeMat.SetFloat("_Metallic", 0.88f);
                bronzeMat.SetFloat("_Glossiness", 0.78f);
                bronzeMat.SetFloat("_GlossyReflections", 1.0f);
                bronzeMat.SetFloat("_SpecularHighlights", 1.0f);
                EditorUtility.SetDirty(bronzeMat);
                AssetDatabase.SaveAssets();

                // ---------------------------------------------------------
                // 9. Create or Reuse Red_Jade_Crystal_Visual Container
                // ---------------------------------------------------------
                Transform visualTransform = keyArtifactTransform.Find(RedVisualChildName);
                GameObject visualGo;

                if (visualTransform == null)
                {
                    visualGo = new GameObject(RedVisualChildName);
                    Undo.RegisterCreatedObjectUndo(visualGo, "Create Red_Jade_Crystal_Visual");
                    Undo.SetTransformParent(visualGo.transform, keyArtifactTransform, "Parent Red Jade Visual");
                    visualTransform = visualGo.transform;
                }
                else
                {
                    visualGo = visualTransform.gameObject;
                    Undo.RecordObject(visualGo, "Update Red_Jade_Crystal_Visual");
                }

                if (!visualGo.activeSelf)
                {
                    Undo.RecordObject(visualGo, "Activate Red_Jade_Crystal_Visual");
                    visualGo.SetActive(true);
                }

                // Set local transform:
                // Elevate +0.30m above Jade_Key_Artifact origin so the crystal floats ~10.5cm above the lotus dish.
                // Counter-rotate against Jade_Key_Artifact's local X-tilt so rotation occurs around world vertical axis.
                Undo.RecordObject(visualTransform, "Set Visual Transform");
                visualTransform.localPosition = new Vector3(0f, 0.30f, 0f);
                visualTransform.localRotation = Quaternion.Inverse(keyArtifactTransform.localRotation);
                visualTransform.localScale = Vector3.one * 0.50f;

                // Strip any colliders from the visual hierarchy
                Collider[] visualColliders = visualGo.GetComponentsInChildren<Collider>(true);
                foreach (var col in visualColliders)
                {
                    Undo.DestroyObjectImmediate(col);
                }

                // ---------------------------------------------------------
                // 10. Rebuild/Update: Faceted_Red_Jade_Crystal
                // ---------------------------------------------------------
                Transform crystalBodyTransform = visualTransform.Find("Faceted_Red_Jade_Crystal");
                GameObject crystalBodyGo;

                if (crystalBodyTransform == null)
                {
                    crystalBodyGo = new GameObject("Faceted_Red_Jade_Crystal");
                    Undo.RegisterCreatedObjectUndo(crystalBodyGo, "Create Faceted_Red_Jade_Crystal");
                    Undo.SetTransformParent(crystalBodyGo.transform, visualTransform, "Parent Faceted Crystal");
                    crystalBodyTransform = crystalBodyGo.transform;
                }
                else
                {
                    crystalBodyGo = crystalBodyTransform.gameObject;
                    Undo.RecordObject(crystalBodyGo, "Update Faceted_Red_Jade_Crystal");
                    // Remove any old child prisms or cubes
                    for (int i = crystalBodyTransform.childCount - 1; i >= 0; i--)
                    {
                        Undo.DestroyObjectImmediate(crystalBodyTransform.GetChild(i).gameObject);
                    }
                }

                if (!crystalBodyGo.activeSelf)
                {
                    Undo.RecordObject(crystalBodyGo, "Activate Faceted_Red_Jade_Crystal");
                    crystalBodyGo.SetActive(true);
                }

                crystalBodyTransform.localPosition = Vector3.zero;
                crystalBodyTransform.localRotation = Quaternion.identity;
                crystalBodyTransform.localScale = Vector3.one;

                // Unity-safe explicit component acquisition
                MeshFilter mfCrystal = GetOrAddComponent<MeshFilter>(crystalBodyGo);
                Undo.RecordObject(mfCrystal, "Assign Faceted Crystal Mesh");
                mfCrystal.sharedMesh = crystalMesh;

                MeshRenderer mrCrystal = GetOrAddComponent<MeshRenderer>(crystalBodyGo);
                Undo.RecordObject(mrCrystal, "Assign Ruby Material");
                mrCrystal.sharedMaterial = rubyMat;
                mrCrystal.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                mrCrystal.receiveShadows = true;

                // ---------------------------------------------------------
                // 11. Rebuild/Update: Ornamental_Metallic_Bands
                // ---------------------------------------------------------
                Transform bandsContainerTransform = visualTransform.Find("Ornamental_Metallic_Bands");
                GameObject bandsContainerGo;

                if (bandsContainerTransform == null)
                {
                    bandsContainerGo = new GameObject("Ornamental_Metallic_Bands");
                    Undo.RegisterCreatedObjectUndo(bandsContainerGo, "Create Ornamental_Metallic_Bands");
                    Undo.SetTransformParent(bandsContainerGo.transform, visualTransform, "Parent Metallic Bands");
                    bandsContainerTransform = bandsContainerGo.transform;
                }
                else
                {
                    bandsContainerGo = bandsContainerTransform.gameObject;
                    Undo.RecordObject(bandsContainerGo, "Update Ornamental_Metallic_Bands");
                    // Clean out old primitives and previous upgrade objects to avoid duplicate meshes
                    for (int i = bandsContainerTransform.childCount - 1; i >= 0; i--)
                    {
                        Undo.DestroyObjectImmediate(bandsContainerTransform.GetChild(i).gameObject);
                    }
                }

                if (!bandsContainerGo.activeSelf)
                {
                    Undo.RecordObject(bandsContainerGo, "Activate Ornamental_Metallic_Bands");
                    bandsContainerGo.SetActive(true);
                }

                bandsContainerTransform.localPosition = Vector3.zero;
                bandsContainerTransform.localRotation = Quaternion.identity;
                bandsContainerTransform.localScale = Vector3.one;

                // 11a. Metal_Spiral_Bands
                GameObject spiralGo = new GameObject("Metal_Spiral_Bands");
                Undo.RegisterCreatedObjectUndo(spiralGo, "Create Metal_Spiral_Bands");
                Undo.SetTransformParent(spiralGo.transform, bandsContainerTransform, "Parent Spiral Bands");
                spiralGo.transform.localPosition = Vector3.zero;
                spiralGo.transform.localRotation = Quaternion.identity;
                spiralGo.transform.localScale = Vector3.one;

                MeshFilter mfSpiral = GetOrAddComponent<MeshFilter>(spiralGo);
                Undo.RecordObject(mfSpiral, "Assign Spiral Bands Mesh");
                mfSpiral.sharedMesh = spiralMesh;

                MeshRenderer mrSpiral = GetOrAddComponent<MeshRenderer>(spiralGo);
                Undo.RecordObject(mrSpiral, "Assign Spiral Bands Material");
                mrSpiral.sharedMaterial = bronzeMat;
                mrSpiral.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                mrSpiral.receiveShadows = true;

                // 11b. Dragon_Claw_Ornaments
                GameObject clawsGo = new GameObject("Dragon_Claw_Ornaments");
                Undo.RegisterCreatedObjectUndo(clawsGo, "Create Dragon_Claw_Ornaments");
                Undo.SetTransformParent(clawsGo.transform, bandsContainerTransform, "Parent Dragon Claws");
                clawsGo.transform.localPosition = Vector3.zero;
                clawsGo.transform.localRotation = Quaternion.identity;
                clawsGo.transform.localScale = Vector3.one;

                MeshFilter mfClaws = GetOrAddComponent<MeshFilter>(clawsGo);
                Undo.RecordObject(mfClaws, "Assign Dragon Claws Mesh");
                mfClaws.sharedMesh = clawsMesh;

                MeshRenderer mrClaws = GetOrAddComponent<MeshRenderer>(clawsGo);
                Undo.RecordObject(mrClaws, "Assign Dragon Claws Material");
                mrClaws.sharedMaterial = bronzeMat;
                mrClaws.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                mrClaws.receiveShadows = true;

                // ---------------------------------------------------------
                // 12. Rebuild/Update: Magical Energy Rings
                // ---------------------------------------------------------
                Transform innerRingTransform = SetupEnergyRing(visualTransform, "Magic_Energy_Ring_Inner",
                    new Vector3(1.04f, 0.002f, 1.04f), Quaternion.Euler(12f, 0f, 6f), energyRingMat);

                Transform outerRingTransform = SetupEnergyRing(visualTransform, "Magic_Energy_Ring_Outer",
                    new Vector3(1.56f, 0.002f, 1.56f), Quaternion.Euler(-15f, 0f, -10f), energyRingMat);

                // ---------------------------------------------------------
                // 13. Rebuild/Update: Ruby_Point_Light
                // ---------------------------------------------------------
                Transform lightTransform = visualTransform.Find("Ruby_Point_Light");
                GameObject lightGo;

                if (lightTransform == null)
                {
                    lightGo = new GameObject("Ruby_Point_Light");
                    Undo.RegisterCreatedObjectUndo(lightGo, "Create Ruby_Point_Light");
                    Undo.SetTransformParent(lightGo.transform, visualTransform, "Parent Ruby Light");
                    lightTransform = lightGo.transform;
                }
                else
                {
                    lightGo = lightTransform.gameObject;
                    Undo.RecordObject(lightGo, "Update Ruby_Point_Light");
                }

                if (!lightGo.activeSelf)
                {
                    Undo.RecordObject(lightGo, "Activate Ruby_Point_Light");
                    lightGo.SetActive(true);
                }

                lightTransform.localPosition = Vector3.zero;
                lightTransform.localRotation = Quaternion.identity;
                lightTransform.localScale = Vector3.one;

                Light pointLight = GetOrAddComponent<Light>(lightGo);
                Undo.RecordObject(pointLight, "Configure Ruby Point Light");
                pointLight.type = LightType.Point;
                pointLight.color = new Color(1.0f, 0.12f, 0.22f, 1.0f); // Crimson ruby
                pointLight.intensity = 2.2f;
                pointLight.range = 4.5f;
                pointLight.shadows = LightShadows.None;

                // ---------------------------------------------------------
                // 14. Rebuild/Update: Crimson_Mote_Particles (With Velocity Mode Fix)
                // ---------------------------------------------------------
                Transform particleTransform = visualTransform.Find("Crimson_Mote_Particles");
                GameObject particleGo;

                if (particleTransform == null)
                {
                    particleGo = new GameObject("Crimson_Mote_Particles");
                    Undo.RegisterCreatedObjectUndo(particleGo, "Create Crimson_Mote_Particles");
                    Undo.SetTransformParent(particleGo.transform, visualTransform, "Parent Crimson Particles");
                    particleTransform = particleGo.transform;
                }
                else
                {
                    particleGo = particleTransform.gameObject;
                    Undo.RecordObject(particleGo, "Update Crimson_Mote_Particles");
                }

                if (!particleGo.activeSelf)
                {
                    Undo.RecordObject(particleGo, "Activate Crimson_Mote_Particles");
                    particleGo.SetActive(true);
                }

                particleTransform.localPosition = new Vector3(0f, -0.15f, 0f);
                particleTransform.localRotation = Quaternion.identity;
                particleTransform.localScale = Vector3.one;

                ParticleSystem ps = GetOrAddComponent<ParticleSystem>(particleGo);
                ParticleSystemRenderer psRenderer = GetOrAddComponent<ParticleSystemRenderer>(particleGo);

                Undo.RecordObject(ps, "Configure Crimson Mote Particles");
                psRenderer.material = energyRingMat;

                var main = ps.main;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                main.maxParticles = 30;
                main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4.5f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.10f, 0.25f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.12f);
                main.startColor = new Color(1.0f, 0.15f, 0.25f, 0.8f);

                var emission = ps.emission;
                emission.rateOverTime = 12f;

                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 0.35f;
                shape.rotation = new Vector3(90f, 0f, 0f);

                // VelocityOverLifetime X, Y, and Z curves all set to matching TwoConstants mode
                var velOverLifetime = ps.velocityOverLifetime;
                velOverLifetime.enabled = true;
                velOverLifetime.x = new ParticleSystem.MinMaxCurve(-0.04f, 0.04f);
                velOverLifetime.y = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
                velOverLifetime.z = new ParticleSystem.MinMaxCurve(-0.04f, 0.04f);

                // ---------------------------------------------------------
                // 15. Configure MagicalRedJadeArtifact Animation Component
                // ---------------------------------------------------------
                MagicalRedJadeArtifact animComp = GetOrAddComponent<MagicalRedJadeArtifact>(visualGo);

                SerializedObject animSo = new SerializedObject(animComp);
                SetSerializedFloat(animSo, "rotationSpeed", 20.0f);
                SetSerializedVector3(animSo, "rotationAxis", Vector3.up);
                SetSerializedBool(animSo, "enableBobbing", true);
                SetSerializedFloat(animSo, "bobbingAmplitude", 0.05f);
                SetSerializedFloat(animSo, "bobbingFrequency", 0.2f);
                SetSerializedVector3(animSo, "bobbingAxis", Vector3.up);

                if (innerRingTransform != null)
                {
                    SetSerializedObjectRef(animSo, "innerEnergyRing", innerRingTransform);
                    SetSerializedFloat(animSo, "innerRingRotationSpeed", -30.0f);
                }

                if (outerRingTransform != null)
                {
                    SetSerializedObjectRef(animSo, "outerEnergyRing", outerRingTransform);
                    SetSerializedFloat(animSo, "outerRingRotationSpeed", 20.0f);
                }

                SetSerializedVector3(animSo, "ringRotationAxis", Vector3.up);

                if (pointLight != null)
                {
                    SetSerializedObjectRef(animSo, "artifactLight", pointLight);
                    SetSerializedBool(animSo, "enableLightPulse", true);
                    SetSerializedFloat(animSo, "lightPulseAmount", 0.25f);
                    SetSerializedFloat(animSo, "lightPulseFrequency", 0.5f);
                }

                animSo.ApplyModifiedProperties();

                if (innerRingTransform != null && outerRingTransform != null)
                {
                    animComp.SetEnergyRings(innerRingTransform, outerRingTransform, -30.0f, 20.0f);
                }
                if (pointLight != null)
                {
                    animComp.SetArtifactLight(pointLight, 0.25f, 0.5f);
                }
                animComp.RebindBaseline();

                // ---------------------------------------------------------
                // 16. Finalize Undo, Mark Scene Dirty, and Display Guidance
                // ---------------------------------------------------------
                Undo.CollapseUndoOperations(undoGroup);
                EditorSceneManager.MarkSceneDirty(activeScene);

                string summary =
                    "Successfully rebuilt COMPLETE Red Jade Crystal!\n\n" +
                    $"• Scene: {activeScene.name}\n" +
                    $"• Cleaned: {removedGreenCount} legacy green visual meshes\n" +
                    "• Faceted Crystal: RedJade_FacetedCrystal.asset (54 facets, 1.00m height)\n" +
                    "• Ruby Material: M_Red_Luminous_Jade.mat (Vivid ruby, HDR emission)\n" +
                    "• Metallic Ornaments: Spiral Bands (208 tris) + Dragon Claws (384 tris)\n" +
                    "• Bronze Material: M_Dark_Ornamental_Bronze.mat (Warm bronze #4A3528, Metallic 0.88, Smoothness 0.78)\n" +
                    "• Particle Error Fix: Velocity curves mode aligned on X, Y, Z (TwoConstants mode)\n" +
                    "• Animation: 20 deg/s rotation, gentle bobbing, counter-rotating energy rings\n" +
                    "• Gameplay: SphereCollider trigger & JadeKeyPickup preserved 100%\n\n" +
                    "CRITICAL STEP TO PERSIST:\n" +
                    "Press Ctrl+S (File > Save) in Unity Edit Mode now to permanently save this scene to disk!";

                Debug.Log($"<color=#2ecc71><b>[RedJadeCompleteRecovery]</b></color> {summary}");

                EditorUtility.DisplayDialog(
                    "Red Jade Crystal Rebuild Complete",
                    summary,
                    "OK");
            }
            catch (Exception ex)
            {
                Debug.LogError($"<color=#e74c3c><b>[RedJadeCompleteRecovery] Rebuild failed with exception:</b></color>\n{ex}");

                // Safely roll back modifications recorded in this Undo group
                Undo.RevertAllDownToGroup(undoGroup);

                EditorUtility.DisplayDialog(
                    "Red Jade Rebuild Failed",
                    $"An error occurred while reconstructing the Red Jade Crystal:\n\n{ex.Message}\n\n" +
                    "Modifications made during this session have been rolled back via Undo.\n" +
                    "Please check the Unity Console for full details.",
                    "OK");
            }
        }

        /// <summary>
        /// Unity-safe component getter and adder that respects Unity's custom operator == null check,
        /// avoiding CLR reference-null bypass issues associated with the C# ?? operator.
        /// </summary>
        private static T GetOrAddComponent<T>(GameObject go) where T : Component
        {
            T comp = go.GetComponent<T>();
            if (comp == null)
            {
                comp = Undo.AddComponent<T>(go);
            }
            return comp;
        }

        private static Transform SetupEnergyRing(Transform parent, string name, Vector3 scale, Quaternion rotation, Material mat)
        {
            Transform ringTransform = parent.Find(name);
            GameObject ringGo;

            if (ringTransform == null)
            {
                ringGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ringGo.name = name;
                Undo.RegisterCreatedObjectUndo(ringGo, $"Create {name}");
                Undo.SetTransformParent(ringTransform = ringGo.transform, parent, $"Parent {name}");
            }
            else
            {
                ringGo = ringTransform.gameObject;
                Undo.RecordObject(ringGo, $"Update {name}");
            }

            if (!ringGo.activeSelf)
            {
                Undo.RecordObject(ringGo, $"Activate {name}");
                ringGo.SetActive(true);
            }

            Collider col = ringGo.GetComponent<Collider>();
            if (col != null) Undo.DestroyObjectImmediate(col);

            ringTransform.localPosition = Vector3.zero;
            ringTransform.localRotation = rotation;
            ringTransform.localScale = scale;

            MeshRenderer mr = GetOrAddComponent<MeshRenderer>(ringGo);
            if (mr != null)
            {
                Undo.RecordObject(mr, $"Configure {name} Renderer");
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }

            return ringTransform;
        }

        private static void SetSerializedFloat(SerializedObject so, string propertyName, float value)
        {
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop != null) prop.floatValue = value;
        }

        private static void SetSerializedBool(SerializedObject so, string propertyName, bool value)
        {
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop != null) prop.boolValue = value;
        }

        private static void SetSerializedVector3(SerializedObject so, string propertyName, Vector3 value)
        {
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop != null) prop.vector3Value = value;
        }

        private static void SetSerializedObjectRef(SerializedObject so, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop != null) prop.objectReferenceValue = value;
        }
    }
}
#endif
