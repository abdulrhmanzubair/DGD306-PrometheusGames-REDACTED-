using UnityEngine;

public class Grenade : MonoBehaviour
{
    [Header("Grenade Settings")]
    public float explosionRadius = 3f;
    public float explosionDamage = 50f;
    public float fuseTime = 3f; // Time before explosion
    public LayerMask damageableLayers = -1;

    [Header("Visual Effects")]
    public GameObject explosionEffectPrefab;
    public float explosionEffectDuration = 2f;

    [Header("Audio")]
    public AudioClip explosionSound;

    private Rigidbody2D rb;
    private bool hasExploded = false;
    private float timer;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        // Check if Rigidbody2D exists
        if (rb == null)
        {
            Debug.LogError("Grenade: No Rigidbody2D component found! Adding one automatically.");
            rb = gameObject.AddComponent<Rigidbody2D>();
        }
    }

    void Start()
    {
        timer = fuseTime;

        // Normal physics with gravity for natural arc
        rb.gravityScale = 1f;
        rb.angularVelocity = Random.Range(-180f, 180f);
    }

    void Update()
    {
        // Countdown to explosion
        timer -= Time.deltaTime;
        if (timer <= 0f && !hasExploded)
        {
            Explode();
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // Bounce off surfaces with energy loss
        if (collision.gameObject.CompareTag("Ground") || collision.gameObject.layer == LayerMask.NameToLayer("Ground"))
        {
            rb.linearVelocity = rb.linearVelocity * 0.7f; // Lose some energy on bounce
        }
    }

    void Explode()
    {
        if (hasExploded) return;
        hasExploded = true;

        // Find all objects in explosion radius
        Collider2D[] objectsInRange = Physics2D.OverlapCircleAll(transform.position, explosionRadius, damageableLayers);

        foreach (Collider2D obj in objectsInRange)
        {
            // Calculate distance for damage falloff
            float distance = Vector2.Distance(transform.position, obj.transform.position);
            float damageMultiplier = Mathf.Clamp01(1f - (distance / explosionRadius));
            float actualDamage = explosionDamage * damageMultiplier;

            // Try to damage the object
            Enemy enemy = obj.GetComponent<Enemy>();
            if (enemy != null)
            {
                enemy.TakeDamage(actualDamage);
                Debug.Log($"Grenade damaged {obj.name} for {actualDamage} damage!");
            }

            // Add knockback effect
            Rigidbody2D targetRb = obj.GetComponent<Rigidbody2D>();
            if (targetRb != null)
            {
                Vector2 knockbackDirection = (obj.transform.position - transform.position).normalized;
                float knockbackForce = (explosionDamage * damageMultiplier) * 0.5f;
                targetRb.AddForce(knockbackDirection * knockbackForce, ForceMode2D.Impulse);
            }
        }

        // Create explosion effect
        if (explosionEffectPrefab != null)
        {
            GameObject effect = Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity);
            Destroy(effect, explosionEffectDuration);
        }

        // Play explosion sound
        if (explosionSound != null)
        {
            AudioSource.PlayClipAtPoint(explosionSound, transform.position);
        }

        Debug.Log($"Grenade exploded! Affected {objectsInRange.Length} objects.");
        Destroy(gameObject);
    }

    // Launch the grenade forward
    public void Launch(Vector2 direction, float force)
    {
        if (rb == null)
        {
            Debug.LogError("Grenade: Cannot launch - Rigidbody2D is null!");
            return;
        }

        rb.linearVelocity = direction * force;
        Debug.Log($"Grenade launched with velocity: {rb.linearVelocity}");
    }

    // Visualize explosion radius
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}