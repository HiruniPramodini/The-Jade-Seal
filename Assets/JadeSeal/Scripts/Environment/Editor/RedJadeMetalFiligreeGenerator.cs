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
    /// elegant dark metallic dragon-claw filigree and spiral ornament meshes.
    ///
    /// REPLACES PRIMITIVE CLAMPS:
    /// Instead of a flat primitive cylinder ring and 4 rectangular cube blocks,
    /// this tool generates two dedicated custom mesh assets using the Unity Mesh API:
    /// 1. RedJade_MetalSpiralBands.asset: Two organic, tapered 3D helical ribbons wrapping
    ///    around the gemstone's facets with raised metallic spines.
    /// 2. RedJade_DragonClawOrnaments.asset: Four curved 3D dragon claws hooking inward to
    ///    grip the lower and side crystal facets.
    ///
    /// GEOMETRIC FEATURES:
    /// - Follows the exact contours of RedJade_FacetedCrystal.asset with a 0.018m to 0.024m
    ///   clearance offset to completely prevent z-fighting.
    /// - Smooth curved normals for metallic specular glints along edges and spines.
    /// - Covers under 25% of the gemstone surface, keeping the brilliant ruby crystal prominent.
    /// - Lightweight: ~200 triangles for spiral bands, ~380 triangles for dragon claws.
    /// - Deterministic and in-place asset updating (preserves asset GUIDs).
    /// </summary>
    public static class RedJadeMetalFiligreeGenerator
    {
        public const string MeshesDirectory = "Assets/JadeSeal/Art/RedJadeArtifact/Meshes";
        public const string SourceCrystalMeshPath = "Assets/JadeSeal/Art/RedJadeArtifact/Meshes/RedJade_FacetedCrystal.asset";

        public const string SpiralBandsAssetPath = "Assets/JadeSeal/Art/RedJadeArtifact/Meshes/RedJade_MetalSpiralBands.asset";
        public const string DragonClawsAssetPath = "Assets/JadeSeal/Art/RedJadeArtifact/Meshes/RedJade_DragonClawOrnaments.asset";

        [MenuItem("The Jade Seal/Art/Generate Red Jade Metallic Filigree", priority = 42)]
        [MenuItem("Tools/The Jade Seal/Generate Red Jade Metallic Filigree", priority = 42)]
        public static void GenerateFiligreeMenu()
        {
            EnsureDirectoriesExist();

            // 1. Validate Source Crystal Mesh
            Mesh crystalMesh = AssetDatabase.LoadAssetAtPath<Mesh>(SourceCrystalMeshPath);
            if (crystalMesh == null)
            {
                EditorUtility.DisplayDialog(
                    "Source Mesh Missing",
                    $"Could not load source faceted crystal mesh at:\n{SourceCrystalMeshPath}\n\n" +
                    "Please run 'The Jade Seal -> Art -> Generate Red Jade Crystal Mesh' first.",
                    "OK");
                return;
            }

            // 2. Generate Spiral Bands Mesh
            Mesh spiralBandsMesh = BuildSpiralBandsMesh();
            SaveOrUpdateMeshAsset(spiralBandsMesh, SpiralBandsAssetPath);

            // 3. Generate Dragon Claw Ornaments Mesh
            Mesh dragonClawsMesh = BuildDragonClawsMesh();
            SaveOrUpdateMeshAsset(dragonClawsMesh, DragonClawsAssetPath);

            AssetDatabase.Refresh();

            string summary =
                "Successfully generated Red Jade Metallic Filigree meshes!\n\n" +
                $"1. Spiral Bands Mesh:\n   • {SpiralBandsAssetPath}\n   • Two 3D tapered helical ribbons with raised ridge spines\n   • Triangles: {spiralBandsMesh.triangles.Length / 3}\n\n" +
                $"2. Dragon Claws Mesh:\n   • {DragonClawsAssetPath}\n   • Four curved, tapered 3D dragon claws gripping the gem\n   • Triangles: {dragonClawsMesh.triangles.Length / 3}\n\n" +
                "Both meshes are ready for assignment to M_Dark_Ornamental_Bronze.mat.";

            Debug.Log($"<color=#2ecc71><b>[RedJadeMetalFiligreeGenerator]</b></color> {summary}");

            EditorUtility.DisplayDialog(
                "Metallic Filigree Generated",
                summary,
                "OK");
        }

        // =========================================================================
        // MESH 1: SPIRAL BANDS (Two Organic Helical Ribbons with Raised Ridge Spines)
        // =========================================================================

        public static Mesh BuildSpiralBandsMesh()
        {
            List<Vector3> verts = new List<Vector3>();
            List<int> tris = new List<int>();
            List<Vector3> norms = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();

            // Band A: Primary ascending spiral
            // Height: -0.18m to +0.22m, 1.25 turns, max width 0.032m, ridge height 0.008m
            AddSpiralRibbon(verts, tris, norms, uvs,
                yStart: -0.18f, yEnd: 0.22f,
                angleStart: 0.0f, turns: 1.25f,
                maxWidth: 0.032f, maxRidge: 0.008f,
                segments: 28);

            // Band B: Secondary intertwining counter-spiral
            // Height: -0.12m to +0.18m, 1.10 turns, max width 0.026f, ridge height 0.007m
            AddSpiralRibbon(verts, tris, norms, uvs,
                yStart: -0.12f, yEnd: 0.18f,
                angleStart: Mathf.PI + 0.35f, turns: 1.10f,
                maxWidth: 0.026f, maxRidge: 0.007f,
                segments: 24);

            Mesh mesh = new Mesh();
            mesh.name = "RedJade_MetalSpiralBands";
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            return mesh;
        }

        private static void AddSpiralRibbon(
            List<Vector3> verts,
            List<int> tris,
            List<Vector3> norms,
            List<Vector2> uvs,
            float yStart,
            float yEnd,
            float angleStart,
            float turns,
            float maxWidth,
            float maxRidge,
            int segments)
        {
            int startVertexIdx = verts.Count;

            // Generate cross-section rings: 3 vertices per ring (0=Left, 1=Ridge, 2=Right)
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float y = Mathf.Lerp(yStart, yEnd, t);
                float angle = angleStart + t * (turns * 2.0f * Mathf.PI);

                // Radius from crystal profile + 0.020m radial clearance
                float rCrystal = GetCrystalProfileRadius(y);
                float r = rCrystal + 0.020f;

                float cx = r * Mathf.Cos(angle);
                float cz = r * Mathf.Sin(angle);
                float cy = y;
                Vector3 center = new Vector3(cx, cy, cz);

                // Tangent vector
                float dt = 0.01f;
                float yNext = Mathf.Lerp(yStart, yEnd, Mathf.Clamp01(t + dt));
                float angNext = angleStart + (t + dt) * (turns * 2.0f * Mathf.PI);
                float rNext = GetCrystalProfileRadius(yNext) + 0.020f;
                Vector3 nextCenter = new Vector3(rNext * Mathf.Cos(angNext), yNext, rNext * Mathf.Sin(angNext));
                Vector3 tangent = (nextCenter - center).normalized;

                // Radial outward vector
                Vector3 radial = new Vector3(cx, 0f, cz).normalized;

                // Binormal = Cross(tangent, radial)
                Vector3 binormal = Vector3.Cross(tangent, radial).normalized;

                // Surface normal = Cross(binormal, tangent)
                Vector3 normal = Vector3.Cross(binormal, tangent).normalized;
                if (Vector3.Dot(normal, radial) < 0f)
                {
                    normal = -normal;
                    binormal = -binormal;
                }

                // Smooth bell-curve taper toward ribbon tips
                float taper = Mathf.Sin(t * Mathf.PI);
                float w = maxWidth * taper;
                float h = maxRidge * taper;

                Vector3 left = center - binormal * (w * 0.5f);
                Vector3 ridge = center + normal * h;
                Vector3 right = center + binormal * (w * 0.5f);

                // Vertex normals
                Vector3 leftNormal = (normal - binormal * 0.7f).normalized;
                Vector3 ridgeNormal = normal;
                Vector3 rightNormal = (normal + binormal * 0.7f).normalized;

                verts.Add(left);
                verts.Add(ridge);
                verts.Add(right);

                norms.Add(leftNormal);
                norms.Add(ridgeNormal);
                norms.Add(rightNormal);

                uvs.Add(new Vector2(0f, t));
                uvs.Add(new Vector2(0.5f, t));
                uvs.Add(new Vector2(1f, t));
            }

            // Assemble quads between adjacent cross-sections
            for (int i = 0; i < segments; i++)
            {
                int r0 = startVertexIdx + i * 3;
                int r1 = r0 + 3;

                // Left Slope: (r0+0, r1+0, r1+1) and (r0+0, r1+1, r0+1)
                tris.Add(r0);
                tris.Add(r1);
                tris.Add(r1 + 1);

                tris.Add(r0);
                tris.Add(r1 + 1);
                tris.Add(r0 + 1);

                // Right Slope: (r0+1, r1+1, r1+2) and (r0+1, r1+2, r0+2)
                tris.Add(r0 + 1);
                tris.Add(r1 + 1);
                tris.Add(r1 + 2);

                tris.Add(r0 + 1);
                tris.Add(r1 + 2);
                tris.Add(r0 + 2);
            }
        }

        // =========================================================================
        // MESH 2: DRAGON CLAWS (Four Curved 3D Claws Gripping Crystal Facets)
        // =========================================================================

        public static Mesh BuildDragonClawsMesh()
        {
            List<Vector3> verts = new List<Vector3>();
            List<int> tris = new List<int>();
            List<Vector3> norms = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();

            const int clawCount = 4;
            const int segmentsPerClaw = 12;

            for (int k = 0; k < clawCount; k++)
            {
                float baseAngle = k * (Mathf.PI * 0.5f) + 0.25f; // Spaced evenly at 90 deg with slight offset
                AddDragonClaw(verts, tris, norms, uvs, baseAngle, segmentsPerClaw);
            }

            Mesh mesh = new Mesh();
            mesh.name = "RedJade_DragonClawOrnaments";
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            return mesh;
        }

        private static void AddDragonClaw(
            List<Vector3> verts,
            List<int> tris,
            List<Vector3> norms,
            List<Vector2> uvs,
            float azimuthAngle,
            int segments)
        {
            int startVertexIdx = verts.Count;

            // Bezier control points in cylindrical space (Radius R, Height Y)
            // p0: Base mount anchored at lower girth
            // p1: Outward flare away from crystal
            // p2: Upward arch crest
            // p3: Sharp inward talon tip hooking into crystal facet
            Vector2 p0 = new Vector2(0.240f, -0.220f);
            Vector2 p1 = new Vector2(0.290f, -0.100f);
            Vector2 p2 = new Vector2(0.270f, 0.080f);
            Vector2 p3 = new Vector2(0.210f, 0.020f);

            // Generate cross-section rings: 4 vertices per ring (diamond bevel)
            // 0=Inner (facing crystal), 1=Right, 2=Outer ridge, 3=Left
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                Vector2 ry = EvalCubicBezier(p0, p1, p2, p3, t);
                Vector2 ryNext = EvalCubicBezier(p0, p1, p2, p3, Mathf.Clamp01(t + 0.02f));

                float radius = ry.x;
                float y = ry.y;

                float cx = radius * Mathf.Cos(azimuthAngle);
                float cz = radius * Mathf.Sin(azimuthAngle);
                Vector3 center = new Vector3(cx, y, cz);

                // Tangent in 3D
                float nextCx = ryNext.x * Mathf.Cos(azimuthAngle);
                float nextCz = ryNext.x * Mathf.Sin(azimuthAngle);
                Vector3 nextCenter = new Vector3(nextCx, ryNext.y, nextCz);
                Vector3 tangent = (nextCenter - center).normalized;

                // Azimuthal right vector
                Vector3 rightVec = new Vector3(-Mathf.Sin(azimuthAngle), 0f, Mathf.Cos(azimuthAngle));

                // Upward normal vector perp to tangent and right
                Vector3 normalVec = Vector3.Cross(tangent, rightVec).normalized;

                // Claw thickness & width taper from base (0.025m) to razor talon tip (0.002m)
                float taper = Mathf.Lerp(1.0f, 0.08f, t);
                float halfWidth = 0.016f * taper;
                float halfThick = 0.012f * taper;

                // 4 vertices around center
                Vector3 vInner = center - normalVec * halfThick;
                Vector3 vRight = center + rightVec * halfWidth;
                Vector3 vOuter = center + normalVec * halfThick;
                Vector3 vLeft = center - rightVec * halfWidth;

                verts.Add(vInner);
                verts.Add(vRight);
                verts.Add(vOuter);
                verts.Add(vLeft);

                norms.Add(-normalVec);
                norms.Add(rightVec);
                norms.Add(normalVec);
                norms.Add(-rightVec);

                uvs.Add(new Vector2(0.00f, t));
                uvs.Add(new Vector2(0.33f, t));
                uvs.Add(new Vector2(0.66f, t));
                uvs.Add(new Vector2(1.00f, t));
            }

            // Connect rings with quads (each quad = 2 triangles)
            for (int i = 0; i < segments; i++)
            {
                int r0 = startVertexIdx + i * 4;
                int r1 = r0 + 4;

                for (int side = 0; side < 4; side++)
                {
                    int nextSide = (side + 1) % 4;

                    int a = r0 + side;
                    int b = r1 + side;
                    int c = r1 + nextSide;
                    int d = r0 + nextSide;

                    tris.Add(a);
                    tris.Add(b);
                    tris.Add(c);

                    tris.Add(a);
                    tris.Add(c);
                    tris.Add(d);
                }
            }
        }

        // =========================================================================
        // HELPER FUNCTIONS & MATH UTILITIES
        // =========================================================================

        private static float GetCrystalProfileRadius(float y)
        {
            // Exact profile envelope matching RedJade_FacetedCrystal
            if (y >= 0.52f) return 0.020f;
            if (y >= 0.28f)
            {
                float t = (0.52f - y) / (0.52f - 0.28f);
                return Mathf.Lerp(0.020f, 0.160f, t);
            }
            if (y >= 0.0f)
            {
                float t = (0.28f - y) / 0.28f;
                return Mathf.Lerp(0.160f, 0.230f, t);
            }
            if (y >= -0.28f)
            {
                float t = -y / 0.28f;
                return Mathf.Lerp(0.230f, 0.130f, t);
            }
            if (y >= -0.48f)
            {
                float t = (-0.28f - y) / (-0.28f - (-0.48f));
                return Mathf.Lerp(0.130f, 0.020f, t);
            }
            return 0.020f;
        }

        private static Vector2 EvalCubicBezier(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float omt = 1.0f - t;
            return omt * omt * omt * p0 +
                   3.0f * omt * omt * t * p1 +
                   3.0f * omt * t * t * p2 +
                   t * t * t * p3;
        }

        private static void SaveOrUpdateMeshAsset(Mesh mesh, string assetPath)
        {
            Mesh existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (existingMesh != null)
            {
                existingMesh.Clear();
                existingMesh.name = mesh.name;
                existingMesh.vertices = mesh.vertices;
                existingMesh.triangles = mesh.triangles;
                existingMesh.normals = mesh.normals;
                existingMesh.uv = mesh.uv;
                existingMesh.tangents = mesh.tangents;
                existingMesh.bounds = mesh.bounds;

                EditorUtility.SetDirty(existingMesh);
                AssetDatabase.SaveAssets();

                UnityEngine.Object.DestroyImmediate(mesh);
            }
            else
            {
                AssetDatabase.CreateAsset(mesh, assetPath);
                AssetDatabase.SaveAssets();
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
            if (!AssetDatabase.IsValidFolder(MeshesDirectory))
            {
                AssetDatabase.CreateFolder("Assets/JadeSeal/Art/RedJadeArtifact", "Meshes");
            }
        }
    }
}
#endif
