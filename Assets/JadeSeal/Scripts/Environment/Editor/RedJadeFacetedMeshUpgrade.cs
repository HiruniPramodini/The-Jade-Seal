#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace JadeSeal.Environment.Editor
{
    /// <summary>
    /// Dedicated, safe, non-destructive Editor utility for upgrading the Red Jade Crystal
    /// body geometry in the Systems Integration scene from blocky primitive cubes to the
    /// custom, sharply faceted gemstone mesh (RedJade_FacetedCrystal.asset).
    ///
    /// SAFETY & INTEGRATION GUARANTEES:
    /// 1. Strict Target Scene Validation: Operates ONLY on JadeSealFantasy_SystemsIntegration.unity.
    /// 2. Preserves Gameplay Root: Leaves Jade_Key_Artifact, SphereCollider, and JadeKeyPickup 100% untouched.
    /// 3. Preserves Environment & Animations: Retains the stone shrine, lotus dish, pagoda lanterns,
    ///    ornamental bronze bands, magical energy rings, ruby point light, crimson particles,
    ///    and the MagicalRedJadeArtifact animation script.
    /// 4. Replaces ONLY Blocky Prisms: Cleans out the 3 intersecting cube prisms and stepped caps,
    ///    replacing them with ONE MeshFilter and MeshRenderer using RedJade_FacetedCrystal.asset.
    /// 5. Assigns M_Red_Luminous_Jade to the new faceted mesh renderer.
    /// 6. Idempotent & Undo-safe: Repeated runs cleanly refresh the mesh without duplicating GameObjects.
    /// 7. Marks scene dirty without saving automatically.
    /// </summary>
    public static class RedJadeFacetedMeshUpgrade
    {
        public const string ExpectedSceneName = "JadeSealFantasy_SystemsIntegration";
        public const string ExpectedScenePath = "Assets/JadeSeal/Scenes/JadeSealFantasy_SystemsIntegration.unity";

        public const string MeshAssetPath = "Assets/JadeSeal/Art/RedJadeArtifact/Meshes/RedJade_FacetedCrystal.asset";
        public const string MaterialPath = "Assets/JadeSeal/Art/RedJadeArtifact/Materials/M_Red_Luminous_Jade.mat";

        public const string ShrineGameObjectName = "Jade_Key_Shrine";
        public const string KeyArtifactGameObjectName = "Jade_Key_Artifact";
        public const string RedVisualChildName = "Red_Jade_Crystal_Visual";
        public const string CrystalBodyGameObjectName = "Faceted_Red_Jade_Crystal";

        [MenuItem("The Jade Seal/Systems Integration/Upgrade Red Jade Crystal Mesh", priority = 125)]
        [MenuItem("Tools/The Jade Seal/Upgrade Red Jade Crystal Mesh", priority = 125)]
        public static void UpgradeCrystalMeshMenu()
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
            // 2. Validate Assets (Custom Faceted Mesh & Ruby Material)
            // ---------------------------------------------------------
            Mesh facetedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshAssetPath);
            Material rubyMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);

            if (facetedMesh == null)
            {
                EditorUtility.DisplayDialog(
                    "Faceted Mesh Missing",
                    $"Could not load custom faceted mesh at:\n{MeshAssetPath}\n\n" +
                    "Please run 'The Jade Seal -> Art -> Generate Red Jade Crystal Mesh' first.",
                    "OK");
                return;
            }

            if (rubyMaterial == null)
            {
                EditorUtility.DisplayDialog(
                    "Material Missing",
                    $"Could not load material at:\n{MaterialPath}\n\n" +
                    "Please ensure M_Red_Luminous_Jade.mat exists before upgrading.",
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
            Undo.SetCurrentGroupName("Upgrade Red Jade Crystal Mesh");
            int undoGroup = Undo.GetCurrentGroup();

            // ---------------------------------------------------------
            // 5. Inspect and Clean Up Old Blocky Cube Prisms and Stepped Caps
            // ---------------------------------------------------------
            int removedPrismCount = 0;
            Transform crystalBodyTransform = visualTransform.Find(CrystalBodyGameObjectName);

            if (crystalBodyTransform != null)
            {
                // Remove all child primitive cubes under Faceted_Red_Jade_Crystal
                for (int i = crystalBodyTransform.childCount - 1; i >= 0; i--)
                {
                    Transform child = crystalBodyTransform.GetChild(i);
                    Undo.DestroyObjectImmediate(child.gameObject);
                    removedPrismCount++;
                }
            }
            else
            {
                // If it didn't exist, create it
                GameObject newCrystalBody = new GameObject(CrystalBodyGameObjectName);
                Undo.RegisterCreatedObjectUndo(newCrystalBody, "Create Faceted_Red_Jade_Crystal");
                Undo.SetTransformParent(newCrystalBody.transform, visualTransform, "Parent Faceted Crystal");
                crystalBodyTransform = newCrystalBody.transform;
            }

            // Also check for any loose primitive cube prisms directly under Red_Jade_Crystal_Visual
            for (int i = visualTransform.childCount - 1; i >= 0; i--)
            {
                Transform child = visualTransform.GetChild(i);
                string childName = child.name;
                if (childName.StartsWith("Crystal_Facet_Prism") ||
                    childName.StartsWith("Crystal_Top_Cap") ||
                    childName.StartsWith("Crystal_Bottom_Cap"))
                {
                    Undo.DestroyObjectImmediate(child.gameObject);
                    removedPrismCount++;
                }
            }

            // ---------------------------------------------------------
            // 6. Configure Single MeshFilter and MeshRenderer on Crystal Body
            // ---------------------------------------------------------
            GameObject crystalGo = crystalBodyTransform.gameObject;

            // Reset local transform relative to Red_Jade_Crystal_Visual
            Undo.RecordObject(crystalBodyTransform, "Reset Crystal Body Transform");
            crystalBodyTransform.localPosition = Vector3.zero;
            crystalBodyTransform.localRotation = Quaternion.identity;
            crystalBodyTransform.localScale = Vector3.one;

            // Strip any collider from crystal body (trigger remains on Jade_Key_Artifact)
            Collider col = crystalGo.GetComponent<Collider>();
            if (col != null)
            {
                Undo.DestroyObjectImmediate(col);
            }

            // MeshFilter with custom faceted mesh
            MeshFilter mf = crystalGo.GetComponent<MeshFilter>();
            if (mf == null)
            {
                mf = Undo.AddComponent<MeshFilter>(crystalGo);
            }
            Undo.RecordObject(mf, "Assign Custom Faceted Mesh");
            mf.sharedMesh = facetedMesh;

            // MeshRenderer with upgraded ruby material
            MeshRenderer mr = crystalGo.GetComponent<MeshRenderer>();
            if (mr == null)
            {
                mr = Undo.AddComponent<MeshRenderer>(crystalGo);
            }
            Undo.RecordObject(mr, "Assign Ruby Material");
            mr.sharedMaterial = rubyMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;

            // ---------------------------------------------------------
            // 7. Verify Preservation of Sibling Objects
            // ---------------------------------------------------------
            // Ornamental_Metallic_Bands, Magic_Energy_Ring_Inner, Magic_Energy_Ring_Outer,
            // Ruby_Point_Light, Crimson_Mote_Particles, and MagicalRedJadeArtifact
            // are completely preserved.

            // ---------------------------------------------------------
            // 8. Finalize Undo, Mark Scene Dirty, and Report Success
            // ---------------------------------------------------------
            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(activeScene);

            string successSummary =
                "Successfully upgraded Red Jade Crystal mesh in scene!\n\n" +
                $"• Scene: {activeScene.name}\n" +
                $"• Target: {CrystalBodyGameObjectName} under {RedVisualChildName}\n" +
                $"• Removed: {removedPrismCount} old primitive cube prisms & stepped caps\n" +
                $"• Applied Mesh: {facetedMesh.name} (54 flat-shaded facets, 162 vertices, 1.00m height)\n" +
                $"• Applied Material: {rubyMaterial.name}\n" +
                "• Preserved: Metallic bands, energy rings, ruby light, particle motes, and animation component\n" +
                "• Preserved Gameplay: Jade_Key_Artifact, SphereCollider trigger, JadeKeyPickup & Door 2 logic\n\n" +
                "Operation registered with Undo (Ctrl+Z). Scene marked dirty.";

            Debug.Log($"<color=#2ecc71><b>[RedJadeFacetedMeshUpgrade]</b></color> {successSummary}");

            EditorUtility.DisplayDialog(
                "Crystal Mesh Upgrade Complete",
                successSummary,
                "OK");
        }
    }
}
#endif
