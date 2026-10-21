using UnityEngine;
using JadeSeal.Combat;
using JadeSeal.EnemyAI.Core;

namespace JadeSeal.EnemyAI.Combat
{
    /// <summary>
    /// Interactive Player Combat Proxy for Play Mode testing.
    /// Implements IDamageable to take arrow damage, supports WASD movement,
    /// and allows attacking the Mountain Archer to verify the full combat loop.
    /// </summary>
    public class TestPlayerCombatTarget : MonoBehaviour, IDamageable
    {
        [Header("Player Combat Stats")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float currentHealth = 100f;
        [SerializeField] private float moveSpeed = 6.0f;
        [SerializeField] private float attackDamage = 35f;
        [SerializeField] private float attackRange = 3.5f;

        public bool IsDead => currentHealth <= 0f;
        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;

        private MountainArcherAgent targetArcher;
        private CharacterController controller;

        private void Awake()
        {
            currentHealth = maxHealth;
            controller = GetComponent<CharacterController>();
        }

        public void SetArcherReference(MountainArcherAgent archer)
        {
            targetArcher = archer;
        }

        private void Update()
        {
            if (IsDead) return;

            // Movement and Attack inputs
            HandleMovement();
            HandleAttackInput();
        }

        private void HandleAttackInput()
        {
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
            {
                AttackTargetArcher();
            }
#else
            try
            {
                if (Input.GetMouseButtonDown(0))
                {
                    AttackTargetArcher();
                }
            }
            catch (System.Exception)
            {
                // New Input System is active in PlayerSettings, ignore legacy error
            }
#endif
        }

        private void HandleMovement()
        {
            float h = 0f;
            float v = 0f;

#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) h -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) h += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v -= 1f;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v += 1f;
            }
#else
            try
            {
                h = Input.GetAxisRaw("Horizontal");
                v = Input.GetAxisRaw("Vertical");
            }
            catch (System.InvalidOperationException)
            {
                // New Input System is active in PlayerSettings
            }
#endif

            if (Mathf.Abs(h) > 0.01f || Mathf.Abs(v) > 0.01f)
            {
                Vector3 moveDir = new Vector3(h, 0, v).normalized;
                transform.position += moveDir * (moveSpeed * Time.deltaTime);

                if (moveDir.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(moveDir), Time.deltaTime * 10f);
                }
            }
        }

        public void AttackTargetArcher()
        {
            AttackNearbyEnemies();
        }

        public void AttackNearbyEnemies()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position + transform.forward * 1.0f, attackRange);
            IDamageable closestTarget = null;
            float closestDist = float.MaxValue;
            Vector3 hitPoint = Vector3.zero;

            foreach (var h in hits)
            {
                if (h.gameObject == gameObject) continue;
                var damageable = h.GetComponent<IDamageable>();
                if (damageable != null && !damageable.IsDead)
                {
                    float dist = Vector3.Distance(transform.position, h.transform.position);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closestTarget = damageable;
                        hitPoint = h.transform.position + Vector3.up * 1.2f;
                    }
                }
            }

            if (closestTarget != null)
            {
                Vector3 hitNormal = (transform.position - hitPoint).normalized;
                closestTarget.TakeDamage(attackDamage, hitPoint, hitNormal);
                Debug.Log($"<color=#FFFF00>[PlayerAttack]</color> Struck enemy for {attackDamage} damage!");
            }
        }

        public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal)
        {
            if (IsDead) return;

            currentHealth = Mathf.Max(0f, currentHealth - amount);
            Debug.Log($"<color=#FF3333>[PlayerHealth]</color> Player took {amount:F1} damage! Current HP: {currentHealth:F0}/{maxHealth:F0}");

            if (currentHealth <= 0f)
            {
                Debug.Log("<color=#FF0000><b>[PlayerHealth]</b> Player was defeated!</color>");
            }
        }

        public void TakeDamage(float amount)
        {
            TakeDamage(amount, transform.position, -transform.forward);
        }

        private void OnGUI()
        {
            // Player HUD
            GUI.Box(new Rect(20, Screen.height - 100, 240, 85), GUIContent.none);
            GUILayout.BeginArea(new Rect(25, Screen.height - 95, 230, 75));
            GUILayout.Label("<color=#00CCFF><b>👤 PLAYER STATUS</b></color>", new GUIStyle(GUI.skin.label) { richText = true });
            GUILayout.Label($"<b>Health:</b> <color={(currentHealth > 30 ? "#00FF99" : "#FF3333")}>{currentHealth:F0} / {maxHealth:F0}</color>", new GUIStyle(GUI.skin.label) { richText = true });
            GUILayout.Label("<size=10><color=gray>[W,A,S,D] Move | [L-Click] Attack</color></size>", new GUIStyle(GUI.skin.label) { richText = true });
            GUILayout.EndArea();

            // Multi-Agent Status Display at Top-Right
            GUI.Box(new Rect(Screen.width - 290, 20, 270, 160), GUIContent.none);
            GUILayout.BeginArea(new Rect(Screen.width - 285, 25, 260, 150));
            GUILayout.Label("<color=#FFD700><b>🏯 THE JADE SEAL — ENEMY AGENTS</b></color>", new GUIStyle(GUI.skin.label) { richText = true });

            var archer = Object.FindFirstObjectByType<MountainArcherAgent>();
            if (archer != null)
            {
                var h = archer.GetComponent<MountainArcherHealth>();
                GUILayout.Label($"<color=#55FF55>🏹 Archer:</color> {archer.CurrentState} | HP: {(h != null ? h.CurrentHealth.ToString("F0") : "?")}", new GUIStyle(GUI.skin.label) { richText = true });
            }

            var guard = Object.FindFirstObjectByType<JadeSeal.EnemyAI.RoyalGuard.RoyalGuardAgent>();
            if (guard != null)
            {
                var h = guard.GetComponent<JadeSeal.EnemyAI.RoyalGuard.RoyalGuardHealth>();
                GUILayout.Label($"<color=#FF5555>🛡️ Guard:</color> {guard.CurrentState} | HP: {(h != null ? h.CurrentHealth.ToString("F0") : "?")}", new GUIStyle(GUI.skin.label) { richText = true });
            }

            var hunter = Object.FindFirstObjectByType<JadeSeal.EnemyAI.BambooHunter.BambooHunterAgent>();
            if (hunter != null)
            {
                var h = hunter.GetComponent<JadeSeal.EnemyAI.BambooHunter.BambooHunterHealth>();
                GUILayout.Label($"<color=#33FF88>🎋 Hunter:</color> {hunter.CurrentState} | HP: {(h != null ? h.CurrentHealth.ToString("F0") : "?")}", new GUIStyle(GUI.skin.label) { richText = true });
            }

            var master = Object.FindFirstObjectByType<JadeSeal.EnemyAI.ClanMaster.ClanMasterAgent>();
            if (master != null)
            {
                var h = master.GetComponent<JadeSeal.EnemyAI.ClanMaster.ClanMasterHealth>();
                GUILayout.Label($"<color=#33CCFF>🥋 Master:</color> {master.CurrentState} | HP: {(h != null ? h.CurrentHealth.ToString("F0") : "?")}", new GUIStyle(GUI.skin.label) { richText = true });
            }

            GUILayout.EndArea();
        }
    }
}
