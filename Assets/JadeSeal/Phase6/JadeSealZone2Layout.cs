using UnityEngine;

namespace JadeSeal.World
{
    /// <summary>World-builder integration metadata only. No combat or progression behavior.</summary>
    [DisallowMultipleComponent]
    public sealed class JadeSealZone2Layout : MonoBehaviour
    {
        public string version="Crimson Clan Courtyard / 1.0";
        public Vector3[] centralRoute,leftRoute,rightRoute,exitRoute;
        public Vector3 entry,exit;
        public Bounds levelBounds=new Bounds(new Vector3(50,3,-32),new Vector3(57,12,55));
        public TerrainData approvedTerrain;
        public string preservedObjectsFingerprint;
    }
}
