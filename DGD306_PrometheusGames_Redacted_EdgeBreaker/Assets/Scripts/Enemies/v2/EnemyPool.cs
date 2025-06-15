using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Object pool for enemies to improve performance by reusing enemy objects
/// Attach this to an empty GameObject in your scene
/// </summary>
public class EnemyPool : MonoBehaviour
{
    public static EnemyPool Instance;

    [Header("Pool Settings")]
    public GameObject[] enemyPrefabs; // Support multiple enemy types
    public int initialPoolSizePerType = 10;
    public int maxPoolSizePerType = 20;
    public bool allowGrowth = true;

    [Header("Debug Info")]
    [SerializeField] private int totalActiveEnemies = 0;
    [SerializeField] private int totalPooledEnemies = 0;

    // Separate pools for each enemy type
    private Dictionary<GameObject, Queue<GameObject>> enemyPools = new Dictionary<GameObject, Queue<GameObject>>();
    private Dictionary<GameObject, HashSet<GameObject>> activeEnemies = new Dictionary<GameObject, HashSet<GameObject>>();

    void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializePools();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void InitializePools()
    {
        foreach (GameObject enemyPrefab in enemyPrefabs)
        {
            if (enemyPrefab == null) continue;

            // Create pool and active tracking for this enemy type
            enemyPools[enemyPrefab] = new Queue<GameObject>();
            activeEnemies[enemyPrefab] = new HashSet<GameObject>();

            // Pre-instantiate enemies of this type
            for (int i = 0; i < initialPoolSizePerType; i++)
            {
                CreateNewEnemy(enemyPrefab);
            }
        }

        UpdateDebugInfo();
    }

    GameObject CreateNewEnemy(GameObject prefab)
    {
        GameObject enemy = Instantiate(prefab);
        enemy.SetActive(false);

        // Ensure enemy has the optimized AI component
        OptimizedEnemyAI ai = enemy.GetComponent<OptimizedEnemyAI>();
        if (ai == null)
        {
            Debug.LogWarning($"Enemy prefab {prefab.name} doesn't have OptimizedEnemyAI component!");
        }

        enemyPools[prefab].Enqueue(enemy);
        return enemy;
    }

    public GameObject GetEnemy(GameObject prefabType = null)
    {
        // If no specific type requested, use the first available type
        if (prefabType == null && enemyPrefabs.Length > 0)
        {
            prefabType = enemyPrefabs[0];
        }

        if (prefabType == null || !enemyPools.ContainsKey(prefabType))
        {
            Debug.LogError("Invalid enemy prefab type requested from pool!");
            return null;
        }

        GameObject enemy = null;
        Queue<GameObject> pool = enemyPools[prefabType];
        HashSet<GameObject> active = activeEnemies[prefabType];

        // Try to get from pool
        if (pool.Count > 0)
        {
            enemy = pool.Dequeue();
        }
        // Create new if pool is empty and growth is allowed
        else if (allowGrowth && active.Count < maxPoolSizePerType)
        {
            enemy = CreateNewEnemy(prefabType);
            pool.Dequeue(); // Remove it from pool since we're using it
        }
        // Return null if we can't create more
        else
        {
            Debug.LogWarning($"Enemy pool for {prefabType.name} exhausted! Consider increasing pool size.");
            return null;
        }

        // Activate and reset the enemy
        enemy.SetActive(true);
        active.Add(enemy);

        // Reset enemy state
        OptimizedEnemyAI ai = enemy.GetComponent<OptimizedEnemyAI>();
        if (ai != null)
        {
            ai.ResetEnemy();
        }

        UpdateDebugInfo();
        return enemy;
    }

    public GameObject GetRandomEnemy()
    {
        if (enemyPrefabs.Length == 0) return null;

        GameObject randomPrefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
        return GetEnemy(randomPrefab);
    }

    public void ReturnEnemy(GameObject enemy)
    {
        if (enemy == null) return;

        // Find which pool this enemy belongs to
        GameObject prefabType = null;
        foreach (var kvp in activeEnemies)
        {
            if (kvp.Value.Contains(enemy))
            {
                prefabType = kvp.Key;
                break;
            }
        }

        if (prefabType == null)
        {
            // Enemy not from our pool, just destroy it
            Destroy(enemy);
            return;
        }

        // Remove from active tracking
        activeEnemies[prefabType].Remove(enemy);

        // Reset enemy state
        enemy.SetActive(false);
        enemy.transform.position = Vector3.zero;
        enemy.transform.rotation = Quaternion.identity;

        // Reset Rigidbody2D if present
        Rigidbody2D rb = enemy.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        // Return to pool
        enemyPools[prefabType].Enqueue(enemy);

        UpdateDebugInfo();
    }

    public void ReturnAllEnemies()
    {
        // Return all active enemies to their respective pools
        foreach (var kvp in activeEnemies)
        {
            var enemiesToReturn = new List<GameObject>(kvp.Value);
            foreach (var enemy in enemiesToReturn)
            {
                ReturnEnemy(enemy);
            }
        }
    }

    void UpdateDebugInfo()
    {
        totalActiveEnemies = 0;
        totalPooledEnemies = 0;

        foreach (var active in activeEnemies.Values)
        {
            totalActiveEnemies += active.Count;
        }

        foreach (var pool in enemyPools.Values)
        {
            totalPooledEnemies += pool.Count;
        }
    }

    public void ClearAllPools()
    {
        // Return all active enemies
        ReturnAllEnemies();

        // Destroy all pooled enemies
        foreach (var pool in enemyPools.Values)
        {
            while (pool.Count > 0)
            {
                GameObject enemy = pool.Dequeue();
                if (enemy != null)
                    Destroy(enemy);
            }
        }

        // Clear all collections
        foreach (var active in activeEnemies.Values)
        {
            active.Clear();
        }

        UpdateDebugInfo();
    }

    // Get statistics for debugging
    public int GetActiveCount(GameObject prefabType = null)
    {
        if (prefabType == null) return totalActiveEnemies;

        return activeEnemies.ContainsKey(prefabType) ? activeEnemies[prefabType].Count : 0;
    }

    public int GetPooledCount(GameObject prefabType = null)
    {
        if (prefabType == null) return totalPooledEnemies;

        return enemyPools.ContainsKey(prefabType) ? enemyPools[prefabType].Count : 0;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            ClearAllPools();
            Instance = null;
        }
    }
}