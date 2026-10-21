using UnityEditor;
using UnityEngine;
namespace JadeSeal.RoyalGuard.Editor
{
    [CustomEditor(typeof(RoyalGuardBrain))]
    public sealed class RoyalGuardBrainInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if (!Application.isPlaying) return;
            var brain = (RoyalGuardBrain)target;
            if (brain.settings) EditorGUILayout.HelpBox(brain.DebugStatus, MessageType.Info);
            Repaint();
        }
    }
}
