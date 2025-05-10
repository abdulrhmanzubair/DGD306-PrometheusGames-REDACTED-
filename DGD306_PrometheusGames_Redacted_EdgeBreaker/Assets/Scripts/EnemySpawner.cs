using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    public GameObject enemyPrefab;
    public Transform[] spawnPoints;
    public float spawnInterval = 3f;
    public bool startOnAwake = true;

    [Header("Player Reference")]
    public Transform playerTarget; // Assign your player here

    private float spawnTimer;

    void Start()
    {
        spawnTimer = spawnInterval;

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
        if (spawnPoints.Length == 0 || enemyPrefab == null || playerTarget == null) return;

        Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
        GameObject enemy = Instantiate(enemyPrefab, spawnPoint.position, Quaternion.identity);

        // Try to assign the player to the enemy
        EnemyAI ai = enemy.GetComponent<EnemyAI>();
        if (ai != null)
        {
            ai.player = playerTarget;
        }
    }
}
