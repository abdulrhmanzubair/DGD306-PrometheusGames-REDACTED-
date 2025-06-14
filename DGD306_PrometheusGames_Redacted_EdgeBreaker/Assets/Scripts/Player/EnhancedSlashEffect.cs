using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Enhanced slash effect with better damage detection and visual feedback - FIXED VERSION
/// </summary>
public class EnhancedSlashEffect : MonoBehaviour
{
    [Header("Damage Settings")]
    public float damage = 20f;
    public float knockbackForce = 10f;
    public LayerMask targetLayers = -1;
    public string[] targetTags = { "Enemy" };

    [Header("Effect Settings")]
    public float lifetime = 0.3f;
    public float damageRadius = 1.5f;
    public bool useRadiusDamage = true;

    [Header("Visual Effects")]
    public GameObject hitEffectPrefab;
    public GameObject slashTrailPrefab;
    public ParticleSystem hitParticles;

    [Header("Audio")]
    public AudioClip slashSound;
    public AudioClip[] hitSounds;
    [Range(0f, 1f)] public float slashVolume = 0.6f;
    [Range(0f, 1f)] public float hitVolume = 0.5f;

    [Header("Screen Effects")]
    public bool useScreenShake = true;
    public float screenShakeIntensity = 0.3f;
    public float screenShakeDuration = 0.2f;

    private Animator animator;
    private AudioSource audioSource;
    private HashSet<GameObject> hitTargets = new HashSet<GameObject>();
    private bool hasDealtDamage = false;

    void Start()
    {
        Initialize();
    }

    void Initialize()
    {
        Debug.Log($"EnhancedSlashEffect initialized with {damage} damage, radius {damageRadius}");

        animator = GetComponent<Animator>();

        // Set up audio
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0.5f;

        // Play slash animation
        if (animator != null)
        {
            animator.SetTrigger("Slash");
        }

        // Play slash sound
        PlaySlashSound();

        // Spawn slash trail effect
        if (slashTrailPrefab != null)
        {
            GameObject trail = Instantiate(slashTrailPrefab, transform.position, transform.rotation);
            Destroy(trail, lifetime + 0.5f);
        }

        // FIX: Deal damage immediately instead of waiting
        DealDamage();

        // Destroy after lifetime
        Destroy(gameObject, lifetime);
    }

    void DealDamage()
    {
        if (hasDealtDamage)
        {
            Debug.Log("Damage already dealt, skipping");
            return;
        }

        hasDealtDamage = true;
        Debug.Log("Dealing slash damage...");

        if (useRadiusDamage)
        {
            DealRadiusDamage();
        }
    }

    void DealRadiusDamage()
    {
        Debug.Log($"Checking for targets in radius {damageRadius} at position {transform.position}");

        // Get all colliders in damage radius
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(transform.position, damageRadius, targetLayers);

        Debug.Log($"Found {hitColliders.Length} colliders in damage radius");

        int enemiesHit = 0;

        foreach (Collider2D hitCollider in hitColliders)
        {
            Debug.Log($"Checking collider: {hitCollider.name}, tag: {hitCollider.tag}");

            // Check if target has valid tag
            if (!HasValidTag(hitCollider.gameObject))
            {
                Debug.Log($"Skipping {hitCollider.name} - invalid tag");
                continue;
            }

            // Prevent hitting the same target multiple times
            if (hitTargets.Contains(hitCollider.gameObject))
            {
                Debug.Log($"Skipping {hitCollider.name} - already hit");
                continue;
            }

            // Add to hit targets
            hitTargets.Add(hitCollider.gameObject);

            // Deal damage
            IDamageable damageable = hitCollider.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(damage);
                enemiesHit++;

                Debug.Log($"Slash hit {hitCollider.name} for {damage} damage!");

                // Apply knockback
                ApplyKnockback(hitCollider);

                // Spawn hit effect
                SpawnHitEffect(hitCollider.transform.position);

                // Play hit sound
                PlayHitSound();

                // Screen shake for each hit
                if (useScreenShake)
                {
                    Debug.Log($"Screen shake: {screenShakeIntensity} for {screenShakeDuration}s");
                }
            }
            else
            {
                // FIX: Try alternative damage methods
                Debug.LogWarning($"No IDamageable on {hitCollider.name}, trying alternative damage methods...");

                // Try UniversalEnemyHealth
                UniversalEnemyHealth enemyHealth = hitCollider.GetComponent<UniversalEnemyHealth>();
                if (enemyHealth != null)
                {
                    enemyHealth.TakeDamage(damage);
                    enemiesHit++;
                    Debug.Log($"Used UniversalEnemyHealth to damage {hitCollider.name} for {damage}!");

                    ApplyKnockback(hitCollider);
                    SpawnHitEffect(hitCollider.transform.position);
                    PlayHitSound();
                }
                else
                {
                    Debug.LogWarning($"No damage component found on {hitCollider.name}!");
                }
            }
        }

        // Bonus effects for multiple hits
        if (enemiesHit > 1)
        {
            Debug.Log($"Multi-hit! Struck {enemiesHit} enemies!");
        }
        else if (enemiesHit == 0)
        {
            Debug.LogWarning("Slash attack hit no enemies!");
        }

        // If we hit something, play particles
        if (enemiesHit > 0 && hitParticles != null)
        {
            hitParticles.Play();
        }
    }

    bool HasValidTag(GameObject target)
    {
        foreach (string tag in targetTags)
        {
            if (target.CompareTag(tag))
            {
                return true;
            }
        }
        Debug.Log($"Target {target.name} has tag '{target.tag}' which is not in valid tags: [{string.Join(", ", targetTags)}]");
        return false;
    }

    void ApplyKnockback(Collider2D target)
    {
        if (knockbackForce <= 0) return;

        Rigidbody2D targetRb = target.GetComponent<Rigidbody2D>();
        if (targetRb != null)
        {
            // Calculate knockback direction (away from slash)
            Vector2 knockbackDirection = (target.transform.position - transform.position).normalized;

            // Apply knockback force
            targetRb.AddForce(knockbackDirection * knockbackForce, ForceMode2D.Impulse);

            Debug.Log($"Applied knockback to {target.name}: {knockbackDirection * knockbackForce}");
        }
    }

    void SpawnHitEffect(Vector3 position)
    {
        if (hitEffectPrefab != null)
        {
            GameObject hitEffect = Instantiate(hitEffectPrefab, position, Quaternion.identity);

            // Scale hit effect based on damage
            float scale = Mathf.Clamp(damage / 20f, 0.5f, 2f);
            hitEffect.transform.localScale = Vector3.one * scale;

            // Auto-destroy hit effect
            Destroy(hitEffect, 1f);
        }
    }

    void PlaySlashSound()
    {
        if (slashSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(slashSound, slashVolume);
        }
    }

    void PlayHitSound()
    {
        if (hitSounds.Length > 0 && audioSource != null)
        {
            AudioClip hitClip = hitSounds[Random.Range(0, hitSounds.Length)];
            audioSource.PlayOneShot(hitClip, hitVolume);
        }
    }

    // Handle trigger-based damage (fallback)
    void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log($"Slash effect triggered by: {other.name} (tag: {other.tag})");

        if (!useRadiusDamage && HasValidTag(other.gameObject))
        {
            // Prevent hitting the same target multiple times
            if (hitTargets.Contains(other.gameObject))
            {
                Debug.Log($"Already hit {other.name}, skipping");
                return;
            }

            hitTargets.Add(other.gameObject);

            IDamageable damageable = other.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(damage);
                Debug.Log($"Trigger slash hit {other.name} for {damage} damage!");

                ApplyKnockback(other);
                SpawnHitEffect(other.transform.position);
                PlayHitSound();
            }
            else
            {
                // Try UniversalEnemyHealth as fallback
                UniversalEnemyHealth enemyHealth = other.GetComponent<UniversalEnemyHealth>();
                if (enemyHealth != null)
                {
                    enemyHealth.TakeDamage(damage);
                    Debug.Log($"Trigger slash used UniversalEnemyHealth on {other.name} for {damage} damage!");

                    ApplyKnockback(other);
                    SpawnHitEffect(other.transform.position);
                    PlayHitSound();
                }
                else
                {
                    Debug.LogWarning($"No damage component found on triggered object {other.name}!");
                }
            }
        }
    }

    // Debug visualization
    void OnDrawGizmosSelected()
    {
        // Draw damage radius
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, damageRadius);

        // Draw slash direction
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, transform.right * damageRadius);

        // Draw target layer info
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position + Vector3.up * 2f, Vector3.one * 0.3f);
    }

    void OnDrawGizmos()
    {
        // Always draw damage radius for debugging
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, damageRadius);
    }

    // Public methods for external control
    public void SetDamage(float newDamage)
    {
        damage = newDamage;
        Debug.Log($"Slash effect damage set to {damage}");
    }

    public void SetKnockback(float newKnockback)
    {
        knockbackForce = newKnockback;
    }

    public void AddTargetTag(string tag)
    {
        // Expand target tags array
        string[] newTags = new string[targetTags.Length + 1];
        targetTags.CopyTo(newTags, 0);
        newTags[targetTags.Length] = tag;
        targetTags = newTags;

        Debug.Log($"Added target tag: {tag}. Current tags: [{string.Join(", ", targetTags)}]");
    }

    public void SetTargetLayers(LayerMask layers)
    {
        targetLayers = layers;
        Debug.Log($"Slash effect target layers set to: {targetLayers.value}");
    }
}