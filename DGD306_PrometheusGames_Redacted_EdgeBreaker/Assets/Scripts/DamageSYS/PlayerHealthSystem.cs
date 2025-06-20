using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Updated PlayerHealthSystem integrated with checkpoint and death management
/// Expert Unity implementation with proper architecture
/// </summary>
public class PlayerHealthSystem : MonoBehaviour, IDamageable
{
    #region Serialized Fields

    [Header("Health Settings")]
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private bool canTakeDamage = true;
    [SerializeField] private float invulnerabilityTime = 1f;

    [Header("Health UI")]
    [SerializeField] private Slider healthBar;
    [SerializeField] private Text healthText;
    [SerializeField] private Image healthBarFill;
    [SerializeField] private Color healthyColor = Color.green;
    [SerializeField] private Color damagedColor = Color.yellow;
    [SerializeField] private Color criticalColor = Color.red;

    [Header("Damage Visual Effects")]
    [SerializeField] private SpriteRenderer playerSprite;
    [SerializeField] private Color damageFlashColor = Color.red;
    [SerializeField] private float flashDuration = 0.1f;
    [SerializeField] private GameObject damageTextPrefab;

    [Header("Audio")]
    [SerializeField] private AudioClip[] hurtSounds;
    [SerializeField] private AudioClip[] deathSounds;
    [SerializeField] private AudioClip healSound;
    [SerializeField][Range(0f, 1f)] private float hurtVolume = 0.7f;
    [SerializeField][Range(0f, 1f)] private float deathVolume = 0.8f;
    [SerializeField][Range(0f, 1f)] private float healVolume = 0.5f;

    [Header("Death Effects")]
    [SerializeField] private GameObject deathEffectPrefab;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    #endregion

    #region Private Fields

    // Health state
    private int currentHealth;
    private bool isInvulnerable = false;
    private bool isDead = false;

    // Components
    private AudioSource audioSource;
    private Color originalSpriteColor;
    private Rigidbody2D rb;
    private Collider2D playerCollider;

    // Coroutine tracking
    private Coroutine invulnerabilityCoroutine;
    private Coroutine damageFlashCoroutine;

    #endregion

    #region Events

    public event System.Action<int, int> OnHealthChanged; // (currentHealth, maxHealth)
    public event System.Action OnPlayerDeath;
    public event System.Action OnPlayerRespawn;
    public event System.Action<float> OnDamageTaken; // (damage amount)
    public event System.Action<int> OnPlayerHealed; // (heal amount)

    #endregion

    #region Properties

    public int PlayerIndex { get; set; }
    public bool IsAlive => !isDead;
    public bool IsInvulnerable => isInvulnerable;
    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public float HealthPercentage => (float)currentHealth / maxHealth;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        InitializeComponents();
    }

    private void Start()
    {
        InitializeHealth();
        RegisterWithGameManager();

        if (enableDebugLogs)
        {
            Debug.Log($"[PlayerHealthSystem] Player {PlayerIndex} initialized - Health: {currentHealth}/{maxHealth}");
        }
    }

    private void OnDestroy()
    {
        UnregisterFromGameManager();
        StopAllCoroutines();
    }

    #endregion

    #region Initialization

    private void InitializeComponents()
    {
        // Get required components
        rb = GetComponent<Rigidbody2D>();
        playerCollider = GetComponent<Collider2D>();

        // Setup audio source
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0.3f; // Slight 3D audio

        // Store original sprite color
        if (playerSprite != null)
        {
            originalSpriteColor = playerSprite.color;
        }
        else
        {
            // Try to find sprite renderer if not assigned
            playerSprite = GetComponent<SpriteRenderer>();
            if (playerSprite != null)
            {
                originalSpriteColor = playerSprite.color;
            }
        }
    }

    private void InitializeHealth()
    {
        currentHealth = maxHealth;
        isDead = false;
        isInvulnerable = false;
        UpdateHealthUI();
    }

    private void RegisterWithGameManager()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RegisterPlayer(this);
        }
        else
        {
            Debug.LogWarning($"[PlayerHealthSystem] Player {PlayerIndex} - No GameManager found for registration!");
        }
    }

    private void UnregisterFromGameManager()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.UnregisterPlayer(this);
        }
    }

    #endregion

    #region Damage System

    public void TakeDamage(float amount)
    {
        if (!canTakeDamage || isInvulnerable || isDead)
        {
            if (enableDebugLogs && amount > 0)
            {
                Debug.Log($"[PlayerHealthSystem] Player {PlayerIndex} damage blocked - CanTakeDamage: {canTakeDamage}, Invulnerable: {isInvulnerable}, Dead: {isDead}");
            }
            return;
        }

        int damage = Mathf.RoundToInt(amount);
        int oldHealth = currentHealth;

        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0);

        if (enableDebugLogs)
        {
            Debug.Log($"[PlayerHealthSystem] Player {PlayerIndex} took {damage} damage! Health: {currentHealth}/{maxHealth}");
        }

        // Trigger events
        OnDamageTaken?.Invoke(amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        // Audio and visual feedback
        PlayHurtSound();
        StartDamageFlash();
        ShowDamageText(damage);

        // Start invulnerability period
        if (invulnerabilityTime > 0)
        {
            StartInvulnerability();
        }

        // Check for death
        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            UpdateHealthUI();
        }
    }

    #endregion

    #region Healing System

    public void Heal(int amount)
    {
        if (isDead || amount <= 0) return;

        int oldHealth = currentHealth;
        currentHealth += amount;
        currentHealth = Mathf.Min(currentHealth, maxHealth);

        int actualHealed = currentHealth - oldHealth;

        if (actualHealed > 0)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[PlayerHealthSystem] Player {PlayerIndex} healed for {actualHealed}! Health: {currentHealth}/{maxHealth}");
            }

            // Play heal sound
            if (healSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(healSound, healVolume);
            }

            // Trigger events
            OnPlayerHealed?.Invoke(actualHealed);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);

            // Update UI
            UpdateHealthUI();
        }
    }

    public void HealToFull()
    {
        Heal(maxHealth - currentHealth);
    }

    #endregion

    #region Death and Respawn System

    private void Die()
    {
        if (isDead) return;

        isDead = true;

        if (enableDebugLogs)
        {
            Debug.Log($"[PlayerHealthSystem] Player {PlayerIndex} died!");
        }

        // Play death audio
        PlayDeathSound();

        // Trigger death event (GameManager will handle respawn)
        OnPlayerDeath?.Invoke();

        // Stop movement
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        // Disable player controls and interaction
        SetPlayerActive(false);

        // Spawn death effect
        if (deathEffectPrefab != null)
        {
            GameObject deathEffect = Instantiate(deathEffectPrefab, transform.position, Quaternion.identity);
            Destroy(deathEffect, 3f);
        }

        // Update UI
        UpdateHealthUI();
    }

    public void RespawnAtPosition(Vector3 position)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[PlayerHealthSystem] Player {PlayerIndex} respawning at position: {position}");
        }

        // Reset health and state
        currentHealth = maxHealth;
        isDead = false;
        isInvulnerable = false;

        // Move to respawn position
        transform.position = position;

        // Reset visual state
        ResetVisualState();

        // Re-enable player
        SetPlayerActive(true);

        // Trigger respawn event
        OnPlayerRespawn?.Invoke();

        // Update UI
        UpdateHealthUI();

        if (enableDebugLogs)
        {
            Debug.Log($"[PlayerHealthSystem] Player {PlayerIndex} respawn complete");
        }
    }

    #endregion

    #region Player State Management

    private void SetPlayerActive(bool active)
    {
        // Enable/disable collision
        if (playerCollider != null)
        {
            playerCollider.enabled = active;
        }

        // Enable/disable player controllers
        PlayerController gunnerController = GetComponent<PlayerController>();
        Player_Melee_Controller1 meleeController = GetComponent<Player_Melee_Controller1>();

        if (gunnerController != null)
        {
            gunnerController.enabled = active;
        }

        if (meleeController != null)
        {
            meleeController.enabled = active;
        }

        // Update sprite visibility
        if (playerSprite != null)
        {
            Color color = playerSprite.color;
            color.a = active ? 1f : 0.5f;
            playerSprite.color = color;
        }
    }

    private void ResetVisualState()
    {
        // Stop any ongoing visual effects
        StopAllVisualEffects();

        // Reset sprite color
        if (playerSprite != null)
        {
            playerSprite.color = originalSpriteColor;
        }
    }

    private void StopAllVisualEffects()
    {
        // Stop invulnerability effect
        if (invulnerabilityCoroutine != null)
        {
            StopCoroutine(invulnerabilityCoroutine);
            invulnerabilityCoroutine = null;
        }

        // Stop damage flash
        if (damageFlashCoroutine != null)
        {
            StopCoroutine(damageFlashCoroutine);
            damageFlashCoroutine = null;
        }
    }

    #endregion

    #region Invulnerability System

    public void SetInvulnerable(bool invulnerable, float duration = 0f)
    {
        if (invulnerable && duration > 0f)
        {
            StartTemporaryInvulnerability(duration);
        }
        else
        {
            isInvulnerable = invulnerable;

            if (!invulnerable)
            {
                StopInvulnerabilityEffects();
            }
        }
    }

    private void StartInvulnerability()
    {
        SetInvulnerable(true, invulnerabilityTime);
    }

    private void StartTemporaryInvulnerability(float duration)
    {
        if (invulnerabilityCoroutine != null)
        {
            StopCoroutine(invulnerabilityCoroutine);
        }

        invulnerabilityCoroutine = StartCoroutine(InvulnerabilityCoroutine(duration));
    }

    private IEnumerator InvulnerabilityCoroutine(float duration)
    {
        isInvulnerable = true;

        // Flash effect during invulnerability
        float flashTimer = 0f;
        const float flashSpeed = 10f;

        while (flashTimer < duration)
        {
            if (playerSprite != null && !isDead)
            {
                float alpha = Mathf.PingPong(flashTimer * flashSpeed, 1f);
                Color color = originalSpriteColor;
                color.a = 0.5f + (alpha * 0.5f);
                playerSprite.color = color;
            }

            flashTimer += Time.deltaTime;
            yield return null;
        }

        // End invulnerability
        isInvulnerable = false;
        StopInvulnerabilityEffects();

        invulnerabilityCoroutine = null;
    }

    private void StopInvulnerabilityEffects()
    {
        if (playerSprite != null && !isDead)
        {
            playerSprite.color = originalSpriteColor;
        }
    }

    #endregion

    #region Visual Effects

    private void StartDamageFlash()
    {
        if (damageFlashCoroutine != null)
        {
            StopCoroutine(damageFlashCoroutine);
        }

        damageFlashCoroutine = StartCoroutine(DamageFlashCoroutine());
    }

    private IEnumerator DamageFlashCoroutine()
    {
        if (playerSprite != null)
        {
            Color originalColor = playerSprite.color;
            playerSprite.color = damageFlashColor;

            yield return new WaitForSeconds(flashDuration);

            // Only restore color if not in invulnerability state
            if (!isInvulnerable && !isDead)
            {
                playerSprite.color = originalColor;
            }
        }

        damageFlashCoroutine = null;
    }

    private void ShowDamageText(int damage)
    {
        if (damageTextPrefab != null)
        {
            Vector3 spawnPos = transform.position + Vector3.up * 1.5f;
            GameObject damageText = Instantiate(damageTextPrefab, spawnPos, Quaternion.identity);

            // Try to set damage amount
            Text textComponent = damageText.GetComponent<Text>();
            if (textComponent != null)
            {
                textComponent.text = $"-{damage}";
                textComponent.color = Color.red;
            }

            // Auto-destroy damage text
            Destroy(damageText, 2f);
        }
    }

    #endregion

    #region Audio System

    private void PlayHurtSound()
    {
        if (hurtSounds != null && hurtSounds.Length > 0 && audioSource != null)
        {
            AudioClip hurtClip = hurtSounds[Random.Range(0, hurtSounds.Length)];
            if (hurtClip != null)
            {
                audioSource.PlayOneShot(hurtClip, hurtVolume);
            }
        }
    }

    private void PlayDeathSound()
    {
        if (deathSounds != null && deathSounds.Length > 0 && audioSource != null)
        {
            AudioClip deathClip = deathSounds[Random.Range(0, deathSounds.Length)];
            if (deathClip != null)
            {
                audioSource.PlayOneShot(deathClip, deathVolume);
            }
        }
    }

    #endregion

    #region UI System

    private void UpdateHealthUI()
    {
        // Update health bar
        if (healthBar != null)
        {
            float healthPercentage = HealthPercentage;
            healthBar.value = healthPercentage;

            // Update health bar color
            if (healthBarFill != null)
            {
                Color barColor = GetHealthColor(healthPercentage);
                healthBarFill.color = barColor;
            }
        }

        // Update health text
        if (healthText != null)
        {
            healthText.text = $"{currentHealth}/{maxHealth}";

            // Update text color
            float healthPercentage = HealthPercentage;
            Color textColor = GetHealthColor(healthPercentage);
            healthText.color = textColor;
        }
    }

    private Color GetHealthColor(float healthPercentage)
    {
        if (healthPercentage > 0.6f)
            return healthyColor;
        else if (healthPercentage > 0.3f)
            return damagedColor;
        else
            return criticalColor;
    }

    #endregion

    #region Public Utility Methods

    public void SetMaxHealth(int newMaxHealth)
    {
        if (newMaxHealth <= 0)
        {
            Debug.LogError($"[PlayerHealthSystem] Invalid max health value: {newMaxHealth}");
            return;
        }

        maxHealth = newMaxHealth;
        currentHealth = Mathf.Min(currentHealth, maxHealth);
        UpdateHealthUI();

        if (enableDebugLogs)
        {
            Debug.Log($"[PlayerHealthSystem] Player {PlayerIndex} max health set to {maxHealth}");
        }
    }

    public void ModifyHealth(int amount)
    {
        if (amount > 0)
        {
            Heal(amount);
        }
        else if (amount < 0)
        {
            TakeDamage(-amount);
        }
    }

    public bool IsHealthFull()
    {
        return currentHealth >= maxHealth;
    }

    public bool IsHealthCritical(float threshold = 0.3f)
    {
        return HealthPercentage <= threshold;
    }

    #endregion

    #region Debug Visualization

    private void OnDrawGizmosSelected()
    {
        // Draw health status indicator
        Vector3 pos = transform.position + Vector3.up * 2f;

        if (isDead)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(pos, 0.5f);
        }
        else if (isInvulnerable)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(pos, 0.4f);
        }
        else
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(pos, 0.3f);
        }

        // Draw health percentage as wire cube
        if (Application.isPlaying)
        {
            float healthPercent = HealthPercentage;
            Gizmos.color = GetHealthColor(healthPercent);
            Vector3 healthBarSize = new Vector3(healthPercent * 2f, 0.2f, 0.2f);
            Gizmos.DrawWireCube(pos + Vector3.up * 0.8f, healthBarSize);
        }
    }

    #endregion

    #region Debug Methods

    [ContextMenu("Debug: Take 25 Damage")]
    public void DebugTakeDamage()
    {
        TakeDamage(25f);
    }

    [ContextMenu("Debug: Heal 25 HP")]
    public void DebugHeal()
    {
        Heal(25);
    }

    [ContextMenu("Debug: Kill Player")]
    public void DebugKillPlayer()
    {
        TakeDamage(currentHealth);
    }

    [ContextMenu("Debug: Full Heal")]
    public void DebugFullHeal()
    {
        HealToFull();
    }

    [ContextMenu("Debug: Toggle Invulnerability")]
    public void DebugToggleInvulnerability()
    {
        SetInvulnerable(!isInvulnerable, isInvulnerable ? 0f : 5f);
    }

    #endregion
}