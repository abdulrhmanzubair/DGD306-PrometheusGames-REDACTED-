using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class BossEnemy : MonoBehaviour, IDamageable
{
    [Header("Boss References")]
    public Rigidbody2D rb;
    public SpriteRenderer spriteRenderer;
    public Animator animator;

    [Header("Boss Movement")]
    public float moveSpeed = 1.5f;
    public float stopDistance = 8f;

    [Header("Boss Detection")]
    public float detectionRadius = 15f;

    [Header("Boss Shooting")]
    public GameObject bulletPrefab;
    public Transform[] firePoints; // Multiple fire points for boss
    public float shootCooldown = 1f;
    public float bulletSpeed = 12f;
    public int bulletsPerShot = 3; // Boss fires multiple bullets

    private float lastShootTime;

    [Header("Boss Stats")]
    public int health = 500;
    public int maxHealth = 500;
    public int scoreValue = 1000;

    [Header("Boss Audio")]
    public AudioClip shootSound;
    public AudioClip hurtSound;
    public AudioClip deathSound;
    public AudioClip detectSound;
    public AudioClip[] footstepSounds;
    public AudioClip bossMusic; // Special boss music

    [Range(0f, 1f)]
    public float shootVolume = 0.8f;
    [Range(0f, 1f)]
    public float hurtVolume = 0.9f;
    [Range(0f, 1f)]
    public float deathVolume = 1f;
    [Range(0f, 1f)]
    public float detectVolume = 0.7f;
    [Range(0f, 1f)]
    public float footstepVolume = 0.4f;

    [Header("Boss Behavior")]
    public bool waitForAllEnemiesToDie = true; // Boss becomes vulnerable only after all enemies die
    public float invulnerabilityDuration = 2f; // Time boss is invulnerable when taking damage

    [Header("Game Over UI")]
    public GameObject gameOverUIPrefab; // Assign the Game Over UI prefab
    public string gameOverUITag = "GameOverUI"; // Tag to find existing UI

    [Header("Boss Visual Effects")]
    public GameObject deathExplosionPrefab;
    public float deathExplosionDelay = 1f;
    public GameObject bossHealthBarPrefab; // Optional health bar UI

    // Private variables
    private Transform currentTarget;
    private bool hasTarget = false;
    private bool hasDetectedBefore = false;
    private bool isDead = false;
    private bool isInvulnerable = false;
    private AudioSource audioSource;

    // Enemy tracking
    private List<EnemyAI> allEnemies = new List<EnemyAI>();
    private int initialEnemyCount = 0;
    private bool allEnemiesDefeated = false;

    // Health bar reference
    private GameObject healthBarInstance;

    private void Start()
    {
        Initialize();
        FindAndTrackAllEnemies();
        SetupBossHealthBar();
    }

    void Initialize()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        maxHealth = health;
        isDead = false;

        // Set up audio source
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0.5f; // More 2D audio for boss
        audioSource.volume = 1f;

        // Play boss music if available
        if (bossMusic != null)
        {
            audioSource.clip = bossMusic;
            audioSource.loop = true;
            audioSource.Play();
        }

        Debug.Log($"Boss {gameObject.name} initialized - Health: {health}");
    }

    void FindAndTrackAllEnemies()
    {
        // Find all EnemyAI components in the scene (excluding this boss)
        EnemyAI[] enemies = FindObjectsOfType<EnemyAI>();
        allEnemies.Clear();

        foreach (EnemyAI enemy in enemies)
        {
            // Make sure we don't include the boss itself if it has EnemyAI component
            if (enemy.gameObject != this.gameObject)
            {
                allEnemies.Add(enemy);
                // Subscribe to each enemy's death event
                enemy.OnEnemyDeath += OnEnemyDefeated;
            }
        }

        initialEnemyCount = allEnemies.Count;
        Debug.Log($"Boss tracking {initialEnemyCount} enemies");
    }

    void SetupBossHealthBar()
    {
        if (bossHealthBarPrefab != null)
        {
            healthBarInstance = Instantiate(bossHealthBarPrefab);
            BossHealthBar healthBar = healthBarInstance.GetComponent<BossHealthBar>();
            if (healthBar != null)
            {
                healthBar.Initialize(this);
            }
        }
    }

    void Update()
    {
        if (isDead) return;

        // Check if boss should be vulnerable
        CheckEnemyStatus();

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
            PlayFootstepSound();
        }
        else
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }

        spriteRenderer.flipX = currentTarget.position.x > transform.position.x;

        // Boss shooting (more aggressive)
        if (distance <= stopDistance && Time.time - lastShootTime >= shootCooldown)
        {
            BossShoot();
            lastShootTime = Time.time;
        }

        // Animations
        float currentSpeed = Mathf.Abs(rb.linearVelocity.x);
        if (animator != null)
        {
            animator.SetFloat("Speed", currentSpeed);
            animator.SetBool("AllEnemiesDefeated", allEnemiesDefeated);
            animator.SetBool("IsInvulnerable", isInvulnerable);
        }
    }

    void CheckEnemyStatus()
    {
        if (!allEnemiesDefeated)
        {
            // Remove any null references (destroyed enemies)
            allEnemies.RemoveAll(enemy => enemy == null);

            if (allEnemies.Count == 0)
            {
                allEnemiesDefeated = true;
                Debug.Log("All enemies defeated! Boss is now vulnerable!");

                // Visual/audio feedback that boss is now vulnerable
                if (animator != null)
                {
                    animator.SetTrigger("BecomeVulnerable");
                }

                PlayDetectSound(); // Reuse detect sound for vulnerability notification
            }
        }
    }

    void OnEnemyDefeated()
    {
        Debug.Log($"An enemy was defeated. Remaining: {allEnemies.Count - 1}");
    }

    void SearchForTarget()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        float closestDistance = Mathf.Infinity;
        Transform closestPlayer = null;

        foreach (GameObject player in players)
        {
            float dist = Vector2.Distance(transform.position, player.transform.position);
            if (dist <= detectionRadius && dist < closestDistance)
            {
                closestDistance = dist;
                closestPlayer = player.transform;
            }
        }

        if (closestPlayer != null)
        {
            if (!hasDetectedBefore)
            {
                PlayDetectSound();
                hasDetectedBefore = true;
                Debug.Log("Boss has detected the player!");
            }

            currentTarget = closestPlayer;
            hasTarget = true;
        }
        else
        {
            currentTarget = null;
            hasTarget = false;
            if (hasDetectedBefore)
            {
                hasDetectedBefore = false;
            }
        }
    }

    void BossShoot()
    {
        if (bulletPrefab == null || firePoints.Length == 0 || isDead) return;

        PlayShootSound();

        // Boss fires from multiple points
        foreach (Transform firePoint in firePoints)
        {
            if (firePoint == null) continue;

            for (int i = 0; i < bulletsPerShot; i++)
            {
                Vector2 baseDirection = (currentTarget.position - firePoint.position).normalized;

                // Add some spread to bullets
                float spreadAngle = (i - (bulletsPerShot - 1) / 2f) * 15f;
                float radianAngle = spreadAngle * Mathf.Deg2Rad;
                Vector2 shootDir = new Vector2(
                    baseDirection.x * Mathf.Cos(radianAngle) - baseDirection.y * Mathf.Sin(radianAngle),
                    baseDirection.x * Mathf.Sin(radianAngle) + baseDirection.y * Mathf.Cos(radianAngle)
                );

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
                    bulletScript.SetShooter("Boss");
                    bulletScript.SetDirection(shootDir);
                }
            }
        }
    }

    public void TakeDamage(float amount)
    {
        // Boss is invulnerable until all enemies are defeated
        if (waitForAllEnemiesToDie && !allEnemiesDefeated)
        {
            Debug.Log("Boss is invulnerable until all enemies are defeated!");
            return;
        }

        if (isDead || isInvulnerable) return;

        Debug.Log($"Boss took {amount} damage! Health: {health} -> {health - (int)amount}");

        PlayHurtSound();
        StartCoroutine(InvulnerabilityFrames());

        health -= (int)amount;
        health = Mathf.Max(health, 0);

        // Update health bar
        UpdateHealthBar();

        Debug.Log($"Boss health after damage: {health}");

        if (health <= 0)
        {
            Die();
        }
    }

    private IEnumerator InvulnerabilityFrames()
    {
        isInvulnerable = true;

        // Visual feedback for invulnerability
        Color originalColor = spriteRenderer.color;
        for (float t = 0; t < invulnerabilityDuration; t += 0.1f)
        {
            spriteRenderer.color = Color.red;
            yield return new WaitForSeconds(0.05f);
            spriteRenderer.color = originalColor;
            yield return new WaitForSeconds(0.05f);
        }

        spriteRenderer.color = originalColor;
        isInvulnerable = false;
    }

    void UpdateHealthBar()
    {
        if (healthBarInstance != null)
        {
            BossHealthBar healthBar = healthBarInstance.GetComponent<BossHealthBar>();
            if (healthBar != null)
            {
                healthBar.UpdateHealth(health, maxHealth);
            }
        }
    }

    void Die()
    {
        if (isDead) return;

        isDead = true;
        Debug.Log("Boss is dying!");

        PlayDeathSound();
        rb.linearVelocity = Vector2.zero;

        // Add boss score
        if (ScoreManagerV2.instance != null)
        {
            ScoreManagerV2.instance.AddPoints(scoreValue);
            Debug.Log($"Added {scoreValue} points for defeating the boss!");
        }

        StartCoroutine(BossDeathSequence());
    }

    private IEnumerator BossDeathSequence()
    {
        // Boss death animation
        if (animator != null)
        {
            animator.SetBool("IsDead", true);
        }

        // Wait for dramatic effect
        yield return new WaitForSeconds(deathExplosionDelay);

        // Create death explosion
        if (deathExplosionPrefab != null)
        {
            GameObject explosion = Instantiate(deathExplosionPrefab, transform.position, Quaternion.identity);
            Destroy(explosion, 3f); // Clean up explosion after 3 seconds
        }

        // Hide boss health bar
        if (healthBarInstance != null)
        {
            Destroy(healthBarInstance);
        }

        // Wait a bit more for dramatic effect
        yield return new WaitForSeconds(1f);

        // Show Game Over UI
        ShowGameOverUI();

        // Destroy the boss
        Destroy(gameObject);
    }

    void ShowGameOverUI()
    {
        // First try to find existing Game Over UI
        GameObject existingUI = GameObject.FindGameObjectWithTag(gameOverUITag);

        if (existingUI != null)
        {
            existingUI.SetActive(true);
            Debug.Log("Activated existing Game Over UI");
        }
        else if (gameOverUIPrefab != null)
        {
            // Create new Game Over UI
            GameObject gameOverUI = Instantiate(gameOverUIPrefab);
            Debug.Log("Created new Game Over UI");
        }
        else
        {
            Debug.LogWarning("No Game Over UI prefab assigned and no existing UI found!");
        }
    }

    // Audio methods (similar to regular enemy but with boss-specific tweaks)
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
            audioSource.Stop(); // Stop boss music
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

    void PlayFootstepSound()
    {
        if (footstepSounds.Length > 0 && Mathf.Abs(rb.linearVelocity.x) > 0.1f)
        {
            AudioClip footstep = footstepSounds[Random.Range(0, footstepSounds.Length)];
            if (footstep != null && audioSource != null)
            {
                audioSource.PlayOneShot(footstep, footstepVolume);
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, stopDistance);

        // Draw lines to all tracked enemies
        Gizmos.color = Color.blue;
        foreach (EnemyAI enemy in allEnemies)
        {
            if (enemy != null)
            {
                Gizmos.DrawLine(transform.position, enemy.transform.position);
            }
        }
    }

    // Public methods for external access
    public int GetRemainingEnemies()
    {
        return allEnemies.Count(enemy => enemy != null);
    }

    public bool AreAllEnemiesDefeated()
    {
        return allEnemiesDefeated;
    }

    public float GetHealthPercentage()
    {
        return (float)health / maxHealth;
    }
}