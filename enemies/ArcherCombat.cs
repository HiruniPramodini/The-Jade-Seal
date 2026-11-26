using JadeSeal.Sound;
using UnityEngine;

namespace JadeSeal.Archery
{
    public sealed class ArcherCombat : MonoBehaviour
    {
        public enum Phase { Locomotion, CombatIdle, Load, Hold, Release, Recovery, Dead, HitReaction }

        private static readonly int PhaseParameter = Animator.StringToHash("Phase");
        private static readonly int SideSpeed = Animator.StringToHash("SideSpeed");
        private static readonly int ForwardSpeed = Animator.StringToHash("ForwardSpeed");
        private static readonly int IsDeadParameter = Animator.StringToHash("IsDead");
        private static readonly int[] StateHashes = System.Array.ConvertAll(
            (Phase[])System.Enum.GetValues(typeof(Phase)), value => Animator.StringToHash(value.ToString()));

        [SerializeField] private Animator animator;
        [SerializeField] private ArrowProjectile projectile;
        [SerializeField] private Transform arrowSpawnPoint;
        [SerializeField] private Transform nock, drawAnchor, upperTip, lowerTip, upperLimb, lowerLimb;
        [SerializeField] private LineRenderer bowstring;
        [SerializeField] private Transform nockedArrow;
        [SerializeField] private LayerMask sightMask = ~0;
        [SerializeField, Min(0)] private float attackCooldown = 2.5f;
        [SerializeField, Min(0)] private float holdSeconds = 0.4f;
        [SerializeField, Range(0.01f, 0.9f)] private float releaseNormalizedTime = 0.05f;
        [SerializeField] private float aimYawOffset;

        private PlayerHealth target;
        private float targetRangeSquared;
        private Phase phase;
        private float nextShotTime;
        private float releaseTime;
        private float draw;
        private Vector3 restNock;
        private Quaternion restUpper, restLower;
        private bool pendingRelease;
        private bool usesDeathParameter;
        public bool InSequence => phase is Phase.Load or Phase.Hold or Phase.Release or Phase.Recovery;
        public bool IsReacting => phase == Phase.HitReaction;
        public float AimYawOffset => aimYawOffset;
        public int ShotsFired { get; private set; }

        private void Awake()
        {
            if (!animator || !animator.avatar || !animator.isHuman || !animator.runtimeAnimatorController ||
                !projectile || !arrowSpawnPoint || !nock || !drawAnchor || !upperTip || !lowerTip ||
                !upperLimb || !lowerLimb || !bowstring || !nockedArrow)
            {
                Debug.LogError("ArcherCombat requires its Humanoid Animator, arrow prefab, and bow references.", this);
                enabled = false;
                return;
            }
            restNock = nock.localPosition;
            restUpper = upperLimb.localRotation;
            restLower = lowerLimb.localRotation;
            bowstring.useWorldSpace = true;
            bowstring.positionCount = 3;
            foreach (var parameter in animator.parameters)
                if (parameter.nameHash == IsDeadParameter && parameter.type == AnimatorControllerParameterType.Bool)
                    usesDeathParameter = true;
        }

        public bool HasSight(PlayerHealth player, Vector3 origin)
        {
            return !ArrowProjectile.Trace(origin, player.AimPosition, transform, sightMask, out Collider hit)
                   || hit.transform.IsChildOf(player.transform);
        }

        public void Move(Vector3 velocity)
        {
            Vector3 local = transform.InverseTransformDirection(velocity);
            animator.SetFloat(SideSpeed, local.x, 0.1f, Time.deltaTime);
            animator.SetFloat(ForwardSpeed, local.z, 0.1f, Time.deltaTime);
        }

        public void AimAndAttack(PlayerHealth player, bool aligned, float attackRange)
        {
            if (phase == Phase.Dead) return;
            target = player;
            targetRangeSquared = attackRange * attackRange;
            if (!InSequence)
            {
                if (!aligned || Time.time < nextShotTime)
                {
                    SetPhase(Phase.CombatIdle);
                    return;
                }
                draw = 0;
                releaseTime = -1;
                SetPhase(Phase.Load);
            }

            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (state.shortNameHash != StateHashes[(int)phase] || animator.IsInTransition(0)) return;
            float progress = state.normalizedTime;
            switch (phase)
            {
                case Phase.Load:
                    draw = Mathf.InverseLerp(0.30f, 0.96f, progress);
                    if (progress >= 0.96f) SetPhase(Phase.Hold);
                    break;
                case Phase.Hold:
                    draw = 1f;
                    if (aligned && progress * state.length >= holdSeconds) SetPhase(Phase.Release);
                    break;
                case Phase.Release:
                    if (releaseTime < 0 && progress >= releaseNormalizedTime)
                        pendingRelease = true;
                    if (releaseTime >= 0 && progress >= 0.96f) SetPhase(Phase.Recovery);
                    break;
                case Phase.Recovery:
                    if (progress >= 0.18f) SetPhase(Phase.CombatIdle);
                    break;
            }
        }

        public void Cancel(bool dead = false)
        {
            if (phase == Phase.Dead) return;
            target = null;
            pendingRelease = false;
            draw = 0;
            SetPhase(dead ? Phase.Dead : Phase.Locomotion);
            if (dead && usesDeathParameter) animator.SetBool(IsDeadParameter, true);
        }

        internal bool TryPlayHitReaction()
        {
            int state = Animator.StringToHash("Base Layer.HitReaction");
            if (phase is Phase.Dead or Phase.HitReaction || !animator.HasState(0, state)) return false;
            Cancel();
            SetPhase(Phase.HitReaction);
            animator.CrossFadeInFixedTime(state, .06f);
            return true;
        }

        private void SetPhase(Phase value)
        {
            if (phase == value) return;
            phase = value;
            if (value == Phase.Load) JadeSealAudio.At(Cue.BowDraw, transform.position);
            animator.SetInteger(PhaseParameter, (int)phase);
        }

        private void LateUpdate()
        {
            if (IsReacting && !animator.IsInTransition(0))
            {
                var state = animator.GetCurrentAnimatorStateInfo(0);
                if (state.shortNameHash == StateHashes[(int)Phase.HitReaction] && state.normalizedTime >= .98f)
                    SetPhase(Phase.Locomotion);
            }
            // Resolve the shot after the Animator has evaluated the current hand pose.
            if (pendingRelease)
            {
                pendingRelease = false;
                releaseTime = Time.time;
                nextShotTime = Time.time + attackCooldown;
                if (target && !target.IsDead &&
                    (target.transform.position - transform.position).sqrMagnitude <= targetRangeSquared &&
                    HasSight(target, arrowSpawnPoint.position))
                {
                    Vector3 direction = target.AimPosition - arrowSpawnPoint.position;
                    if (direction.sqrMagnitude > 0.001f)
                    {
                        var arrow = Instantiate(projectile, arrowSpawnPoint.position, Quaternion.LookRotation(direction));
                        arrow.Launch(transform, direction);
                        JadeSealAudio.At(Cue.BowRelease, arrowSpawnPoint.position);
                        ShotsFired++;
                    }
                }
            }

            if ((phase is Phase.Release or Phase.Recovery) && releaseTime >= 0)
                draw = 1f - Mathf.Clamp01((Time.time - releaseTime) / 0.12f);
            upperLimb.localRotation = restUpper * Quaternion.Euler(0, 0, -15f * draw);
            lowerLimb.localRotation = restLower * Quaternion.Euler(0, 0, -15f * draw);
            nock.position = Vector3.Lerp(nock.parent.TransformPoint(restNock), drawAnchor.position, draw);
            bowstring.SetPosition(0, upperTip.position);
            bowstring.SetPosition(1, nock.position);
            bowstring.SetPosition(2, lowerTip.position);

            bool showArrow = draw > 0.08f && releaseTime < 0;
            nockedArrow.gameObject.SetActive(showArrow);
            Vector3 shaft = arrowSpawnPoint.position - nock.position;
            if (showArrow && shaft.sqrMagnitude > 0.001f)
                nockedArrow.SetPositionAndRotation(nock.position, Quaternion.LookRotation(shaft));
        }
    }
}
