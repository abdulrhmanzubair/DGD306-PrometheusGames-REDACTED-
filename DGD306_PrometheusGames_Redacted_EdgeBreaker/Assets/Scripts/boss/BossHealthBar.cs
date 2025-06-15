using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BossHealthBar : MonoBehaviour
{
    [Header("UI References")]
    public Slider healthSlider;
    public Image fillImage;
    public TextMeshProUGUI healthText;
    public TextMeshProUGUI bossNameText;
    public TextMeshProUGUI enemiesRemainingText;

    [Header("Visual Settings")]
    public Color fullHealthColor = Color.green;
    public Color lowHealthColor = Color.red;
    public string bossName = "Boss Enemy";
    public bool showEnemyCount = true;

    [Header("Animation")]
    public bool animateHealthChange = true;
    public float animationSpeed = 2f;

    private BossEnemy bossReference;
    private float targetHealth;
    private float currentDisplayHealth;
    private int maxHealth;

    public void Initialize(BossEnemy boss)
    {
        bossReference = boss;
        maxHealth = boss.maxHealth;
        targetHealth = boss.health;
        currentDisplayHealth = boss.health;

        // Set up UI elements
        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = maxHealth;
        }

        if (bossNameText != null)
        {
            bossNameText.text = bossName;
        }

        UpdateDisplay();
    }

    private void Update()
    {
        if (bossReference == null) return;

        // Update enemy count if enabled
        if (showEnemyCount && enemiesRemainingText != null)
        {
            int remaining = bossReference.GetRemainingEnemies();
            bool allDefeated = bossReference.AreAllEnemiesDefeated();

            if (allDefeated)
            {
                enemiesRemainingText.text = "Boss Vulnerable!";
                enemiesRemainingText.color = Color.red;
            }
            else
            {
                enemiesRemainingText.text = $"Enemies: {remaining}";
                enemiesRemainingText.color = Color.white;
            }
        }

        // Animate health change if enabled
        if (animateHealthChange && Mathf.Abs(currentDisplayHealth - targetHealth) > 0.1f)
        {
            currentDisplayHealth = Mathf.Lerp(currentDisplayHealth, targetHealth, Time.deltaTime * animationSpeed);
            UpdateSliderDisplay();
        }
    }

    public void UpdateHealth(int currentHealth, int maxHP)
    {
        targetHealth = currentHealth;
        maxHealth = maxHP;

        if (!animateHealthChange)
        {
            currentDisplayHealth = currentHealth;
            UpdateSliderDisplay();
        }

        UpdateDisplay();
    }

    private void UpdateSliderDisplay()
    {
        if (healthSlider != null)
        {
            healthSlider.value = currentDisplayHealth;
        }

        // Update fill color based on health percentage
        if (fillImage != null)
        {
            float healthPercentage = currentDisplayHealth / maxHealth;
            fillImage.color = Color.Lerp(lowHealthColor, fullHealthColor, healthPercentage);
        }
    }

    private void UpdateDisplay()
    {
        UpdateSliderDisplay();

        // Update health text
        if (healthText != null)
        {
            healthText.text = $"{Mathf.RoundToInt(currentDisplayHealth)} / {maxHealth}";
        }
    }
}