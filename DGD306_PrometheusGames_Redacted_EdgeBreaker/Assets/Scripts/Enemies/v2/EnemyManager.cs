using UnityEngine;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// Manages all enemies in the scene with camera-based culling and distance optimization
/// Attach this to an empty GameObject in your scene
/// </summary>
public class EnemyManager : MonoBehaviour
{
    [Header("Camera Culling")]
    public Camera playerCamera;
    public float cullBuffer = 5f; // Extra distance beyond camera view
    public LayerMask enemyLayer;

    [Header("Distance-Based Optimization")]
    public float closeDistance = 15f;   // Full AI
    public float mediumDistance = 30f;  // Reduced AI
    public float farDistance = 50f;     // Minimal AI
    public float cullDistance = 80f;    // Completely disable

    [Header("Performance Settings")]
    public int maxActiveEnemies = 20;
    public int enemiesPerFrameCheck = 3; // How many to check per frame

    private List<OptimizedEnemyAI> allEnemies = new List<OptimizedEnemyAI>();
    private int currentCheckIndex = 0;
    private Transform player;

    void Start()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;

        // Don't find player immediately - wait for it to spawn
        StartCoroutine(WaitForPlayer());
    }

    IEnumerator WaitForPlayer()
    {
        // Wait until player spawns
        while (player == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
                break;
            }
            yield return new WaitForSeconds(0.1f); // Check every 0.1 seconds
        }

        // Now start the enemy management system
        StartCoroutine(ManageEnemiesCoroutine());
    }

    public void RegisterEnemy(OptimizedEnemyAI enemy)
    {
        if (!allEnemies.Contains(enemy))
            allEnemies.Add(enemy);
    }

    public void UnregisterEnemy(OptimizedEnemyAI enemy)
    {
        allEnemies.Remove(enemy);
    }

    IEnumerator ManageEnemiesCoroutine()
    {
        while (true)
        {
            // Check a subset of enemies each frame for performance
            for (int i = 0; i < enemiesPerFrameCheck && allEnemies.Count > 0; i++)
            {
                if (currentCheckIndex >= allEnemies.Count)
                    currentCheckIndex = 0;

                if (currentCheckIndex < allEnemies.Count && allEnemies[currentCheckIndex] != null)
                {
                    OptimizeEnemy(allEnemies[currentCheckIndex]);
                }

                currentCheckIndex++;
            }
            yield return null; // Wait one frame
        }
    }

    void OptimizeEnemy(OptimizedEnemyAI enemy)
    {
        if (enemy == null || player == null) return;

        float distance = Vector3.Distance(enemy.transform.position, player.position);
        bool isVisible = IsEnemyVisible(enemy.transform.position);

        // Set optimization level based on distance and visibility
        if (!isVisible && distance > cullDistance)
        {
            enemy.SetOptimizationLevel(OptimizationLevel.Disabled);
        }
        else if (!isVisible && distance > farDistance)
        {
            enemy.SetOptimizationLevel(OptimizationLevel.Minimal);
        }
        else if (distance > mediumDistance)
        {
            enemy.SetOptimizationLevel(OptimizationLevel.Reduced);
        }
        else
        {
            enemy.SetOptimizationLevel(OptimizationLevel.Full);
        }
    }

    bool IsEnemyVisible(Vector3 enemyPosition)
    {
        // Check if enemy is within camera frustum with buffer
        Vector3 viewportPoint = playerCamera.WorldToViewportPoint(enemyPosition);
        float buffer = cullBuffer / Vector3.Distance(playerCamera.transform.position, enemyPosition);

        return viewportPoint.x >= -buffer && viewportPoint.x <= 1 + buffer &&
               viewportPoint.y >= -buffer && viewportPoint.y <= 1 + buffer &&
               viewportPoint.z > 0;
    }

    void OnDrawGizmosSelected()
    {
        if (player == null) return;

        // Draw optimization distance rings
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(player.position, closeDistance);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(player.position, mediumDistance);

       
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(player.position, cullDistance);
    }
}