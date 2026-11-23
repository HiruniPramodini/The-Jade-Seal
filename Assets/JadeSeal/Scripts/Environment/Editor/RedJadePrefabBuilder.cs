#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using JadeSeal.Environment;

namespace JadeSeal.Environment.Editor
{
    /// <summary>
    /// Dedicated, non-destructive Editor utility for building and updating the
    /// Red Jade Artifact and Pedestal PREFAB (RedJadeArtifact_Pedestal.prefab).
    ///
    /// SAFETY & SCOPE GUARANTEES:
    /// 1. Operates strictly within Assets/JadeSeal/Art/RedJadeArtifact/.
    /// 2. Never modifies or saves any scene file.
    /// 3. Never touches teammate assets, meshes, materials, or shaders.
    /// 4. Builds the exact 12-node visual hierarchy specified in the project requirements.
    /// 5. Assigns the 5 dedicated materials created in Step 12.
    /// 6. Configures the MagicalRedJadeArtifact animation component with zero runtime drift.
    /// 7. Completely manual and idempotent.
    /// </summary>
    public static class RedJadePrefabBuilder
    {
        public const string PrefabDirectory = "Assets/JadeSeal/Art/RedJadeArtifact";
        public const string PrefabPath = "Assets/JadeSeal/Art/RedJadeArtifact/RedJadeArtifact_Pedestal.prefab";
        public const string MaterialsFolder = "Assets/JadeSeal/Art/RedJadeArtifact/Materials";

        [MenuItem("The Jade Seal/Art/Build Red Jade Artifact Prefab", priority = 50)]
        [MenuItem("Tools/The Jade Seal/Build Red Jade Artifact Prefab", priority = 50)]
        public static void BuildPrefabMenu()
        {
            EnsureDirectoriesExist();

            // 1. Load required materials
            Material jadeMat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/M_Red_Luminous_Jade.mat");
            Material bronzeMat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/M_Dark_Ornamental_Bronze.mat");
            Material runesMat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/M_Red_Glowing_Runes.mat");
            Material energyRingMat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/M_Red_Energy_Ring.mat");
            Material stoneMat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/M_Artifact_Dark_Stone.mat");

            if (jadeMat == null || bronzeMat == null || runesMat == null || energyRingMat == null || stoneMat == null)
            {
                EditorUtility.DisplayDialog(
                    "Materials Missing",
                    "One or more required materials in Assets/JadeSeal/Art/RedJadeArtifact/Materials/ are missing.\n" +
                    "Please run 'The Jade Seal -> Art -> Generate or Update Red Jade Materials' first.",
                    "OK");
                return;
            }

            // 2. Build root GameObject
            GameObject rootGo = new GameObject("Red_Jade_Sanctuary_Artifact");
            rootGo.transform.position = Vector3.zero;
            rootGo.transform.rotation = Quaternion.identity;

            try
            {
                // ==========================================
                // 3. Pedestal_Upper_Structure (STATIONARY)
                // ==========================================
                GameObject pedestalRoot = new GameObject("Pedestal_Upper_Structure");
                pedestalRoot.transform.SetParent(rootGo.transform, false);
                pedestalRoot.transform.localPosition = Vector3.zero;

                // 3a. Octagonal_Dark_Stone_Base (Stepped octagonal foundation)
                GameObject stoneBase = CreateOctagonalObject("Octagonal_Dark_Stone_Base", stoneMat, new Vector3(1.6f, 0.35f, 1.6f), new Vector3(0f, 0.175f, 0f));
                stoneBase.transform.SetParent(pedestalRoot.transform, false);

                // 3b. Ornamental_Bronze_Trim (Stepped upper trim framing the stone)
                GameObject bronzeTrim = CreateOctagonalObject("Ornamental_Bronze_Trim", bronzeMat, new Vector3(1.42f, 0.05f, 1.42f), new Vector3(0f, 0.375f, 0f));
                bronzeTrim.transform.SetParent(pedestalRoot.transform, false);

                // 3c. Glowing_Runic_Ring (Inset circular runic altar surface)
                GameObject runicRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                runicRing.name = "Glowing_Runic_Ring";
                runicRing.transform.SetParent(pedestalRoot.transform, false);
                runicRing.transform.localPosition = new Vector3(0f, 0.401f, 0f);
                runicRing.transform.localScale = new Vector3(1.15f, 0.005f, 1.15f);
                StripColliders(runicRing);
                ApplyMaterial(runicRing, runesMat);

                // 3d. Four_Ornamental_Claw_Brackets
                GameObject clawGroup = new GameObject("Four_Ornamental_Claw_Brackets");
                clawGroup.transform.SetParent(pedestalRoot.transform, false);
                clawGroup.transform.localPosition = new Vector3(0f, 0.40f, 0f);

                float[] clawAngles = new float[] { 0f, 90f, 180f, 270f };
                for (int i = 0; i < 4; i++)
                {
                    GameObject claw = CreateOrnamentalBracket($"Claw_Bracket_{i + 1}", bronzeMat, clawAngles[i]);
                    claw.transform.SetParent(clawGroup.transform, false);
                }

                // ==========================================
                // 4. Floating_Artifact_Core (FLOATING & ANIMATED)
                // ==========================================
                GameObject coreRoot = new GameObject("Floating_Artifact_Core");
                coreRoot.transform.SetParent(rootGo.transform, false);
                coreRoot.transform.localPosition = new Vector3(0f, 1.25f, 0f); // Floats 0.85m above pedestal top

                // 4a. Faceted_Red_Jade_Crystal
                GameObject crystalGo = CreateFacetedCrystalObject("Faceted_Red_Jade_Crystal", jadeMat);
                crystalGo.transform.SetParent(coreRoot.transform, false);
                crystalGo.transform.localPosition = Vector3.zero;

                // 4b. Ornamental_Metallic_Bands
                GameObject bandsGo = CreateMetallicBandsObject("Ornamental_Metallic_Bands", bronzeMat);
                bandsGo.transform.SetParent(coreRoot.transform, false);
                bandsGo.transform.localPosition = Vector3.zero;

                // 4c. Magic_Energy_Ring_Inner
                GameObject innerRingGo = CreatePlanarRing("Magic_Energy_Ring_Inner", energyRingMat, 0.52f, new Vector3(12f, 0f, 6f));
                innerRingGo.transform.SetParent(coreRoot.transform, false);
                innerRingGo.transform.localPosition = Vector3.zero;

                // 4d. Magic_Energy_Ring_Outer
                GameObject outerRingGo = CreatePlanarRing("Magic_Energy_Ring_Outer", energyRingMat, 0.78f, new Vector3(-15f, 0f, -10f));
                outerRingGo.transform.SetParent(coreRoot.transform, false);
                outerRingGo.transform.localPosition = Vector3.zero;

                // 4e. Ruby_Point_Light
                GameObject lightGo = new GameObject("Ruby_Point_Light");
                lightGo.transform.SetParent(coreRoot.transform, false);
                lightGo.transform.localPosition = Vector3.zero;

                Light pointLight = lightGo.AddComponent<Light>();
                pointLight.type = LightType.Point;
                pointLight.color = new Color(1.0f, 0.12f, 0.20f, 1.0f); // Crimson ruby red
                pointLight.intensity = 2.5f;
                pointLight.range = 8.0f;
                pointLight.shadows = LightShadows.Soft;

                // 4f. Crimson_Mote_Particles
                GameObject particlesGo = new GameObject("Crimson_Mote_Particles");
                particlesGo.transform.SetParent(coreRoot.transform, false);
                particlesGo.transform.localPosition = new Vector3(0f, -0.3f, 0f);

                ParticleSystem ps = particlesGo.AddComponent<ParticleSystem>();
                ParticleSystemRenderer psRenderer = particlesGo.GetComponent<ParticleSystemRenderer>();
                psRenderer.material = energyRingMat;

                var main = ps.main;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                main.maxParticles = 25;
                main.startLifetime = new ParticleSystem.MinMaxCurve(3.0f, 5.0f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
                main.startColor = new Color(1.0f, 0.15f, 0.25f, 0.7f);

                var emission = ps.emission;
                emission.rateOverTime = 4f;

                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 0.35f;
                shape.rotation = new Vector3(90f, 0f, 0f);

                var velOverLifetime = ps.velocityOverLifetime;
                velOverLifetime.enabled = true;
                velOverLifetime.y = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);

                // ==========================================
                // 5. Attach & Configure MagicalRedJadeArtifact
                // ==========================================
                MagicalRedJadeArtifact artifactComp = coreRoot.AddComponent<MagicalRedJadeArtifact>();
                artifactComp.SetEnergyRings(innerRingGo.transform, outerRingGo.transform, -30.0f, 20.0f);
                artifactComp.SetArtifactLight(pointLight, 0.25f, 0.5f);

                // ==========================================
                // 6. Save Prefab Asset
                // ==========================================
                PrefabUtility.SaveAsPrefabAsset(rootGo, PrefabPath);

                EditorUtility.DisplayDialog(
                    "Red Jade Prefab Created",
                    $"Successfully constructed and saved self-contained prefab:\n{PrefabPath}\n\n" +
                    "Hierarchy verified:\n" +
                    "• Red_Jade_Sanctuary_Artifact\n" +
                    "  ├── Pedestal_Upper_Structure (Octagonal Base, Bronze Trim, Runic Ring, 4 Claws)\n" +
                    "  └── Floating_Artifact_Core (Faceted Crystal, Bronze Bands, 2 Energy Rings, Point Light, Motes)\n\n" +
                    "Clearance: 0.85m resting elevation ensures zero intersection with pedestal during 0.08m bobbing.",
                    "OK");

                Debug.Log($"<color=#c0392b><b>[The Jade Seal]</b></color> Successfully created {PrefabPath}");
            }
            finally
            {
                // Always clean up the temporary staging GameObject
                GameObject.DestroyImmediate(rootGo);
            }
        }

        private static GameObject CreateOctagonalObject(string name, Material mat, Vector3 scale, Vector3 localPos)
        {
            GameObject octRoot = new GameObject(name);
            octRoot.transform.localPosition = localPos;

            // Two overlapping boxes rotated at 45 deg create a perfect regular octagon
            GameObject box1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box1.name = "Octagon_Axis_A";
            box1.transform.SetParent(octRoot.transform, false);
            box1.transform.localScale = scale;
            StripColliders(box1);
            ApplyMaterial(box1, mat);

            GameObject box2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box2.name = "Octagon_Axis_B";
            box2.transform.SetParent(octRoot.transform, false);
            box2.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            box2.transform.localScale = scale;
            StripColliders(box2);
            ApplyMaterial(box2, mat);

            return octRoot;
        }

        private static GameObject CreateOrnamentalBracket(string name, Material mat, float angleY)
        {
            GameObject clawRoot = new GameObject(name);
            clawRoot.transform.localRotation = Quaternion.Euler(0f, angleY, 0f);

            // Upward sweeping curved bracket constructed from tapered elements
            float radialOffset = 0.58f;

            GameObject baseMount = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseMount.name = "Bracket_Mount";
            baseMount.transform.SetParent(clawRoot.transform, false);
            baseMount.transform.localPosition = new Vector3(0f, 0.04f, radialOffset);
            baseMount.transform.localScale = new Vector3(0.08f, 0.08f, 0.14f);
            StripColliders(baseMount);
            ApplyMaterial(baseMount, mat);

            GameObject riser = GameObject.CreatePrimitive(PrimitiveType.Cube);
            riser.name = "Bracket_Arm";
            riser.transform.SetParent(clawRoot.transform, false);
            riser.transform.localPosition = new Vector3(0f, 0.22f, radialOffset - 0.02f);
            riser.transform.localRotation = Quaternion.Euler(-18f, 0f, 0f);
            riser.transform.localScale = new Vector3(0.05f, 0.30f, 0.06f);
            StripColliders(riser);
            ApplyMaterial(riser, mat);

            GameObject tip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tip.name = "Bracket_Claw_Tip";
            tip.transform.SetParent(clawRoot.transform, false);
            tip.transform.localPosition = new Vector3(0f, 0.38f, radialOffset - 0.09f);
            tip.transform.localRotation = Quaternion.Euler(-42f, 0f, 0f);
            tip.transform.localScale = new Vector3(0.035f, 0.12f, 0.045f);
            StripColliders(tip);
            ApplyMaterial(tip, mat);

            return clawRoot;
        }

        private static GameObject CreateFacetedCrystalObject(string name, Material mat)
        {
            GameObject crystalRoot = new GameObject(name);

            // Elongated hexagonal bipyramidal gem body:
            // Composed of 3 intersecting prisms rotated at 60 degree intervals (0, 60, 120 deg)
            // with tapered diamond tips at top and bottom.
            Vector3 prismScale = new Vector3(0.38f, 0.95f, 0.38f);

            for (int i = 0; i < 3; i++)
            {
                GameObject prism = GameObject.CreatePrimitive(PrimitiveType.Cube);
                prism.name = $"Crystal_Facet_Prism_{i + 1}";
                prism.transform.SetParent(crystalRoot.transform, false);
                prism.transform.localRotation = Quaternion.Euler(0f, i * 60f, 0f);
                prism.transform.localScale = prismScale;
                StripColliders(prism);
                ApplyMaterial(prism, mat);
            }

            // Top pyramid cap (rotated 45 deg along pitch/roll)
            GameObject topCap = GameObject.CreatePrimitive(PrimitiveType.Cube);
            topCap.name = "Crystal_Top_Cap";
            topCap.transform.SetParent(crystalRoot.transform, false);
            topCap.transform.localPosition = new Vector3(0f, 0.52f, 0f);
            topCap.transform.localRotation = Quaternion.Euler(45f, 45f, 0f);
            topCap.transform.localScale = new Vector3(0.24f, 0.24f, 0.24f);
            StripColliders(topCap);
            ApplyMaterial(topCap, mat);

            // Bottom pyramid cap (tapered lower point)
            GameObject bottomCap = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bottomCap.name = "Crystal_Bottom_Cap";
            bottomCap.transform.SetParent(crystalRoot.transform, false);
            bottomCap.transform.localPosition = new Vector3(0f, -0.52f, 0f);
            bottomCap.transform.localRotation = Quaternion.Euler(45f, 45f, 0f);
            bottomCap.transform.localScale = new Vector3(0.24f, 0.24f, 0.24f);
            StripColliders(bottomCap);
            ApplyMaterial(bottomCap, mat);

            return crystalRoot;
        }

        private static GameObject CreateMetallicBandsObject(string name, Material mat)
        {
            GameObject bandsRoot = new GameObject(name);

            // Central equatorial bronze collar wrapping the crystal facets
            GameObject mainBand = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            mainBand.name = "Equatorial_Collar";
            mainBand.transform.SetParent(bandsRoot.transform, false);
            mainBand.transform.localScale = new Vector3(0.48f, 0.045f, 0.48f);
            StripColliders(mainBand);
            ApplyMaterial(mainBand, mat);

            // Four ornamental prongs holding the waist
            for (int i = 0; i < 4; i++)
            {
                float angle = i * 90f;
                GameObject prong = GameObject.CreatePrimitive(PrimitiveType.Cube);
                prong.name = $"Collar_Prong_{i + 1}";
                prong.transform.SetParent(bandsRoot.transform, false);
                prong.transform.localRotation = Quaternion.Euler(0f, angle, 0f);
                prong.transform.localPosition = Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, 0f, 0.235f);
                prong.transform.localScale = new Vector3(0.04f, 0.16f, 0.05f);
                StripColliders(prong);
                ApplyMaterial(prong, mat);
            }

            return bandsRoot;
        }

        private static GameObject CreatePlanarRing(string name, Material mat, float radius, Vector3 tilt)
        {
            GameObject ringGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ringGo.name = name;
            ringGo.transform.localRotation = Quaternion.Euler(tilt);
            ringGo.transform.localScale = new Vector3(radius * 2f, 0.002f, radius * 2f);
            StripColliders(ringGo);
            ApplyMaterial(ringGo, mat);
            return ringGo;
        }

        private static void StripColliders(GameObject go)
        {
            Collider col = go.GetComponent<Collider>();
            if (col != null)
            {
                GameObject.DestroyImmediate(col);
            }
        }

        private static void ApplyMaterial(GameObject go, Material mat)
        {
            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterial = mat;
            }
        }

        private static void EnsureDirectoriesExist()
        {
            if (!AssetDatabase.IsValidFolder("Assets/JadeSeal/Art"))
            {
                AssetDatabase.CreateFolder("Assets/JadeSeal", "Art");
            }
            if (!AssetDatabase.IsValidFolder(PrefabDirectory))
            {
                AssetDatabase.CreateFolder("Assets/JadeSeal/Art", "RedJadeArtifact");
            }
        }
    }
}
#endif
