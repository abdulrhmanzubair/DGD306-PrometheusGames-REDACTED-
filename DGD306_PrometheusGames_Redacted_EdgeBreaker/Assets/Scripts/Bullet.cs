using UnityEngine;

public class Bullet : MonoBehaviour
{
    [Header("Bullet Settings")]
    public float speed = 15f;
    public float damage = 10f;
    public float lifetime = 3f;
    public Sprite[] lifeStages; // Optional, for visual life progression
    public GameObject hitEffectPrefab; // Spark/explosion prefab

    [Header("Shooter Tag")]
    public string shooterTag = "Enemy"; // Set in Inspector

    private Vector2 direction;
    private SpriteRenderer sr;
    private float timer;

    public void SetShooter(string tag)
    {
        shooterTag = tag;
    }

    public void SetDirection(Vector2 dir)
    {
        direction = dir.normalized;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
    }

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        Destroy(gameObject, lifetime); // Auto-destroy after lifetime
    }

    void Update()
    {
        transform.Translate(direction * speed * Time.deltaTime, Space.World);

        // Optional: change sprite based on lifetime progression
        timer += Time.deltaTime;
        if (timer >= 2f && lifeStages.Length > 2)
            sr.sprite = lifeStages[2];
        else if (timer >= 1f && lifeStages.Length > 1)
            sr.sprite = lifeStages[1];
        else if (lifeStages.Length > 0)
            sr.sprite = lifeStages[0];
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // Don't hit shooter or friendly targets
        if (other.CompareTag(shooterTag)) return;

        // Deal damage if the target is damageable
        IDamageable damageable = other.GetComponent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(damage);
        }

        // Spawn hit effect if available
        if (hitEffectPrefab != null)
        {
            Instantiate(hitEffectPrefab, transform.position, Quaternion.identity);
        }

        // If the enemy is destroyed, add score
        if (other.CompareTag("Enemy")) // If it's an enemy, increase score
        {
            if (ScoreManager.Instance != null)
            {
                EnemyAI enemy = other.GetComponent<EnemyAI>();
                if (enemy != null)
                {
                    ScoreManager.Instance.AddScore(enemy.scoreValue); // Assuming enemy has a scoreValue field
                }
            }
        }

        // Destroy the bullet after the hit
        Destroy(gameObject);
    }
}
