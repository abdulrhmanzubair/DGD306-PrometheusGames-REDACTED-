using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Enhanced slash effect with better damage detection and visual feedback
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

        // Schedule damage dealing (slight delay for animation timing)
        Invoke(nameof(DealDamage), 0.1f);

        // Destroy after lifetime
        Destroy(gameObject, lifetime);
    }

    void DealDamage()
    {
        if (hasDealtDamage) return;
        hasDealtDamage = true;

        if (useRadiusDamage)
        {
            DealRadiusDamage();
        }
    }

    void DealRadiusDamage()
    {
        // Get all colliders in damage radius
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(transform.position, damageRadius, targetLayers);

        int enemiesHit = 0;

        foreach (Collider2D hitCollider in hitColliders)
        {
            // Check if target has valid tag
            if (!HasValidTag(hitCollider.gameObject)) continue;

            // Prevent hitting the same target multiple times
            if (hitTargets.Contains(hitCollider.gameObject)) continue;

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
                    // You can implement screen shake here or call a screen shake manager
                    Debug.Log($"Screen shake: {screenShakeIntensity} for {screenShakeDuration}s");
                }
            }
        }

        // Bonus effects for multiple hits
        if (enemiesHit > 1)
        {
            Debug.Log($"Multi-hit! Struck {enemiesHit} enemies!");
            // Could add combo effects, extra damage, etc.
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
        if (!useRadiusDamage && HasValidTag(other.gameObject))
        {
            // Prevent hitting the same target multiple times
            if (hitTargets.Contains(other.gameObject)) return;
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
    }

    // Public methods for external control
    public void SetDamage(float newDamage)
    {
        damage = newDamage;
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
    }
}