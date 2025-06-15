using UnityEngine;
using System.Collections;

public class EnemyAI : MonoBehaviour, IDamageable
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
    public int health = 100;
    public int maxHealth = 100; // Store original health
    public int scoreValue = 50;

    [Header("Audio Settings")]
    public AudioClip shootSound;        // Sound when enemy shoots
    public AudioClip hurtSound;         // Sound when taking damage
    public AudioClip deathSound;        // Sound when enemy dies
    public AudioClip detectSound;       // Sound when player is detected
    public AudioClip[] footstepSounds;  // Array of footstep sounds for variety

    [Range(0f, 1f)]
    public float shootVolume = 0.6f;
    [Range(0f, 1f)]
    public float hurtVolume = 0.7f;
    [Range(0f, 1f)]
    public float deathVolume = 0.8f;
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

    private void Start()
    {
        Initialize();
    }

    void Initialize()
    {
        animator = GetComponent<Animator>();

        // Store max health
        maxHealth = health;
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

        Debug.Log($"Enemy {gameObject.name} initialized - Health: {health}, Audio: {audioSource != null}");
    }

    void Update()
    {
        if (isDead) return; // Don't update if dead

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

    public void TakeDamage(float amount)
    {
        if (isDead) return; // Don't take damage if already dead

        Debug.Log($"{gameObject.name} took {amount} damage! Health: {health} -> {health - (int)amount}");

        // Play hurt sound
        PlayHurtSound();

        health -= (int)amount;
        health = Mathf.Max(health, 0); // Clamp to 0

        Debug.Log($"{gameObject.name} health after damage: {health}");

        if (health <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        if (isDead) return; // Prevent multiple death calls

        isDead = true;
        Debug.Log($"{gameObject.name} is dying!");

        // Play death sound immediately
        PlayDeathSound();

        // Stop all movement
        rb.linearVelocity = Vector2.zero;

        // Trigger death event
        OnEnemyDeath?.Invoke();

        // Add score
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.AddScore(scoreValue);
            Debug.Log($"Added {scoreValue} points for killing {gameObject.name}");
        }

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

        // Destroy the enemy
        Destroy(gameObject);
    }

    // Audio Methods with debug logging
    void PlayShootSound()
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

    void PlayHurtSound()
    {
        if (hurtSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(hurtSound, hurtVolume);
            Debug.Log($"{gameObject.name} played hurt sound");
        }
        else
        {
            Debug.LogWarning($"{gameObject.name} - Cannot play hurt sound: hurtSound={hurtSound}, audioSource={audioSource}");
        }
    }

    void PlayDeathSound()
    {
        if (deathSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(deathSound, deathVolume);
            Debug.Log($"{gameObject.name} played death sound");
        }
        else
        {
            Debug.LogWarning($"{gameObject.name} - Cannot play death sound: deathSound={deathSound}, audioSource={audioSource}");
        }
    }

    void PlayDetectSound()
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
    }

    // Public method to reset enemy (useful for object pooling)
    public void ResetEnemy()
    {
        health = maxHealth;
        isDead = false;
        hasDetectedBefore = false;
        hasTarget = false;
        currentTarget = null;
        rb.linearVelocity = Vector2.zero;
    }
}