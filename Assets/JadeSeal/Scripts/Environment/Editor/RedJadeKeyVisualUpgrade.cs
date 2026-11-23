#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using JadeSeal.Environment;
using JadeSeal.Interaction;

namespace JadeSeal.Environment.Editor
{
    /// <summary>
    /// Dedicated, safe, non-destructive Editor tool for upgrading the existing
    /// Green Jade Key in the Systems Integration scene to the glowing, rotating
    /// Red Jade Crystal artifact.
    ///
    /// SAFETY & SCOPE GUARANTEES:
    /// 1. Strict Target Validation: ONLY operates on JadeSealFantasy_SystemsIntegration.unity.
    ///    Refuses execution on teammate scenes (JadeSealFantasy.unity) or test scenes.
    /// 2. Zero Gameplay Regressions: Preserves Jade_Key_Artifact root GameObject, Transform,
    ///    SphereCollider trigger, JadeKeyPickup component, player inventory integration,
    ///    and Door 2 progression hooks exactly.
    /// 3. Visuals Replacement: Replaces ONLY the 4 legacy green child meshes (BiDisc_Crown,
    ///    Talisman_Shaft, Gold_Band, Key_Teeth) with a single dedicated child: 'Red_Jade_Crystal_Visual'.
    /// 4. Prefab Reuse: Clones only the floating crystal core visuals, metallic bands, energy rings,
    ///    ruby light, and crimson particles from Assets/JadeSeal/Art/RedJadeArtifact/RedJadeArtifact_Pedestal.prefab.
    ///    Does NOT duplicate the large stone pedestal structure.
    /// 5. Animation Component: Attaches MagicalRedJadeArtifact exclusively to 'Red_Jade_Crystal_Visual',
    ///    ensuring 20 deg/s rotation and gentle sinusoidal bobbing with zero baseline drift.
    /// 6. Automatic Disappearance: Because the new visual is parented directly to Jade_Key_Artifact,
    ///    calling gameObject.SetActive(false) upon player pickup automatically hides the entire visual.
    /// 7. Lighting Isolation: Disables the old green Jade_Emerald_Glow light on the shrine.
    /// 8. Idempotent: Can be run multiple times safely without creating duplicate objects.
    /// 9. Full Undo Support: All operations are registered with Undo and mark the scene dirty.
    /// </summary>
    public static class RedJadeKeyVisualUpgrade
    {
        public const string ExpectedSceneName = "JadeSealFantasy_SystemsIntegration";
        public const string ExpectedScenePath = "Assets/JadeSeal/Scenes/JadeSealFantasy_SystemsIntegration.unity";

        public const string PrefabPath = "Assets/JadeSeal/Art/RedJadeArtifact/RedJadeArtifact_Pedestal.prefab";
        public const string MaterialsFolder = "Assets/JadeSeal/Art/RedJadeArtifact/Materials";

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

        [MenuItem("The Jade Seal/Systems Integration/Upgrade Jade Key to Red Jade Crystal", priority = 120)]
        [MenuItem("Tools/The Jade Seal/Upgrade Jade Key to Red Jade Crystal", priority = 120)]
        public static void UpgradeJadeKeyVisualMenu()
        {
            // ---------------------------------------------------------
            // 1. Validate Active Target Scene
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
            // 2. Validate Required Prefab and Materials
            // ---------------------------------------------------------
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Material jadeMat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/M_Red_Luminous_Jade.mat");
            Material bronzeMat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/M_Dark_Ornamental_Bronze.mat");
            Material energyRingMat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/M_Red_Energy_Ring.mat");

            if (prefabAsset == null)
            {
                EditorUtility.DisplayDialog(
                    "Prefab Validation Failed",
                    $"Could not load the Red Jade Artifact prefab at:\n{PrefabPath}\n\n" +
                    "Please ensure the prefab exists and is imported without errors before upgrading.\n" +
                    "(You can run 'The Jade Seal -> Art -> Build Red Jade Artifact Prefab' to generate it).",
                    "OK");
                return;
            }

            if (jadeMat == null || bronzeMat == null || energyRingMat == null)
            {
                EditorUtility.DisplayDialog(
                    "Material Validation Failed",
                    $"One or more required materials are missing in {MaterialsFolder}:\n\n" +
                    $"• M_Red_Luminous_Jade.mat: {(jadeMat != null ? "FOUND" : "MISSING")}\n" +
                    $"• M_Dark_Ornamental_Bronze.mat: {(bronzeMat != null ? "FOUND" : "MISSING")}\n" +
                    $"• M_Red_Energy_Ring.mat: {(energyRingMat != null ? "FOUND" : "MISSING")}\n\n" +
                    "Aborting without making changes. Please generate the materials first.",
                    "OK");
                return;
            }

            Transform coreSource = FindChildRecursive(prefabAsset.transform, "Floating_Artifact_Core");
            if (coreSource == null)
            {
                EditorUtility.DisplayDialog(
                    "Prefab Structure Error",
                    $"Could not locate 'Floating_Artifact_Core' inside the prefab at:\n{PrefabPath}\n\n" +
                    "The prefab hierarchy is missing the floating crystal core. Aborting.",
                    "OK");
                return;
            }

            // ---------------------------------------------------------
            // 3. Locate Shrine and Jade Key Artifact
            // ---------------------------------------------------------
            GameObject shrineGo = GameObject.Find(ShrineGameObjectName);
            if (shrineGo == null)
            {
                EditorUtility.DisplayDialog(
                    "Shrine Not Found",
                    $"Could not locate '{ShrineGameObjectName}' in the active scene.\n" +
                    "Please verify that the Systems Integration scene hierarchy is loaded.",
                    "OK");
                return;
            }

            Transform keyArtifactTransform = shrineGo.transform.Find(KeyArtifactGameObjectName);
            if (keyArtifactTransform == null)
            {
                // Fallback search in scene
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
                    $"Could not locate '{KeyArtifactGameObjectName}' under '{ShrineGameObjectName}' or in the active scene.\n" +
                    "Aborting upgrade.",
                    "OK");
                return;
            }

            GameObject keyArtifactGo = keyArtifactTransform.gameObject;

            // ---------------------------------------------------------
            // 4. Verify Existing Gameplay Components
            // ---------------------------------------------------------
            SphereCollider sphereCollider = keyArtifactGo.GetComponent<SphereCollider>();
            JadeKeyPickup keyPickup = keyArtifactGo.GetComponent<JadeKeyPickup>();

            if (sphereCollider == null || keyPickup == null)
            {
                EditorUtility.DisplayDialog(
                    "Gameplay Component Missing",
                    $"'{KeyArtifactGameObjectName}' is missing required gameplay components:\n\n" +
                    $"• SphereCollider: {(sphereCollider != null ? "FOUND" : "MISSING")}\n" +
                    $"• JadeKeyPickup: {(keyPickup != null ? "FOUND" : "MISSING")}\n\n" +
                    "Aborting to prevent breaking player interaction or Door 2 unlocking.",
                    "OK");
                return;
            }

            // ---------------------------------------------------------
            // 5. Begin Undo Recording
            // ---------------------------------------------------------
            Undo.SetCurrentGroupName("Upgrade Jade Key to Red Jade Crystal");
            int undoGroup = Undo.GetCurrentGroup();

            // ---------------------------------------------------------
            // 6. Disable Old Green Glow (Jade_Emerald_Glow)
            // ---------------------------------------------------------
            Transform emeraldGlowTransform = shrineGo.transform.Find(EmeraldGlowGameObjectName);
            if (emeraldGlowTransform != null && emeraldGlowTransform.gameObject.activeSelf)
            {
                Undo.RecordObject(emeraldGlowTransform.gameObject, "Disable Jade Emerald Glow");
                emeraldGlowTransform.gameObject.SetActive(false);
            }

            // ---------------------------------------------------------
            // 7. Remove Obsolete Visual Children & Previous Upgrades (Idempotent)
            // ---------------------------------------------------------
            int removedLegacyCount = 0;
            for (int i = keyArtifactTransform.childCount - 1; i >= 0; i--)
            {
                Transform child = keyArtifactTransform.GetChild(i);
                if (Array.IndexOf(LegacyGreenChildNames, child.name) >= 0)
                {
                    Undo.DestroyObjectImmediate(child.gameObject);
                    removedLegacyCount++;
                }
                else if (string.Equals(child.name, RedVisualChildName, StringComparison.OrdinalIgnoreCase))
                {
                    Undo.DestroyObjectImmediate(child.gameObject);
                }
            }

            // Remove any accidental duplicate animation component on root
            MagicalRedJadeArtifact rootAnim = keyArtifactGo.GetComponent<MagicalRedJadeArtifact>();
            if (rootAnim != null)
            {
                Undo.DestroyObjectImmediate(rootAnim);
            }

            // Ensure keyPickup idle animation stays disabled on root
            SerializedObject pickupSo = new SerializedObject(keyPickup);
            SerializedProperty enableIdleAnimProp = pickupSo.FindProperty("enableIdleAnimation");
            if (enableIdleAnimProp != null && enableIdleAnimProp.boolValue)
            {
                enableIdleAnimProp.boolValue = false;
                pickupSo.ApplyModifiedProperties();
            }

            // ---------------------------------------------------------
            // 8. Instantiate & Setup Red_Jade_Crystal_Visual
            // ---------------------------------------------------------
            // Instantiate only the floating crystal core hierarchy (NOT the pedestal)
            GameObject visualGo = UnityEngine.Object.Instantiate(coreSource.gameObject);
            visualGo.name = RedVisualChildName;
            Undo.RegisterCreatedObjectUndo(visualGo, "Create Red_Jade_Crystal_Visual");

            // Strip any colliders from the visual children to guarantee zero interference with trigger
            Collider[] visualColliders = visualGo.GetComponentsInChildren<Collider>(true);
            foreach (var col in visualColliders)
            {
                UnityEngine.Object.DestroyImmediate(col);
            }

            // Parent under Jade_Key_Artifact
            Undo.SetTransformParent(visualGo.transform, keyArtifactTransform, "Parent Red Jade Visual");

            // Position and scale:
            // Elevate slightly (+0.30m) above Jade_Key_Artifact origin so the crystal floats ~8.5cm above the lotus dish.
            // Counter-rotate against Jade_Key_Artifact's local X-tilt so rotation occurs around world vertical axis.
            visualGo.transform.localPosition = new Vector3(0f, 0.30f, 0f);
            visualGo.transform.localRotation = Quaternion.Inverse(keyArtifactTransform.localRotation);
            visualGo.transform.localScale = Vector3.one * 0.50f;

            // Re-bind materials explicitly to ensure visual perfection
            BindMaterials(visualGo.transform, jadeMat, bronzeMat, energyRingMat);

            // Configure ruby point light
            Light pointLight = visualGo.GetComponentInChildren<Light>();
            if (pointLight != null)
            {
                pointLight.color = new Color(1.0f, 0.15f, 0.22f, 1.0f);
                pointLight.range = 4.0f;
                pointLight.intensity = 2.0f;
                pointLight.shadows = LightShadows.Soft;
            }

            // Find energy ring transforms
            Transform innerRing = FindChildRecursive(visualGo.transform, "Magic_Energy_Ring_Inner");
            Transform outerRing = FindChildRecursive(visualGo.transform, "Magic_Energy_Ring_Outer");

            // ---------------------------------------------------------
            // 9. Configure MagicalRedJadeArtifact Animation
            // ---------------------------------------------------------
            MagicalRedJadeArtifact animComp = visualGo.GetComponent<MagicalRedJadeArtifact>();
            if (animComp == null)
            {
                animComp = Undo.AddComponent<MagicalRedJadeArtifact>(visualGo);
            }

            SerializedObject animSo = new SerializedObject(animComp);
            SetSerializedFloat(animSo, "rotationSpeed", 20.0f);
            SetSerializedVector3(animSo, "rotationAxis", Vector3.up);
            SetSerializedBool(animSo, "enableBobbing", true);
            SetSerializedFloat(animSo, "bobbingAmplitude", 0.05f);
            SetSerializedFloat(animSo, "bobbingFrequency", 0.2f);
            SetSerializedVector3(animSo, "bobbingAxis", Vector3.up);

            if (innerRing != null)
            {
                SetSerializedObjectRef(animSo, "innerEnergyRing", innerRing);
                SetSerializedFloat(animSo, "innerRingRotationSpeed", -30.0f);
            }

            if (outerRing != null)
            {
                SetSerializedObjectRef(animSo, "outerEnergyRing", outerRing);
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

            // Also call runtime setup helpers and initialize resting baseline
            if (innerRing != null && outerRing != null)
            {
                animComp.SetEnergyRings(innerRing, outerRing, -30.0f, 20.0f);
            }
            if (pointLight != null)
            {
                animComp.SetArtifactLight(pointLight, 0.25f, 0.5f);
            }
            animComp.RebindBaseline();

            // ---------------------------------------------------------
            // 10. Finalize Undo, Mark Scene Dirty, and Report Success
            // ---------------------------------------------------------
            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(activeScene);

            string successSummary =
                "Successfully upgraded Jade Key visual to Red Jade Crystal!\n\n" +
                $"• Scene: {activeScene.name}\n" +
                $"• Target: {KeyArtifactGameObjectName} at {keyArtifactTransform.position}\n" +
                $"• Removed: {removedLegacyCount} legacy green visual children (BiDisc, Shaft, Band, Teeth)\n" +
                $"• Created Child: '{RedVisualChildName}' (Scale 0.50, Height offset +0.30m)\n" +
                "• Animated: MagicalRedJadeArtifact (20 deg/s rotation, 0.05m bobbing, counter-rotating rings)\n" +
                "• Lighting: Disabled green Jade_Emerald_Glow; added soft Ruby Point Light\n" +
                "• Gameplay: SphereCollider trigger, JadeKeyPickup & Door 2 logic preserved 100%\n\n" +
                "Operation registered with Undo (Ctrl+Z). Scene marked dirty.";

            Debug.Log($"<color=#2ecc71><b>[RedJadeKeyVisualUpgrade]</b></color> {successSummary}");

            EditorUtility.DisplayDialog(
                "Jade Key Visual Upgrade Complete",
                successSummary,
                "OK");
        }

        private static Transform FindChildRecursive(Transform parent, string childName)
        {
            if (parent == null) return null;
            Transform direct = parent.Find(childName);
            if (direct != null) return direct;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindChildRecursive(parent.GetChild(i), childName);
                if (found != null) return found;
            }
            return null;
        }

        private static void BindMaterials(Transform visualRoot, Material jadeMat, Material bronzeMat, Material energyRingMat)
        {
            MeshRenderer[] renderers = visualRoot.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var mr in renderers)
            {
                string n = mr.name;
                string parentName = mr.transform.parent != null ? mr.transform.parent.name : string.Empty;

                if (n.Contains("Crystal") || n.Contains("Prism") || n.Contains("Cap") || parentName.Contains("Crystal"))
                {
                    mr.sharedMaterial = jadeMat;
                }
                else if (n.Contains("Collar") || n.Contains("Prong") || n.Contains("Band") || parentName.Contains("Bands"))
                {
                    mr.sharedMaterial = bronzeMat;
                }
                else if (n.Contains("Ring"))
                {
                    mr.sharedMaterial = energyRingMat;
                }
            }

            ParticleSystemRenderer psRenderer = visualRoot.GetComponentInChildren<ParticleSystemRenderer>();
            if (psRenderer != null)
            {
                psRenderer.sharedMaterial = energyRingMat;
            }
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
