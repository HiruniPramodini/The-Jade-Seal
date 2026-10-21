using UnityEngine;
using UnityEditor;

namespace JadeSeal.EnemyAI.Editor
{
    public static class LightingDiagnostic
    {
        [MenuItem("The Jade Seal/Lighting/Diagnose and Fix White Overexposure", false, 30)]
        public static void FixOverexposure()
        {
            Light[] lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            Debug.Log($"<color=cyan>[LightingDiagnostic]</color> Found {lights.Length} lights in scene.");

            foreach (var l in lights)
            {
                Debug.Log($"[Light] '{l.gameObject.name}' Type: {l.type}, Intensity: {l.intensity}, Range: {l.range}, Enabled: {l.enabled}, Parent: {l.transform.parent?.name}");

                // Check if this is an imported rogue light from FBX (often inside characters or props with massive intensity)
                string objName = l.gameObject.name.ToLower();
                Transform p = l.transform.parent;
                bool isUnderCharacter = false;
                while (p != null)
                {
                    string pName = p.name.ToLower();
                    if (pName.Contains("archer") || pName.Contains("model") || pName.Contains("ma") || pName.Contains("sakura"))
                    {
                        isUnderCharacter = true;
                        break;
                    }
                    p = p.parent;
                }

                if (isUnderCharacter || (objName.StartsWith("light") && l.type == LightType.Point && l.transform.parent != null) || l.intensity > 5f)
                {
                    Debug.Log($"<color=red>[LightingDiagnostic] Disabling rogue blown-out light: '{l.gameObject.name}' on '{l.transform.parent?.name}' (Intensity: {l.intensity})</color>");
                    Undo.RecordObject(l, "Disable Rogue Blown Out Light");
                    l.enabled = false;
                    l.intensity = 0f;
                }
            }

            // Also check Directional Light
            var dirLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (var dl in dirLights)
            {
                if (dl.type == LightType.Directional && dl.intensity > 2.0f)
                {
                    Debug.Log($"[LightingDiagnostic] Adjusting Directional Light '{dl.name}' intensity from {dl.intensity} to 1.0f");
                    Undo.RecordObject(dl, "Normalize Directional Light");
                    dl.intensity = 1.0f;
                }
            }

            Debug.Log("<color=#00FF99><b>[LightingDiagnostic] Completed! Blown-out lights disabled and normal lighting restored.</b></color>");
        }
    }
}
