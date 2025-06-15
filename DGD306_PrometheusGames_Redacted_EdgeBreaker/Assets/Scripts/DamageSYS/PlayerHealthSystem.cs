using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Universal health system for all player types (Gunner, Melee, etc.)
/// Handles damage, healing, death, and UI updates
/// </summary>
public class PlayerHealthSystem : MonoBehaviour, IDamageable
{
    [Header("Health Settings")]
    public int maxHealth = 100;
    public int currentHealth;
    public bool canTakeDamage = true;
    public float invulnerabilityTime = 1f; // Time after taking damage where player can't be hurt

    [Header("Health UI")]
    public Slider healthBar;
    public Text healthText;
    public Image healthBarFill;
    public Color healthyColor = Color.green;
    public Color damagedColor = Color.yellow;
    public Color criticalColor = Color.red;

    [Header("Damage Visual Effects")]
    public SpriteRenderer playerSprite;
    public Color damageFlashColor = Color.red;
    public float flashDuration = 0.1f;
    public GameObject damageTextPrefab; // Optional floating damage text

    [Header("Audio")]
    public AudioClip[] hurtSounds;
    public AudioClip[] deathSounds;
    public AudioClip healSound;
    [Range(0f, 1f)] public float hurtVolume = 0.7f;
    [Range(0f, 1f)] public float deathVolume = 0.8f;
    [Range(0f, 1f)] public float healVolume = 0.5f;

    [Header("Death Settings")]
    public float respawnDelay = 3f;
    public Transform respawnPoint;
    public GameObject deathEffectPrefab;

    // Private variables
    private bool isInvulnerable = false;
    private bool isDead = false;
    private AudioSource audioSource;
    private Color originalSpriteColor;
    private Rigidbody2D rb;
    private Collider2D playerCollider;

    // Events
    public event System.Action<int, int> OnHealthChanged; // (currentHealth, maxHealth)
    public event System.Action OnPlayerDeath;
    public event System.Action OnPlayerRespawn;
    public event System.Action<float> OnDamageTaken; // (damage amount)

    // Public properties
    public int PlayerIndex { get; set; }
    public bool IsAlive { get { return !isDead; } }
    public bool IsInvulnerable { get { return isInvulnerable; } }

    void Start()
    {
        Initialize();
    }

    void Initialize()
    {
        // Initialize health
        currentHealth = maxHealth;
        isDead = false;
        isInvulnerable = false;

        // Get components
        rb = GetComponent<Rigidbody2D>();
        playerCollider = GetComponent<Collider2D>();

        // Set up audio
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0.3f;

        // Store original sprite color
        if (playerSprite != null)
        {
            originalSpriteColor = playerSprite.color;
        }

        // Set up respawn point if not assigned
        if (respawnPoint == null)
        {
            respawnPoint = transform;
        }

        // Initialize UI
        UpdateHealthUI();

        Debug.Log($"Player {PlayerIndex} Health System initialized - Health: {currentHealth}/{maxHealth}");
    }

    public void TakeDamage(float amount)
    {
        if (!canTakeDamage || isInvulnerable || isDead)
        {
            Debug.Log($"Player {PlayerIndex} damage blocked - CanTakeDamage: {canTakeDamage}, Invulnerable: {isInvulnerable}, Dead: {isDead}");
            return;
        }

        int damage = Mathf.RoundToInt(amount);
        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0);

        Debug.Log($"Player {PlayerIndex} took {damage} damage! Health: {currentHealth}/{maxHealth}");

        // Trigger events
        OnDamageTaken?.Invoke(amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        // Play hurt sound
        PlayHurtSound();

        // Visual feedback
        StartCoroutine(DamageFlash());
        ShowDamageText(damage);

        // Start invulnerability
        if (invulnerabilityTime > 0)
        {
            StartCoroutine(InvulnerabilityCoroutine());
        }

        // Check for death
        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            // Update UI
            UpdateHealthUI();
        }
    }

    public void Heal(int amount)
    {
        if (isDead) return;

        int oldHealth = currentHealth;
        currentHealth += amount;
        currentHealth = Mathf.Min(currentHealth, maxHealth);

        if (currentHealth > oldHealth)
        {
            Debug.Log($"Player {PlayerIndex} healed for {currentHealth - oldHealth}! Health: {currentHealth}/{maxHealth}");

            // Play heal sound
            if (healSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(healSound, healVolume);
            }

            // Trigger events
            OnHealthChanged?.Invoke(currentHealth, maxHealth);

            // Update UI
            UpdateHealthUI();
        }
    }

    void Die()
    {
        if (isDead) return;

        isDead = true;
        Debug.Log($"Player {PlayerIndex} died!");

        // Play death sound
        PlayDeathSound();

        // Trigger death event
        OnPlayerDeath?.Invoke();

        // Stop movement
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        // Disable player controls and collision
        SetPlayerActive(false);

        // Spawn death effect
        if (deathEffectPrefab != null)
        {
            GameObject deathEffect = Instantiate(deathEffectPrefab, transform.position, Quaternion.identity);
            Destroy(deathEffect, 3f);
        }

        // Start respawn timer
        if (respawnDelay > 0)
        {
            StartCoroutine(RespawnCoroutine());
        }

        // Update UI
        UpdateHealthUI();
    }

    IEnumerator RespawnCoroutine()
    {
        yield return new WaitForSeconds(respawnDelay);
        Respawn();
    }

    public void Respawn()
    {
        Debug.Log($"Player {PlayerIndex} respawning!");

        // Reset health
        currentHealth = maxHealth;
        isDead = false;
        isInvulnerable = false;

        // Move to respawn point
        transform.position = respawnPoint.position;

        // Reset sprite color
        if (playerSprite != null)
        {
            playerSprite.color = originalSpriteColor;
        }

        // Re-enable player
        SetPlayerActive(true);

        // Trigger respawn event
        OnPlayerRespawn?.Invoke();

        // Update UI
        UpdateHealthUI();

        // Brief invulnerability after respawn
        StartCoroutine(InvulnerabilityCoroutine());
    }

    void SetPlayerActive(bool active)
    {
        // Enable/disable collision
        if (playerCollider != null)
        {
            playerCollider.enabled = active;
        }

        // Enable/disable player controls
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

        // Set sprite transparency if dead
        if (playerSprite != null)
        {
            Color color = playerSprite.color;
            color.a = active ? 1f : 0.5f;
            playerSprite.color = color;
        }
    }

    IEnumerator InvulnerabilityCoroutine()
    {
        isInvulnerable = true;

        // Flash effect during invulnerability
        float flashTimer = 0f;
        while (flashTimer < invulnerabilityTime)
        {
            if (playerSprite != null)
            {
                float alpha = Mathf.PingPong(flashTimer * 10f, 1f);
                Color color = originalSpriteColor;
                color.a = 0.5f + (alpha * 0.5f);
                playerSprite.color = color;
            }

            flashTimer += Time.deltaTime;
            yield return null;
        }

        // Restore original color
        if (playerSprite != null)
        {
            playerSprite.color = originalSpriteColor;
        }

        isInvulnerable = false;
    }

    IEnumerator DamageFlash()
    {
        if (playerSprite != null)
        {
            playerSprite.color = damageFlashColor;
            yield return new WaitForSeconds(flashDuration);

            if (!isInvulnerable) // Don't restore if invulnerability flashing is active
            {
                playerSprite.color = originalSpriteColor;
            }
        }
    }

    void ShowDamageText(int damage)
    {
        if (damageTextPrefab != null)
        {
            GameObject damageText = Instantiate(damageTextPrefab, transform.position + Vector3.up, Quaternion.identity);

            // Try to set damage amount if the prefab has a Text component
            Text textComponent = damageText.GetComponent<Text>();
            if (textComponent != null)
            {
                textComponent.text = $"-{damage}";
            }

            // Auto-destroy damage text
            Destroy(damageText, 2f);
        }
    }

    void UpdateHealthUI()
    {
        // Update health bar
        if (healthBar != null)
        {
            float healthPercentage = (float)currentHealth / maxHealth;
            healthBar.value = healthPercentage;

            // Update health bar color based on health percentage
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

        // Update health text
        if (healthText != null)
        {
            healthText.text = $"{currentHealth}/{maxHealth}";

            // Change text color based on health
            float healthPercentage = (float)currentHealth / maxHealth;
            if (healthPercentage > 0.6f)
                healthText.color = healthyColor;
            else if (healthPercentage > 0.3f)
                healthText.color = damagedColor;
            else
                healthText.color = criticalColor;
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
    public bool IsAliveMethod() => !isDead;
    public bool IsInvulnerableMethod() => isInvulnerable;
    public float GetHealthPercentage() => (float)currentHealth / maxHealth;
    public void SetMaxHealth(int newMaxHealth)
    {
        maxHealth = newMaxHealth;
        currentHealth = Mathf.Min(currentHealth, maxHealth);
        UpdateHealthUI();
    }

    public void SetInvulnerable(bool invulnerable, float duration = 0f)
    {
        if (invulnerable && duration > 0f)
        {
            StartCoroutine(TemporaryInvulnerability(duration));
        }
        else
        {
            isInvulnerable = invulnerable;
        }
    }

    IEnumerator TemporaryInvulnerability(float duration)
    {
        isInvulnerable = true;
        yield return new WaitForSeconds(duration);
        isInvulnerable = false;
    }

    // Debug methods
    void OnDrawGizmosSelected()
    {
        // Draw health info in scene view
        if (isDead)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, 1f);
        }
        else if (isInvulnerable)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.8f);
        }
        else
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, 0.6f);
        }
    }
}