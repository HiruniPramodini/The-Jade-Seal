using UnityEngine;
namespace JadeSeal.World
{
    [DisallowMultipleComponent]
    public sealed class JadeSealZone4Layout : MonoBehaviour
    {
        public string version="Celestial Jade Sanctuary / 1.0";
        public Vector3[] arrivalRoute,centralRoute,leftRoute,rightRoute,objectiveRoute,arenaEdgeRoute;
        public Vector3 entry,boss,objective,seal;public Vector3[] bossPhaseAnchors;
        public float arenaDiameter=31;
        public Bounds arenaBounds=new Bounds(new Vector3(-35,13,39),new Vector3(31,3,31));
        public TerrainData approvedTerrain;public string approvedObjectsFingerprint;
    }
}
