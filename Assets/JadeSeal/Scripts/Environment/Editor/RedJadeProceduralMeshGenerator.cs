#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace JadeSeal.Environment.Editor
{
    /// <summary>
    /// Dedicated Unity Editor procedural mesh generator for creating the custom,
    /// sharply faceted, irregular Red Jade crystal geometry.
    ///
    /// REPLACES PRIMITIVE STACKING:
    /// Instead of overlapping cubes or cylinders, this tool generates a true single-mesh
    /// elongated faceted gemstone (RedJade_FacetedCrystal.asset) using the Unity Mesh API.
    ///
    /// GEOMETRIC FEATURES:
    /// 1. Sharp asymmetric upper apex tip (+0.52m).
    /// 2. 9-sided irregular upper shoulder ring (+0.25m to +0.32m, radius 0.13m to 0.18m).
    /// 3. 9-sided asymmetric waist/girth ring (-0.04m to +0.05m, radius 0.20m to 0.25m).
    /// 4. 9-sided narrow lower taper ring (-0.24m to -0.31m, radius 0.11m to 0.15m).
    /// 5. Sharp asymmetric lower keel tip (-0.48m).
    /// 6. Total height = 1.00m (optimal for 0.50 scale in shrine lotus dish).
    /// 7. 54 flat-shaded polygonal facets (162 unshared vertices with split outward normals).
    /// 8. Clean UV coordinates without seam distortion.
    /// 9. Deterministic generation and in-place asset updating (preserves asset GUIDs).
    /// </summary>
    public static class RedJadeProceduralMeshGenerator
    {
        public const string OutputDirectory = "Assets/JadeSeal/Art/RedJadeArtifact/Meshes";
        public const string MeshAssetPath = "Assets/JadeSeal/Art/RedJadeArtifact/Meshes/RedJade_FacetedCrystal.asset";

        [MenuItem("The Jade Seal/Art/Generate Red Jade Crystal Mesh", priority = 40)]
        [MenuItem("Tools/The Jade Seal/Generate Red Jade Crystal Mesh", priority = 40)]
        public static void GenerateMeshMenu()
        {
            EnsureDirectoriesExist();

            Mesh crystalMesh = BuildFacetedCrystalMesh();
            if (crystalMesh == null)
            {
                EditorUtility.DisplayDialog(
                    "Generation Failed",
                    "Failed to construct the procedural crystal mesh.",
                    "OK");
                return;
            }

            // Save or update asset in-place to preserve GUID
            Mesh existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshAssetPath);
            if (existingMesh != null)
            {
                existingMesh.Clear();
                existingMesh.name = crystalMesh.name;
                existingMesh.vertices = crystalMesh.vertices;
                existingMesh.triangles = crystalMesh.triangles;
                existingMesh.normals = crystalMesh.normals;
                existingMesh.uv = crystalMesh.uv;
                existingMesh.tangents = crystalMesh.tangents;
                existingMesh.bounds = crystalMesh.bounds;

                EditorUtility.SetDirty(existingMesh);
                AssetDatabase.SaveAssets();

                UnityEngine.Object.DestroyImmediate(crystalMesh);
            }
            else
            {
                AssetDatabase.CreateAsset(crystalMesh, MeshAssetPath);
                AssetDatabase.SaveAssets();
            }

            AssetDatabase.Refresh();

            string summary =
                "Successfully generated custom Red Jade Crystal mesh!\n\n" +
                $"• Asset: {MeshAssetPath}\n" +
                "• Height: 1.00m (Apex +0.52m to Keel -0.48m)\n" +
                "• Max Girth Radius: 0.25m (Width ~0.50m)\n" +
                "• Topology: 54 flat-shaded facets, 162 vertices, 54 triangles\n" +
                "• Shading: Split per-face normals for crisp specular gemstone glints\n" +
                "• UVs: Cylindrical mapping with seam wrap handling\n\n" +
                "The mesh asset is ready for assignment to Red Jade materials and prefabs.";

            Debug.Log($"<color=#2ecc71><b>[RedJadeProceduralMeshGenerator]</b></color> {summary}");

            EditorUtility.DisplayDialog(
                "Crystal Mesh Generated",
                summary,
                "OK");
        }

        /// <summary>
        /// Builds the deterministic, asymmetric, flat-shaded gemstone mesh using Unity Mesh API.
        /// </summary>
        public static Mesh BuildFacetedCrystalMesh()
        {
            // 1. Define Key Asymmetric Terminations
            Vector3 apex = new Vector3(0.015f, 0.52f, 0.010f); // Subtle organic tilt
            Vector3 keel = new Vector3(-0.010f, -0.48f, -0.015f); // Crisp lower termination

            // 2. Define Ring Profiles (9 vertices per ring for rich polygonal silhouette)
            const int n = 9;

            // Shoulder Ring (Tapered upper transition)
            float[] shoulderRadii = new float[] { 0.15f, 0.17f, 0.14f, 0.18f, 0.15f, 0.16f, 0.13f, 0.17f, 0.15f };
            float[] shoulderHeights = new float[] { 0.28f, 0.31f, 0.26f, 0.30f, 0.27f, 0.32f, 0.29f, 0.25f, 0.30f };

            // Waist Ring (Maximum body width with asymmetric facet breaks)
            float[] waistRadii = new float[] { 0.22f, 0.25f, 0.21f, 0.24f, 0.20f, 0.25f, 0.22f, 0.23f, 0.21f };
            float[] waistHeights = new float[] { 0.02f, -0.02f, 0.05f, -0.01f, 0.04f, -0.03f, 0.03f, 0.01f, -0.04f };

            // Lower Taper Ring (Narrowing toward keel)
            float[] lowerRadii = new float[] { 0.13f, 0.11f, 0.14f, 0.12f, 0.15f, 0.12f, 0.14f, 0.11f, 0.13f };
            float[] lowerHeights = new float[] { -0.28f, -0.25f, -0.30f, -0.26f, -0.29f, -0.31f, -0.27f, -0.30f, -0.26f };

            Vector3[] shoulderPts = new Vector3[n];
            Vector3[] waistPts = new Vector3[n];
            Vector3[] lowerPts = new Vector3[n];

            for (int i = 0; i < n; i++)
            {
                float baseAngle = (i * 2.0f * Mathf.PI / n);

                // Irregular angle offsets create varied facet widths (some broad, some narrow)
                float angS = baseAngle + 0.05f * Mathf.Sin(i * 1.7f);
                shoulderPts[i] = new Vector3(shoulderRadii[i] * Mathf.Cos(angS), shoulderHeights[i], shoulderRadii[i] * Mathf.Sin(angS));

                float angW = baseAngle + 0.15f + 0.04f * Mathf.Cos(i * 2.1f);
                waistPts[i] = new Vector3(waistRadii[i] * Mathf.Cos(angW), waistHeights[i], waistRadii[i] * Mathf.Sin(angW));

                float angL = baseAngle - 0.10f + 0.06f * Mathf.Sin(i * 1.3f);
                lowerPts[i] = new Vector3(lowerRadii[i] * Mathf.Cos(angL), lowerHeights[i], lowerRadii[i] * Mathf.Sin(angL));
            }

            // 3. Assemble Flat-Shaded Triangle Faces
            List<Vector3> verts = new List<Vector3>(162);
            List<int> tris = new List<int>(162);
            List<Vector3> norms = new List<Vector3>(162);
            List<Vector2> uvs = new List<Vector2>(162);

            // Total height range for UV normalization
            float minY = keel.y;
            float maxY = apex.y;

            // 3a. Apex Fan: Apex -> Shoulder[nxt] -> Shoulder[curr]
            for (int i = 0; i < n; i++)
            {
                int nxt = (i + 1) % n;
                AddFlatTriangle(verts, tris, norms, uvs, apex, shoulderPts[nxt], shoulderPts[i], minY, maxY);
            }

            // 3b. Shoulder to Waist Quads (2 triangles per quad)
            for (int i = 0; i < n; i++)
            {
                int nxt = (i + 1) % n;
                // Quad: shoulder[i], shoulder[nxt], waist[nxt], waist[i]
                AddFlatTriangle(verts, tris, norms, uvs, shoulderPts[i], shoulderPts[nxt], waistPts[nxt], minY, maxY);
                AddFlatTriangle(verts, tris, norms, uvs, shoulderPts[i], waistPts[nxt], waistPts[i], minY, maxY);
            }

            // 3c. Waist to Lower Taper Quads (2 triangles per quad)
            for (int i = 0; i < n; i++)
            {
                int nxt = (i + 1) % n;
                // Quad: waist[i], waist[nxt], lower[nxt], lower[i]
                AddFlatTriangle(verts, tris, norms, uvs, waistPts[i], waistPts[nxt], lowerPts[nxt], minY, maxY);
                AddFlatTriangle(verts, tris, norms, uvs, waistPts[i], lowerPts[nxt], lowerPts[i], minY, maxY);
            }

            // 3d. Lower Taper to Keel Fan: Lower[curr] -> Lower[nxt] -> Keel
            for (int i = 0; i < n; i++)
            {
                int nxt = (i + 1) % n;
                AddFlatTriangle(verts, tris, norms, uvs, lowerPts[i], lowerPts[nxt], keel, minY, maxY);
            }

            // 4. Construct and Return Unity Mesh
            Mesh mesh = new Mesh();
            mesh.name = "RedJade_FacetedCrystal";
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            return mesh;
        }

        private static void AddFlatTriangle(
            List<Vector3> verts,
            List<int> tris,
            List<Vector3> norms,
            List<Vector2> uvs,
            Vector3 v0,
            Vector3 v1,
            Vector3 v2,
            float minY,
            float maxY)
        {
            int baseIdx = verts.Count;

            // Calculate exact outward normal via Cross(v1 - v0, v2 - v0)
            Vector3 normal = Vector3.Cross(v1 - v0, v2 - v0).normalized;

            // Ensure outward orientation relative to crystal vertical axis
            Vector3 centroid = (v0 + v1 + v2) / 3.0f;
            Vector3 radial = new Vector3(centroid.x, 0f, centroid.z);
            if (Vector3.Dot(normal, radial) < 0f && normal.sqrMagnitude > 0.001f)
            {
                normal = -normal;
            }

            // Calculate UV coordinates
            Vector2 uv0 = CalculateUV(v0, minY, maxY);
            Vector2 uv1 = CalculateUV(v1, minY, maxY);
            Vector2 uv2 = CalculateUV(v2, minY, maxY);

            // Handle cylindrical UV seam crossing (wrap prevention)
            FixUVSeam(ref uv0, ref uv1, ref uv2);

            verts.Add(v0);
            verts.Add(v1);
            verts.Add(v2);

            norms.Add(normal);
            norms.Add(normal);
            norms.Add(normal);

            uvs.Add(uv0);
            uvs.Add(uv1);
            uvs.Add(uv2);

            tris.Add(baseIdx);
            tris.Add(baseIdx + 1);
            tris.Add(baseIdx + 2);
        }

        private static Vector2 CalculateUV(Vector3 pos, float minY, float maxY)
        {
            float u = Mathf.Atan2(pos.z, pos.x) / (2.0f * Mathf.PI) + 0.5f;
            float v = Mathf.InverseLerp(minY, maxY, pos.y);
            return new Vector2(u, v);
        }

        private static void FixUVSeam(ref Vector2 uv0, ref Vector2 uv1, ref Vector2 uv2)
        {
            // If any UV coordinate spans across the 0.0/1.0 boundary, unwrap it
            if (Mathf.Abs(uv0.x - uv1.x) > 0.5f)
            {
                if (uv0.x < uv1.x) uv0.x += 1.0f;
                else uv1.x += 1.0f;
            }
            if (Mathf.Abs(uv0.x - uv2.x) > 0.5f)
            {
                if (uv0.x < uv2.x) uv0.x += 1.0f;
                else uv2.x += 1.0f;
            }
            if (Mathf.Abs(uv1.x - uv2.x) > 0.5f)
            {
                if (uv1.x < uv2.x) uv1.x += 1.0f;
                else uv2.x += 1.0f;
            }
        }

        private static void EnsureDirectoriesExist()
        {
            if (!AssetDatabase.IsValidFolder("Assets/JadeSeal/Art"))
            {
                AssetDatabase.CreateFolder("Assets/JadeSeal", "Art");
            }
            if (!AssetDatabase.IsValidFolder("Assets/JadeSeal/Art/RedJadeArtifact"))
            {
                AssetDatabase.CreateFolder("Assets/JadeSeal/Art", "RedJadeArtifact");
            }
            if (!AssetDatabase.IsValidFolder(OutputDirectory))
            {
                AssetDatabase.CreateFolder("Assets/JadeSeal/Art/RedJadeArtifact", "Meshes");
            }
        }
    }
}
#endif
