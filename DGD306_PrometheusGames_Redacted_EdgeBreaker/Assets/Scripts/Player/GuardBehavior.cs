using UnityEngine;
using System.Collections.Generic;

public class GuardBehavior : MonoBehaviour
{
    private Player_Melee_Controller1 owner;
    private float lifetime = 5f;
    private float timer = 0f;

    [Header("Guard Settings")]
    public Vector3 followOffset = new Vector3(1f, 0.5f, 0f);
    public int maxHealth = 3;
    public float protectionRadius = 2f;

    [Header("Enhanced Detection")]
    public bool useContinuousDetection = true;
    public float detectionUpdateRate = 0.02f; // 50 times per second
    public LayerMask projectileLayer = 1 << 8; // Set to your projectile layer

    [Header("Protection Settings")]
    public bool blockContactDamage = true;
    public bool blockProjectiles = true;
    public float blockCooldown = 0.1f;

    [Header("Visual Effects")]
    public GameObject blockEffect;
    public GameObject damageEffect;
    public Color[] healthColors = { Color.red, Color.yellow, Color.green };

    [Header("Audio")]
    public AudioClip blockSound;
    public AudioClip damageSound;
    public AudioClip breakSound;
    [Range(0f, 1f)] public float audioVolume = 0.7f;

    private int currentHealth;
    private AudioSource audioSource;
    private SpriteRenderer spriteRenderer;
    private float lastBlockTime = -999f;
    private HashSet<GameObject> blockedThisFrame = new HashSet<GameObject>();
    private HashSet<GameObject> trackedProjectiles = new HashSet<GameObject>();
    private Collider2D guardCollider;
    private float lastDetectionUpdate = 0f;

    public void SetOwner(Player_Melee_Controller1 player)
    {
        owner = player;
    }

    public void SetLifetime(float duration)
    {
        lifetime = duration;
        timer = 0f;
    }

    void Start()
    {
        currentHealth = maxHealth;

        // Setup components
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        audioSource.volume = audioVolume;

        spriteRenderer = GetComponent<SpriteRenderer>();
        guardCollider = GetComponent<Collider2D>();

        // Ensure collider is set as trigger
        if (guardCollider != null)
        {
            guardCollider.isTrigger = true;
        }

        // Update visual state
        UpdateGuardVisuals();

        Debug.Log($"Guard initialized with {currentHealth}/{maxHealth} health, Continuous Detection: {useContinuousDetection}");
    }

    void Update()
    {
        timer += Time.deltaTime;

        if (owner != null)
        {
            FollowPlayer();
        }

        // ENHANCED DETECTION: Continuously check for projectiles
        if (useContinuousDetection && Time.time >= lastDetectionUpdate + detectionUpdateRate)
        {
            ContinuousProjectileDetection();
            lastDetectionUpdate = Time.time;
        }

        if (timer >= lifetime)
        {
            BreakGuard();
        }

        // Clear blocked objects each frame
        blockedThisFrame.Clear();
    }

    void FollowPlayer()
    {
        if (owner == null) return;

        float direction = Mathf.Sign(owner.transform.localScale.x);
        Vector3 targetPos = owner.transform.position + new Vector3(followOffset.x * direction, followOffset.y, 0f);
        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * 10f);

        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * direction;
        transform.localScale = scale;
    }

    // ENHANCED: Continuous detection for projectiles that start inside trigger
    void ContinuousProjectileDetection()
    {
        if (!blockProjectiles) return;

        // Find all projectiles in protection area
        Collider2D[] nearbyProjectiles = Physics2D.OverlapCircleAll(
            transform.position,
            protectionRadius,
            projectileLayer
        );

        foreach (Collider2D projectile in nearbyProjectiles)
        {
            // Skip if not a valid projectile
            if (!IsValidProjectile(projectile)) continue;

            // Skip if already processed this frame
            if (blockedThisFrame.Contains(projectile.gameObject)) continue;

            // Check if this projectile is new or moving toward player
            if (ShouldBlockProjectile(projectile))
            {
                Debug.Log($"Continuous detection blocking: {projectile.name}");
                BlockProjectile(projectile);
            }
        }
    }

    bool IsValidProjectile(Collider2D projectile)
    {
        return projectile != null &&
               projectile.gameObject != gameObject &&
               (projectile.CompareTag("EnemyProjectile") || projectile.CompareTag("Projectiles"));
    }

    bool ShouldBlockProjectile(Collider2D projectile)
    {
        // Always block if not tracked yet (new projectile)
        if (!trackedProjectiles.Contains(projectile.gameObject))
        {
            trackedProjectiles.Add(projectile.gameObject);
            return true;
        }

        // For tracked projectiles, check if they're moving toward the player
        if (owner != null)
        {
            Rigidbody2D projectileRb = projectile.GetComponent<Rigidbody2D>();
            if (projectileRb != null)
            {
                Vector2 directionToPlayer = (owner.transform.position - projectile.transform.position).normalized;
                Vector2 projectileVelocity = projectileRb.linearVelocity.normalized;

                // If projectile is moving toward player, block it
                float dot = Vector2.Dot(projectileVelocity, directionToPlayer);
                return dot > 0.3f; // Moving roughly toward player
            }
        }

        return false;
    }

    // ORIGINAL: OnTriggerEnter2D for projectiles entering from outside
    void OnTriggerEnter2D(Collider2D other)
    {
        // Block enemy projectiles
        if ((other.CompareTag("EnemyProjectile") || other.CompareTag("Projectiles")) && blockProjectiles)
        {
            Debug.Log($"Trigger detection blocking: {other.name}");
            BlockProjectile(other);
        }
        // Block enemy contact damage
        else if (other.CompareTag("Enemy") && blockContactDamage)
        {
            BlockEnemyContact(other);
        }
    }

    // CLEANUP: Remove projectiles from tracking when they leave
    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("EnemyProjectile") || other.CompareTag("Projectiles"))
        {
            trackedProjectiles.Remove(other.gameObject);
        }
    }

    void BlockProjectile(Collider2D projectile)
    {
        if (Time.time < lastBlockTime + blockCooldown) return;
        if (blockedThisFrame.Contains(projectile.gameObject)) return;

        lastBlockTime = Time.time;
        blockedThisFrame.Add(projectile.gameObject);

        // Get projectile damage for guard damage calculation
        Bullet bullet = projectile.GetComponent<Bullet>();
        int damageToGuard = 1; // Default damage to guard

        if (bullet != null)
        {
            damageToGuard = Mathf.Max(1, Mathf.RoundToInt(bullet.damage / 15f));
        }

        // Take damage to guard
        TakeDamage(damageToGuard);

        // Play blocking effects
        PlayBlockEffect();

        // CRITICAL: Destroy the projectile so it doesn't hit the player
        Destroy(projectile.gameObject);

        // Remove from tracking
        trackedProjectiles.Remove(projectile.gameObject);

        Debug.Log($"Guard blocked projectile! Health: {currentHealth}/{maxHealth}");
    }

    void BlockEnemyContact(Collider2D enemy)
    {
        if (Time.time < lastBlockTime + blockCooldown) return;
        if (blockedThisFrame.Contains(enemy.gameObject)) return;

        lastBlockTime = Time.time;
        blockedThisFrame.Add(enemy.gameObject);

        // Take damage from blocking contact
        TakeDamage(1);

        // Push enemy away
        Rigidbody2D enemyRb = enemy.GetComponent<Rigidbody2D>();
        if (enemyRb != null)
        {
            Vector2 pushDirection = (enemy.transform.position - transform.position).normalized;
            enemyRb.AddForce(pushDirection * 8f, ForceMode2D.Impulse);
        }

        // Play block effect
        PlayBlockEffect();

        Debug.Log($"Guard blocked enemy contact! Health: {currentHealth}/{maxHealth}");
    }

    void PlayBlockEffect()
    {
        // Visual effect
        if (blockEffect != null)
        {
            GameObject effect = Instantiate(blockEffect, transform.position, Quaternion.identity);
            Destroy(effect, 1f);
        }

        // Audio effect
        if (blockSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(blockSound, audioVolume);
        }

        // Flash effect
        StartCoroutine(FlashGuard());
    }

    System.Collections.IEnumerator FlashGuard()
    {
        if (spriteRenderer != null)
        {
            Color originalColor = spriteRenderer.color;
            spriteRenderer.color = Color.white;
            yield return new WaitForSeconds(0.1f);
            spriteRenderer.color = originalColor;
        }
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;

        // Play damage effect
        if (damageEffect != null)
        {
            GameObject effect = Instantiate(damageEffect, transform.position, Quaternion.identity);
            Destroy(effect, 1f);
        }

        if (damageSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(damageSound, audioVolume * 0.8f);
        }

        // Update visuals
        UpdateGuardVisuals();

        if (currentHealth <= 0)
        {
            BreakGuard();
        }
    }

    void UpdateGuardVisuals()
    {
        if (spriteRenderer != null && healthColors.Length > 0)
        {
            // Calculate health percentage
            float healthPercent = (float)currentHealth / maxHealth;

            // Choose color based on health
            Color targetColor;
            if (healthPercent > 0.66f)
                targetColor = healthColors[healthColors.Length - 1]; // Green (full health)
            else if (healthPercent > 0.33f)
                targetColor = healthColors[1]; // Yellow (medium health)
            else
                targetColor = healthColors[0]; // Red (low health)

            spriteRenderer.color = targetColor;
        }
    }

    void BreakGuard()
    {
        // Play break effect
        if (breakSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(breakSound, audioVolume);
        }

        if (damageEffect != null)
        {
            GameObject effect = Instantiate(damageEffect, transform.position, Quaternion.identity);
            Destroy(effect, 2f);
        }

        if (owner != null)
        {
            owner.OnGuardDestroyed(); // Notify player to start cooldown
        }

        Destroy(gameObject);
    }

    // Public getters
    public float GetHealthPercentage()
    {
        return (float)currentHealth / maxHealth;
    }

    public bool IsBlocking()
    {
        return Time.time < lastBlockTime + 0.2f;
    }

    void OnDrawGizmosSelected()
    {
        // Draw protection radius (continuous detection area)
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, protectionRadius);

        // Draw trigger collider bounds
        if (guardCollider != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(transform.position, guardCollider.bounds.size);
        }

        // Draw health indicator
        Gizmos.color = currentHealth > maxHealth * 0.5f ? Color.green : Color.red;
        Gizmos.DrawWireCube(transform.position + Vector3.up * 2f, Vector3.one * 0.5f);
    }
}