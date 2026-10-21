using System;
using UnityEngine;
namespace JadeSeal.RoyalGuard
{
    [RequireComponent(typeof(RoyalGuardNavigation), typeof(RoyalGuardPerception), typeof(RoyalGuardTactics))]
    [RequireComponent(typeof(RoyalGuardHealth), typeof(RoyalGuardCrossbow))]
    public sealed class RoyalGuardBrain : MonoBehaviour
    {
        public RoyalGuardSettings settings;
        public Transform[] patrolWaypoints;
        public RoyalGuardUtility.State CurrentAction { get; private set; }
        public string DebugStatus => $"{CurrentAction}; visible={sense.Visible}; memory={sense.HasMemory(settings.memorySeconds)}; cover={tactics.Score:F2}; navigationFailed={nav.Failed}";
        public event Action<RoyalGuardUtility.State> StateChanged;
        RoyalGuardPerception sense;
        RoyalGuardNavigation nav;
        RoyalGuardTactics tactics;
        RoyalGuardHealth health;
        RoyalGuardCrossbow weapon;
        float nextThink, nextCover, entered, lastDamage;
        int waypoint;
        bool retreating, ownsSettings;
        void Awake()
        {
            if (!settings) { settings = ScriptableObject.CreateInstance<RoyalGuardSettings>(); ownsSettings = true; }
            sense = GetComponent<RoyalGuardPerception>(); nav = GetComponent<RoyalGuardNavigation>();
            tactics = GetComponent<RoyalGuardTactics>(); health = GetComponent<RoyalGuardHealth>(); weapon = GetComponent<RoyalGuardCrossbow>();
            health.Died += Die; health.Damaged += Hit;
        }
        void Hit(float damage) { lastDamage = Time.time; nextThink = 0; }
        void Die() { nav.Stop(); tactics.Release(); enabled = false; }
        void OnDisable() { if (nav) nav.Stop(); if (tactics) tactics.Release(); }
        void OnDestroy()
        {
            if (health) { health.Died -= Die; health.Damaged -= Hit; }
            if (ownsSettings && settings) Destroy(settings);
        }
        void Update()
        {
            if (health.IsDead) return;
            if (!nav.Ready) { tactics.Release(); return; }
            if (Time.time >= nextThink)
            {
                nextThink = Time.time + Mathf.Max(.05f, settings.thinkInterval);
                sense.Sample(settings);
                bool memory = sense.HasMemory(settings.memorySeconds);
                if (health.Ratio <= settings.retreatThreshold && (sense.Visible || memory)) retreating = true;
                if (health.Ratio >= Mathf.Max(settings.retreatThreshold + .05f, settings.recoveryTarget)) retreating = false;
                if (Time.time >= nextCover && (sense.Visible || memory))
                {
                    nextCover = Time.time + 1;
                    tactics.Select(nav, sense, settings, retreating);
                }
                if (!sense.Visible && !memory) tactics.Release();
                if (Time.time - entered > settings.maximumStateSeconds &&
                    (CurrentAction == RoyalGuardUtility.State.SeekCover || CurrentAction == RoyalGuardUtility.State.Reposition))
                { tactics.Reject(); nextCover = 0; entered = Time.time; nav.Refresh(); }
                var cover = tactics.Selected;
                bool hide = cover && Vector3.Distance(transform.position, cover.HidePosition) < .8f;
                bool peek = cover && Vector3.Distance(transform.position, cover.PeekPosition) < .8f;
                var context = new RoyalGuardUtility.Context {
                    visible = sense.Visible, memory = memory, lowHealth = retreating, cover = cover,
                    coverScore = tactics.Score, atHide = hide, atPeek = peek,
                    inRange = sense.Visible && Vector3.Distance(transform.position, sense.AimPosition) <= settings.attackRange,
                    firingLine = sense.Visible && weapon.ClearShot(sense.AimPosition, sense.obstacles),
                    reload = weapon.NeedsReload || weapon.IsReloading
                };
                var selected = RoyalGuardUtility.Choose(context, CurrentAction, Time.time - entered, settings);
                if (selected != CurrentAction) { CurrentAction = selected; entered = Time.time; nav.Refresh(); StateChanged?.Invoke(selected); }
            }
            Act();
            bool safe = !sense.Visible && Time.time - lastDamage >= settings.recoveryDelay;
            if (retreating && safe) health.Recover(settings.recoveryPerSecond * Time.deltaTime, settings.recoveryTarget);
        }
        void Move(Vector3 position)
        {
            if (nav.Move(position, settings)) return;
            tactics.Reject(); nextCover = 0;
            // Failed patrol destinations are skipped; failed memories are abandoned after their bounded lifetime.
            if (CurrentAction == RoyalGuardUtility.State.Patrol && patrolWaypoints != null && patrolWaypoints.Length > 0)
                waypoint = (waypoint + 1) % patrolWaypoints.Length;
        }
        void Act()
        {
            var cover = tactics.Selected;
            switch (CurrentAction)
            {
                case RoyalGuardUtility.State.Patrol:
                    if (patrolWaypoints == null || patrolWaypoints.Length == 0) { nav.Stop(); break; }
                    waypoint %= patrolWaypoints.Length;
                    var point = patrolWaypoints[waypoint];
                    if (!point) { waypoint = (waypoint + 1) % patrolWaypoints.Length; break; }
                    if (Vector3.Distance(transform.position, point.position) < .8f) { waypoint = (waypoint + 1) % patrolWaypoints.Length; nav.Refresh(); }
                    else Move(point.position);
                    break;
                case RoyalGuardUtility.State.Investigate:
                    if (Vector3.Distance(transform.position, sense.LastPosition) > 1) Move(sense.LastPosition);
                    else { nav.Stop(); transform.Rotate(0, 45 * Time.deltaTime, 0); }
                    break;
                case RoyalGuardUtility.State.SeekCover:
                    if (cover) Move(cover.HidePosition); else nav.Stop();
                    break;
                case RoyalGuardUtility.State.Reposition:
                    if (cover) Move(cover.PeekPosition);
                    else if (sense.Visible)
                    {
                        Vector3 away = transform.position - sense.LastPosition; away.y = 0;
                        if (away.sqrMagnitude < .01f) away = -transform.forward;
                        Vector3 flank = Vector3.Cross(Vector3.up, away.normalized);
                        Move(sense.LastPosition + away.normalized * settings.attackRange * .7f + flank * 2);
                    }
                    else nav.Stop();
                    Face();
                    break;
                case RoyalGuardUtility.State.Attack:
                    nav.Stop(); Face();
                    if (sense.Visible && sense.player && sense.player.gameObject.activeInHierarchy && Vector3.Distance(transform.position, sense.AimPosition) <= settings.attackRange &&
                        weapon.ClearShot(sense.AimPosition, sense.obstacles)) weapon.TryFire(sense.AimPosition, gameObject);
                    break;
                case RoyalGuardUtility.State.Reload:
                    if (cover && Vector3.Distance(transform.position, cover.HidePosition) > .8f) Move(cover.HidePosition); else nav.Stop();
                    weapon.StartReload(); break;
                case RoyalGuardUtility.State.Retreat:
                    if (cover) Move(cover.HidePosition);
                    else if (sense.HasMemory(settings.memorySeconds))
                    {
                        Vector3 away = transform.position - sense.LastPosition; away.y = 0;
                        if (away.sqrMagnitude < .01f) away = -transform.forward;
                        Move(transform.position + away.normalized * 5);
                    }
                    else nav.Stop();
                    break;
            }
        }
        void Face()
        {
            if (!sense.Visible) return;
            Vector3 direction = sense.AimPosition - transform.position; direction.y = 0;
            if (direction.sqrMagnitude > .01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 8);
        }
        void OnDrawGizmosSelected()
        {
            if (!settings) return;
            Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, settings.detectionRange);
            Gizmos.color = Color.red; Gizmos.DrawWireSphere(transform.position, settings.attackRange);
            Vector3 origin = transform.position + Vector3.up * 1.6f;
            Gizmos.DrawRay(origin, Quaternion.Euler(0, settings.fieldOfView * .5f, 0) * transform.forward * settings.detectionRange);
            Gizmos.DrawRay(origin, Quaternion.Euler(0, -settings.fieldOfView * .5f, 0) * transform.forward * settings.detectionRange);
            if (Application.isPlaying && sense && sense.HasMemory(settings.memorySeconds)) { Gizmos.color = Color.magenta; Gizmos.DrawSphere(sense.LastPosition, .25f); }
        }
    }
}


