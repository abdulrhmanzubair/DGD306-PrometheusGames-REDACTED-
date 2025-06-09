using UnityEngine;
using System.Collections.Generic;

public class EnemySpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    public GameObject enemyPrefab;
    public Transform[] spawnPoints;
    public int maxEnemies = 10;                // Max total enemies alive at once
    public int maxEnemiesPerSpawnPoint = 3;   // Max enemies per spawn point
    public float spawnInterval = 3f;
    public bool startOnAwake = true;

    private float spawnTimer;
    private int totalEnemies = 0;

    // Track how many enemies per spawn point index
    private Dictionary<int, int> enemiesPerSpawnPoint = new Dictionary<int, int>();

    void Start()
    {
        spawnTimer = spawnInterval;

        // Initialize counts for each spawn point
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            enemiesPerSpawnPoint[i] = 0;
        }

        if (startOnAwake)
            InvokeRepeating(nameof(SpawnEnemy), 0f, spawnInterval);
    }

    void Update()
    {
        if (!startOnAwake)
        {
            spawnTimer -= Time.deltaTime;
            if (spawnTimer <= 0f)
            {
                SpawnEnemy();
                spawnTimer = spawnInterval;
            }
        }
    }

    void SpawnEnemy()
    {
        if (enemyPrefab == null || spawnPoints.Length == 0) return;

        if (totalEnemies >= maxEnemies)
            return; // Don't spawn if total limit reached

        // Get list of spawn points that are below their max limit
        List<int> availableSpawnIndices = new List<int>();

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            if (enemiesPerSpawnPoint[i] < maxEnemiesPerSpawnPoint)
            {
                availableSpawnIndices.Add(i);
            }
        }

        if (availableSpawnIndices.Count == 0)
        {
            // All spawn points are full, no spawn
            return;
        }

        // Choose a random spawn point from the available ones
        int spawnIndex = availableSpawnIndices[Random.Range(0, availableSpawnIndices.Count)];
        Transform spawnPoint = spawnPoints[spawnIndex];

        GameObject enemy = Instantiate(enemyPrefab, spawnPoint.position, Quaternion.identity);

        // Track counts
        totalEnemies++;
        enemiesPerSpawnPoint[spawnIndex]++;

        // Subscribe to enemy death to reduce counts when it dies
        EnemyAI enemyAI = enemy.GetComponent<EnemyAI>();
        if (enemyAI != null)
        {
            enemyAI.OnEnemyDeath += () => OnEnemyKilled(spawnIndex);
        }
    }

    // Called when an enemy dies
    void OnEnemyKilled(int spawnIndex)
    {
        totalEnemies = Mathf.Max(0, totalEnemies - 1);
        enemiesPerSpawnPoint[spawnIndex] = Mathf.Max(0, enemiesPerSpawnPoint[spawnIndex] - 1);
    }
}
