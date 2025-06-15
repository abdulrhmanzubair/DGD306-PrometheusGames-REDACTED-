using UnityEngine;

/// <summary>
/// Fixed Bullet script with proper audio timing
/// </summary>
public class Bullet : MonoBehaviour
{
    [Header("Bullet Settings")]
    public float speed = 15f;
    public float damage = 10f;
    public float lifetime = 3f;
    public Sprite[] lifeStages; // Optional, for visual life progression
    public GameObject hitEffectPrefab; // Spark/explosion prefab

    [Header("Audio Settings")]
    public AudioClip shootSound; // Sound effect when bullet is fired
    public AudioClip hitSound; // Optional: sound effect when bullet hits something
    [Range(0f, 1f)]
    public float shootVolume = 0.7f;
    [Range(0f, 1f)]
    public float hitVolume = 0.5f;

    [Header("Shooter Tag")]
    public string shooterTag = "Enemy"; // Set in Inspector

    private Vector2 direction;
    private SpriteRenderer sr;
    private float timer;
    private AudioSource audioSource;
    private bool isPooled = false;
    private bool hasPlayedShootSound = false; // Track if shoot sound was played

    public void SetShooter(string tag)
    {
        shooterTag = tag;
    }

    public void SetDirection(Vector2 dir)
    {
        direction = dir.normalized;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);

        // Play shoot sound when direction is set (when bullet is fired)
        if (!hasPlayedShootSound)
        {
            PlayShootSound();
            hasPlayedShootSound = true;
        }
    }

    void Start()
    {
        Initialize();
    }

    void OnEnable()
    {
        // Reset bullet when reactivated from pool
        ResetBullet();
    }

    void Initialize()
    {
        sr = GetComponent<SpriteRenderer>();

        // Set up audio source
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // Configure audio source settings
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0.3f; // Mix between 2D and 3D for better positional audio

        // Check if this bullet is pooled
        isPooled = BulletPool.Instance != null && GetComponent<BulletPooled>() != null;
    }

    void ResetBullet()
    {
        timer = 0f;
        direction = Vector2.right; // Default direction
        hasPlayedShootSound = false; // Reset shoot sound flag

        // Reset sprite to first stage
        if (sr != null && lifeStages.Length > 0)
        {
            sr.sprite = lifeStages[0];
        }

        // DON'T play shoot sound here - it will be played when SetDirection is called

        // Set lifetime destruction only if not pooled
        if (!isPooled)
        {
            Destroy(gameObject, lifetime);
        }
    }

    void Update()
    {
        // Move the bullet
        transform.Translate(direction * speed * Time.deltaTime, Space.World);

        // Update timer and sprite stages
        timer += Time.deltaTime;

        // Optional: change sprite based on lifetime progression
        if (timer >= 2f && lifeStages.Length > 2)
            sr.sprite = lifeStages[2];
        else if (timer >= 1f && lifeStages.Length > 1)
            sr.sprite = lifeStages[1];
        else if (lifeStages.Length > 0)
            sr.sprite = lifeStages[0];

        // Handle lifetime for pooled bullets
        if (isPooled && timer >= lifetime)
        {
            ReturnToPool();
        }
    }

    void PlayShootSound()
    {
        if (shootSound != null && audioSource != null)
        {
            // Make sure audio source is properly configured
            audioSource.clip = shootSound;
            audioSource.volume = shootVolume;
            audioSource.pitch = Random.Range(0.9f, 1.1f); // Add slight pitch variation
            audioSource.Play();

            // Debug log to verify it's being called
            Debug.Log($"Playing shoot sound for {shooterTag} bullet at {transform.position}");
        }
        else
        {
            Debug.LogWarning($"Cannot play shoot sound - shootSound: {shootSound}, audioSource: {audioSource}");
        }
    }

    void PlayHitSound()
    {
        if (hitSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(hitSound, hitVolume);
            Debug.Log($"Playing hit sound for {shooterTag} bullet");
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // Don't hit shooter or friendly targets
        if (other.CompareTag(shooterTag)) return;

        // Play hit sound effect
        PlayHitSound();

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
        if (other.CompareTag("Enemy"))
        {
            if (ScoreManager.Instance != null)
            {
                OptimizedEnemyAI enemy = other.GetComponent<OptimizedEnemyAI>();
                if (enemy != null)
                {
                    ScoreManager.Instance.AddScore(enemy.scoreValue);
                }
                else
                {
                    // Fallback for old EnemyAI script
                    EnemyAI oldEnemy = other.GetComponent<EnemyAI>();
                    if (oldEnemy != null)
                    {
                        ScoreManager.Instance.AddScore(oldEnemy.scoreValue);
                    }
                }
            }
        }

        // Destroy or return bullet after hit
        if (isPooled)
        {
            // Small delay to allow hit sound to play
            Invoke(nameof(ReturnToPool), 0.1f);
        }
        else
        {
            Destroy(gameObject, 0.1f);
        }
    }

    void ReturnToPool()
    {
        if (BulletPool.Instance != null)
        {
            BulletPool.Instance.ReturnBullet(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Public method to manually play shoot sound (for debugging)
    public void ForcePlayShootSound()
    {
        PlayShootSound();
    }
}