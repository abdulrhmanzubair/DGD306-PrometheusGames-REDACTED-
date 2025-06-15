using UnityEngine;
using System.Collections;

/// <summary>
/// Optimized Enemy AI script that adjusts behavior based on distance and visibility
/// Replaces your original EnemyAI script with performance optimizations
/// </summary>
public class OptimizedEnemyAI : MonoBehaviour, IDamageable
{
    [Header("References")]
    public Rigidbody2D rb;
    public SpriteRenderer spriteRenderer;
    public Animator animator;

    [Header("Movement Settings")]
    public float moveSpeed = 3f;
    public float stopDistance = 5f;

    [Header("Detection Settings")]
    public float detectionRadius = 10f;

    [Header("Shooting Settings")]
    public GameObject bulletPrefab;
    public Transform firePoint;
    public float shootCooldown = 2f;
    public float bulletSpeed = 10f;

    [Header("Enemy Stats")]
    public int health = 100;
    public int maxHealth = 100;
    public int scoreValue = 50;

    [Header("Audio Settings")]
    public AudioClip shootSound;
    public AudioClip hurtSound;
    public AudioClip deathSound;
    public AudioClip detectSound;
    public AudioClip[] footstepSounds;

    [Range(0f, 1f)] public float shootVolume = 0.6f;
    [Range(0f, 1f)] public float hurtVolume = 0.7f;
    [Range(0f, 1f)] public float deathVolume = 0.8f;
    [Range(0f, 1f)] public float detectVolume = 0.5f;
    [Range(0f, 1f)] public float footstepVolume = 0.3f;

    [Header("Footstep Settings")]
    public float footstepInterval = 0.5f;

    [Header("Optimization Debug")]
    [SerializeField] private OptimizationLevel currentLevel = OptimizationLevel.Full;

    // Private variables
    private Transform player;
    private Transform currentTarget;
    private bool hasTarget = false;
    private bool hasDetectedBefore = false;
    private float lastShootTime;
    private float lastUpdateTime;
    private float lastFootstepTime;
    private EnemyManager enemyManager;
    private AudioSource audioSource;

    // Update frequencies for different optimization levels
    private float fullUpdateRate = 0f;      // Every frame
    private float reducedUpdateRate = 0.1f; // 10 times per second
    private float minimalUpdateRate = 0.5f; // 2 times per second

    public event System.Action OnEnemyDeath;

    void Start()
    {
        Initialize();
    }

    void Initialize()
    {
        player = GameObject.FindWithTag("Player")?.transform;
        enemyManager = FindObjectOfType<EnemyManager>();

        // Set up audio source
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0.7f; // Partially 3D audio

        // Register with enemy manager
        if (enemyManager != null)
            enemyManager.RegisterEnemy(this);

        // Reset health
        health = maxHealth;
        hasDetectedBefore = false;
    }

    void Update()
    {
        // Skip updates based on optimization level
        if (!ShouldUpdate()) return;

        switch (currentLevel)
        {
            case OptimizationLevel.Full:
                FullAIUpdate();
                break;
            case OptimizationLevel.Reduced:
                ReducedAIUpdate();
                break;
            case OptimizationLevel.Minimal:
                MinimalAIUpdate();
                break;
            case OptimizationLevel.Disabled:
                // Do nothing
                break;
        }

        lastUpdateTime = Time.time;
    }

    bool ShouldUpdate()
    {
        switch (currentLevel)
        {
            case OptimizationLevel.Full:
                return true;
            case OptimizationLevel.Reduced:
                return Time.time - lastUpdateTime >= reducedUpdateRate;
            case OptimizationLevel.Minimal:
                return Time.time - lastUpdateTime >= minimalUpdateRate;
            case OptimizationLevel.Disabled:
                return false;
            default:
                return true;
        }
    }

    void FullAIUpdate()
    {
        // Full AI logic - everything enabled
        SearchForTarget();
        HandleMovement();
        HandleShooting();
        UpdateAnimations();
        PlayFootstepSound();
    }

    void ReducedAIUpdate()
    {
        // Reduced AI - skip some expensive operations
        SearchForTarget();
        HandleMovement();
        if (Time.time - lastShootTime >= shootCooldown * 1.5f) // Slower shooting
            HandleShooting();
        // Skip animation and footstep updates for performance
    }

    void MinimalAIUpdate()
    {
        // Minimal AI - only basic movement toward player
        if (player != null)
        {
            Vector2 direction = (player.position - transform.position).normalized;
            float distance = Vector2.Distance(transform.position, player.position);

            if (distance > stopDistance)
            {
                rb.linearVelocity = direction * moveSpeed * 0.5f; // Half speed
            }
            else
            {
                rb.linearVelocity = Vector2.zero;
            }
        }
    }

    public void SetOptimizationLevel(OptimizationLevel level)
    {
        if (currentLevel == level) return;

        currentLevel = level;

        // Enable/disable components based on level
        switch (level)
        {
            case OptimizationLevel.Disabled:
                gameObject.SetActive(false);
                break;
            case OptimizationLevel.Minimal:
                if (!gameObject.activeInHierarchy)
                    gameObject.SetActive(true);
                // Disable expensive components
                if (animator != null) animator.enabled = false;
                break;
            case OptimizationLevel.Reduced:
                if (!gameObject.activeInHierarchy)
                    gameObject.SetActive(true);
                if (animator != null) animator.enabled = false;
                break;
            case OptimizationLevel.Full:
                if (!gameObject.activeInHierarchy)
                    gameObject.SetActive(true);
                if (animator != null) animator.enabled = true;
                break;
        }
    }

    void SearchForTarget()
    {
        if (player == null) return;

        float distance = Vector2.Distance(transform.position, player.position);
        if (distance <= detectionRadius)
        {
            // Play detection sound only when first detecting
            if (!hasDetectedBefore)
            {
                PlayDetectSound();
                hasDetectedBefore = true;
            }

            currentTarget = player;
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

    void HandleMovement()
    {
        if (!hasTarget || currentTarget == null)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        float distance = Vector2.Distance(transform.position, currentTarget.position);

        if (distance > stopDistance)
        {
            Vector2 direction = (currentTarget.position - transform.position).normalized;
            rb.linearVelocity = new Vector2(direction.x * moveSpeed, rb.linearVelocity.y);
        }
        else
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }

        if (spriteRenderer != null)
            spriteRenderer.flipX = currentTarget.position.x > transform.position.x;
    }

    void HandleShooting()
    {
        if (!hasTarget || currentTarget == null) return;

        float distance = Vector2.Distance(transform.position, currentTarget.position);

        if (distance <= stopDistance && Time.time - lastShootTime >= shootCooldown)
        {
            Shoot();
            lastShootTime = Time.time;
        }
    }

    void Shoot()
    {
        if (bulletPrefab == null || firePoint == null) return;

        PlayShootSound();

        Vector2 shootDir = (currentTarget.position - firePoint.position).normalized;

        // Try to use bullet pool first, fallback to instantiate
        GameObject bullet = null;
        if (BulletPool.Instance != null)
        {
            bullet = BulletPool.Instance.GetBullet();
        }

        if (bullet == null)
        {
            bullet = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);
        }
        else
        {
            bullet.transform.position = firePoint.position;
        }

        // Set bullet rotation and properties
        float angle = Mathf.Atan2(shootDir.y, shootDir.x) * Mathf.Rad2Deg;
        bullet.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);

        Bullet bulletScript = bullet.GetComponent<Bullet>();
        if (bulletScript != null)
        {
            bulletScript.SetDirection(shootDir);
            bulletScript.SetShooter("Enemy");
        }

        // Fallback for non-pooled bullets
        Rigidbody2D bulletRb = bullet.GetComponent<Rigidbody2D>();
        if (bulletRb != null)
        {
            bulletRb.linearVelocity = shootDir * bulletSpeed;
        }
    }

    void UpdateAnimations()
    {
        if (animator == null) return;

        float currentSpeed = Mathf.Abs(rb.linearVelocity.x);
        animator.SetFloat("Speed", currentSpeed);
    }

    void PlayFootstepSound()
    {
        if (footstepSounds.Length > 0 &&
            Time.time - lastFootstepTime >= footstepInterval &&
            Mathf.Abs(rb.linearVelocity.x) > 0.1f)
        {
            AudioClip footstep = footstepSounds[Random.Range(0, footstepSounds.Length)];

            if (footstep != null && audioSource != null)
            {
                audioSource.PlayOneShot(footstep, footstepVolume);
            }

            lastFootstepTime = Time.time;
        }
    }

    // Audio Methods
    void PlayShootSound()
    {
        if (shootSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(shootSound, shootVolume);
        }
    }

    void PlayHurtSound()
    {
        if (hurtSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(hurtSound, hurtVolume);
        }
    }

    void PlayDeathSound()
    {
        if (deathSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(deathSound, deathVolume);
        }
    }

    void PlayDetectSound()
    {
        if (detectSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(detectSound, detectVolume);
        }
    }

    public void TakeDamage(float amount)
    {
        PlayHurtSound();

        health -= (int)amount;
        if (health <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        PlayDeathSound();
        OnEnemyDeath?.Invoke();

        if (ScoreManager.Instance != null)
            ScoreManager.Instance.AddScore(scoreValue);

        // Return to pool if available, otherwise destroy
        if (EnemyPool.Instance != null)
        {
            EnemyPool.Instance.ReturnEnemy(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void ResetEnemy()
    {
        // Called when enemy is returned from pool
        health = maxHealth;
        hasDetectedBefore = false;
        hasTarget = false;
        currentTarget = null;
        rb.linearVelocity = Vector2.zero;
        SetOptimizationLevel(OptimizationLevel.Full);
    }

    void OnDestroy()
    {
        if (enemyManager != null)
            enemyManager.UnregisterEnemy(this);
    }

    void OnDrawGizmosSelected()
    {
        // Draw detection radius
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        // Draw stop distance
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, stopDistance);
    }
}