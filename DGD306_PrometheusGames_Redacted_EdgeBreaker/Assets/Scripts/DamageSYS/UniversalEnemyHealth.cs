using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Universal health system for all enemy types
/// Can be attached to any enemy GameObject regardless of AI type
/// </summary>
public class UniversalEnemyHealth : MonoBehaviour, IDamageable
{
    [Header("Health Settings")]
    public int maxHealth = 100;
    public int currentHealth;
    public int scoreValue = 50;
    public bool canTakeDamage = true;

    [Header("Enemy Type")]
    public EnemyType enemyType = EnemyType.Basic;
    public string enemyName = "Enemy";

    [Header("Health UI")]
    public Canvas healthUICanvas; // Drag a Canvas prefab here, or it will be auto-created
    public Slider healthBar;
    public Text healthText;
    public Image healthBarFill;
    public Color healthyColor = Color.green;
    public Color damagedColor = Color.yellow;
    public Color criticalColor = Color.red;
    public Vector3 healthBarOffset = new Vector3(0, 1.5f, 0);
    public bool showHealthBar = true;
    public bool hideWhenFullHealth = false;

    [Header("Damage Visual Effects")]
    public SpriteRenderer enemySprite;
    public Color damageFlashColor = Color.red;
    public float flashDuration = 0.1f;
    public GameObject damageTextPrefab;
    public Transform damageTextSpawnPoint;

    [Header("Audio")]
    public AudioClip[] hurtSounds;
    public AudioClip[] deathSounds;
    [Range(0f, 1f)] public float hurtVolume = 0.7f;
    [Range(0f, 1f)] public float deathVolume = 0.8f;

    [Header("Death Settings")]
    public GameObject deathEffectPrefab;
    public float deathEffectLifetime = 2f;
    public bool dropItems = false;
    public GameObject[] possibleDrops;
    [Range(0f, 1f)] public float dropChance = 0.3f;

    [Header("Knockback Settings")]
    public bool canBeKnockedBack = true;
    public float knockbackResistance = 1f; // Higher values = less knockback

    // Private variables
    private bool isDead = false;
    private AudioSource audioSource;
    private Color originalSpriteColor;
    private Rigidbody2D rb;
    private Camera playerCamera;

    // Events
    public event System.Action<int, int> OnHealthChanged; // (currentHealth, maxHealth)
    public event System.Action<UniversalEnemyHealth> OnEnemyDeath;
    public event System.Action<float> OnDamageTaken; // (damage amount)

    // References to different AI types (automatically detected)
    private EnemyAI basicAI;
    private OptimizedEnemyAI optimizedAI;
    private MonoBehaviour customAI; // For future enemy types

    void Start()
    {
        Initialize();
    }

    void Initialize()
    {
        // Initialize health
        currentHealth = maxHealth;
        isDead = false;

        // Get components
        rb = GetComponent<Rigidbody2D>();
        playerCamera = Camera.main;

        // Detect AI type
        DetectAIType();

        // Set up audio
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0.7f;

        // Store original sprite color
        if (enemySprite == null)
        {
            enemySprite = GetComponent<SpriteRenderer>();
        }
        if (enemySprite != null)
        {
            originalSpriteColor = enemySprite.color;
        }

        // Set up damage text spawn point
        if (damageTextSpawnPoint == null)
        {
            damageTextSpawnPoint = transform;
        }

        // Set up health UI
        SetupHealthUI();

        Debug.Log($"{enemyName} ({enemyType}) Health System initialized - Health: {currentHealth}/{maxHealth}");
    }

    void DetectAIType()
    {
        // Try to find different AI types
        basicAI = GetComponent<EnemyAI>();
        optimizedAI = GetComponent<OptimizedEnemyAI>();

        // If we found an AI, get score value from it
        if (basicAI != null)
        {
            scoreValue = basicAI.scoreValue;
            Debug.Log($"Detected EnemyAI on {gameObject.name}");
        }
        else if (optimizedAI != null)
        {
            scoreValue = optimizedAI.scoreValue;
            Debug.Log($"Detected OptimizedEnemyAI on {gameObject.name}");
        }

        // You can add more AI types here as needed
    }

    void SetupHealthUI()
    {
        if (!showHealthBar) return;

        // Create health UI if it doesn't exist
        if (healthUICanvas == null)
        {
            CreateHealthUI();
        }

        // Position health bar
        if (healthUICanvas != null)
        {
            healthUICanvas.transform.position = transform.position + healthBarOffset;
            healthUICanvas.transform.SetParent(transform); // Make it follow the enemy
        }

        // Initialize UI
        UpdateHealthUI();
    }

    void CreateHealthUI()
    {
        // Create a simple health bar UI
        GameObject canvasGO = new GameObject($"{enemyName}_HealthUI");
        canvasGO.transform.SetParent(transform);

        healthUICanvas = canvasGO.AddComponent<Canvas>();
        healthUICanvas.renderMode = RenderMode.WorldSpace;
        healthUICanvas.worldCamera = playerCamera;

        // Scale the canvas
        healthUICanvas.transform.localScale = Vector3.one * 0.01f;

        // Add CanvasScaler for consistent sizing
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        // Create background
        GameObject bgGO = new GameObject("HealthBar_Background");
        bgGO.transform.SetParent(canvasGO.transform);
        Image bgImage = bgGO.AddComponent<Image>();
        bgImage.color = Color.black;
        RectTransform bgRect = bgGO.GetComponent<RectTransform>();
        bgRect.sizeDelta = new Vector2(100, 10);
        bgRect.anchoredPosition = Vector2.zero;

        // Create health bar
        GameObject sliderGO = new GameObject("HealthBar");
        sliderGO.transform.SetParent(canvasGO.transform);
        healthBar = sliderGO.AddComponent<Slider>();

        RectTransform sliderRect = sliderGO.GetComponent<RectTransform>();
        sliderRect.sizeDelta = new Vector2(100, 10);
        sliderRect.anchoredPosition = Vector2.zero;

        // Create fill area
        GameObject fillAreaGO = new GameObject("Fill Area");
        fillAreaGO.transform.SetParent(sliderGO.transform);
        RectTransform fillAreaRect = fillAreaGO.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.sizeDelta = Vector2.zero;
        fillAreaRect.anchoredPosition = Vector2.zero;

        // Create fill
        GameObject fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(fillAreaGO.transform);
        healthBarFill = fillGO.AddComponent<Image>();
        healthBarFill.color = healthyColor;
        RectTransform fillRect = fillGO.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.sizeDelta = Vector2.zero;
        fillRect.anchoredPosition = Vector2.zero;

        // Configure slider
        healthBar.fillRect = fillRect;
        healthBar.value = 1f;
        healthBar.minValue = 0f;
        healthBar.maxValue = 1f;
    }

    void Update()
    {
        // Update health bar position and rotation
        if (healthUICanvas != null && playerCamera != null)
        {
            // Position health bar above enemy
            healthUICanvas.transform.position = transform.position + healthBarOffset;

            // Make health bar face camera
            healthUICanvas.transform.LookAt(playerCamera.transform);
            healthUICanvas.transform.Rotate(0, 180, 0); // Flip to face camera correctly

            // Hide health bar if full health and setting is enabled
            if (hideWhenFullHealth && currentHealth >= maxHealth)
            {
                healthUICanvas.gameObject.SetActive(false);
            }
            else if (showHealthBar && !isDead)
            {
                healthUICanvas.gameObject.SetActive(true);
            }
        }
    }

    public void TakeDamage(float amount)
    {
        if (!canTakeDamage || isDead)
        {
            Debug.Log($"{enemyName} damage blocked - CanTakeDamage: {canTakeDamage}, Dead: {isDead}");
            return;
        }

        int damage = Mathf.RoundToInt(amount);
        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0);

        Debug.Log($"{enemyName} took {damage} damage! Health: {currentHealth}/{maxHealth}");

        // Trigger events
        OnDamageTaken?.Invoke(amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        // Play hurt sound
        PlayHurtSound();

        // Visual feedback
        StartCoroutine(DamageFlash());
        ShowDamageText(damage);

        // Update UI
        UpdateHealthUI();

        // Check for death
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void TakeKnockback(Vector2 force)
    {
        if (!canBeKnockedBack || isDead || rb == null) return;

        // Apply knockback resistance
        Vector2 adjustedForce = force / knockbackResistance;
        rb.AddForce(adjustedForce, ForceMode2D.Impulse);

        Debug.Log($"{enemyName} knocked back with force: {adjustedForce}");
    }

    void Die()
    {
        if (isDead) return;

        isDead = true;
        Debug.Log($"{enemyName} died!");

        // Play death sound
        PlayDeathSound();

        // Trigger death event
        OnEnemyDeath?.Invoke(this);

        // Add score
        if (ScoreManagerV2.instance != null)
        {
            ScoreManagerV2.instance.AddPoints(scoreValue);
        }

        // Stop movement
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        // Disable AI components
        DisableAI();

        // Spawn death effect
        if (deathEffectPrefab != null)
        {
            GameObject deathEffect = Instantiate(deathEffectPrefab, transform.position, Quaternion.identity);
            Destroy(deathEffect, deathEffectLifetime);
        }

        // Drop items
        if (dropItems)
        {
            DropItems();
        }

        // Hide health UI
        if (healthUICanvas != null)
        {
            healthUICanvas.gameObject.SetActive(false);
        }

        // Start death sequence
        StartCoroutine(DeathSequence());
    }

    void DisableAI()
    {
        // Disable different AI types
        if (basicAI != null)
        {
            basicAI.enabled = false;
        }

        if (optimizedAI != null)
        {
            optimizedAI.enabled = false;
        }

        // Disable collider to prevent further damage
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.enabled = false;
        }
    }

    void DropItems()
    {
        if (possibleDrops.Length == 0) return;

        if (Random.value <= dropChance)
        {
            GameObject dropPrefab = possibleDrops[Random.Range(0, possibleDrops.Length)];
            Vector3 dropPosition = transform.position + Random.insideUnitSphere * 0.5f;
            dropPosition.z = 0; // Keep in 2D plane

            GameObject drop = Instantiate(dropPrefab, dropPosition, Quaternion.identity);
            Debug.Log($"{enemyName} dropped {drop.name}");
        }
    }

    IEnumerator DeathSequence()
    {
        // Fade out sprite
        if (enemySprite != null)
        {
            float fadeTime = 1f;
            float timer = 0f;
            Color startColor = enemySprite.color;

            while (timer < fadeTime)
            {
                timer += Time.deltaTime;
                float alpha = Mathf.Lerp(1f, 0f, timer / fadeTime);
                Color fadeColor = startColor;
                fadeColor.a = alpha;
                enemySprite.color = fadeColor;
                yield return null;
            }
        }

        // Wait a bit more before destroying
        yield return new WaitForSeconds(0.5f);

        // Destroy the enemy
        Destroy(gameObject);
    }

    IEnumerator DamageFlash()
    {
        if (enemySprite != null)
        {
            enemySprite.color = damageFlashColor;
            yield return new WaitForSeconds(flashDuration);
            enemySprite.color = originalSpriteColor;
        }
    }

    void ShowDamageText(int damage)
    {
        if (damageTextPrefab != null)
        {
            Vector3 spawnPos = damageTextSpawnPoint.position + Random.insideUnitSphere * 0.5f;
            spawnPos.z = 0;

            GameObject damageText = Instantiate(damageTextPrefab, spawnPos, Quaternion.identity);

            // Try to set damage amount
            Text textComponent = damageText.GetComponent<Text>();
            if (textComponent != null)
            {
                textComponent.text = $"-{damage}";
                textComponent.color = Color.red;
            }

            // Add floating animation
            FloatingText floatingText = damageText.GetComponent<FloatingText>();
            if (floatingText != null)
            {
                floatingText.SetText($"-{damage}");
            }

            Destroy(damageText, 2f);
        }
    }

    void UpdateHealthUI()
    {
        if (healthBar != null)
        {
            float healthPercentage = (float)currentHealth / maxHealth;
            healthBar.value = healthPercentage;

            // Update health bar color
            if (healthBarFill != null)
            {
                if (healthPercentage > 0.6f)
                    healthBarFill.color = healthyColor;
                else if (healthPercentage > 0.3f)
                    healthBarFill.color = damagedColor;
                else
                    healthBarFill.color = criticalColor;
            }
        }

        if (healthText != null)
        {
            healthText.text = $"{currentHealth}/{maxHealth}";
        }
    }

    void PlayHurtSound()
    {
        if (hurtSounds.Length > 0 && audioSource != null)
        {
            AudioClip hurtClip = hurtSounds[Random.Range(0, hurtSounds.Length)];
            audioSource.PlayOneShot(hurtClip, hurtVolume);
        }
    }

    void PlayDeathSound()
    {
        if (deathSounds.Length > 0 && audioSource != null)
        {
            AudioClip deathClip = deathSounds[Random.Range(0, deathSounds.Length)];
            audioSource.PlayOneShot(deathClip, deathVolume);
        }
    }

    // Public methods for external access
    public bool IsAlive() => !isDead;
    public float GetHealthPercentage() => (float)currentHealth / maxHealth;
    public EnemyType GetEnemyType() => enemyType;

    public void SetMaxHealth(int newMaxHealth)
    {
        maxHealth = newMaxHealth;
        currentHealth = Mathf.Min(currentHealth, maxHealth);
        UpdateHealthUI();
    }

    public void Heal(int amount)
    {
        if (isDead) return;

        currentHealth += amount;
        currentHealth = Mathf.Min(currentHealth, maxHealth);
        UpdateHealthUI();

        Debug.Log($"{enemyName} healed for {amount}! Health: {currentHealth}/{maxHealth}");
    }

    public void SetHealthBarVisibility(bool visible)
    {
        showHealthBar = visible;
        if (healthUICanvas != null)
        {
            healthUICanvas.gameObject.SetActive(visible && !isDead);
        }
    }

    // Reset method for object pooling
    public void ResetEnemy()
    {
        currentHealth = maxHealth;
        isDead = false;
        canTakeDamage = true;

        // Reset sprite color
        if (enemySprite != null)
        {
            enemySprite.color = originalSpriteColor;
        }

        // Re-enable AI
        if (basicAI != null)
        {
            basicAI.enabled = true;
        }

        if (optimizedAI != null)
        {
            optimizedAI.enabled = true;
        }

        // Re-enable collider
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.enabled = true;
        }

        // Reset health UI
        UpdateHealthUI();
        if (healthUICanvas != null)
        {
            healthUICanvas.gameObject.SetActive(showHealthBar);
        }
    }

    void OnDrawGizmosSelected()
    {
        // Draw health status
        if (isDead)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, 1f);
        }
        else
        {
            float healthPercentage = (float)currentHealth / maxHealth;
            Gizmos.color = Color.Lerp(Color.red, Color.green, healthPercentage);
            Gizmos.DrawWireSphere(transform.position, 0.8f);
        }

        // Draw health bar position
        Gizmos.color = Color.blue;
        Gizmos.DrawWireCube(transform.position + healthBarOffset, Vector3.one * 0.2f);
    }
}

/// <summary>
/// Enum for different enemy types
/// </summary>
public enum EnemyType
{
    Basic,
    Heavy,
    Fast,
    Sniper,
    Boss,
    Elite,
    Swarm,
    Tank,
    Assassin,
    Mage
}