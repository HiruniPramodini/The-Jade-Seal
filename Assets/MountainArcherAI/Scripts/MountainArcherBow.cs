using UnityEngine;

namespace JadeSeal.MountainArcher
{
    /// <summary>
    /// Ranged Bow and Arrow combat system for the Mountain Archer.
    /// Delivers continuous archery fire at a faster rate than Bamboo Hunters.
    /// </summary>
    public sealed class MountainArcherBow : MonoBehaviour
    {
        [Header("Weapon Transform & Prefabs")]
        [SerializeField] private Transform muzzle;
        [SerializeField] private MountainArcherArrow arrowPrefab;

        [Header("Archery Ballistics & Timing")]
        [Tooltip("Arrow velocity in m/s.")]
        [SerializeField, Min(1f)] private float arrowSpeed = 40f;

        [Tooltip("Physical kinetic arrow damage.")]
        [SerializeField, Min(0f)] private float damage = 18f;

        [Tooltip("Rapid firing interval in seconds (faster than Bamboo Hunter's 2.0s).")]
        [SerializeField, Min(.1f)] private float fireInterval = 0.65f;

        [Tooltip("Arrows held in quiver before a quick restock.")]
        [SerializeField, Min(1)] private int quiverCapacity = 8;

        [Tooltip("Quick quiver restock time in seconds.")]
        [SerializeField, Min(.1f)] private float restockSeconds = 1.2f;

        private int arrowsRemaining;
        private float nextFireTime;
        private float restockEndsAt = -1f;

        public bool IsRestocking => restockEndsAt >= 0f;
        public bool NeedsRestock => arrowsRemaining == 0;
        public float RestockDuration => restockSeconds;

        public bool ClearShot(Vector3 target, LayerMask obstacles) =>
            muzzle && !Physics.Linecast(muzzle.position, target, obstacles, QueryTriggerInteraction.Ignore);

        public bool CanFire => !IsRestocking && arrowsRemaining > 0 && Time.time >= nextFireTime;

        private void Awake()
        {
            arrowsRemaining = quiverCapacity;
        }

        private void Update()
        {
            if (IsRestocking && Time.time >= restockEndsAt)
            {
                arrowsRemaining = quiverCapacity;
                restockEndsAt = -1f;
            }
        }

        public void StartRestock()
        {
            if (IsRestocking || arrowsRemaining == quiverCapacity) return;
            restockEndsAt = Time.time + restockSeconds;
        }

        public bool TryFire(Vector3 target, GameObject shooter)
        {
            if (!CanFire || !muzzle || !arrowPrefab) return false;

            Vector3 direction = (target - muzzle.position).normalized;
            if (direction.sqrMagnitude < .001f) return false;

            MountainArcherArrow arrow = Instantiate(arrowPrefab, muzzle.position, Quaternion.LookRotation(direction));
            arrow.Initialize(shooter, damage);

            Rigidbody rb = arrow.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.useGravity = false;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.linearVelocity = direction * arrowSpeed;
            }

            arrowsRemaining--;
            nextFireTime = Time.time + fireInterval;
            return true;
        }
    }
}
