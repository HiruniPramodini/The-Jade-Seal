using UnityEngine;

namespace JadeSeal.BambooHunter
{
    /// <summary>
    /// Lightweight, self-contained runtime HUD and interactive testing interface for the Bamboo Hunter AI demo.
    /// Utilizes Unity's native Immediate Mode GUI (OnGUI) to guarantee zero UI canvas/font dependencies
    /// while displaying complete real-time tactical state, health, ammunition, perception, and debug controls.
    /// </summary>
    [AddComponentMenu("Jade Seal/Bamboo Hunter/Demo HUD")]
    [DisallowMultipleComponent]
    public sealed class BambooHunterDemoHUD : MonoBehaviour
    {
        [Header("Scene References")]
        [Tooltip("Reference to the active Bamboo Hunter enemy brain.")]
        [SerializeField] private BambooHunterBrain hunterBrain;

        [Tooltip("Reference to the player test dummy health component.")]
        [SerializeField] private BambooHunterPlayerHealthDemo playerDummyHealth;

        [Tooltip("Reference to the player test dummy transform.")]
        [SerializeField] private Transform playerDummyTransform;

        [Header("Display Options")]
        [Tooltip("Toggle HUD visibility on/off.")]
        [SerializeField] private bool showHUD = true;

        [Tooltip("Show interactive testing buttons for live scenario manipulation.")]
        [SerializeField] private bool showTestControls = true;

        /// <summary>Active Bamboo Hunter enemy brain referenced by this HUD.</summary>
        public BambooHunterBrain HunterBrain
        {
            get => hunterBrain;
            set => hunterBrain = value;
        }

        /// <summary>Target player dummy health component.</summary>
        public BambooHunterPlayerHealthDemo PlayerDummyHealth
        {
            get => playerDummyHealth;
            set => playerDummyHealth = value;
        }

        /// <summary>Target player dummy transform.</summary>
        public Transform PlayerDummyTransform
        {
            get => playerDummyTransform;
            set => playerDummyTransform = value;
        }

        private GUIStyle headerStyle;
        private GUIStyle labelStyle;
        private GUIStyle boldLabelStyle;
        private GUIStyle boxStyle;
        private GUIStyle buttonStyle;
        private bool stylesInitialized = false;

        private void Start()
        {
            FindSceneReferences();
        }

        /// <summary>
        /// Resolves missing scene references dynamically if not assigned via Inspector.
        /// </summary>
        public void FindSceneReferences()
        {
            if (hunterBrain == null)
            {
                hunterBrain = FindFirstObjectByType<BambooHunterBrain>();
            }

            if (playerDummyHealth == null)
            {
                playerDummyHealth = FindFirstObjectByType<BambooHunterPlayerHealthDemo>();
            }

            if (playerDummyTransform == null && playerDummyHealth != null)
            {
                playerDummyTransform = playerDummyHealth.transform;
            }
        }

        private void InitStyles()
        {
            if (stylesInitialized) return;

            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            headerStyle.normal.textColor = new Color(0.95f, 0.85f, 0.35f);

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft
            };
            labelStyle.normal.textColor = Color.white;

            boldLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            boldLabelStyle.normal.textColor = Color.white;

            boxStyle = new GUIStyle(GUI.skin.box);

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold
            };

            stylesInitialized = true;
        }

        private void OnGUI()
        {
            if (!showHUD) return;
            InitStyles();

            if (hunterBrain == null)
            {
                FindSceneReferences();
            }

            // Tactical Telemetry HUD Window (Top Left)
            float panelWidth = 360f;
            float panelHeight = 290f;
            GUI.Box(new Rect(10, 10, panelWidth, panelHeight), GUIContent.none, boxStyle);

            GUILayout.BeginArea(new Rect(18, 14, panelWidth - 16, panelHeight - 16));
            {
                GUILayout.Label("THE JADE SEAL — BAMBOO HUNTER AI", headerStyle);
                GUILayout.Space(4);

                if (hunterBrain != null)
                {
                    // State & Intent with color highlighting
                    string stateStr = hunterBrain.CurrentState.ToString();
                    Color stateColor = GetStateColor(hunterBrain.CurrentState);

                    GUI.color = stateColor;
                    GUILayout.Label($"TACTICAL STATE:  [{stateStr.ToUpper()}]  ({hunterBrain.StateElapsedTime:F1}s)", boldLabelStyle);
                    GUI.color = Color.white;

                    GUILayout.Label($"Tactical Intent:   {hunterBrain.CurrentIntent}", labelStyle);
                    GUILayout.Space(4);

                    // Health Bar
                    var health = hunterBrain.Health;
                    if (health != null)
                    {
                        float hpRatio = health.HealthRatio;
                        string hpText = $"Hunter Health:     {health.CurrentHealth:F0} / {health.MaxHealth:F0} HP ({hpRatio * 100f:F0}%)";
                        DrawProgressBar(hpText, hpRatio, hpRatio > 0.25f ? new Color(0.3f, 0.85f, 0.3f) : new Color(0.95f, 0.25f, 0.25f));
                    }

                    // Ammo & Bow Status
                    var bow = hunterBrain.Bow;
                    if (bow != null)
                    {
                        string ammoText = $"Quiver Ammo:       {bow.CurrentArrows} / {(bow.Settings ? bow.Settings.quiverCapacity : 6)} arrows (Shots fired: {bow.ConsecutiveShots})";
                        float ammoRatio = (float)bow.CurrentArrows / Mathf.Max(1, bow.Settings ? bow.Settings.quiverCapacity : 6);
                        DrawProgressBar(ammoText, ammoRatio, new Color(0.9f, 0.7f, 0.2f));

                        string drawStatus = bow.IsDrawing
                            ? $"Drawing bowstring ({bow.DrawProgress * 100f:F0}% tension)"
                            : (bow.IsCooldownActive ? $"Cooldown ({bow.CooldownRemaining:F1}s)" : "Bow relaxed");
                        GUILayout.Label($"Bow Status:        {drawStatus}", labelStyle);
                    }

                    // Perception Status
                    var perception = hunterBrain.Perception;
                    if (perception != null)
                    {
                        string visText;
                        if (perception.CanSeeTarget)
                        {
                            visText = $"<color=#55FF55>IN SIGHT</color> (Dist: {perception.TargetDistance:F1}m, FOV: {perception.LastFovAngle:F0}°)";
                        }
                        else
                        {
                            string reasonStr = perception.LastVisibilityRejectionReason.ToString();
                            visText = $"<color=#FF6666>HIDDEN</color> [{reasonStr}] (Dist: {perception.TargetDistance:F1}m, FOV: {perception.LastFovAngle:F0}°)";
                        }

                        string shotText;
                        if (perception.HasClearLineOfFire)
                        {
                            shotText = "<color=#55FF55>CLEAR</color>";
                        }
                        else
                        {
                            string blocker = !string.IsNullOrEmpty(perception.LastLineOfFireBlocker)
                                ? perception.LastLineOfFireBlocker
                                : (!perception.CanSeeTarget ? "Target Hidden" : "Trajectory Obstructed");
                            shotText = $"<color=#FF6666>BLOCKED</color> [{blocker}]";
                        }

                        GUILayout.Label($"Target Visibility: {visText}", labelStyle);
                        GUILayout.Label($"Line of Fire:      {shotText}", labelStyle);

                        if (!string.IsNullOrEmpty(perception.LastOcclusionCollider) && !perception.CanSeeTarget)
                        {
                            GUILayout.Label($"Sight Occluder:    <color=#FFAA44>{perception.LastOcclusionCollider}</color>", labelStyle);
                        }

                        if (perception.HasTargetMemory(perception.Settings ? perception.Settings.memoryDuration : 5f))
                        {
                            GUILayout.Label($"Target Memory:     Active ({perception.MemoryAge:F1}s ago at {perception.LastKnownPosition})", labelStyle);
                        }
                    }

                    // Cover System Status
                    var cover = hunterBrain.CoverSystem;
                    if (cover != null && cover.SelectedCover != null)
                    {
                        string coverText = $"{cover.SelectedCover.name} (At cover: {(cover.IsAtCover ? "YES" : "NO")})";
                        GUILayout.Label($"Selected Cover:    {coverText}", labelStyle);
                    }
                }
                else
                {
                    FindSceneReferences();
                    if (hunterBrain == null)
                    {
                        GUILayout.Label("Waiting for Bamboo Hunter Brain in scene...", labelStyle);
                    }
                }

                GUILayout.Space(4);

                // Player Dummy Health
                if (playerDummyHealth != null)
                {
                    float dummyRatio = playerDummyHealth.HealthRatio;
                    string dummyText = $"Player Dummy:      {playerDummyHealth.CurrentHealth:F0} / {playerDummyHealth.MaxHealth:F0} HP";
                    DrawProgressBar(dummyText, dummyRatio, playerDummyHealth.IsDead ? Color.gray : new Color(0.2f, 0.7f, 1.0f));
                }
            }
            GUILayout.EndArea();

            // Interactive Live Testing Controls (Top Right)
            if (showTestControls)
            {
                float ctrlWidth = 220f;
                float ctrlHeight = 220f;
                float ctrlX = Screen.width - ctrlWidth - 10f;
                GUI.Box(new Rect(ctrlX, 10, ctrlWidth, ctrlHeight), GUIContent.none, boxStyle);

                GUILayout.BeginArea(new Rect(ctrlX + 8, 14, ctrlWidth - 16, ctrlHeight - 16));
                {
                    GUILayout.Label("INTERACTIVE TESTING", headerStyle);
                    GUILayout.Space(4);

                    if (GUILayout.Button("Damage Hunter 30 HP", buttonStyle))
                    {
                        if (hunterBrain != null && hunterBrain.Health != null)
                        {
                            hunterBrain.Health.TakeDamage(30f);
                        }
                    }

                    if (GUILayout.Button("Damage Hunter to Retreat (<25%)", buttonStyle))
                    {
                        if (hunterBrain != null && hunterBrain.Health != null)
                        {
                            float targetHp = hunterBrain.Health.MaxHealth * 0.20f;
                            float dmg = Mathf.Max(0f, hunterBrain.Health.CurrentHealth - targetHp);
                            hunterBrain.Health.TakeDamage(dmg);
                        }
                    }

                    if (GUILayout.Button("Lethal Hit on Hunter", buttonStyle))
                    {
                        if (hunterBrain != null && hunterBrain.Health != null)
                        {
                            hunterBrain.Health.TakeDamage(hunterBrain.Health.MaxHealth + 10f);
                        }
                    }

                    if (GUILayout.Button("Reset Hunter Health & Quiver", buttonStyle))
                    {
                        if (hunterBrain != null)
                        {
                            if (hunterBrain.Health != null) hunterBrain.Health.ResetHealth();
                            if (hunterBrain.Bow != null) hunterBrain.Bow.RestockQuiver();
                            hunterBrain.TransitionTo(BambooHunterState.Patrol, force: true);
                        }
                    }

                    if (GUILayout.Button("Reset Player Dummy (100 HP)", buttonStyle))
                    {
                        if (playerDummyHealth != null)
                        {
                            playerDummyHealth.ResetHealth();
                        }
                    }

                    if (playerDummyTransform != null)
                    {
                        bool active = playerDummyTransform.gameObject.activeSelf;
                        string toggleText = active ? "Hide Player Dummy (Break LOS)" : "Show Player Dummy (Restore LOS)";
                        if (GUILayout.Button(toggleText, buttonStyle))
                        {
                            playerDummyTransform.gameObject.SetActive(!active);
                        }
                    }
                }
                GUILayout.EndArea();
            }
        }

        private void DrawProgressBar(string label, float ratio, Color barColor)
        {
            GUILayout.Label(label, labelStyle);
            Rect r = GUILayoutUtility.GetRect(18, 10);
            GUI.color = new Color(0.15f, 0.15f, 0.15f, 0.8f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);

            GUI.color = barColor;
            Rect fillRect = new Rect(r.x, r.y, r.width * Mathf.Clamp01(ratio), r.height);
            GUI.DrawTexture(fillRect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUILayout.Space(2);
        }

        private static Color GetStateColor(BambooHunterState state)
        {
            switch (state)
            {
                case BambooHunterState.Patrol: return new Color(0.4f, 0.9f, 0.4f);
                case BambooHunterState.Investigate: return new Color(1.0f, 0.85f, 0.2f);
                case BambooHunterState.Aim: return new Color(1.0f, 0.55f, 0.1f);
                case BambooHunterState.Shoot: return new Color(1.0f, 0.2f, 0.2f);
                case BambooHunterState.Reposition: return new Color(0.3f, 0.65f, 1.0f);
                case BambooHunterState.Retreat: return new Color(0.9f, 0.3f, 0.9f);
                case BambooHunterState.Dead: return new Color(0.5f, 0.5f, 0.5f);
                default: return Color.white;
            }
        }
    }
}
