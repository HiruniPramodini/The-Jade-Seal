using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using JadeSeal.EnemyAI.Core;
using JadeSeal.EnemyAI.Sensory;
using JadeSeal.EnemyAI.Combat;

namespace JadeSeal.EnemyAI.Visuals
{
    /// <summary>
    /// Decoupled Visual & Animation Bridge for the Mountain Archer.
    /// Supports Idle, Walk, Run, Aim, Shoot, Hit, Dodge, and Death states.
    /// Allows swapping 3D models at any time without rewriting AI or Combat logic.
    /// </summary>
    [RequireComponent(typeof(MountainArcherAgent), typeof(NavMeshAgent))]
    public class MountainArcherVisualizer : MonoBehaviour
    {
        [Header("Animator Component")]
        [Tooltip("The Animator attached to the character model. If null, it will search children automatically.")]
        [SerializeField] private Animator characterAnimator;

        [Header("Configurable Animator Parameter Names")]
        [SerializeField] private string speedFloatParam = "Speed";
        [SerializeField] private string isAimingBoolParam = "IsAiming";
        [SerializeField] private string shootTriggerParam = "Shoot";
        [SerializeField] private string hitTriggerParam = "Hit";
        [SerializeField] private string dodgeTriggerParam = "Dodge";
        [SerializeField] private string isKitingBoolParam = "IsKiting";
        [SerializeField] private string deathTriggerParam = "Death";
        [SerializeField] private string isDeadBoolParam = "IsDead";

        [Header("Procedural Visuals Fallback")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform bowProp;

        // Cached Parameter Hashes
        private int speedHash;
        private int isAimingHash;
        private int shootHash;
        private int hitHash;
        private int dodgeHash;
        private int isKitingHash;
        private int deathHash;
        private int isDeadHash;

        // Validated parameter set to prevent Unity console warnings if parameter is missing
        private readonly HashSet<int> existingParamHashes = new HashSet<int>();

        private MountainArcherAgent agent;
        private NavMeshAgent navAgent;
        private MountainArcherHealth health;

        private ArcherState previousState;
        private float bobTimer = 0f;
        private float walkCycle = 0f;
        private bool hasPlayedDeath = false;

        // Skeletal Bone Cache for Procedural Gait & Archer Stance
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

        // Bind Pose Rotations Cache (Prevents limb flipping/inverting)
        private bool hasInitializedBoneRotations = false;
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

        private void Awake()
        {
            agent = GetComponent<MountainArcherAgent>();
            navAgent = GetComponent<NavMeshAgent>();
            health = GetComponent<MountainArcherHealth>();

            DisableBlenderRogueLights();
            CacheAnimatorParameters();
            ResolveAnimator();

            if (health != null)
            {
                health.OnHitReceived += HandleHitReceived;
                health.OnDied += HandleDied;
            }

            BuildProceduralPrototypeIfNeeded();
        }

        private void Start()
        {
            DisableBlenderRogueLights();
        }

        public void DisableBlenderRogueLights()
        {
            Light[] lights = GetComponentsInChildren<Light>(true);
            foreach (var l in lights)
            {
                Debug.Log($"<color=yellow>[MountainArcherVisualizer] Disabling rogue FBX light: '{l.name}'</color>");
                l.enabled = false;
                l.intensity = 0f;
            }
        }

        private void OnDestroy()
        {
            if (health != null)
            {
                health.OnHitReceived -= HandleHitReceived;
                health.OnDied -= HandleDied;
            }
        }

        public void CacheAnimatorParameters()
        {
            speedHash = Animator.StringToHash(speedFloatParam);
            isAimingHash = Animator.StringToHash(isAimingBoolParam);
            shootHash = Animator.StringToHash(shootTriggerParam);
            hitHash = Animator.StringToHash(hitTriggerParam);
            dodgeHash = Animator.StringToHash(dodgeTriggerParam);
            isKitingHash = Animator.StringToHash(isKitingBoolParam);
            deathHash = Animator.StringToHash(deathTriggerParam);
            isDeadHash = Animator.StringToHash(isDeadBoolParam);
        }

        public void ResolveAnimator()
        {
            if (characterAnimator == null)
            {
                characterAnimator = GetComponentInChildren<Animator>();
            }

            existingParamHashes.Clear();
            if (characterAnimator != null)
            {
                characterAnimator.applyRootMotion = false; // Prevent NavMeshAgent conflict
                if (characterAnimator.runtimeAnimatorController != null)
                {
                    foreach (var param in characterAnimator.parameters)
                    {
                        existingParamHashes.Add(param.nameHash);
                    }
                }
            }
        }

        private void Update()
        {
            if (hasPlayedDeath) return;

            float currentSpeed = navAgent != null ? navAgent.velocity.magnitude : 0f;
            ArcherState state = agent != null ? agent.CurrentState : ArcherState.Idle;

            // 1. Mecanim Animator Updates
            if (characterAnimator != null && characterAnimator.isActiveAndEnabled)
            {
                SetAnimatorFloat(speedHash, currentSpeed);
                SetAnimatorBool(isAimingHash, state == ArcherState.Aiming);
                SetAnimatorBool(isKitingHash, state == ArcherState.Kiting);

                // Trigger shooting
                if (state == ArcherState.Shooting && previousState != ArcherState.Shooting)
                {
                    SetAnimatorTrigger(shootHash);
                }

                // Trigger dodge / kiting
                if (state == ArcherState.Kiting && previousState != ArcherState.Kiting)
                {
                    SetAnimatorTrigger(dodgeHash);
                }
            }

            // 2. Procedural Prototype Rig Animation (Fallback when no 3D humanoid animator is active)
            if (visualRoot != null && (characterAnimator == null || !characterAnimator.isActiveAndEnabled))
            {
                UpdateProceduralMotion(currentSpeed, state);
            }

            previousState = state;
        }

        private void LateUpdate()
        {
            if (hasPlayedDeath)
            {
                ApplyDeathRagdollPose();
                return;
            }

            // If an Animator Controller with clips is actively driving the skeleton, let Mecanim animate!
            if (characterAnimator != null && characterAnimator.isActiveAndEnabled && characterAnimator.runtimeAnimatorController != null && characterAnimator.avatar != null && characterAnimator.avatar.isValid)
            {
                return;
            }

            if (boneLeftArm == null || boneLeftUpLeg == null)
            {
                ResolveSkeletonBones();
                if (boneLeftArm == null && boneLeftUpLeg == null) return;
            }

            float currentSpeed = navAgent != null ? navAgent.velocity.magnitude : 0f;
            ArcherState state = agent != null ? agent.CurrentState : ArcherState.Idle;

            AnimateSkeletonProcedurally(currentSpeed, state);
        }

        public void ResolveSkeletonBones()
        {
            Transform[] allChildren = GetComponentsInChildren<Transform>();
            foreach (var t in allChildren)
            {
                string n = t.name.ToLower();
                if (n.Contains("hips")) boneHips = t;
                else if (n.Contains("spine") && boneSpine == null) boneSpine = t;
                else if (n.Contains("leftarm") || n.Contains("leftshoulder") || n.Contains("arm_l") || n.Contains("upperarm_l")) boneLeftArm = t;
                else if (n.Contains("rightarm") || n.Contains("rightshoulder") || n.Contains("arm_r") || n.Contains("upperarm_r")) boneRightArm = t;
                else if (n.Contains("leftforearm") || n.Contains("elbow_l") || n.Contains("forearm_l")) boneLeftForeArm = t;
                else if (n.Contains("rightforearm") || n.Contains("elbow_r") || n.Contains("forearm_r")) boneRightForeArm = t;
                else if (n.Contains("leftupleg") || n.Contains("thigh_l")) boneLeftUpLeg = t;
                else if (n.Contains("rightupleg") || n.Contains("thigh_r")) boneRightUpLeg = t;
                else if (n.Contains("leftleg") || n.Contains("knee_l") || n.Contains("shin_l") || n.Contains("calf_l")) boneLeftLeg = t;
                else if (n.Contains("rightleg") || n.Contains("knee_r") || n.Contains("shin_r") || n.Contains("calf_r")) boneRightLeg = t;
            }

            if (!hasInitializedBoneRotations)
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
                hasInitializedBoneRotations = true;
            }
        }

        private void AnimateSkeletonProcedurally(float speed, ArcherState state)
        {
            // 1. Locomotion Leg Gait (Relative to initial rest orientation so feet stay on ground)
            if (speed > 0.15f)
            {
                walkCycle += Time.deltaTime * speed * 4.0f;
                float stride = Mathf.Sin(walkCycle) * 20f * Mathf.Clamp01(speed / 2.5f);

                if (boneLeftUpLeg != null)
                    boneLeftUpLeg.localRotation = initLeftUpLeg * Quaternion.Euler(stride, 0f, 0f);
                if (boneRightUpLeg != null)
                    boneRightUpLeg.localRotation = initRightUpLeg * Quaternion.Euler(-stride, 0f, 0f);

                // Knee flex on back stride
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

            // 2. Arms & Combat Stance (Relative to initial bind pose)
            if (state == ArcherState.Aiming || state == ArcherState.Shooting)
            {
                if (boneLeftArm != null)
                    boneLeftArm.localRotation = initLeftArm * Quaternion.Euler(-20f, 15f, -30f);
                if (boneLeftForeArm != null)
                    boneLeftForeArm.localRotation = initLeftForeArm * Quaternion.Euler(0f, 25f, 10f);

                if (boneRightArm != null)
                    boneRightArm.localRotation = initRightArm * Quaternion.Euler(-35f, -20f, 35f);
                if (boneRightForeArm != null)
                    boneRightForeArm.localRotation = initRightForeArm * Quaternion.Euler(0f, -45f, 0f);
            }
            else
            {
                float armSway = (speed > 0.15f) ? Mathf.Sin(walkCycle) * 12f : 0f;

                if (boneLeftArm != null)
                    boneLeftArm.localRotation = initLeftArm * Quaternion.Euler(armSway, 0f, -15f);
                if (boneLeftForeArm != null)
                    boneLeftForeArm.localRotation = initLeftForeArm * Quaternion.Euler(0f, 15f, 0f);

                if (boneRightArm != null)
                    boneRightArm.localRotation = initRightArm * Quaternion.Euler(-armSway, 0f, 15f);
                if (boneRightForeArm != null)
                    boneRightForeArm.localRotation = initRightForeArm * Quaternion.Euler(0f, -15f, 0f);
            }
        }

        private void ApplyDeathRagdollPose()
        {
            if (boneHips != null)
            {
                boneHips.localPosition = Vector3.Lerp(boneHips.localPosition, new Vector3(0, 0.25f, 0), Time.deltaTime * 4f);
                boneHips.localRotation = Quaternion.Slerp(boneHips.localRotation, Quaternion.Euler(70f, 0f, 0f), Time.deltaTime * 4f);
            }
        }

        private void HandleHitReceived(Vector3 hitPoint, Vector3 hitNormal)
        {
            if (hasPlayedDeath) return;

            if (characterAnimator != null && characterAnimator.isActiveAndEnabled)
            {
                SetAnimatorTrigger(hitHash);
            }

            // Procedural flinch feedback
            if (visualRoot != null)
            {
                visualRoot.localPosition = new Vector3(0, 0, -0.15f);
            }
        }

        private void HandleDied()
        {
            if (hasPlayedDeath) return;
            hasPlayedDeath = true;

            if (characterAnimator != null && characterAnimator.isActiveAndEnabled)
            {
                SetAnimatorBool(isDeadHash, true);
                SetAnimatorTrigger(deathHash);
            }

            // Procedural death fallback: tip capsule over
            if (visualRoot != null && (characterAnimator == null || !characterAnimator.isActiveAndEnabled))
            {
                visualRoot.localRotation = Quaternion.Euler(85f, 0, 0);
                visualRoot.localPosition = new Vector3(0, 0.2f, 0);
            }
        }

        private void SetAnimatorFloat(int hash, float val)
        {
            if (existingParamHashes.Contains(hash)) characterAnimator.SetFloat(hash, val);
        }

        private void SetAnimatorBool(int hash, bool val)
        {
            if (existingParamHashes.Contains(hash)) characterAnimator.SetBool(hash, val);
        }

        private void SetAnimatorTrigger(int hash)
        {
            if (existingParamHashes.Contains(hash)) characterAnimator.SetTrigger(hash);
        }

        private void UpdateProceduralMotion(float speed, ArcherState state)
        {
            // Locomotion stride bob
            if (speed > 0.1f)
            {
                bobTimer += Time.deltaTime * speed * 3.5f;
                float bobY = Mathf.Sin(bobTimer) * 0.08f;
                visualRoot.localPosition = new Vector3(0, bobY, 0);

                // Banking tilt during turns
                float tiltAngle = Mathf.Clamp(-transform.InverseTransformDirection(navAgent.velocity).x * 6f, -12f, 12f);
                visualRoot.localRotation = Quaternion.Euler(0, 0, tiltAngle);
            }
            else
            {
                visualRoot.localPosition = Vector3.Lerp(visualRoot.localPosition, Vector3.zero, Time.deltaTime * 5f);
                visualRoot.localRotation = Quaternion.Slerp(visualRoot.localRotation, Quaternion.identity, Time.deltaTime * 5f);
            }

            // Bow Aim Pose
            if (bowProp != null)
            {
                if (state == ArcherState.Aiming || state == ArcherState.Shooting)
                {
                    bowProp.localPosition = Vector3.Lerp(bowProp.localPosition, new Vector3(0.35f, 1.4f, 0.45f), Time.deltaTime * 8f);
                    bowProp.localRotation = Quaternion.Slerp(bowProp.localRotation, Quaternion.Euler(-10f, 0, 90f), Time.deltaTime * 8f);
                }
                else
                {
                    // Rest pose at hip
                    bowProp.localPosition = Vector3.Lerp(bowProp.localPosition, new Vector3(-0.3f, 0.9f, -0.1f), Time.deltaTime * 4f);
                    bowProp.localRotation = Quaternion.Slerp(bowProp.localRotation, Quaternion.Euler(15f, 0, 15f), Time.deltaTime * 4f);
                }
            }
        }

        public void BindNewCharacterModel(GameObject newModelInstance)
        {
            // Destroy prototype capsule if present
            Transform oldCapsule = transform.Find("ArcherPrototypeVisuals");
            if (oldCapsule != null)
            {
                if (Application.isPlaying) Destroy(oldCapsule.gameObject);
                else DestroyImmediate(oldCapsule.gameObject);
            }

            // Find Animator on new model
            characterAnimator = newModelInstance.GetComponentInChildren<Animator>();
            ResolveAnimator();

            Debug.Log($"<color=#00FF99>[MountainArcherVisualizer]</color> Bound new model '{newModelInstance.name}' with Animator: {(characterAnimator != null ? "FOUND" : "NONE")}");
        }

        private void BuildProceduralPrototypeIfNeeded()
        {
            Transform model3D = transform.Find("MountainArcher_3DModel");
            Transform existingCapsule = transform.Find("ArcherPrototypeVisuals");

            if (model3D != null || characterAnimator != null)
            {
                if (existingCapsule != null)
                {
                    if (Application.isPlaying) Destroy(existingCapsule.gameObject);
                    else DestroyImmediate(existingCapsule.gameObject);
                }
                return;
            }

            if (visualRoot != null || existingCapsule != null) return;

            // Create clean prototype visuals: Capsule body + Head + Bow prop
            GameObject rootObj = new GameObject("ArcherPrototypeVisuals");
            rootObj.transform.SetParent(transform);
            rootObj.transform.localPosition = Vector3.zero;
            rootObj.transform.localRotation = Quaternion.identity;
            visualRoot = rootObj.transform;

            // Body Capsule
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(visualRoot);
            body.transform.localPosition = new Vector3(0, 1.0f, 0);
            body.transform.localScale = new Vector3(0.6f, 1.0f, 0.6f);
            Destroy(body.GetComponent<Collider>());

            // Head Sphere
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(visualRoot);
            head.transform.localPosition = new Vector3(0, 1.75f, 0);
            head.transform.localScale = new Vector3(0.45f, 0.45f, 0.45f);
            Destroy(head.GetComponent<Collider>());

            // Visor / Eyes
            GameObject visor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visor.name = "VisorFacing";
            visor.transform.SetParent(head.transform);
            visor.transform.localPosition = new Vector3(0, 0.05f, 0.22f);
            visor.transform.localScale = new Vector3(0.35f, 0.12f, 0.15f);
            Destroy(visor.GetComponent<Collider>());

            // Bow Prop
            GameObject bow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bow.name = "PrototypeBow";
            bow.transform.SetParent(visualRoot);
            bow.transform.localPosition = new Vector3(-0.3f, 0.9f, -0.1f);
            bow.transform.localScale = new Vector3(0.04f, 0.6f, 0.04f);
            bow.transform.localRotation = Quaternion.Euler(15, 0, 15);
            Destroy(bow.GetComponent<Collider>());
            bowProp = bow.transform;
        }
    }
}
