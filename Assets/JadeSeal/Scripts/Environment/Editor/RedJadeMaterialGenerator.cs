#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace JadeSeal.Environment.Editor
{
    /// <summary>
    /// Safe, idempotent Editor utility for generating and configuring the visual materials
    /// required for the Red Jade Seal artifact.
    ///
    /// SAFETY & SCOPE GUARANTEES:
    /// 1. Restricted strictly to the dedicated folder: Assets/JadeSeal/Art/RedJadeArtifact/Materials/.
    /// 2. Never modifies any teammate scenes, materials, textures, shaders, or prefabs.
    /// 3. Manual-only execution via Unity Editor menu: "The Jade Seal/Art/Generate or Update Red Jade Materials".
    /// 4. Completely idempotent: safely updates existing materials or creates them if missing.
    /// 5. Validates shaders and texture references before assignment.
    /// </summary>
    public static class RedJadeMaterialGenerator
    {
        public const string TargetFolder = "Assets/JadeSeal/Art/RedJadeArtifact/Materials";
        public const string RadialTexturePath = "Assets/JadeSeal/Phase6/Zone3/Materials/Ravine_Mist_Soft.asset";

        [MenuItem("The Jade Seal/Art/Generate or Update Red Jade Materials", priority = 40)]
        [MenuItem("Tools/The Jade Seal/Generate or Update Red Jade Materials", priority = 40)]
        public static void GenerateMaterialsMenu()
        {
            EnsureDirectoryExists();

            Shader standardShader = Shader.Find("Standard");
            if (standardShader == null)
            {
                EditorUtility.DisplayDialog(
                    "Shader Missing",
                    "Could not find the Built-in Standard shader in this project.",
                    "OK");
                return;
            }

            Shader particleShader = Shader.Find("Particles/Standard Unlit");
            if (particleShader == null)
            {
                particleShader = Shader.Find("Legacy Shaders/Particles/Additive");
            }
            if (particleShader == null)
            {
                particleShader = standardShader;
            }

            Texture existingRadialTexture = AssetDatabase.LoadAssetAtPath<Texture>(RadialTexturePath);

            // 1. M_Red_Luminous_Jade.mat
            CreateOrUpdateStandardMaterial(
                "M_Red_Luminous_Jade",
                standardShader,
                albedoHex: "#7A0C14",
                metallic: 0.20f,
                smoothness: 0.85f,
                hasEmission: true,
                emissionHex: "#C41828",
                emissionIntensity: 2.2f);

            // 2. M_Dark_Ornamental_Bronze.mat
            CreateOrUpdateStandardMaterial(
                "M_Dark_Ornamental_Bronze",
                standardShader,
                albedoHex: "#2A201C",
                metallic: 0.75f,
                smoothness: 0.60f,
                hasEmission: false,
                emissionHex: "#000000",
                emissionIntensity: 0.0f);

            // 3. M_Red_Glowing_Runes.mat
            CreateOrUpdateStandardMaterial(
                "M_Red_Glowing_Runes",
                standardShader,
                albedoHex: "#1C1818",
                metallic: 0.05f,
                smoothness: 0.40f,
                hasEmission: true,
                emissionHex: "#FF2035",
                emissionIntensity: 2.0f);

            // 4. M_Red_Energy_Ring.mat
            CreateOrUpdateEnergyRingMaterial(
                "M_Red_Energy_Ring",
                particleShader,
                tintHex: "#E0102A",
                alpha: 0.85f,
                texture: existingRadialTexture);

            // 5. M_Artifact_Dark_Stone.mat
            CreateOrUpdateStandardMaterial(
                "M_Artifact_Dark_Stone",
                standardShader,
                albedoHex: "#1E2022",
                metallic: 0.05f,
                smoothness: 0.30f,
                hasEmission: false,
                emissionHex: "#000000",
                emissionIntensity: 0.0f);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Red Jade Materials Generated",
                $"Successfully configured the 5 Red Jade artifact materials in:\n{TargetFolder}\n\n" +
                "• M_Red_Luminous_Jade (Crimson crystal, metallic 0.20, smoothness 0.85, HDR emission)\n" +
                "• M_Dark_Ornamental_Bronze (Antique bronze bands, metallic 0.75, smoothness 0.60)\n" +
                "• M_Red_Glowing_Runes (Dark stone with cinnabar red emission)\n" +
                "• M_Red_Energy_Ring (Additive/Transparent red energy ring)\n" +
                "• M_Artifact_Dark_Stone (Dark carved slate pedestal topper)",
                "OK");

            Debug.Log($"<color=#c0392b><b>[The Jade Seal]</b></color> Successfully generated/updated Red Jade materials in {TargetFolder}.");
        }

        private static void CreateOrUpdateStandardMaterial(
            string materialName,
            Shader shader,
            string albedoHex,
            float metallic,
            float smoothness,
            bool hasEmission,
            string emissionHex,
            float emissionIntensity)
        {
            string assetPath = $"{TargetFolder}/{materialName}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            bool isNew = false;

            if (mat == null)
            {
                mat = new Material(shader) { name = materialName };
                isNew = true;
            }
            else
            {
                mat.shader = shader;
            }

            // Albedo
            if (ColorUtility.TryParseHtmlString(albedoHex, out Color albedoColor))
            {
                mat.color = albedoColor;
                mat.SetColor("_Color", albedoColor);
            }

            // Metallic & Smoothness
            mat.SetFloat("_Metallic", Mathf.Clamp01(metallic));
            mat.SetFloat("_Glossiness", Mathf.Clamp01(smoothness));

            // Standard Opaque settings
            mat.SetFloat("_Mode", 0.0f);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            mat.SetInt("_ZWrite", 1);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = -1;

            // Emission
            if (hasEmission && ColorUtility.TryParseHtmlString(emissionHex, out Color emissionBaseColor))
            {
                mat.EnableKeyword("_EMISSION");
                Color hdrEmission = emissionBaseColor * emissionIntensity;
                mat.SetColor("_EmissionColor", hdrEmission);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                mat.DisableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", Color.black);
            }

            EditorUtility.SetDirty(mat);

            if (isNew)
            {
                AssetDatabase.CreateAsset(mat, assetPath);
            }
        }

        private static void CreateOrUpdateEnergyRingMaterial(
            string materialName,
            Shader shader,
            string tintHex,
            float alpha,
            Texture texture)
        {
            string assetPath = $"{TargetFolder}/{materialName}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            bool isNew = false;

            if (mat == null)
            {
                mat = new Material(shader) { name = materialName };
                isNew = true;
            }
            else
            {
                mat.shader = shader;
            }

            if (ColorUtility.TryParseHtmlString(tintHex, out Color tintColor))
            {
                tintColor.a = alpha;
                mat.color = tintColor;
                mat.SetColor("_Color", tintColor);
            }

            if (texture != null)
            {
                mat.mainTexture = texture;
                mat.SetTexture("_MainTex", texture);
            }

            // Configure Additive/Transparent blending for Particles/Standard Unlit
            if (mat.HasProperty("_Mode"))
            {
                mat.SetFloat("_Mode", 4.0f); // Additive
            }
            if (mat.HasProperty("_SrcBlend"))
            {
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            }
            if (mat.HasProperty("_DstBlend"))
            {
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            }
            if (mat.HasProperty("_ZWrite"))
            {
                mat.SetInt("_ZWrite", 0);
            }
            if (mat.HasProperty("_Cull"))
            {
                mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off); // Double-sided
            }

            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat.SetOverrideTag("RenderType", "Transparent");

            EditorUtility.SetDirty(mat);

            if (isNew)
            {
                AssetDatabase.CreateAsset(mat, assetPath);
            }
        }

        private static void EnsureDirectoryExists()
        {
            if (!AssetDatabase.IsValidFolder("Assets/JadeSeal/Art"))
            {
                AssetDatabase.CreateFolder("Assets/JadeSeal", "Art");
            }
            if (!AssetDatabase.IsValidFolder("Assets/JadeSeal/Art/RedJadeArtifact"))
            {
                AssetDatabase.CreateFolder("Assets/JadeSeal/Art", "RedJadeArtifact");
            }
            if (!AssetDatabase.IsValidFolder(TargetFolder))
            {
                AssetDatabase.CreateFolder("Assets/JadeSeal/Art/RedJadeArtifact", "Materials");
            }
        }
    }
}
#endif
