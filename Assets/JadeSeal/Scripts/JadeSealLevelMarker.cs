using UnityEngine;

namespace JadeSeal.World
{
    public enum JadeSealMarkerKind
    {
        PlayerSpawn, EnemySpawn, BossSpawn, Objective, EncounterArea, BambooPlacement
    }

    /// <summary>Layout metadata only: no AI, spawning, combat, or runtime movement.</summary>
    [DisallowMultipleComponent]
    public sealed class JadeSealLevelMarker : MonoBehaviour
    {
        public JadeSealMarkerKind kind;
        [Range(1, 4)] public int zone = 1;
        [TextArea] public string notes;
        public Vector3 areaSize = new Vector3(1f, 0.1f, 1f);

        private void OnDrawGizmos()
        {
            Color color;
            switch (kind)
            {
                case JadeSealMarkerKind.PlayerSpawn: color = Color.cyan; break;
                case JadeSealMarkerKind.EnemySpawn: color = new Color(1f, 0.35f, 0.2f); break;
                case JadeSealMarkerKind.BossSpawn: color = Color.magenta; break;
                case JadeSealMarkerKind.Objective: color = new Color(0.2f, 1f, 0.6f); break;
                case JadeSealMarkerKind.EncounterArea: color = new Color(1f, 0.7f, 0.15f); break;
                default: color = new Color(0.4f, 0.8f, 0.3f); break;
            }
            Gizmos.color = color;
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            if (kind == JadeSealMarkerKind.EncounterArea)
                Gizmos.DrawWireCube(Vector3.up * areaSize.y * 0.5f, areaSize);
            else
            {
                Gizmos.DrawWireSphere(Vector3.up * 0.35f, 0.35f);
                Gizmos.DrawLine(Vector3.zero, Vector3.up * 1.8f);
                Gizmos.DrawLine(Vector3.up, Vector3.up + Vector3.forward * 1.5f);
            }
            Gizmos.matrix = Matrix4x4.identity;
#if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, name);
#endif
        }
    }
}
