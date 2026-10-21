using UnityEngine;
using UnityEngine.AI;

namespace JadeSeal.EnemyAI.ClanMaster
{
    /// <summary>
    /// Decoupled Visual & Procedural Presentation for the Clan Master.
    /// Manages ceremonial silk robe & jade styling, dragon glaive prop,
    /// procedural locomotion gait, heavy sweeping cleave, rally stance, and death collapse.
    /// </summary>
    [RequireComponent(typeof(ClanMasterAgent), typeof(NavMeshAgent))]
    public class ClanMasterVisualizer : MonoBehaviour
    {
        [Header("Animator Component (Optional)")]
        [SerializeField] private Animator characterAnimator;

        [Header("Appearance Materials")]
        [SerializeField] private Material robeMaterial;
        [SerializeField] private Material armorMaterial;
        [SerializeField] private Material jadeMaterial;
        [SerializeField] private Material skinMaterial;
        [SerializeField] private Material bronzeMaterial;

        [Header("Weapons & Props")]
        [SerializeField] private Transform glaiveTransform;

        private ClanMasterAgent agent;
        private NavMeshAgent navAgent;
        private ClanMasterHealth health;

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
            agent = GetComponent<ClanMasterAgent>();
            navAgent = GetComponent<NavMeshAgent>();
            health = GetComponent<ClanMasterHealth>();

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
                if (n.Contains("glaive") || n.Contains("blade") || n.Contains("staff"))
                {
                    if (bronzeMaterial != null) r.material = bronzeMaterial;
                }
                else if (n.Contains("jade") || n.Contains("orb") || n.Contains("pendant"))
                {
                    if (jadeMaterial != null) r.material = jadeMaterial;
                }
                else if (r is SkinnedMeshRenderer smr)
                {
                    if (robeMaterial != null) smr.material = robeMaterial;
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

                if (glaiveTransform == null && (n.Contains("glaive") || n.Contains("staff")))
                {
                    glaiveTransform = t;
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
                characterAnimator.SetBool("IsDefending", agent != null && agent.IsDefending);
                characterAnimator.SetBool("IsRallying", agent != null && agent.IsRallying);
                return;
            }

            if (!hasInitializedBones) ResolveSkeletonBones();

            float currentSpeed = navAgent != null ? navAgent.velocity.magnitude : 0f;
            ClanMasterState state = agent != null ? agent.CurrentState : ClanMasterState.Idle;
            AnimateSkeletonProcedurally(currentSpeed, state);
        }

        private void AnimateSkeletonProcedurally(float speed, ClanMasterState state)
        {
            // 1. Locomotion Gait (Feet stay planted)
            if (speed > 0.15f)
            {
                walkCycle += Time.deltaTime * speed * 3.6f;
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

            // 2. Arms & Combat Stances
            if (state == ClanMasterState.RallyAllies)
            {
                // Raising Dragon Glaive skyward to rally the clan
                if (boneRightArm != null)
                    boneRightArm.localRotation = initRightArm * Quaternion.Euler(-135f, 0f, 20f);
                if (boneRightForeArm != null)
                    boneRightForeArm.localRotation = initRightForeArm * Quaternion.Euler(0f, 15f, 0f);

                if (boneLeftArm != null)
                    boneLeftArm.localRotation = initLeftArm * Quaternion.Euler(-30f, 40f, -20f);
            }
            else if (state == ClanMasterState.CleaveAttack)
            {
                // Sweeping horizontal two-handed glaive cleave
                float cleaveSwing = Mathf.Sin(Time.time * 14f) * 60f;
                if (boneRightArm != null)
                    boneRightArm.localRotation = initRightArm * Quaternion.Euler(-45f, cleaveSwing, 30f);
                if (boneLeftArm != null)
                    boneLeftArm.localRotation = initLeftArm * Quaternion.Euler(-45f, cleaveSwing * 0.8f, -20f);
            }
            else if (state == ClanMasterState.DefensiveStance)
            {
                // Grounded staff defense
                if (boneRightArm != null)
                    boneRightArm.localRotation = initRightArm * Quaternion.Euler(-30f, -25f, 35f);
                if (boneLeftArm != null)
                    boneLeftArm.localRotation = initLeftArm * Quaternion.Euler(-50f, 20f, -15f);
            }
            else
            {
                // Calm, authoritative walk with glaive in hand
                float armSway = (speed > 0.15f) ? Mathf.Sin(walkCycle) * 10f : 0f;

                if (boneLeftArm != null)
                    boneLeftArm.localRotation = initLeftArm * Quaternion.Euler(armSway, 0f, -10f);
                if (boneRightArm != null)
                    boneRightArm.localRotation = initRightArm * Quaternion.Euler(-armSway - 20f, 0f, 20f);
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
