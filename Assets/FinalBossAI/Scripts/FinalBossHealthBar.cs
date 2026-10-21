using UnityEngine;
using UnityEngine.UI;

namespace JadeSeal.FinalBoss
{
    /// <summary>
    /// Dedicated Boss Health HUD.
    /// Displays the Boss's name, animated health slider, and numeric health readout.
    /// Fades or disables itself upon the boss's defeat.
    /// </summary>
    public sealed class FinalBossHealthBar : MonoBehaviour
    {
        [Header("Target Boss")]
        [SerializeField] private FinalBossHealth bossHealth;

        [Header("UI Elements")]
        [SerializeField] private GameObject hudRoot;
        [SerializeField] private Text bossNameText;
        [SerializeField] private Slider healthSlider;
        [SerializeField] private Image healthFillImage;
        [SerializeField] private Text healthValueText;

        [Header("Styling & Animation")]
        [SerializeField] private float smoothSpeed = 5f;
        [SerializeField] private Color healthyColor = new Color(0.9f, 0.2f, 0.2f, 1f);
        [SerializeField] private Color criticalColor = new Color(0.6f, 0.05f, 0.05f, 1f);

        private float targetFillRatio = 1f;

        private void Awake()
        {
            if (hudRoot == null) hudRoot = gameObject;
            if (bossHealth == null) bossHealth = FindFirstObjectByType<FinalBossHealth>();
        }

        private void OnEnable()
        {
            if (bossHealth != null)
            {
                bossHealth.HealthChanged += OnHealthChanged;
                bossHealth.Died += OnBossDied;
                targetFillRatio = bossHealth.Ratio;
                UpdateDisplay(bossHealth.CurrentHealth, bossHealth.MaxHealth);
            }
        }

        private void OnDisable()
        {
            if (bossHealth != null)
            {
                bossHealth.HealthChanged -= OnHealthChanged;
                bossHealth.Died -= OnBossDied;
            }
        }

        public void BindBoss(FinalBossHealth health, string displayName)
        {
            if (bossHealth != null)
            {
                bossHealth.HealthChanged -= OnHealthChanged;
                bossHealth.Died -= OnBossDied;
            }

            bossHealth = health;
            if (bossNameText != null && !string.IsNullOrEmpty(displayName))
            {
                bossNameText.text = displayName;
            }

            if (bossHealth != null)
            {
                bossHealth.HealthChanged += OnHealthChanged;
                bossHealth.Died += OnBossDied;
                targetFillRatio = bossHealth.Ratio;
                UpdateDisplay(bossHealth.CurrentHealth, bossHealth.MaxHealth);
            }
        }

        private void Update()
        {
            if (healthSlider != null)
            {
                healthSlider.value = Mathf.Lerp(healthSlider.value, targetFillRatio, Time.deltaTime * smoothSpeed);
            }

            if (healthFillImage != null)
            {
                healthFillImage.fillAmount = Mathf.Lerp(healthFillImage.fillAmount, targetFillRatio, Time.deltaTime * smoothSpeed);
                healthFillImage.color = Color.Lerp(criticalColor, healthyColor, targetFillRatio);
            }
        }

        private void OnHealthChanged(float current, float max)
        {
            targetFillRatio = Mathf.Clamp01(current / Mathf.Max(1f, max));
            UpdateDisplay(current, max);
        }

        private void UpdateDisplay(float current, float max)
        {
            if (healthValueText != null)
            {
                healthValueText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
            }
        }

        private void OnBossDied()
        {
            targetFillRatio = 0f;
            if (healthValueText != null) healthValueText.text = "DEFEATED";
            // Optional: hide after 3 seconds
            Invoke(nameof(HideHUD), 3f);
        }

        private void HideHUD()
        {
            if (hudRoot != null) hudRoot.SetActive(false);
        }
    }
}
