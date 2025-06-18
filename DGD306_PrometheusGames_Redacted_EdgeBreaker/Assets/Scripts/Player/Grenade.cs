using UnityEngine;
using System.Collections;

/// <summary>
/// Simple Grenade script that matches the melee controller requirements
/// </summary>
public class Grenade : MonoBehaviour
{
    [Header("Explosion Settings")]
    public float explosionRadius = 3f;
    public float explosionDamage = 50f;
    public float explosionForce = 15f;
    public LayerMask damageableLayers = -1;

    [Header("Timer Settings")]
    public float fuseTime = 3f;

    [Header("Effects")]
    public GameObject explosionEffectPrefab;
    public AudioClip explosionSound;
    [Range(0f, 1f)] public float explosionVolume = 1f;

    private float timer;
    private bool hasExploded = false;
    private GameObject thrower;
    private Rigidbody2D rb;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        timer = fuseTime;
    }

    void Update()
    {
        if (hasExploded) return;

        timer -= Time.deltaTime;

        if (timer <= 0)
        {
            Explode();
        }
    }

    // Required methods for Player_Melee_Controller1
    public void SetTimer(float newTimer)
    {
        fuseTime = newTimer;
        timer = newTimer;
    }

    public void SetThrower(GameObject throwingObject)
    {
        thrower = throwingObject;

        // Ignore collision with thrower initially
        if (throwingObject != null)
        {
            Collider2D throwerCollider = throwingObject.GetComponent<Collider2D>();
            Collider2D grenadeCollider = GetComponent<Collider2D>();

            if (throwerCollider != null && grenadeCollider != null)
            {
                Physics2D.IgnoreCollision(grenadeCollider, throwerCollider, true);
                StartCoroutine(EnableCollisionWithThrower(throwerCollider, grenadeCollider));
            }
        }
    }

    // Required method for PlayerController (gunner)
    public void Launch(Vector2 direction, float force, GameObject launcher = null)
    {
        if (rb != null)
        {
            rb.linearVelocity = direction.normalized * force;
        }

        if (launcher != null)
        {
            SetThrower(launcher);
        }

        Debug.Log($"Grenade launched with force {force} in direction {direction}");
    }

    // Alternative Launch method with different parameters (in case your PlayerController uses different signature)
    public void Launch(Vector2 velocity)
    {
        if (rb != null)
        {
            rb.linearVelocity = velocity;
        }

        Debug.Log($"Grenade launched with velocity {velocity}");
    }

    // Alternative Launch method for more complex setup
    public void Launch(Vector2 direction, float force, float fuseTimer, GameObject launcher)
    {
        // Set the fuse timer
        SetTimer(fuseTimer);

        // Set the thrower
        SetThrower(launcher);

        // Launch the grenade
        if (rb != null)
        {
            rb.linearVelocity = direction.normalized * force;
        }

        Debug.Log($"Grenade launched with force {force}, fuse timer {fuseTimer}");
    }

    IEnumerator EnableCollisionWithThrower(Collider2D throwerCollider, Collider2D grenadeCollider)
    {
        yield return new WaitForSeconds(0.5f);

        if (throwerCollider != null && grenadeCollider != null)
        {
            Physics2D.IgnoreCollision(grenadeCollider, throwerCollider, false);
        }
    }

    void Explode()
    {
        if (hasExploded) return;

        hasExploded = true;

        Debug.Log($"Grenade exploded at {transform.position}!");

        // Play explosion sound
        if (explosionSound != null)
        {
            AudioSource.PlayClipAtPoint(explosionSound, transform.position, explosionVolume);
        }

        // Spawn explosion effect
        if (explosionEffectPrefab != null)
        {
            GameObject explosion = Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity);
            Destroy(explosion, 5f);
        }

        // Deal explosion damage
        DealExplosionDamage();

        // Destroy grenade
        Destroy(gameObject);
    }

    void DealExplosionDamage()
    {
        Collider2D[] objectsInRange = Physics2D.OverlapCircleAll(transform.position, explosionRadius, damageableLayers);

        foreach (Collider2D obj in objectsInRange)
        {
            // Skip the thrower for a brief moment to prevent self-damage
            if (obj.gameObject == thrower && timer > fuseTime - 0.5f) continue;

            // Calculate distance for damage falloff
            float distance = Vector2.Distance(transform.position, obj.transform.position);
            float damageMultiplier = 1f - (distance / explosionRadius);
            damageMultiplier = Mathf.Clamp01(damageMultiplier);

            float finalDamage = explosionDamage * damageMultiplier;
            DealDamageToTarget(obj, finalDamage);
            ApplyExplosionForce(obj, damageMultiplier);
        }
    }

    void DealDamageToTarget(Collider2D target, float damage)
    {
        // Try IDamageable interface first
        IDamageable damageable = target.GetComponent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(damage);
            Debug.Log($"Grenade dealt {damage:F1} damage to {target.name}");
            return;
        }

        // Try PlayerHealthSystem
        PlayerHealthSystem playerHealth = target.GetComponent<PlayerHealthSystem>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(damage);
            Debug.Log($"Grenade dealt {damage:F1} damage to Player {playerHealth.PlayerIndex}");
            return;
        }

        // Try UniversalEnemyHealth
        UniversalEnemyHealth enemyHealth = target.GetComponent<UniversalEnemyHealth>();
        if (enemyHealth != null)
        {
            enemyHealth.TakeDamage(damage);
            Debug.Log($"Grenade dealt {damage:F1} damage to enemy {target.name}");
            return;
        }
    }

    void ApplyExplosionForce(Collider2D target, float forceMultiplier)
    {
        Rigidbody2D targetRb = target.GetComponent<Rigidbody2D>();
        if (targetRb != null)
        {
            Vector2 forceDirection = (target.transform.position - transform.position).normalized;
            float finalForce = explosionForce * forceMultiplier;

            targetRb.AddForce(forceDirection * finalForce, ForceMode2D.Impulse);
        }
    }

    void OnDrawGizmosSelected()
    {
        // Draw explosion radius
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}