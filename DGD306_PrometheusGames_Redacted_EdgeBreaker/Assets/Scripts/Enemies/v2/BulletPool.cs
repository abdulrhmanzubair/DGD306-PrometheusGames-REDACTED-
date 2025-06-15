using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Object pool for bullets to improve performance by reusing bullet objects
/// Attach this to an empty GameObject in your scene
/// </summary>
public class BulletPool : MonoBehaviour
{
    public static BulletPool Instance;

    [Header("Pool Settings")]
    public GameObject bulletPrefab;
    public int initialPoolSize = 50;
    public int maxPoolSize = 100;
    public bool allowGrowth = true;

    [Header("Debug Info")]
    [SerializeField] private int activeCount = 0;
    [SerializeField] private int pooledCount = 0;

    private Queue<GameObject> bulletPool = new Queue<GameObject>();
    private HashSet<GameObject> activeBullets = new HashSet<GameObject>();

    void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializePool();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void InitializePool()
    {
        // Pre-instantiate bullets
        for (int i = 0; i < initialPoolSize; i++)
        {
            CreateNewBullet();
        }

        UpdateDebugInfo();
    }

    GameObject CreateNewBullet()
    {
        GameObject bullet = Instantiate(bulletPrefab);
        bullet.SetActive(false);

        // Ensure bullet has the proper component to return to pool
        BulletPooled pooledComponent = bullet.GetComponent<BulletPooled>();
        if (pooledComponent == null)
        {
            pooledComponent = bullet.AddComponent<BulletPooled>();
        }

        bulletPool.Enqueue(bullet);
        return bullet;
    }

    public GameObject GetBullet()
    {
        GameObject bullet = null;

        // Try to get from pool
        if (bulletPool.Count > 0)
        {
            bullet = bulletPool.Dequeue();
        }
        // Create new if pool is empty and growth is allowed
        else if (allowGrowth && activeBullets.Count < maxPoolSize)
        {
            bullet = CreateNewBullet();
            bulletPool.Dequeue(); // Remove it from pool since we're using it
        }
        // Return null if we can't create more
        else
        {
            Debug.LogWarning("Bullet pool exhausted! Consider increasing pool size.");
            return null;
        }

        // Activate and track the bullet
        bullet.SetActive(true);
        activeBullets.Add(bullet);

        UpdateDebugInfo();
        return bullet;
    }

    public void ReturnBullet(GameObject bullet)
    {
        if (bullet == null) return;

        // Only return if it's one of our active bullets
        if (activeBullets.Contains(bullet))
        {
            activeBullets.Remove(bullet);
            bullet.SetActive(false);

            // Reset bullet position and rotation
            bullet.transform.position = Vector3.zero;
            bullet.transform.rotation = Quaternion.identity;

            // Reset bullet velocity if it has a Rigidbody2D
            Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }

            bulletPool.Enqueue(bullet);
            UpdateDebugInfo();
        }
    }

    void UpdateDebugInfo()
    {
        activeCount = activeBullets.Count;
        pooledCount = bulletPool.Count;
    }

    public void ClearPool()
    {
        // Return all active bullets to pool
        var bulletsToReturn = new List<GameObject>(activeBullets);
        foreach (var bullet in bulletsToReturn)
        {
            ReturnBullet(bullet);
        }

        // Destroy all pooled bullets
        while (bulletPool.Count > 0)
        {
            GameObject bullet = bulletPool.Dequeue();
            if (bullet != null)
                Destroy(bullet);
        }

        activeBullets.Clear();
        UpdateDebugInfo();
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            ClearPool();
            Instance = null;
        }
    }
}

/// <summary>
/// Helper component that gets added to pooled bullets
/// Automatically returns bullet to pool when it should be "destroyed"
/// </summary>
public class BulletPooled : MonoBehaviour
{
    void OnDisable()
    {
        // When bullet gets disabled, make sure it's returned to pool
        if (BulletPool.Instance != null)
        {
            BulletPool.Instance.ReturnBullet(gameObject);
        }
    }

    // Call this instead of Destroy() on pooled bullets
    public void ReturnToPool()
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
}