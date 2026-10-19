using UnityEngine;
namespace JadeSeal.World
{
    /// <summary>Environment integration metadata only; no gameplay behavior.</summary>
    [DisallowMultipleComponent]
    public sealed class JadeSealZone3Layout : MonoBehaviour
    {
        public string version="Celestial Mountain Pass / 1.0";
        public Vector3[] mainRoute,flankRoute,lookoutOneRoute,lookoutTwoRoute;
        public Vector3 entry,exit,lookoutOne,lookoutTwo;
        public TerrainData approvedTerrain;
        public string approvedObjectsFingerprint;
        public Bounds levelBounds=new Bounds(new Vector3(48,9,30),new Vector3(62,30,76));
    }
}
