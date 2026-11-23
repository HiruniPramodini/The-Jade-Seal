#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEditor;

namespace JadeSeal.Environment.Editor
{
    /// <summary>
    /// Dedicated, safe, non-destructive Editor utility for upgrading the Red Jade
    /// gemstone material (M_Red_Luminous_Jade.mat).
    ///
    /// APPLIES USER-APPROVED RUBY GEMSTONE PARAMETERS:
    /// 1. Shader: Unity Built-in Standard shader.
    /// 2. Albedo: Vivid ruby crimson (sRGB #D60D2E).
    /// 3. Metallic: 0.0 (pure dielectric gemstone).
    /// 4. Smoothness: 0.72 (crisp facet highlights).
    /// 5. Reflection Control: Disables glossy sky reflections (_GLOSSYREFLECTIONS_OFF)
    ///    to prevent cyan skybox contamination that caused the previous purple/brown hue.
    /// 6. Emission: Deep saturated HDR crimson (3.5f, 0.12f, 0.25f).
    /// 7. Completely idempotent and undo-compatible.
    /// </summary>
    public static class RedJadeRubyMaterialUpgrade
    {
        public const string MaterialPath = "Assets/JadeSeal/Art/RedJadeArtifact/Materials/M_Red_Luminous_Jade.mat";

        [MenuItem("The Jade Seal/Art/Upgrade Red Jade Ruby Material", priority = 45)]
        [MenuItem("Tools/The Jade Seal/Upgrade Red Jade Ruby Material", priority = 45)]
        public static void UpgradeMaterialMenu()
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat == null)
            {
                EditorUtility.DisplayDialog(
                    "Material Missing",
                    $"Could not locate target material at:\n{MaterialPath}\n\n" +
                    "Please ensure the material exists before upgrading.",
                    "OK");
                return;
            }

            // Record Undo
            Undo.RecordObject(mat, "Upgrade Red Jade Ruby Material");

            // Ensure standard shader
            if (mat.shader == null || mat.shader.name != "Standard")
            {
                Shader standardShader = Shader.Find("Standard");
                if (standardShader != null)
                {
                    mat.shader = standardShader;
                }
            }

            // 1. Albedo: Vivid Ruby Crimson (sRGB #D60D2E)
            Color rubyAlbedo = new Color(0.8392f, 0.0510f, 0.1804f, 1.0f);
            mat.SetColor("_Color", rubyAlbedo);

            // 2. Metallic: 0.0 (pure dielectric gemstone)
            mat.SetFloat("_Metallic", 0.0f);

            // 3. Smoothness: 0.72 (crisp specular glints along facet edges)
            mat.SetFloat("_Glossiness", 0.72f);

            // 4. Eliminate Cool Sky Reflection Contamination
            // Disabling glossy reflections prevents the cyan-blue skybox/fog from mixing with the red diffuse
            mat.SetFloat("_GlossyReflections", 0.0f);
            mat.EnableKeyword("_GLOSSYREFLECTIONS_OFF");

            // Keep direct specular highlights from lights active
            mat.SetFloat("_SpecularHighlights", 1.0f);
            mat.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");

            // 5. Intense Saturated Crimson HDR Emission (3.5, 0.12, 0.25)
            Color rubyEmission = new Color(3.5f, 0.12f, 0.25f, 1.0f);
            mat.SetColor("_EmissionColor", rubyEmission);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

            // 6. Surface mode: Opaque
            mat.SetFloat("_Mode", 0f);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            mat.SetInt("_ZWrite", 1);
            mat.renderQueue = -1;

            // Save changes
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();

            string summary =
                "Successfully upgraded M_Red_Luminous_Jade.mat!\n\n" +
                $"• Material: {MaterialPath}\n" +
                "• Shader: Standard (Built-in Render Pipeline)\n" +
                "• Albedo: Vivid Ruby Crimson (#D60D2E)\n" +
                "• Metallic: 0.0 (Pure gemstone dielectric)\n" +
                "• Smoothness: 0.72 (Crisp facet edge glints)\n" +
                "• Sky Reflections: Disabled (_GLOSSYREFLECTIONS_OFF) to prevent purple/brown sky contamination\n" +
                "• Emission: HDR Saturated Crimson (3.5, 0.12, 0.25)\n" +
                "• Direct Highlights: Active (_SpecularHighlights = 1)\n\n" +
                "The crystal will now render in bright, luminous ruby red under daylight!";

            Debug.Log($"<color=#2ecc71><b>[RedJadeRubyMaterialUpgrade]</b></color> {summary}");

            EditorUtility.DisplayDialog(
                "Ruby Material Upgraded",
                summary,
                "OK");
        }
    }
}
#endif
