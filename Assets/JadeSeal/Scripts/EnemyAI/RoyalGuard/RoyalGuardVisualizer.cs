using UnityEngine;
using UnityEngine.AI;

namespace JadeSeal.EnemyAI.RoyalGuard
{
    /// <summary>
    /// Decoupled Visual and Procedural Animation presentation for the Royal Guard.
    /// Manages material styling, sword positioning, procedural skeletal locomotion gait,
    /// melee slash animation, block stance, and death collapse.
    /// </summary>
    [RequireComponent(typeof(RoyalGuardAgent), typeof(NavMeshAgent))]
    public class RoyalGuardVisualizer : MonoBehaviour
    {
        [Header("Animator Component (Optional)")]
        [SerializeField] private Animator characterAnimator;

        [Header("Appearance Materials")]
        [SerializeField] private Material armorMaterial;
        [SerializeField] private Material clothMaterial;
        [SerializeField] private Material skinMaterial;
        [SerializeField] private Material goldMaterial;
        [SerializeField] private Material swordMaterial;

        [Header("Weapons & Props")]
        [SerializeField] private Transform swordTransform;

        private RoyalGuardAgent agent;
        private NavMeshAgent navAgent;
        private RoyalGuardHealth health;

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
            agent = GetComponent<RoyalGuardAgent>();
            navAgent = GetComponent<NavMeshAgent>();
            health = GetComponent<RoyalGuardHealth>();

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
            if (armorMaterial == null) armorMaterial = Resources.Load<Material>("Materials/M_RoyalGuard_Armor");
            if (clothMaterial == null) clothMaterial = Resources.Load<Material>("Materials/M_RoyalGuard_Cloth");
            if (skinMaterial == null) skinMaterial = Resources.Load<Material>("Materials/M_RoyalGuard_Skin");

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                string n = r.name.ToLower();
                if (n.Contains("sword") || n.Contains("blade"))
                {
                    if (swordMaterial != null) r.material = swordMaterial;
                }
                else if (n.Contains("guard") || n.Contains("hilt"))
                {
                    if (goldMaterial != null) r.material = goldMaterial;
                }
                else if (r is SkinnedMeshRenderer smr)
                {
                    // Apply primary armor & cloth materials
                    if (armorMaterial != null) smr.material = armorMaterial;
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

                if (swordTransform == null && (n.Contains("sword") || n.Contains("blade")))
                {
                    swordTransform = t;
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

            // If an Animator Controller with clips is active, let Mecanim take precedence
            if (characterAnimator != null && characterAnimator.runtimeAnimatorController != null && characterAnimator.avatar != null && characterAnimator.avatar.isValid)
            {
                float speed = navAgent != null ? navAgent.velocity.magnitude : 0f;
                characterAnimator.SetFloat("Speed", speed);
                characterAnimator.SetBool("IsBlocking", agent != null && agent.IsBlocking);
                return;
            }

            if (!hasInitializedBones) ResolveSkeletonBones();

            float currentSpeed = navAgent != null ? navAgent.velocity.magnitude : 0f;
            GuardState state = agent != null ? agent.CurrentState : GuardState.Idle;
            AnimateSkeletonProcedurally(currentSpeed, state);
        }

        private void AnimateSkeletonProcedurally(float speed, GuardState state)
        {
            // 1. Locomotion Gait (Feet stay planted)
            if (speed > 0.15f)
            {
                walkCycle += Time.deltaTime * speed * 3.8f;
                float stride = Mathf.Sin(walkCycle) * 22f * Mathf.Clamp01(speed / 2.5f);

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

            // 2. Arms & Combat Stances
            if (state == GuardState.Blocking)
            {
                // Raised guard parry pose across chest
                if (boneRightArm != null)
                    boneRightArm.localRotation = initRightArm * Quaternion.Euler(-55f, -30f, 40f);
                if (boneRightForeArm != null)
                    boneRightForeArm.localRotation = initRightForeArm * Quaternion.Euler(0f, -70f, 0f);

                if (boneLeftArm != null)
                    boneLeftArm.localRotation = initLeftArm * Quaternion.Euler(-40f, 20f, -30f);
                if (boneLeftForeArm != null)
                    boneLeftForeArm.localRotation = initLeftForeArm * Quaternion.Euler(0f, 60f, 0f);
            }
            else if (state == GuardState.MeleeAttack)
            {
                // Overhead slash swing
                float attackSwing = Mathf.Sin(Time.time * 16f) * 45f;
                if (boneRightArm != null)
                    boneRightArm.localRotation = initRightArm * Quaternion.Euler(-65f + attackSwing, -10f, 20f);
                if (boneRightForeArm != null)
                    boneRightForeArm.localRotation = initRightForeArm * Quaternion.Euler(0f, -30f, 0f);
            }
            else
            {
                // Natural walking arm sway with sword in right hand
                float armSway = (speed > 0.15f) ? Mathf.Sin(walkCycle) * 14f : 0f;

                if (boneLeftArm != null)
                    boneLeftArm.localRotation = initLeftArm * Quaternion.Euler(armSway, 0f, -12f);
                if (boneRightArm != null)
                    boneRightArm.localRotation = initRightArm * Quaternion.Euler(-armSway - 15f, 0f, 15f);
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
