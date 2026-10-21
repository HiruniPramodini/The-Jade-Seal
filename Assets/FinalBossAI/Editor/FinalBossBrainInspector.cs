using UnityEditor;
using UnityEngine;

namespace JadeSeal.FinalBoss.Editor
{
    [CustomEditor(typeof(FinalBossBrain))]
    public sealed class FinalBossBrainInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var brain = (FinalBossBrain)target;
            if (brain == null) return;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Final Boss AI Realtime Diagnostics", EditorStyles.boldLabel);

            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox(brain.DebugStatus, MessageType.Info);

                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Manual Test Triggers (Play Mode)", EditorStyles.boldLabel);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Fire Basic Magic"))
                {
                    brain.TriggerManualAttack(BossAttackType.BasicMagic);
                }
                if (GUILayout.Button("Launch Fireball"))
                {
                    brain.TriggerManualAttack(BossAttackType.Fireball);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Trigger Stomp"))
                {
                    brain.TriggerManualAttack(BossAttackType.StompShockwave);
                }
                if (GUILayout.Button("Toggle Guard"))
                {
                    brain.ToggleManualGuard();
                }
                EditorGUILayout.EndHorizontal();

                var health = brain.GetComponent<FinalBossHealth>();
                if (health != null && GUILayout.Button("Inflict 150 Damage to Boss"))
                {
                    health.TakeDamage(150f);
                }

                Repaint();
            }
            else
            {
                EditorGUILayout.HelpBox("Enter Play Mode to view real-time state machine diagnostics and attack cooldown gauges.", MessageType.None);
            }
        }
    }
}
