using UnityEngine;
using UnityEngine.AI;

namespace JadeSeal.Sound
{
    public sealed class EnemyFootsteps : MonoBehaviour
    {
        private NavMeshAgent agent;
        private JadeSeal.Archery.EnemyHealth health;
        private Vector3 previous;
        private float travel;

        internal void Bind(NavMeshAgent motor, JadeSeal.Archery.EnemyHealth target)
        {
            agent = motor;
            health = target;
            previous = transform.position;
        }

        private void LateUpdate()
        {
            if (!agent || !health || health.IsDead) return;
            Vector3 current = transform.position;
            float step = Vector3.ProjectOnPlane(current - previous, Vector3.up).magnitude;
            previous = current;
            if (step <= .001f || step > 1.5f) { travel = 0; return; }
            travel += step;
            if (travel < .9f) return;
            travel %= .9f;
            JadeSealAudio.At(Cue.Step, current, .55f);
        }
    }
}
