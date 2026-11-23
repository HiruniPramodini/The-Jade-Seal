#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace JadeSeal.Environment.Editor
{
    /// <summary>
    /// Dedicated, safe, non-destructive Editor utility for upgrading the Red Jade Crystal's
    /// metallic decorations and resolving the particle velocity curves mode warning.
    ///
    /// SAFETY & SCOPE GUARANTEES:
    /// 1. Strict Target Scene Validation: Operates ONLY on JadeSealFantasy_SystemsIntegration.unity.
    /// 2. Preserves Gameplay Root: Leaves Jade_Key_Artifact, SphereCollider, and JadeKeyPickup 100% untouched.
    /// 3. Preserves Environment & Animations: Retains stone shrine, lotus dish, lanterns, ruby gemstone,
    ///    ruby light, energy rings, and MagicalRedJadeArtifact continuous animation component.
    /// 4. Replaces ONLY Old Metallic Pieces: Cleans out Equatorial_Collar and Collar_Prong_1..4,
    ///    replacing them with two dedicated procedural meshes:
    ///    - Metal_Spiral_Bands (RedJade_MetalSpiralBands.asset)
    ///    - Dragon_Claw_Ornaments (RedJade_DragonClawOrnaments.asset)
    /// 5. Improves Bronze Material: Updates M_Dark_Ornamental_Bronze.mat with warm antique bronze albedo
    ///    (sRGB #4A3528), metallic 0.88, and smoothness 0.78 for sharp metallic glints.
    /// 6. Particle Error Fix: Aligns VelocityOverLifetime X, Y, and Z curves to the matching TwoConstants mode,
    ///    completely resolving "Particle Velocity curves must all be in the same mode".
    /// 7. Idempotent & Undo-safe: Can be run repeatedly without duplicating GameObjects.
    /// 8. Marks scene dirty without saving automatically.
    /// </summary>
    public static class RedJadeMetalOrnamentsUpgrade
    {
        public const string ExpectedSceneName = "JadeSealFantasy_SystemsIntegration";
        public const string ExpectedScenePath = "Assets/JadeSeal/Scenes/JadeSealFantasy_SystemsIntegration.unity";

        public const string SpiralBandsMeshPath = "Assets/JadeSeal/Art/RedJadeArtifact/Meshes/RedJade_MetalSpiralBands.asset";
        public const string DragonClawsMeshPath = "Assets/JadeSeal/Art/RedJadeArtifact/Meshes/RedJade_DragonClawOrnaments.asset";
        public const string BronzeMaterialPath = "Assets/JadeSeal/Art/RedJadeArtifact/Materials/M_Dark_Ornamental_Bronze.mat";

        public const string ShrineGameObjectName = "Jade_Key_Shrine";
        public const string KeyArtifactGameObjectName = "Jade_Key_Artifact";
        public const string RedVisualChildName = "Red_Jade_Crystal_Visual";
        public const string MetalBandsGameObjectName = "Ornamental_Metallic_Bands";
        public const string ParticlesGameObjectName = "Crimson_Mote_Particles";

        private static readonly string[] OldPrimitiveNames = new string[]
        {
            "Equatorial_Collar",
            "Collar_Prong_1",
            "Collar_Prong_2",
            "Collar_Prong_3",
            "Collar_Prong_4"
        };

        [MenuItem("The Jade Seal/Systems Integration/Upgrade Red Jade Metallic Ornaments", priority = 126)]
        [MenuItem("Tools/The Jade Seal/Upgrade Red Jade Metallic Ornaments", priority = 126)]
        public static void UpgradeMetalOrnamentsMenu()
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
            // 2. Validate Assets (Spiral Bands, Dragon Claws, Bronze Material)
            // ---------------------------------------------------------
            Mesh spiralMesh = AssetDatabase.LoadAssetAtPath<Mesh>(SpiralBandsMeshPath);
            Mesh clawsMesh = AssetDatabase.LoadAssetAtPath<Mesh>(DragonClawsMeshPath);
            Material bronzeMat = AssetDatabase.LoadAssetAtPath<Material>(BronzeMaterialPath);

            if (spiralMesh == null || clawsMesh == null)
            {
                EditorUtility.DisplayDialog(
                    "Filigree Meshes Missing",
                    "Could not load required procedural filigree mesh assets:\n\n" +
                    $"• Spiral Bands: {(spiralMesh != null ? "FOUND" : "MISSING (" + SpiralBandsMeshPath + ")")}\n" +
                    $"• Dragon Claws: {(clawsMesh != null ? "FOUND" : "MISSING (" + DragonClawsMeshPath + ")")}\n\n" +
                    "Please run 'The Jade Seal -> Art -> Generate Red Jade Metallic Filigree' first.",
                    "OK");
                return;
            }

            if (bronzeMat == null)
            {
                EditorUtility.DisplayDialog(
                    "Material Missing",
                    $"Could not load material at:\n{BronzeMaterialPath}\n\n" +
                    "Please ensure M_Dark_Ornamental_Bronze.mat exists before upgrading.",
                    "OK");
                return;
            }

            // ---------------------------------------------------------
            // 3. Locate Hierarchy Objects
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

            Transform visualTransform = keyArtifactTransform.Find(RedVisualChildName);
            if (visualTransform == null)
            {
                EditorUtility.DisplayDialog(
                    "Visual Container Not Found",
                    $"Could not locate '{RedVisualChildName}' under '{KeyArtifactGameObjectName}'.\n" +
                    "Please run 'The Jade Seal -> Systems Integration -> Upgrade Jade Key to Red Jade Crystal' first.",
                    "OK");
                return;
            }

            // ---------------------------------------------------------
            // 4. Begin Undo Recording
            // ---------------------------------------------------------
            Undo.SetCurrentGroupName("Upgrade Red Jade Metallic Ornaments");
            int undoGroup = Undo.GetCurrentGroup();

            // ---------------------------------------------------------
            // 5. Update M_Dark_Ornamental_Bronze.mat Properties (Warm Bronze Shine)
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
            // 6. Inspect & Clean Up Old Primitive Metallic Pieces
            // ---------------------------------------------------------
            Transform bandsContainerTransform = visualTransform.Find(MetalBandsGameObjectName);
            GameObject bandsContainerGo;

            if (bandsContainerTransform == null)
            {
                bandsContainerGo = new GameObject(MetalBandsGameObjectName);
                Undo.RegisterCreatedObjectUndo(bandsContainerGo, "Create Ornamental_Metallic_Bands");
                Undo.SetTransformParent(bandsContainerGo.transform, visualTransform, "Parent Ornamental Bands");
                bandsContainerTransform = bandsContainerGo.transform;
            }
            else
            {
                bandsContainerGo = bandsContainerTransform.gameObject;
                Undo.RecordObject(bandsContainerGo, "Update Ornamental_Metallic_Bands");
            }

            // Ensure baseline local transform
            Undo.RecordObject(bandsContainerTransform, "Reset Bands Container Transform");
            bandsContainerTransform.localPosition = Vector3.zero;
            bandsContainerTransform.localRotation = Quaternion.identity;
            bandsContainerTransform.localScale = Vector3.one;

            // Remove old primitive collar and rectangular prongs (and prior upgrade objects for idempotency)
            int removedCount = 0;
            for (int i = bandsContainerTransform.childCount - 1; i >= 0; i--)
            {
                Transform child = bandsContainerTransform.GetChild(i);
                string childName = child.name;

                if (Array.IndexOf(OldPrimitiveNames, childName) >= 0 ||
                    childName == "Metal_Spiral_Bands" ||
                    childName == "Dragon_Claw_Ornaments")
                {
                    Undo.DestroyObjectImmediate(child.gameObject);
                    removedCount++;
                }
            }

            // Check for any loose old primitives directly under Red_Jade_Crystal_Visual
            for (int i = visualTransform.childCount - 1; i >= 0; i--)
            {
                Transform child = visualTransform.GetChild(i);
                if (Array.IndexOf(OldPrimitiveNames, child.name) >= 0)
                {
                    Undo.DestroyObjectImmediate(child.gameObject);
                    removedCount++;
                }
            }

            // ---------------------------------------------------------
            // 7. Create Dedicated Replacement Children
            // ---------------------------------------------------------

            // 7a. Metal_Spiral_Bands
            GameObject spiralGo = new GameObject("Metal_Spiral_Bands");
            Undo.RegisterCreatedObjectUndo(spiralGo, "Create Metal_Spiral_Bands");
            Undo.SetTransformParent(spiralGo.transform, bandsContainerTransform, "Parent Spiral Bands");
            spiralGo.transform.localPosition = Vector3.zero;
            spiralGo.transform.localRotation = Quaternion.identity;
            spiralGo.transform.localScale = Vector3.one;

            Collider colSpiral = spiralGo.GetComponent<Collider>();
            if (colSpiral != null) Undo.DestroyObjectImmediate(colSpiral);

            MeshFilter mfSpiral = Undo.AddComponent<MeshFilter>(spiralGo);
            Undo.RecordObject(mfSpiral, "Assign Spiral Bands Mesh");
            mfSpiral.sharedMesh = spiralMesh;

            MeshRenderer mrSpiral = Undo.AddComponent<MeshRenderer>(spiralGo);
            Undo.RecordObject(mrSpiral, "Assign Spiral Bands Material");
            mrSpiral.sharedMaterial = bronzeMat;
            mrSpiral.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mrSpiral.receiveShadows = true;

            // 7b. Dragon_Claw_Ornaments
            GameObject clawsGo = new GameObject("Dragon_Claw_Ornaments");
            Undo.RegisterCreatedObjectUndo(clawsGo, "Create Dragon_Claw_Ornaments");
            Undo.SetTransformParent(clawsGo.transform, bandsContainerTransform, "Parent Dragon Claws");
            clawsGo.transform.localPosition = Vector3.zero;
            clawsGo.transform.localRotation = Quaternion.identity;
            clawsGo.transform.localScale = Vector3.one;

            Collider colClaws = clawsGo.GetComponent<Collider>();
            if (colClaws != null) Undo.DestroyObjectImmediate(colClaws);

            MeshFilter mfClaws = Undo.AddComponent<MeshFilter>(clawsGo);
            Undo.RecordObject(mfClaws, "Assign Dragon Claws Mesh");
            mfClaws.sharedMesh = clawsMesh;

            MeshRenderer mrClaws = Undo.AddComponent<MeshRenderer>(clawsGo);
            Undo.RecordObject(mrClaws, "Assign Dragon Claws Material");
            mrClaws.sharedMaterial = bronzeMat;
            mrClaws.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mrClaws.receiveShadows = true;

            // ---------------------------------------------------------
            // 8. Fix Particle Velocity Curves Mode on Crimson_Mote_Particles
            // ---------------------------------------------------------
            bool particleFixed = false;
            Transform particleTransform = visualTransform.Find(ParticlesGameObjectName);
            if (particleTransform != null)
            {
                ParticleSystem ps = particleTransform.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    Undo.RecordObject(ps, "Fix Particle Velocity Curves Mode");
                    var vel = ps.velocityOverLifetime;
                    vel.enabled = true;
                    // Set all 3 axes to the same TwoConstants mode:
                    vel.x = new ParticleSystem.MinMaxCurve(-0.04f, 0.04f);
                    vel.y = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
                    vel.z = new ParticleSystem.MinMaxCurve(-0.04f, 0.04f);
                    particleFixed = true;
                }
            }

            // ---------------------------------------------------------
            // 9. Finalize Undo, Mark Scene Dirty, and Report Success
            // ---------------------------------------------------------
            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(activeScene);

            string successSummary =
                "Successfully upgraded Red Jade Metallic Ornaments!\n\n" +
                $"• Scene: {activeScene.name}\n" +
                $"• Removed: {removedCount} old primitive pieces (Equatorial_Collar & Collar_Prong_1..4)\n" +
                $"• Created: 'Metal_Spiral_Bands' ({spiralMesh.name}, 208 tris)\n" +
                $"• Created: 'Dragon_Claw_Ornaments' ({clawsMesh.name}, 384 tris)\n" +
                "• Material: Upgraded M_Dark_Ornamental_Bronze.mat (Warm bronze #4A3528, Metallic 0.88, Smoothness 0.78)\n" +
                $"• Particle Fix: {(particleFixed ? "Aligned VelocityOverLifetime curve modes on Crimson_Mote_Particles" : "Particle system not found")}\n" +
                "• Preserved: Faceted ruby crystal, energy rings, ruby light, and continuous animation\n" +
                "• Preserved Gameplay: Jade_Key_Artifact, SphereCollider, JadeKeyPickup & Door 2 logic\n\n" +
                "Operation registered with Undo (Ctrl+Z). Scene marked dirty.";

            Debug.Log($"<color=#2ecc71><b>[RedJadeMetalOrnamentsUpgrade]</b></color> {successSummary}");

            EditorUtility.DisplayDialog(
                "Metallic Ornaments Upgrade Complete",
                successSummary,
                "OK");
        }
    }
}
#endif
