using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerSpawner : MonoBehaviour
{
    [Header("Character Prefabs")]
    [SerializeField] private GameObject meleePrefab;
    [SerializeField] private GameObject gunnerPrefab;

    [Header("Spawn Points")]
    [SerializeField] private Transform[] spawnPoints = new Transform[2];

    private bool playersSpawned = false;

    void Start()
    {
        // Auto-spawn if we're in a game level
        if (SceneManager.GetActiveScene().name.StartsWith("Level"))
        {
            SpawnPlayers();
        }
    }

    // Public method called by GameSceneManager
    public void SpawnPlayers()
    {
        if (playersSpawned) return;

        if (CharacterSelectionManager.Instance == null)
        {
            Debug.LogWarning("No character selections found! Spawning default characters.");
            SpawnFallbackPlayers();
            playersSpawned = true;
            return;
        }

        for (int i = 0; i < 2; i++)
        {
            SpawnPlayer(i);
        }

        playersSpawned = true;
        Debug.Log("Players spawned successfully");
    }

    private void SpawnPlayer(int playerIndex)
    {
        if (playerIndex >= spawnPoints.Length || spawnPoints[playerIndex] == null)
        {
            Debug.LogError($"Missing spawn point for player {playerIndex}");
            return;
        }

        var selection = CharacterSelectionManager.Instance.PlayerSelections[playerIndex];
        GameObject prefab = selection == CharacterSelectionManager.CharacterType.Melee
            ? meleePrefab : gunnerPrefab;

        if (prefab == null)
        {
            Debug.LogError($"Missing prefab for {selection} character");
            return;
        }

        Instantiate(prefab, spawnPoints[playerIndex].position, Quaternion.identity)
            .GetComponent<PlayerController>().Initialize(playerIndex);
    }

    private void SpawnFallbackPlayers()
    {
        if (meleePrefab && spawnPoints.Length > 0 && spawnPoints[0])
            Instantiate(meleePrefab, spawnPoints[0].position, Quaternion.identity);

        if (gunnerPrefab && spawnPoints.Length > 1 && spawnPoints[1])
            Instantiate(gunnerPrefab, spawnPoints[1].position, Quaternion.identity);
    }
}