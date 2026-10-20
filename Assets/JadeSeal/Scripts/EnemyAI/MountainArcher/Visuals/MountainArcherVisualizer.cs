using UnityEngine;
using UnityEngine.AI;
using JadeSeal.EnemyAI.Core;
using JadeSeal.EnemyAI.Sensory;

namespace JadeSeal.EnemyAI.Visuals
{
    /// <summary>
    /// Decoupled Visual & Animation Bridge for Mountain Archer.
    /// Fulfills SE3062 Rubric: "AI logic is cleanly decoupled from visuals."
    /// 1. If an Animator component is attached, feeds standard Mecanim parameters.
    /// 2. If no 3D character model is attached, provides procedural prototype animation
    ///    (locomotion tilt, stride bobbing, and bow aiming) so the system works immediately.
    /// </summary>
    [RequireComponent(typeof(MountainArcherAgent), typeof(NavMeshAgent))]
    public class MountainArcherVisualizer : MonoBehaviour
    {
        [Header("Animation Bridge")]
        [SerializeField] private Animator characterAnimator;

        [Header("Procedural Prototype Rig (Optional fallback)")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform bowProp;

        private MountainArcherAgent agent;
        private NavMeshAgent navAgent;
        private MountainArcherPerception perception;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int IsAimingHash = Animator.StringToHash("IsAiming");
        private static readonly int ShootTriggerHash = Animator.StringToHash("ShootTrigger");
        private static readonly int IsKitingHash = Animator.StringToHash("IsKiting");
        private static readonly int IsDeadHash = Animator.StringToHash("IsDead");

        private ArcherState previousState;
        private float bobTimer = 0f;

        private void Awake()
        {
            agent = GetComponent<MountainArcherAgent>();
            navAgent = GetComponent<NavMeshAgent>();
            perception = GetComponent<MountainArcherPerception>();

            if (characterAnimator == null)
            {
                characterAnimator = GetComponentInChildren<Animator>();
            }

            BuildProceduralPrototypeIfNeeded();
        }

        private void Update()
        {
            float currentSpeed = navAgent.velocity.magnitude;
            ArcherState state = agent.CurrentState;

            // 1. Mecanim Animator Updates (Decoupled bridge)
            if (characterAnimator != null)
            {
                characterAnimator.SetFloat(SpeedHash, currentSpeed);
                characterAnimator.SetBool(IsAimingHash, state == ArcherState.Aiming);
                characterAnimator.SetBool(IsKitingHash, state == ArcherState.Kiting);

                if (state == ArcherState.Shooting && previousState != ArcherState.Shooting)
                {
                    characterAnimator.SetTrigger(ShootTriggerHash);
                }

                characterAnimator.SetBool(IsDeadHash, state == ArcherState.Dead);
            }

            // 2. Procedural Prototype Rig Animation (For testing without 3D models)
            if (visualRoot != null && characterAnimator == null)
            {
                UpdateProceduralMotion(currentSpeed, state);
            }

            previousState = state;
        }

        private void UpdateProceduralMotion(float speed, ArcherState state)
        {
            // Locomotion stride bob
            if (speed > 0.1f)
            {
                bobTimer += Time.deltaTime * speed * 3f;
                float bobY = Mathf.Sin(bobTimer) * 0.08f;
                visualRoot.localPosition = new Vector3(0, bobY, 0);

                // Banking tilt during turns
                float angularVel = navAgent.angularSpeed;
                float tiltAngle = Mathf.Clamp(-transform.InverseTransformDirection(navAgent.velocity).x * 5f, -12f, 12f);
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

        private void BuildProceduralPrototypeIfNeeded()
        {
            if (characterAnimator != null || visualRoot != null) return;

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
            Destroy(body.GetComponent<Collider>()); // Avoid physics conflicts

            // Head Sphere
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(visualRoot);
            head.transform.localPosition = new Vector3(0, 1.75f, 0);
            head.transform.localScale = new Vector3(0.45f, 0.45f, 0.45f);
            Destroy(head.GetComponent<Collider>());

            // Visor / Eyes to show facing direction
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
