using UnityEngine;

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
    public int scoreValue = 50;

    private Transform currentTarget;
    private bool hasTarget = false;
    public event System.Action OnEnemyDeath;


    void Update()
    {
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
            currentTarget = closestPlayer;
            hasTarget = true;
        }
        else
        {
            currentTarget = null;
            hasTarget = false;
        }
    }

    void Shoot()
    {
        if (bulletPrefab == null || firePoint == null) return;

        Vector2 shootDir = (currentTarget.position - firePoint.position).normalized;

        GameObject bullet = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);
        Rigidbody2D bulletRb = bullet.GetComponent<Rigidbody2D>();
        bulletRb.linearVelocity = shootDir * bulletSpeed;

        float angle = Mathf.Atan2(shootDir.y, shootDir.x) * Mathf.Rad2Deg;
        bullet.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);

        Bullet bulletScript = bullet.GetComponent<Bullet>();
        if (bulletScript != null)
        {
            bulletScript.SetShooter("Enemy");
        }
    }

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
        OnEnemyDeath?.Invoke();

        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.AddScore(scoreValue);
        }

        Destroy(gameObject);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}
