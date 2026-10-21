using UnityEditor;
using UnityEngine;

namespace JadeSeal.MountainArcher.Editor
{
    [CustomEditor(typeof(MountainArcherBrain))]
    public sealed class MountainArcherBrainInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var brain = (MountainArcherBrain)target;
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("AI State & Diagnostic", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(brain.DebugStatus, MessageType.Info);
            Repaint();
        }
    }
}
