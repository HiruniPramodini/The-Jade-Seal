using JadeSeal.Sound;
using JadeSeal.Archery;
using JadeSeal.Magic;
using UnityEngine;

namespace JadeSeal.Boss
{
    [RequireComponent(typeof(BossHealth), typeof(CapsuleCollider))]
    public sealed class JadeSealBossAI : MonoBehaviour
    {
        public enum Phase
        {
            BossIdle, IntroJump, SpellAttack, HeavyAttack, BlockStart,
            BlockIdle, BlockEnd, Kick, HitReaction, Death, WaitingForPlayerRecovery
        }

        private static readonly int[] StateHashes = System.Array.ConvertAll(
            (Phase[])System.Enum.GetValues(typeof(Phase)), phase => Animator.StringToHash("Base Layer." + (phase == Phase.WaitingForPlayerRecovery ? Phase.BossIdle : phase)));

        [SerializeField] private Animator animator;
        [SerializeField] private Transform castPoint;
        [SerializeField] private BossRedOrb orb;
        [SerializeField] private PlayerMagicShieldVFX shield;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip jumpSound, castSound, summonSound, shieldImpactSound, shieldEndSound, kickSound, deathSound;
        [SerializeField, Min(0)] private float introHeight = 5.4f;
        [SerializeField, Range(0, 1)] private float spellRelease = 0.58f;
        [SerializeField, Range(0, 1)] private float heavyRelease = 0.55f;
        [SerializeField, Range(0, 1)] private float kickContact = 0.48f;
        [SerializeField, Min(0)] private float lightInterval = 0.9f;
        [SerializeField, Range(0, 1)] private float blockChance = 0.28f;
        [SerializeField] private Vector2 blockSeconds = new(3f, 4f);
        [SerializeField, Min(0)] private float blockCooldown = 9f;
        [SerializeField, Min(0.1f)] private float kickRange = 2f;
        [SerializeField, Min(0.1f)] private float kickCooldown = 5f;
        [SerializeField, Min(1)] private int kickDamage = 15;
        [SerializeField, Min(0)] private float kickKnockbackDistance = 5f;
        [SerializeField, Min(0.1f)] private float heavyPhaseDuration = 4f;

        private BossArenaController arena;
        private BossHealth health;
        private PlayerHealth player;
        private PlayerMagicCombatController playerCombat;
        private CapsuleCollider body;
        private CharacterController playerBody;
        private Transform kickingFoot;
        private Vector3 launchPosition;
        private float nextAction, nextBlock, nextKick, blockUntil, heavyUntil;
        private bool released, queuedHit, completedIntro, previousWasSpecial;

        public Phase CurrentPhase { get; private set; }
        public bool IsBlocking => CurrentPhase is Phase.BlockStart or Phase.BlockIdle;
        public bool DeathAnimationFinished => CurrentPhase == Phase.Death && !animator.IsInTransition(0) && Progress() >= 1f;
        public bool IsVulnerable => completedIntro && !IsBlocking && CurrentPhase != Phase.Death;
        public int ShotsFired { get; private set; }
        public int LightsCompleted { get; private set; }
        public int SuccessfulKicks { get; private set; }
        public float CurrentBlockDuration { get; private set; }

        private void Awake()
        {
            body = GetComponent<CapsuleCollider>();
            health = GetComponent<BossHealth>();
            kickingFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            health.Damaged += QueueReaction;
            health.Died += Die;
        }

        private void OnDestroy()
        {
            if (playerCombat) playerCombat.KnockdownChanged -= OnPlayerKnockdown;
            if (!health) return;
            health.Damaged -= QueueReaction;
            health.Died -= Die;
        }

        public void BeginFight(BossArenaController encounter, PlayerHealth target)
        {
            if (arena || !target || health.IsDead) return;
            arena = encounter;
            player = target;
            playerBody = player.GetComponent<CharacterController>();
            playerCombat = player.GetComponent<PlayerMagicCombatController>();
            if (playerCombat) playerCombat.KnockdownChanged += OnPlayerKnockdown;
            launchPosition = transform.position;
            Face(arena.Center, true);
            body.enabled = false;
            Play(Phase.IntroJump);
            Sound(jumpSound, Cue.BossJump);
            JadeSealAudio.Music(Cue.Battle);
        }

        private void Update()
        {
            if (!arena || health.IsDead) return;
            float progress = Progress();
            if (CurrentPhase == Phase.IntroJump)
            {
                float flight = Mathf.InverseLerp(0.26f, 0.92f, progress);
                transform.position = Vector3.Lerp(launchPosition, arena.Center, flight) +
                    Vector3.up * (4f * introHeight * flight * (1f - flight));
                if (progress < 0.96f) return;
                transform.position = arena.Center;
                body.enabled = true;
                JadeSealAudio.At(Cue.BossLand, transform.position);
                completedIntro = true;
                nextBlock = Time.time + 5f;
                nextAction = Time.time + 0.55f;
                Play(Phase.BossIdle);
                return;
            }
            if (!player || player.IsDead)
            {
                shield.SetVisible(false);
                if (IsBlocking) Play(Phase.BlockEnd);
                else if (CurrentPhase != Phase.BossIdle && progress >= 0.95f) Play(Phase.BossIdle);
                return;
            }

            if (CurrentPhase == Phase.BossIdle || CurrentPhase == Phase.SpellAttack && !released)
                Face(player.AimPosition, false);

            switch (CurrentPhase)
            {
                case Phase.WaitingForPlayerRecovery:
                    Face(player.AimPosition, false);
                    return;
                case Phase.SpellAttack:
                    if (!released && progress >= spellRelease) released = true;
                    if (progress >= 0.97f && released && shotResolved)
                    {
                        LightsCompleted++;
                        previousWasSpecial = false;
                        Finish(lightInterval);
                    }
                    return;
                case Phase.HeavyAttack:
                    if (!released && progress >= heavyRelease)
                    {
                        released = true;
                        if (summonSound && audioSource) audioSource.PlayOneShot(summonSound);
                    }
                    if (Time.time >= heavyUntil && !arena.MeteorsActive)
                    {
                        LightsCompleted = 0;
                        previousWasSpecial = false;
                        Finish(0f);
                    }
                    return;
                case Phase.BlockStart:
                    if (progress >= 0.95f) Play(Phase.BlockIdle);
                    return;
                case Phase.BlockIdle:
                    if (Time.time >= blockUntil)
                    {
                        shield.SetVisible(false);
                        Sound(shieldEndSound, Cue.YellowOff);
                        Play(Phase.BlockEnd);
                    }
                    return;
                case Phase.BlockEnd:
                case Phase.HitReaction:
                    if (progress >= 0.95f) Finish(0.3f);
                    return;
                case Phase.Kick:
                    if (progress >= 0.96f) Finish(0.5f);
                    return;
            }

            if (queuedHit)
            {
                queuedHit = false;
                Play(Phase.HitReaction);
                return;
            }
            if (!arena.Contains(player.transform.position) || Time.time < nextAction) return;
            Vector3 toPlayer = Vector3.ProjectOnPlane(player.transform.position - transform.position, Vector3.up);
            if (!previousWasSpecial && Time.time >= nextKick && toPlayer.sqrMagnitude <= kickRange * kickRange)
            {
                Face(player.transform.position, true);
                previousWasSpecial = true;
                nextKick = Time.time + kickCooldown;
                Play(Phase.Kick);
            }
            else if (!previousWasSpecial && Time.time >= nextBlock && Random.value < blockChance)
            {
                previousWasSpecial = true;
                CurrentBlockDuration = Random.Range(blockSeconds.x, blockSeconds.y);
                blockUntil = Time.time + CurrentBlockDuration;
                nextBlock = blockUntil + blockCooldown;
                shield.SetVisible(true);
                Play(Phase.BlockStart);
            }
            else if (LightsCompleted >= 3)
            {
                Play(Phase.HeavyAttack);
                if (!summonSound || !audioSource) JadeSealAudio.At(Cue.MeteorCharge, transform.position);
                heavyUntil = Time.time + Mathf.Max(heavyPhaseDuration, arena.MeteorTravelDuration);
                arena.LaunchMeteors(player.transform.position);
            }
            else Play(Phase.SpellAttack);
        }

        private bool shotResolved;
        private void LateUpdate()
        {
            if (CurrentPhase == Phase.Kick && !released && player && !player.IsDead)
            {
                float progress = Progress();
                if (progress >= kickContact && progress <= kickContact + 0.16f)
                    released = ResolveKick();
            }
            if (CurrentPhase != Phase.SpellAttack || !released || shotResolved || health.IsDead || !player || player.IsDead) return;
            shotResolved = true;
            Vector3 direction = player.AimPosition - castPoint.position;
            if (direction.sqrMagnitude < 0.0001f) return;
            Instantiate(orb, castPoint.position, Quaternion.LookRotation(direction)).Launch(health);
            ShotsFired++;
            if (castSound && audioSource) audioSource.PlayOneShot(castSound);
            else JadeSealAudio.At(Cue.RedCast, castPoint.position);
        }

        private bool ResolveKick()
        {
            Vector3 offset = Vector3.ProjectOnPlane(player.transform.position - transform.position, Vector3.up);
            if (offset.sqrMagnitude > kickRange * kickRange || Vector3.Angle(transform.forward, offset) > 65f ||
                !arena.Contains(player.transform.position) || !playerBody ||
                (playerBody.ClosestPoint(kickingFoot.position) - kickingFoot.position).sqrMagnitude > 0.65f * 0.65f ||
                ArrowProjectile.Trace(transform.position + Vector3.up, player.AimPosition, transform, ~0, out var hit) &&
                !hit.transform.IsChildOf(player.transform)) return false;
            int before = player.CurrentHealth;
            player.ReceiveHit(kickDamage, offset);
            Sound(kickSound, Cue.Kick);
            if (player.CurrentHealth >= before) return true;
            SuccessfulKicks++;
            if (!player.IsDead && playerCombat)
                playerCombat.KnockDown(arena.ClampPush(player.transform.position, offset.normalized * kickKnockbackDistance,
                    Mathf.Max(playerBody.bounds.extents.x, playerBody.bounds.extents.z)));
            return true;
        }

        private void OnPlayerKnockdown(bool down)
        {
            if (!completedIntro || health.IsDead || !player || player.IsDead) return;
            if (down)
            {
                if (CurrentPhase == Phase.SpellAttack && shotResolved) LightsCompleted++;
                if (CurrentPhase == Phase.HeavyAttack) arena.CancelMeteors();
                shield.SetVisible(false);
                Play(Phase.WaitingForPlayerRecovery);
            }
            else if (CurrentPhase == Phase.WaitingForPlayerRecovery)
                Finish(0.3f);
        }

        private void QueueReaction() => queuedHit = true;
        internal void PlayShieldImpact() => Sound(shieldImpactSound, Cue.YellowHit);
        private void Die()
        {
            shield.SetVisible(false);
            body.enabled = false;
            if (arena) arena.EndFight();
            Play(Phase.Death);
            Sound(deathSound, Cue.BossDeath);
            JadeSealAudio.Music(Cue.Explore);
        }

        private void Finish(float delay)
        {
            nextAction = Time.time + delay;
            Play(Phase.BossIdle);
        }

        private void Face(Vector3 point, bool immediate)
        {
            var direction = Vector3.ProjectOnPlane(point - transform.position, Vector3.up);
            if (direction.sqrMagnitude < 0.001f) return;
            var target = Quaternion.LookRotation(direction);
            transform.rotation = immediate ? target : Quaternion.RotateTowards(transform.rotation, target, 380f * Time.deltaTime);
        }

        private float Progress()
        {
            var state = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
            return state.fullPathHash == StateHashes[(int)CurrentPhase] ? state.normalizedTime : 0f;
        }

        private void Play(Phase phase)
        {
            if (phase == CurrentPhase) return;
            CurrentPhase = phase;
            released = shotResolved = false;
            animator.CrossFadeInFixedTime(StateHashes[(int)phase], phase == Phase.Death ? 0.06f : 0.08f);
        }

        private void Sound(AudioClip clip, Cue cue)
        {
            if (clip && audioSource) audioSource.PlayOneShot(clip);
            else JadeSealAudio.At(cue, transform.position);
        }
    }
}
