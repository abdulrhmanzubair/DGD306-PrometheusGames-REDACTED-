using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Add this component to your character spawner or call these methods from your spawner
/// to notify the camera when players are ready
/// </summary>
public class CameraSpawnerBridge : MonoBehaviour
{
    [Header("Camera Integration")]
    public CoopCameraController coopCamera;
    public bool autoFindCamera = true;

    private void Start()
    {
        if (autoFindCamera && coopCamera == null)
        {
            coopCamera = FindFirstObjectByType<CoopCameraController>();
            if (coopCamera != null)
            {
                Debug.Log("CameraSpawnerBridge: Auto-found CoopCameraController");
            }
            else
            {
                Debug.LogWarning("CameraSpawnerBridge: Could not find CoopCameraController!");
            }
        }
    }

    /// <summary>
    /// Call this method from your character spawner when all players are spawned
    /// </summary>
    /// <param name="spawnedPlayers">List of all spawned player transforms</param>
    public void NotifyPlayersSpawned(List<Transform> spawnedPlayers)
    {
        if (coopCamera != null)
        {
            Debug.Log($"CameraSpawnerBridge: Notifying camera of {spawnedPlayers.Count} spawned players");
            coopCamera.ManualInitialize(spawnedPlayers);
        }
        else
        {
            Debug.LogWarning("CameraSpawnerBridge: No CoopCameraController assigned!");
        }
    }

    /// <summary>
    /// Call this method each time a single player is spawned
    /// </summary>
    /// <param name="playerTransform">The spawned player's transform</param>
    /// <param name="playerIndex">The player's index (0, 1, etc.)</param>
    public void NotifyPlayerSpawned(Transform playerTransform, int playerIndex)
    {
        if (coopCamera != null)
        {
            Debug.Log($"CameraSpawnerBridge: Notifying camera of Player {playerIndex} spawn: {playerTransform.name}");
            coopCamera.AddPlayer(playerTransform);
        }
        else
        {
            Debug.LogWarning("CameraSpawnerBridge: No CoopCameraController assigned!");
        }
    }

    /// <summary>
    /// Static method that can be called from anywhere without needing a reference
    /// </summary>
    public static void NotifyPlayerSpawnedStatic(Transform playerTransform, int playerIndex)
    {
        CoopCameraController camera = FindFirstObjectByType<CoopCameraController>();
        if (camera != null)
        {
            Debug.Log($"CameraSpawnerBridge (Static): Notifying camera of Player {playerIndex} spawn: {playerTransform.name}");
            camera.AddPlayer(playerTransform);
        }
        else
        {
            Debug.LogWarning("CameraSpawnerBridge (Static): Could not find CoopCameraController!");
        }
    }

    /// <summary>
    /// Static method to notify all players spawned at once
    /// </summary>
    public static void NotifyAllPlayersSpawnedStatic(List<Transform> spawnedPlayers)
    {
        CoopCameraController camera = FindFirstObjectByType<CoopCameraController>();
        if (camera != null)
        {
            Debug.Log($"CameraSpawnerBridge (Static): Notifying camera of {spawnedPlayers.Count} spawned players");
            camera.ManualInitialize(spawnedPlayers);
        }
        else
        {
            Debug.LogWarning("CameraSpawnerBridge (Static): Could not find CoopCameraController!");
        }
    }
}