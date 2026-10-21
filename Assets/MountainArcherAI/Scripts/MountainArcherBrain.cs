using System;
using UnityEngine;

namespace JadeSeal.MountainArcher
{
    [RequireComponent(typeof(MountainArcherNavigation), typeof(MountainArcherPerception), typeof(MountainArcherTactics))]
    [RequireComponent(typeof(MountainArcherHealth), typeof(MountainArcherBow))]
    public sealed class MountainArcherBrain : MonoBehaviour
    {
        public MountainArcherSettings settings;
        public Transform[] patrolWaypoints;
        public MountainArcherUtility.State CurrentAction { get; private set; }

        public string DebugStatus => $"{CurrentAction}; visible={sense.Visible}; memory={sense.HasMemory(settings.memorySeconds)}; cover={tactics.Score:F2}; navigationFailed={nav.Failed}";
        public event Action<MountainArcherUtility.State> StateChanged;

        private MountainArcherPerception sense;
        private MountainArcherNavigation nav;
        private MountainArcherTactics tactics;
        private MountainArcherHealth health;
        private MountainArcherBow weapon;

        private float nextThink, nextCover, entered, lastDamage;
        private int waypoint;
        private bool retreating, ownsSettings;

        private void Awake()
        {
            if (!settings)
            {
                settings = ScriptableObject.CreateInstance<MountainArcherSettings>();
                ownsSettings = true;
            }
            sense = GetComponent<MountainArcherPerception>();
            nav = GetComponent<MountainArcherNavigation>();
            tactics = GetComponent<MountainArcherTactics>();
            health = GetComponent<MountainArcherHealth>();
            weapon = GetComponent<MountainArcherBow>();

            health.Died += Die;
            health.Damaged += Hit;
        }

        private void Hit(float damage)
        {
            lastDamage = Time.time;
            nextThink = 0f;
        }

        private void Die()
        {
            nav.Stop();
            tactics.Release();
            enabled = false;
        }

        private void OnDisable()
        {
            if (nav) nav.Stop();
            if (tactics) tactics.Release();
        }

        private void OnDestroy()
        {
            if (health)
            {
                health.Died -= Die;
                health.Damaged -= Hit;
            }
            if (ownsSettings && settings) Destroy(settings);
        }

        private void Update()
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
                    nextCover = Time.time + 1f;
                    tactics.Select(nav, sense, settings, retreating);
                }

                if (!sense.Visible && !memory) tactics.Release();

                if (Time.time - entered > settings.maximumStateSeconds &&
                    (CurrentAction == MountainArcherUtility.State.SeekCover || CurrentAction == MountainArcherUtility.State.Reposition))
                {
                    tactics.Reject();
                    nextCover = 0f;
                    entered = Time.time;
                    nav.Refresh();
                }

                var cover = tactics.Selected;
                bool hide = cover && Vector3.Distance(transform.position, cover.HidePosition) < .8f;
                bool peek = cover && Vector3.Distance(transform.position, cover.PeekPosition) < .8f;

                var context = new MountainArcherUtility.Context
                {
                    visible = sense.Visible,
                    memory = memory,
                    lowHealth = retreating,
                    cover = cover != null,
                    atHide = hide,
                    atPeek = peek,
                    inRange = (sense.Visible || memory) && Vector3.Distance(transform.position, sense.LastPosition) <= settings.attackRange,
                    firingLine = sense.Visible && weapon.ClearShot(sense.LastPosition + Vector3.up * 1f, sense.obstacles),
                    reload = weapon.NeedsRestock,
                    coverScore = tactics.Score
                };

                var chosen = MountainArcherUtility.Choose(context, CurrentAction, Time.time - entered, settings);
                if (chosen != CurrentAction)
                {
                    CurrentAction = chosen;
                    entered = Time.time;
                    StateChanged?.Invoke(chosen);
                    nav.Refresh();
                }
            }

            ExecuteAction();
        }

        private void ExecuteAction()
        {
            if (!sense.Visible && Time.time - lastDamage >= settings.recoveryDelay)
            {
                health.Recover(settings.recoveryPerSecond * Time.deltaTime, settings.recoveryTarget);
            }

            switch (CurrentAction)
            {
                case MountainArcherUtility.State.Patrol:
                    if (patrolWaypoints != null && patrolWaypoints.Length > 0)
                    {
                        var targetWp = patrolWaypoints[waypoint % patrolWaypoints.Length];
                        if (targetWp && nav.Move(targetWp.position, settings) && nav.Arrived)
                        {
                            waypoint++;
                        }
                    }
                    break;

                case MountainArcherUtility.State.Investigate:
                    if (sense.HasMemory(settings.memorySeconds))
                    {
                        nav.Move(sense.LastPosition, settings);
                    }
                    break;

                case MountainArcherUtility.State.SeekCover:
                    if (tactics.Selected)
                    {
                        nav.Move(tactics.Selected.HidePosition, settings);
                    }
                    break;

                case MountainArcherUtility.State.Reposition:
                    if (tactics.Selected)
                    {
                        nav.Move(tactics.Selected.PeekPosition, settings);
                    }
                    else if (sense.Visible)
                    {
                        // High mobility agile repositioning: flank sideways while maintaining line of sight
                        Vector3 flank = Vector3.Cross((sense.LastPosition - transform.position).normalized, Vector3.up) * 4f;
                        nav.Move(transform.position + flank, settings);
                    }
                    break;

                case MountainArcherUtility.State.Attack:
                    nav.Stop();
                    if (sense.Visible)
                    {
                        Vector3 lookTarget = sense.LastPosition;
                        lookTarget.y = transform.position.y;
                        transform.LookAt(lookTarget);
                        weapon.TryFire(sense.LastPosition + Vector3.up * 1f, gameObject);
                    }
                    break;

                case MountainArcherUtility.State.Reload:
                    weapon.StartRestock();
                    if (tactics.Selected) nav.Move(tactics.Selected.HidePosition, settings);
                    break;

                case MountainArcherUtility.State.Retreat:
                    if (tactics.Selected)
                    {
                        nav.Move(tactics.Selected.HidePosition, settings);
                    }
                    else if (sense.Visible)
                    {
                        // Agile backward sprint retreat away from threat
                        Vector3 away = transform.position + (transform.position - sense.LastPosition).normalized * 8f;
                        nav.Move(away, settings);
                    }
                    break;
            }
        }
    }
}
