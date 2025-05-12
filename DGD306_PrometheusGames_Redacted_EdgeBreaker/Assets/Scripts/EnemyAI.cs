using UnityEngine;

public class EnemyAI : MonoBehaviour, IDamageable
{
    [Header("References")]
    public Transform player;
    public Rigidbody2D rb;
    public SpriteRenderer spriteRenderer;

    [Header("Movement")]
    public float moveSpeed = 2f;
    public float stopDistance = 5f;

    [Header("Shooting")]
    public GameObject bulletPrefab;
    public Transform firePoint;
    public float shootCooldown = 1.5f;
    public float bulletSpeed = 10f;

    private float lastShootTime;

    [Header("Enemy Stats")]
    public int health = 100;
    public int scoreValue = 50; // Score added when killed

    void Update()
    {
        if (!player) return;

        float distance = Vector2.Distance(transform.position, player.position);

        // === Movement ===
        if (distance > stopDistance)
        {
            Vector2 direction = (player.position - transform.position).normalized;
            rb.linearVelocity = new Vector2(direction.x * moveSpeed, rb.linearVelocity.y);
        }
        else
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y); // Stop when in range
        }

        // === Facing ===
        if (player.position.x > transform.position.x)
            spriteRenderer.flipX = true;
        else
            spriteRenderer.flipX = false;

        // === Shooting ===
        if (distance <= stopDistance && Time.time - lastShootTime >= shootCooldown)
        {
            Shoot();
            lastShootTime = Time.time;
        }
    }

    void Shoot()
    {
        Vector2 shootDir = (player.position - firePoint.position).normalized;

        GameObject bullet = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);
        Rigidbody2D bulletRb = bullet.GetComponent<Rigidbody2D>();
        bulletRb.linearVelocity = shootDir * bulletSpeed;

        // Rotate bullet to match direction
        float angle = Mathf.Atan2(shootDir.y, shootDir.x) * Mathf.Rad2Deg;
        bullet.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);

        // Assign shooter tag
        Bullet bulletScript = bullet.GetComponent<Bullet>();
        if (bulletScript != null)
        {
            bulletScript.SetShooter("Enemy");
        }
    }

    // Implement IDamageable interface
    public void TakeDamage(float amount)
    {
        health -= (int)amount;
        if (health <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        // Add score when the enemy dies
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.AddScore(scoreValue);
        }

        // Optional: play death animation/effect
        Destroy(gameObject);
    }
}
