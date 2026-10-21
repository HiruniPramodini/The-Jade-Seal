using UnityEngine;
using UnityEngine.AI;

namespace JadeSeal.EnemyAI.BambooHunter
{
    /// <summary>
    /// Decoupled Visual & Procedural Presentation for the Bamboo Hunter.
    /// Manages forest hunter cloth materials, bow prop, procedural locomotion gait,
    /// archery draw stance, and death collapse.
    /// </summary>
    [RequireComponent(typeof(BambooHunterAgent), typeof(NavMeshAgent))]
    public class BambooHunterVisualizer : MonoBehaviour
    {
        [Header("Animator Component (Optional)")]
        [SerializeField] private Animator characterAnimator;

        [Header("Appearance Materials")]
        [SerializeField] private Material clothMaterial;
        [SerializeField] private Material leatherMaterial;
        [SerializeField] private Material skinMaterial;
        [SerializeField] private Material bowMaterial;

        [Header("Weapons & Props")]
        [SerializeField] private Transform bowTransform;

        private BambooHunterAgent agent;
        private NavMeshAgent navAgent;
        private BambooHunterHealth health;

        // Bone Cache
        private Transform boneHips;
        private Transform boneSpine;
        private Transform boneLeftArm;
        private Transform boneRightArm;
        private Transform boneLeftForeArm;
        private Transform boneRightForeArm;
        private Transform boneLeftUpLeg;
        private Transform boneRightUpLeg;
        private Transform boneLeftLeg;
        private Transform boneRightLeg;

        // Bind Pose Rotations Cache
        private bool hasInitializedBones = false;
        private Quaternion initHips;
        private Quaternion initSpine;
        private Quaternion initLeftArm;
        private Quaternion initRightArm;
        private Quaternion initLeftForeArm;
        private Quaternion initRightForeArm;
        private Quaternion initLeftUpLeg;
        private Quaternion initRightUpLeg;
        private Quaternion initLeftLeg;
        private Quaternion initRightLeg;

        private float walkCycle = 0f;
        private bool isDead = false;

        private void Awake()
        {
            agent = GetComponent<BambooHunterAgent>();
            navAgent = GetComponent<NavMeshAgent>();
            health = GetComponent<BambooHunterHealth>();

            if (characterAnimator == null) characterAnimator = GetComponentInChildren<Animator>();
            if (health != null) health.OnDied += HandleDeath;

            ResolveSkeletonBones();
            ApplyMaterialStyling();
        }

        private void Start()
        {
            ResolveSkeletonBones();
            ApplyMaterialStyling();
        }

        public void ApplyMaterialStyling()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                string n = r.name.ToLower();
                if (n.Contains("bow"))
                {
                    if (bowMaterial != null) r.material = bowMaterial;
                }
                else if (r is SkinnedMeshRenderer smr)
                {
                    if (clothMaterial != null) smr.material = clothMaterial;
                }
            }
        }

        public void ResolveSkeletonBones()
        {
            Transform[] allChildren = GetComponentsInChildren<Transform>();
            foreach (var t in allChildren)
            {
                string n = t.name.ToLower();
                if (n.Contains("hips")) boneHips = t;
                else if (n.Contains("spine") && boneSpine == null) boneSpine = t;
                else if (n.Contains("leftarm") || n.Contains("arm_l") || n.Contains("upperarm_l")) boneLeftArm = t;
                else if (n.Contains("rightarm") || n.Contains("arm_r") || n.Contains("upperarm_r")) boneRightArm = t;
                else if (n.Contains("leftforearm") || n.Contains("elbow_l") || n.Contains("forearm_l")) boneLeftForeArm = t;
                else if (n.Contains("rightforearm") || n.Contains("elbow_r") || n.Contains("forearm_r")) boneRightForeArm = t;
                else if (n.Contains("leftupleg") || n.Contains("thigh_l")) boneLeftUpLeg = t;
                else if (n.Contains("rightupleg") || n.Contains("thigh_r")) boneRightUpLeg = t;
                else if (n.Contains("leftleg") || n.Contains("knee_l") || n.Contains("shin_l")) boneLeftLeg = t;
                else if (n.Contains("rightleg") || n.Contains("knee_r") || n.Contains("shin_r")) boneRightLeg = t;

                if (bowTransform == null && n.Contains("bow"))
                {
                    bowTransform = t;
                }
            }

            if (!hasInitializedBones)
            {
                if (boneHips != null) initHips = boneHips.localRotation;
                if (boneSpine != null) initSpine = boneSpine.localRotation;
                if (boneLeftArm != null) initLeftArm = boneLeftArm.localRotation;
                if (boneRightArm != null) initRightArm = boneRightArm.localRotation;
                if (boneLeftForeArm != null) initLeftForeArm = boneLeftForeArm.localRotation;
                if (boneRightForeArm != null) initRightForeArm = boneRightForeArm.localRotation;
                if (boneLeftUpLeg != null) initLeftUpLeg = boneLeftUpLeg.localRotation;
                if (boneRightUpLeg != null) initRightUpLeg = boneRightUpLeg.localRotation;
                if (boneLeftLeg != null) initLeftLeg = boneLeftLeg.localRotation;
                if (boneRightLeg != null) initRightLeg = boneRightLeg.localRotation;
                hasInitializedBones = true;
            }
        }

        private void LateUpdate()
        {
            if (isDead)
            {
                ApplyDeathRagdollPose();
                return;
            }

            if (characterAnimator != null && characterAnimator.runtimeAnimatorController != null && characterAnimator.avatar != null && characterAnimator.avatar.isValid)
            {
                float speed = navAgent != null ? navAgent.velocity.magnitude : 0f;
                characterAnimator.SetFloat("Speed", speed);
                characterAnimator.SetBool("IsAiming", agent != null && agent.CurrentState == HunterState.Aiming);
                return;
            }

            if (!hasInitializedBones) ResolveSkeletonBones();

            float currentSpeed = navAgent != null ? navAgent.velocity.magnitude : 0f;
            HunterState state = agent != null ? agent.CurrentState : HunterState.Idle;
            AnimateSkeletonProcedurally(currentSpeed, state);
        }

        private void AnimateSkeletonProcedurally(float speed, HunterState state)
        {
            // 1. Locomotion Gait (Feet stay planted)
            if (speed > 0.15f)
            {
                walkCycle += Time.deltaTime * speed * 4.0f;
                float stride = Mathf.Sin(walkCycle) * 20f * Mathf.Clamp01(speed / 2.5f);

                if (boneLeftUpLeg != null)
                    boneLeftUpLeg.localRotation = initLeftUpLeg * Quaternion.Euler(stride, 0f, 0f);
                if (boneRightUpLeg != null)
                    boneRightUpLeg.localRotation = initRightUpLeg * Quaternion.Euler(-stride, 0f, 0f);

                if (boneLeftLeg != null)
                    boneLeftLeg.localRotation = initLeftLeg * Quaternion.Euler(Mathf.Clamp(-stride * 0.7f, 0f, 35f), 0f, 0f);
                if (boneRightLeg != null)
                    boneRightLeg.localRotation = initRightLeg * Quaternion.Euler(Mathf.Clamp(stride * 0.7f, 0f, 35f), 0f, 0f);
            }
            else
            {
                if (boneLeftUpLeg != null)
                    boneLeftUpLeg.localRotation = Quaternion.Slerp(boneLeftUpLeg.localRotation, initLeftUpLeg, Time.deltaTime * 6f);
                if (boneRightUpLeg != null)
                    boneRightUpLeg.localRotation = Quaternion.Slerp(boneRightUpLeg.localRotation, initRightUpLeg, Time.deltaTime * 6f);
                if (boneLeftLeg != null)
                    boneLeftLeg.localRotation = Quaternion.Slerp(boneLeftLeg.localRotation, initLeftLeg, Time.deltaTime * 6f);
                if (boneRightLeg != null)
                    boneRightLeg.localRotation = Quaternion.Slerp(boneRightLeg.localRotation, initRightLeg, Time.deltaTime * 6f);
            }

            // 2. Arms & Combat Stance
            if (state == HunterState.Aiming || state == HunterState.Shooting)
            {
                // Extended left arm holding bow, right arm drawing string
                if (boneLeftArm != null)
                    boneLeftArm.localRotation = initLeftArm * Quaternion.Euler(-25f, 20f, -35f);
                if (boneLeftForeArm != null)
                    boneLeftForeArm.localRotation = initLeftForeArm * Quaternion.Euler(0f, 20f, 10f);

                if (boneRightArm != null)
                    boneRightArm.localRotation = initRightArm * Quaternion.Euler(-35f, -25f, 35f);
                if (boneRightForeArm != null)
                    boneRightForeArm.localRotation = initRightForeArm * Quaternion.Euler(0f, -50f, 0f);
            }
            else
            {
                // Forest hunting walk stance
                float armSway = (speed > 0.15f) ? Mathf.Sin(walkCycle) * 12f : 0f;

                if (boneLeftArm != null)
                    boneLeftArm.localRotation = initLeftArm * Quaternion.Euler(armSway, 0f, -15f);
                if (boneRightArm != null)
                    boneRightArm.localRotation = initRightArm * Quaternion.Euler(-armSway, 0f, 15f);
            }
        }

        private void ApplyDeathRagdollPose()
        {
            if (boneHips != null)
            {
                boneHips.localPosition = Vector3.Lerp(boneHips.localPosition, new Vector3(0, 0.2f, 0), Time.deltaTime * 4f);
                boneHips.localRotation = Quaternion.Slerp(boneHips.localRotation, Quaternion.Euler(75f, 0f, 0f), Time.deltaTime * 4f);
            }
        }

        private void HandleDeath()
        {
            isDead = true;
            if (characterAnimator != null && characterAnimator.runtimeAnimatorController != null)
            {
                characterAnimator.SetBool("IsDead", true);
                characterAnimator.SetTrigger("Death");
            }
        }
    }
}
