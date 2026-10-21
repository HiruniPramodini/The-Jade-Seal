using UnityEngine;

namespace JadeSeal.RoyalGuard
{
    public sealed class RoyalGuardCrossbow : MonoBehaviour
    {
        [SerializeField] private Transform muzzle;
        [SerializeField] private RoyalGuardProjectile boltPrefab;
        [SerializeField, Min(1f)] private float boltSpeed = 38f;
        [SerializeField, Min(0f)] private float damage = 15f;
        [SerializeField, Min(.1f)] private float fireInterval = .75f;
        [SerializeField, Min(1)] private int magazineSize = 5;
        [SerializeField, Min(.1f)] private float reloadSeconds = 2f;
        private int ammunition;
        private float nextFireTime;
        private float reloadEndsAt = -1f;
        public bool IsReloading => reloadEndsAt >= 0f;
        public bool NeedsReload => ammunition == 0;
        public float ReloadDuration => reloadSeconds;
        public bool ClearShot(Vector3 target, LayerMask obstacles) => muzzle && !Physics.Linecast(muzzle.position, target, obstacles, QueryTriggerInteraction.Ignore);
        public bool CanFire => !IsReloading && ammunition > 0 && Time.time >= nextFireTime;
        private void Awake() { ammunition = magazineSize; }
        private void Update()
        {
            if (IsReloading && Time.time >= reloadEndsAt)
            {
                ammunition = magazineSize;
                reloadEndsAt = -1f;
            }
        }
        public void StartReload()
        {
            if (IsReloading || ammunition == magazineSize) return;
            reloadEndsAt = Time.time + reloadSeconds;
        }
        public bool TryFire(Vector3 target, GameObject shooter)
        {
            if (!CanFire || !muzzle || !boltPrefab) return false;
            Vector3 direction = (target - muzzle.position).normalized;
            if (direction.sqrMagnitude < .001f) return false;
            RoyalGuardProjectile bolt = Instantiate(boltPrefab, muzzle.position, Quaternion.LookRotation(direction));
            bolt.Initialize(shooter, damage);
            Rigidbody rb = bolt.GetComponent<Rigidbody>();
            rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.linearVelocity = direction * boltSpeed;
            ammunition--;
            nextFireTime = Time.time + fireInterval;
            return true;
        }
    }
}
