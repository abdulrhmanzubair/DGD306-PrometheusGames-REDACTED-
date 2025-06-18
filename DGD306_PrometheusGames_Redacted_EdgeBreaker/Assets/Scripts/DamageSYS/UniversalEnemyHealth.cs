using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Universal health system for all enemy types
/// Can be attached to any enemy GameObject regardless of AI type
/// Enhanced with improved health bar positioning above enemy heads
/// Modified for large sprites (423x463)
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
    public bool showHealthBar = true;
    public bool hideWhenFullHealth = false;

    [Header("Health Bar Sizing")]
    [Tooltip("Base scale factor for health UI. Adjust this if health bar is too big/small")]
    public float healthBarScaleFactor = 1f;
    [Tooltip("Width of the health bar in world units")]
    public float healthBarWidth = 200f;
    [Tooltip("Height of the health bar in world units")]
    public float healthBarHeight = 25f;
    [Tooltip("Font size for health text")]
    public int healthTextFontSize = 14;

    [Header("Health Bar Positioning")]
    public bool autoPositionAboveSprite = true;
    public float additionalHeightOffset = 0.5f; // Extra space above the sprite
    public Vector3 manualHealthBarOffset = new Vector3(0, 2.5f, 0); // Used when autoPositionAboveSprite is false

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
    private float baseCanvasScale = 0.01f; // Base scale for large sprites

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

        // Detect AI type and get score from them
        DetectAIType();

        // Set up audio (but don't override if EnemyAI already has one configured)
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.7f;
        }

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

        // Position health bar using new positioning logic
        if (healthUICanvas != null)
        {
            Vector3 healthBarPosition = GetHealthBarPosition();
            healthUICanvas.transform.position = healthBarPosition;
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

        // Calculate appropriate scale for large sprites
        float enemyScale = GetEnemyScale();

        // For large sprites like 423x463, we need a different scaling approach
        // Assuming pixels to units ratio of 100 (default), your sprite is about 4.23 x 4.63 units
        // We'll use a fixed scale that works well for large sprites
        float scaleFactor = baseCanvasScale * healthBarScaleFactor;

        // If sprite is particularly large, scale down the UI accordingly
        if (enemySprite != null)
        {
            float spritePixelHeight = 463f; // Your sprite height in pixels
            float pixelsPerUnit = enemySprite.sprite != null ? enemySprite.sprite.pixelsPerUnit : 100f;
            float spriteWorldHeight = spritePixelHeight / pixelsPerUnit;

            // Adjust scale based on sprite world size
            if (spriteWorldHeight > 3f)
            {
                scaleFactor *= (3f / spriteWorldHeight);
            }
        }

        healthUICanvas.transform.localScale = Vector3.one * scaleFactor;

        // Add CanvasScaler for consistent sizing
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        // Create background with rounded corners (optional)
        GameObject bgGO = new GameObject("HealthBar_Background");
        bgGO.transform.SetParent(canvasGO.transform);
        Image bgImage = bgGO.AddComponent<Image>();
        bgImage.color = new Color(0, 0, 0, 0); // Semi-transparent black
        RectTransform bgRect = bgGO.GetComponent<RectTransform>();
        bgRect.sizeDelta = new Vector2(healthBarWidth * 1.1f, healthBarHeight * 1.2f); // Slightly larger than health bar
        bgRect.anchoredPosition = Vector2.zero;

        // Create health bar
        GameObject sliderGO = new GameObject("HealthBar");
        sliderGO.transform.SetParent(canvasGO.transform);
        healthBar = sliderGO.AddComponent<Slider>();

        RectTransform sliderRect = sliderGO.GetComponent<RectTransform>();
        sliderRect.sizeDelta = new Vector2(healthBarWidth, healthBarHeight);
        sliderRect.anchoredPosition = Vector2.zero;

        // Create fill area
        GameObject fillAreaGO = new GameObject("Fill Area");
        fillAreaGO.transform.SetParent(sliderGO.transform);
        RectTransform fillAreaRect = fillAreaGO.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.sizeDelta = new Vector2(-5, -5); // Small padding
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

        // Optional: Add health text
        if (healthText == null)
        {
            CreateHealthText(canvasGO);
        }
    }

    void CreateHealthText(GameObject canvasGO)
    {
        GameObject textGO = new GameObject("HealthText");
        textGO.transform.SetParent(canvasGO.transform);

        healthText = textGO.AddComponent<Text>();
        healthText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        healthText.fontSize = healthTextFontSize;
        healthText.color = Color.white;
        healthText.alignment = TextAnchor.MiddleCenter;
        healthText.text = $"{currentHealth}/{maxHealth}";

        // Add outline for better visibility
        Outline outline = textGO.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(1, -1);

        RectTransform textRect = textGO.GetComponent<RectTransform>();
        textRect.sizeDelta = new Vector2(healthBarWidth, healthBarHeight);
        textRect.anchoredPosition = Vector2.zero;
    }

    void Update()
    {
        // Update health bar position and rotation
        if (healthUICanvas != null && playerCamera != null)
        {
            // Calculate position above enemy's head
            Vector3 healthBarPosition = GetHealthBarPosition();
            healthUICanvas.transform.position = healthBarPosition;

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

    Vector3 GetHealthBarPosition()
    {
        if (autoPositionAboveSprite && enemySprite != null)
        {
            // Get the sprite bounds to position above the actual sprite
            Bounds spriteBounds = enemySprite.bounds;
            float spriteTop = spriteBounds.max.y;

            // For large sprites, we might need more offset
            float dynamicOffset = additionalHeightOffset;

            // Add extra offset for very tall sprites
            if (enemySprite.sprite != null)
            {
                float spriteHeight = enemySprite.sprite.bounds.size.y;
                if (spriteHeight > 4f) // If sprite is taller than 4 units
                {
                    dynamicOffset += (spriteHeight - 4f) * 0.1f;
                }
            }

            // Position health bar above the sprite with additional offset
            return new Vector3(transform.position.x, spriteTop + dynamicOffset, transform.position.z);
        }
        else
        {
            // Use manual offset
            return transform.position + manualHealthBarOffset;
        }
    }

    float GetEnemyScale()
    {
        // Calculate enemy scale based on sprite or transform
        if (enemySprite != null && enemySprite.sprite != null)
        {
            // Get actual sprite size in world units
            float pixelsPerUnit = enemySprite.sprite.pixelsPerUnit;
            float worldWidth = 423f / pixelsPerUnit;
            float worldHeight = 463f / pixelsPerUnit;
            return Mathf.Max(worldWidth, worldHeight);
        }
        else if (enemySprite != null)
        {
            return Mathf.Max(enemySprite.bounds.size.x, enemySprite.bounds.size.y);
        }
        else
        {
            return Mathf.Max(transform.localScale.x, transform.localScale.y);
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

        // Play hurt sound (prioritize EnemyAI sounds if available)
        EnemyAI enemyAI = GetComponent<EnemyAI>();
        if (enemyAI != null && enemyAI.GetComponent<AudioSource>() != null)
        {
            // EnemyAI will handle its own hurt sounds through its audio system
        }
        else
        {
            PlayHurtSound();
        }

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

        // Play death sound (prioritize EnemyAI sounds)
        EnemyAI enemyAI = GetComponent<EnemyAI>();
        if (enemyAI != null)
        {
            // Let EnemyAI handle its own death audio and effects
        }
        else
        {
            PlayDeathSound();
        }

        // Trigger death event
        OnEnemyDeath?.Invoke(this);

        // Add score (avoid duplicate scoring if EnemyAI already handles it)
        if (enemyAI == null && ScoreManagerV2.instance != null)
        {
            ScoreManagerV2.instance.AddPoints(scoreValue);
        }

        // Stop movement
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        // Disable AI components (let them handle their own death sequence)
        if (enemyAI == null)
        {
            DisableAI();
        }

        // Spawn death effect (only if EnemyAI doesn't have its own)
        if (enemyAI == null && deathEffectPrefab != null)
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

        // Start death sequence (only if no EnemyAI to handle it)
        if (enemyAI == null)
        {
            StartCoroutine(DeathSequence());
        }
        else
        {
            // Delay destruction to let EnemyAI handle its death sequence
            StartCoroutine(DelayedDestruction());
        }
    }

    IEnumerator DelayedDestruction()
    {
        // Wait for EnemyAI death sequence to complete
        yield return new WaitForSeconds(3f);

        // Destroy the enemy if it still exists
        if (gameObject != null)
        {
            Destroy(gameObject);
        }
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

        // Disable the new EnemyAI component
        EnemyAI enemyAI = GetComponent<EnemyAI>();
        if (enemyAI != null)
        {
            enemyAI.enabled = false;
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

    // Methods for custom positioning
    public void SetHealthBarOffset(float heightOffset)
    {
        additionalHeightOffset = heightOffset;
    }

    public void SetManualHealthBarPosition(Vector3 offset)
    {
        autoPositionAboveSprite = false;
        manualHealthBarOffset = offset;
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

        // Re-enable the new EnemyAI component
        EnemyAI enemyAI = GetComponent<EnemyAI>();
        if (enemyAI != null)
        {
            enemyAI.enabled = true;
            enemyAI.ResetEnemy(); // Call its own reset method
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
        Vector3 healthBarPos = GetHealthBarPosition();
        Gizmos.color = Color.blue;
        Gizmos.DrawWireCube(healthBarPos, Vector3.one * 0.2f);

        // Draw line from enemy to health bar position
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, healthBarPos);
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