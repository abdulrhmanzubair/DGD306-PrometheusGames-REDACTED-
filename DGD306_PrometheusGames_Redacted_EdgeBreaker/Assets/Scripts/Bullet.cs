using UnityEngine;

public class Bullet : MonoBehaviour
{
    public float speed = 15f;
    public float damage = 10f;
    public Sprite[] lifeStages; // Optional, for visual life progression
    public GameObject hitEffectPrefab; // Spark/explosion prefab

    private Vector2 direction;
    private SpriteRenderer sr;
    private float lifetime = 3f;
    private float timer;

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        Destroy(gameObject, lifetime); // Auto-destroy after 3 seconds
    }

    public void SetDirection(Vector2 dir)
    {
        direction = dir.normalized;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
    }

    void Update()
    {
        transform.Translate(direction * speed * Time.deltaTime, Space.World);

        // Optional: change sprite based on lifetime
        timer += Time.deltaTime;
        if (timer >= 2f && lifeStages.Length > 2)
            sr.sprite = lifeStages[2];
        else if (timer >= 1f && lifeStages.Length > 1)
            sr.sprite = lifeStages[1];
        else if (lifeStages.Length > 0)
            sr.sprite = lifeStages[0];
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Check if the object has a damageable component
        IDamageable damageable = other.GetComponent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(damage);
        }

        // Spawn hit effect (e.g. spark or explosion)
        if (hitEffectPrefab != null)
        {
            Instantiate(hitEffectPrefab, transform.position, Quaternion.identity);
        }

        Destroy(gameObject); // Destroy the bullet
    }
}
