using UnityEngine;
using System.Collections;

/// <summary>
/// Enemy AI script that works with UniversalEnemyHealth
/// Handles movement, shooting, and behavior - health is managed by UniversalEnemyHealth
/// </summary>
public class EnemyAI : MonoBehaviour
{
    [Header("References")]
    public Rigidbody2D rb;
    public SpriteRenderer spriteRenderer;

    [Header("Movement")]
    public float moveSpeed = 2f;
    public float stopDistance = 5f;

    [Header("Detection")]
    public float detectionRadius = 10f;

    [Header("Shooting")]
    public GameObject bulletPrefab;
    public Transform firePoint;
    public float shootCooldown = 1.5f;
    public float bulletSpeed = 10f;

    private float lastShootTime;

    [Header("Enemy Stats")]
    public int scoreValue = 50; // Only score - health is handled by UniversalEnemyHealth

    [Header("Audio Settings")]
    public AudioClip shootSound;        // Sound when enemy shoots
    public AudioClip detectSound;       // Sound when player is detected
    public AudioClip[] footstepSounds;  // Array of footstep sounds for variety

    [Range(0f, 1f)]
    public float shootVolume = 0.6f;
    [Range(0f, 1f)]
    public float detectVolume = 0.5f;
    [Range(0f, 1f)]
    public float footstepVolume = 0.3f;

    [Header("Footstep Settings")]
    public float footstepInterval = 0.5f; // Time between footstep sounds
    private float lastFootstepTime;

    [Header("Death Visual")]
    public GameObject deathSpritePrefab;   // Assign in Inspector
    public float deathSpriteLifetime = 2f; // Customize per enemy

    // Private variables
    private Transform currentTarget;
    private bool hasTarget = false;
    private bool hasDetectedBefore = false; // To play detect sound only once
    private bool isDead = false; // Prevent multiple death calls
    public event System.Action OnEnemyDeath;
    private Animator animator;
    private AudioSource audioSource;

    // Reference to health system
    private UniversalEnemyHealth healthSystem;

    private void Start()
    {
        Initialize();
    }

    void Initialize()
    {
        animator = GetComponent<Animator>();

        // Get reference to health system
        healthSystem = GetComponent<UniversalEnemyHealth>();
        if (healthSystem == null)
        {
            Debug.LogError($"{gameObject.name} - EnemyAI requires UniversalEnemyHealth component!");
            return;
        }

        // Subscribe to health system events
        healthSystem.OnEnemyDeath += HandleDeath;

        // Set score value in health system
        healthSystem.scoreValue = scoreValue;

        isDead = false;

        // Set up audio source
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // Configure audio source settings properly
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0.7f; // Partially 3D audio
        audioSource.volume = 1f; // Make sure volume is at max
        audioSource.enabled = true;

        Debug.Log($"Enemy {gameObject.name} initialized - Health: {healthSystem.currentHealth}, Audio: {audioSource != null}");
    }

    void Update()
    {
        // Check if dead through health system
        if (healthSystem != null && !healthSystem.IsAlive())
        {
            if (!isDead)
            {
                isDead = true;
            }
            return; // Don't update if dead
        }

        if (!hasTarget || currentTarget == null)
        {
            SearchForTarget();
        }

        if (currentTarget == null)
        {
            rb.linearVelocity = Vector2.zero;
            hasTarget = false;
            return;
        }

        float distance = Vector2.Distance(transform.position, currentTarget.position);

        // Movement & Facing
        if (distance > stopDistance)
        {
            Vector2 direction = (currentTarget.position - transform.position).normalized;
            rb.linearVelocity = new Vector2(direction.x * moveSpeed, rb.linearVelocity.y);

            // Play footstep sounds while moving
            PlayFootstepSound();
        }
        else
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }

        spriteRenderer.flipX = currentTarget.position.x > transform.position.x;

        // Shooting if in range and cooldown passed
        if (distance <= stopDistance && Time.time - lastShootTime >= shootCooldown)
        {
            Shoot();
            lastShootTime = Time.time;
        }

        // Animations
        float currentSpeed = Mathf.Abs(rb.linearVelocity.x);
        if (animator != null)
        {
            animator.SetFloat("Speed", currentSpeed);
        }
    }

    void SearchForTarget()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        float closestDistance = Mathf.Infinity;
        Transform closestPlayer = null;

        foreach (GameObject player in players)
        {
            // Check if player is alive
            PlayerHealthSystem playerHealth = player.GetComponent<PlayerHealthSystem>();
            if (playerHealth != null && !playerHealth.IsAlive)
            {
                continue; // Skip dead players
            }

            float dist = Vector2.Distance(transform.position, player.transform.position);
            if (dist <= detectionRadius && dist < closestDistance)
            {
                closestDistance = dist;
                closestPlayer = player.transform;
            }
        }

        if (closestPlayer != null)
        {
            // Play detection sound only when first detecting a player
            if (!hasDetectedBefore)
            {
                PlayDetectSound();
                hasDetectedBefore = true;
            }

            currentTarget = closestPlayer;
            hasTarget = true;
        }
        else
        {
            currentTarget = null;
            hasTarget = false;
            // Reset detection flag when no target
            if (hasDetectedBefore)
            {
                hasDetectedBefore = false;
            }
        }
    }

    void Shoot()
    {
        if (bulletPrefab == null || firePoint == null || isDead) return;

        // Play shoot sound
        PlayShootSound();

        Vector2 shootDir = (currentTarget.position - firePoint.position).normalized;

        GameObject bullet = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);
        Rigidbody2D bulletRb = bullet.GetComponent<Rigidbody2D>();
        if (bulletRb != null)
        {
            bulletRb.linearVelocity = shootDir * bulletSpeed;
        }

        float angle = Mathf.Atan2(shootDir.y, shootDir.x) * Mathf.Rad2Deg;
        bullet.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);

        Bullet bulletScript = bullet.GetComponent<Bullet>();
        if (bulletScript != null)
        {
            bulletScript.SetShooter("Enemy");
            bulletScript.SetDirection(shootDir);
        }
    }

    // This method is called by UniversalEnemyHealth when the enemy dies
    void HandleDeath(UniversalEnemyHealth deadEnemy)
    {
        if (isDead) return; // Prevent multiple death calls

        isDead = true;
        Debug.Log($"{gameObject.name} is dying!");

        // Stop all movement
        rb.linearVelocity = Vector2.zero;

        // Trigger death event
        OnEnemyDeath?.Invoke();

        // Start death sequence (visual effects)
        StartCoroutine(DeathSequence());
    }

    private IEnumerator DeathSequence()
    {
        // Set death animation
        if (animator != null)
        {
            animator.SetBool("IsDead", true);
        }

        // Spawn death sprite immediately
        GameObject deathVisual = null;
        if (deathSpritePrefab != null)
        {
            deathVisual = Instantiate(deathSpritePrefab, transform.position, Quaternion.identity);
            Debug.Log($"Death sprite created at {transform.position}");

            // Make sure death sprite is visible
            SpriteRenderer deathSR = deathVisual.GetComponent<SpriteRenderer>();
            if (deathSR != null)
            {
                deathSR.sortingOrder = 10; // Put it on top
            }
        }
        else
        {
            Debug.LogWarning($"{gameObject.name} has no death sprite prefab assigned!");
        }

        // Wait for death animation/sprite duration
        yield return new WaitForSeconds(deathSpriteLifetime);

        // Clean up death sprite if it still exists
        if (deathVisual != null)
        {
            Destroy(deathVisual);
        }

        // The enemy GameObject will be destroyed by UniversalEnemyHealth
    }

    // Audio Methods with debug logging
    public void PlayShootSound()
    {
        if (shootSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(shootSound, shootVolume);
            Debug.Log($"{gameObject.name} played shoot sound");
        }
        else
        {
            Debug.LogWarning($"{gameObject.name} - Cannot play shoot sound: shootSound={shootSound}, audioSource={audioSource}");
        }
    }

    public void PlayDetectSound()
    {
        if (detectSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(detectSound, detectVolume);
            Debug.Log($"{gameObject.name} played detect sound");
        }
        else
        {
            Debug.LogWarning($"{gameObject.name} - Cannot play detect sound: detectSound={detectSound}, audioSource={audioSource}");
        }
    }

    void PlayFootstepSound()
    {
        // Only play footstep if moving and enough time has passed
        if (footstepSounds.Length > 0 &&
            Time.time - lastFootstepTime >= footstepInterval &&
            Mathf.Abs(rb.linearVelocity.x) > 0.1f)
        {
            // Pick a random footstep sound for variety
            AudioClip footstep = footstepSounds[Random.Range(0, footstepSounds.Length)];

            if (footstep != null && audioSource != null)
            {
                audioSource.PlayOneShot(footstep, footstepVolume);
            }

            lastFootstepTime = Time.time;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, stopDistance);

        // Draw health status if health system exists
        if (healthSystem != null)
        {
            float healthPercent = healthSystem.GetHealthPercentage();
            Gizmos.color = Color.Lerp(Color.red, Color.green, healthPercent);
            Gizmos.DrawWireCube(transform.position + Vector3.up, Vector3.one * 0.5f);
        }
    }

    // Public method to reset enemy (useful for object pooling)
    public void ResetEnemy()
    {
        isDead = false;
        hasDetectedBefore = false;
        hasTarget = false;
        currentTarget = null;
        rb.linearVelocity = Vector2.zero;

        // Reset health system if it exists
        if (healthSystem != null)
        {
            healthSystem.ResetEnemy();
        }
    }

    // Public method to get current health (delegates to health system)
    public int GetCurrentHealth()
    {
        return healthSystem != null ? healthSystem.currentHealth : 0;
    }

    // Public method to get max health (delegates to health system)
    public int GetMaxHealth()
    {
        return healthSystem != null ? healthSystem.maxHealth : 0;
    }

    // Public method to check if alive (delegates to health system)
    public bool IsAlive()
    {
        return healthSystem != null ? healthSystem.IsAlive() : false;
    }

    void OnDestroy()
    {
        // Unsubscribe from events to prevent memory leaks
        if (healthSystem != null)
        {
            healthSystem.OnEnemyDeath -= HandleDeath;
        }
    }
}