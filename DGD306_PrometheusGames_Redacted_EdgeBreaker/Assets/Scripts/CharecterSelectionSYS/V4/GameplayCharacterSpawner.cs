using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class GameplayCharacterSpawner : MonoBehaviour
{
    [Header("Character Prefabs")]
    [SerializeField] private GameObject gunnerPrefab;
    [SerializeField] private GameObject meleePrefab;

    [Header("Spawn Points")]
    [SerializeField] private Transform[] playerSpawnPoints;

    [Header("Debug Settings")]
    [SerializeField] private bool enableDebugLogging = true;

    private Dictionary<int, GameObject> spawnedPlayers = new Dictionary<int, GameObject>();
    private bool hasSpawned = false;

    private void Start()
    {
        DebugLog("=== GAMEPLAY CHARACTER SPAWNER STARTING ===");

        // Validate prefabs
        if (gunnerPrefab == null)
            Debug.LogError("Gunner prefab is not assigned!");
        else
            DebugLog($"Gunner prefab: {gunnerPrefab.name}");

        if (meleePrefab == null)
            Debug.LogError("Melee prefab is not assigned!");
        else
            DebugLog($"Melee prefab: {meleePrefab.name}");

        // Validate spawn points
        if (playerSpawnPoints == null || playerSpawnPoints.Length == 0)
        {
            Debug.LogWarning("No spawn points assigned! Will use default positions.");
        }
        else
        {
            DebugLog($"Spawn points available: {playerSpawnPoints.Length}");
            for (int i = 0; i < playerSpawnPoints.Length; i++)
            {
                if (playerSpawnPoints[i] != null)
                    DebugLog($"  Spawn Point {i}: {playerSpawnPoints[i].position}");
                else
                    Debug.LogWarning($"  Spawn Point {i}: NULL!");
            }
        }

        // Wait a frame to ensure everything is ready
        Invoke(nameof(SpawnSelectedCharacters), 0.1f);
    }

    private void SpawnSelectedCharacters()
    {
        if (hasSpawned)
        {
            DebugLog("Already spawned characters, skipping...");
            return;
        }
        hasSpawned = true;

        DebugLog("=== SPAWNING SELECTED CHARACTERS ===");

        // Get the selected characters from EITHER character selection system
        var selectedCharacters = new Dictionary<int, CharacterType>();

        // Debug both systems explicitly
        DebugLog($"SimplifiedCharacterSelection.SelectedCharacters count: {(SimplifiedCharacterSelection.SelectedCharacters?.Count ?? -1)}");
        if (SimplifiedCharacterSelection.SelectedCharacters != null)
        {
            foreach (var kvp in SimplifiedCharacterSelection.SelectedCharacters)
            {
                DebugLog($"  SimplifiedSystem - Player {kvp.Key}: {kvp.Value}");
            }
        }
        else
        {
            DebugLog("  SimplifiedCharacterSelection.SelectedCharacters is NULL!");
        }

        DebugLog($"CharacterSelectionManager.SelectedCharacters count: {(CharacterSelectionManager.SelectedCharacters?.Count ?? -1)}");
        if (CharacterSelectionManager.SelectedCharacters != null)
        {
            foreach (var kvp in CharacterSelectionManager.SelectedCharacters)
            {
                DebugLog($"  OriginalSystem - Player {kvp.Key}: {kvp.Value}");
            }
        }
        else
        {
            DebugLog("  CharacterSelectionManager.SelectedCharacters is NULL!");
        }

        // Try the new simplified system first
        if (SimplifiedCharacterSelection.SelectedCharacters != null && SimplifiedCharacterSelection.SelectedCharacters.Count > 0)
        {
            selectedCharacters = SimplifiedCharacterSelection.SelectedCharacters;
            DebugLog("Using SimplifiedCharacterSelection data");
        }
        // Fall back to original system
        else if (CharacterSelectionManager.SelectedCharacters != null && CharacterSelectionManager.SelectedCharacters.Count > 0)
        {
            selectedCharacters = CharacterSelectionManager.SelectedCharacters;
            DebugLog("Using CharacterSelectionManager data");
        }
        else
        {
            DebugLog("NO CHARACTER DATA FOUND IN STATIC! Trying PlayerPrefs backup...");

            // Try loading from PlayerPrefs backup
            int playerCount = PlayerPrefs.GetInt("PlayerCount", -1);
            DebugLog($"PlayerPrefs backup - PlayerCount: {playerCount}");

            if (playerCount <= 0)
            {
                DebugLog("No PlayerPrefs backup data found either!");
            }
            else
            {
                for (int i = 0; i < playerCount; i++)
                {
                    string characterTypeString = PlayerPrefs.GetString($"Player{i}Character", "NOTFOUND");
                    DebugLog($"PlayerPrefs - Player{i}Character: '{characterTypeString}'");

                    if (!string.IsNullOrEmpty(characterTypeString) && characterTypeString != "NOTFOUND")
                    {
                        if (System.Enum.TryParse<CharacterType>(characterTypeString, out CharacterType characterType))
                        {
                            selectedCharacters[i] = characterType;
                            DebugLog($"  SUCCESS: Restored from backup - Player {i}: {characterType}");
                        }
                        else
                        {
                            DebugLog($"  FAILED: Could not parse '{characterTypeString}' as CharacterType");
                        }
                    }
                    else
                    {
                        DebugLog($"  FAILED: Empty or not found for Player{i}Character");
                    }
                }

                if (selectedCharacters.Count > 0)
                {
                    DebugLog($"SUCCESS: Using PlayerPrefs backup data with {selectedCharacters.Count} characters");
                }
                else
                {
                    DebugLog("FAILED: No valid characters found in PlayerPrefs backup");
                }
            }
        }

        DebugLog($"Final selected characters count: {selectedCharacters.Count}");

        if (selectedCharacters.Count == 0)
        {
            Debug.LogWarning("No characters were selected! Creating test player.");
            // For testing without character selection, spawn a default gunner
            SpawnPlayerCharacter(0, CharacterType.Gunner, null);
            return;
        }

        // Debug selected characters
        foreach (var kvp in selectedCharacters)
        {
            DebugLog($"  FINAL - Player {kvp.Key}: {kvp.Value}");
        }

        // Find existing PlayerInput components (they persist from character selection)
        PlayerInput[] existingPlayers = FindObjectsOfType<PlayerInput>();
        DebugLog($"Found {existingPlayers.Length} existing PlayerInput components:");

        for (int i = 0; i < existingPlayers.Length; i++)
        {
            DebugLog($"  PlayerInput {i}: Index={existingPlayers[i].playerIndex}, Device={existingPlayers[i].devices}, ActionMap={existingPlayers[i].currentActionMap?.name}");
        }

        foreach (var kvp in selectedCharacters)
        {
            int playerIndex = kvp.Key;
            CharacterType characterType = kvp.Value;

            DebugLog($"Processing Player {playerIndex + 1} ({characterType})...");

            // Find the corresponding PlayerInput component
            PlayerInput targetPlayerInput = null;
            foreach (var playerInput in existingPlayers)
            {
                if (playerInput.playerIndex == playerIndex)
                {
                    targetPlayerInput = playerInput;
                    DebugLog($"  Found matching PlayerInput for Player {playerIndex + 1}");
                    break;
                }
            }

            if (targetPlayerInput == null)
            {
                DebugLog($"  No PlayerInput found for Player {playerIndex + 1}! Creating without input...");
            }

            SpawnPlayerCharacter(playerIndex, characterType, targetPlayerInput);
        }

        DebugLog($"=== SPAWNING COMPLETE - {spawnedPlayers.Count} players spawned ===");
    }

    private void SpawnPlayerCharacter(int playerIndex, CharacterType characterType, PlayerInput existingPlayerInput = null)
    {
        DebugLog($"=== SPAWNING PLAYER {playerIndex + 1} ({characterType}) ===");

        // Choose the appropriate prefab
        GameObject prefabToSpawn = characterType == CharacterType.Gunner ? gunnerPrefab : meleePrefab;

        if (prefabToSpawn == null)
        {
            Debug.LogError($"No prefab assigned for character type: {characterType}");
            return;
        }

        DebugLog($"  Using prefab: {prefabToSpawn.name}");

        // Get spawn position
        Vector3 spawnPosition = Vector3.zero;
        if (playerSpawnPoints != null && playerIndex < playerSpawnPoints.Length && playerSpawnPoints[playerIndex] != null)
        {
            spawnPosition = playerSpawnPoints[playerIndex].position;
            DebugLog($"  Spawn position from spawn point: {spawnPosition}");
        }
        else
        {
            // Default spawn positions if no spawn points are set
            spawnPosition = new Vector3(playerIndex * 2f - 1f, 0, 0);
            DebugLog($"  Using default spawn position: {spawnPosition}");
        }

        // Spawn the player character
        GameObject spawnedPlayer = Instantiate(prefabToSpawn, spawnPosition, Quaternion.identity);
        spawnedPlayer.name = $"Player_{playerIndex + 1}_{characterType}";

        DebugLog($"  Character spawned: {spawnedPlayer.name}");

        // Handle PlayerInput component
        if (existingPlayerInput != null)
        {
            DebugLog($"  Moving existing PlayerInput to spawned character...");
            DebugLog($"    Original parent: {existingPlayerInput.transform.parent?.name ?? "None"}");

            existingPlayerInput.transform.SetParent(spawnedPlayer.transform);
            existingPlayerInput.transform.localPosition = Vector3.zero;

            DebugLog($"    New parent: {existingPlayerInput.transform.parent.name}");
        }
        else
        {
            DebugLog($"  No existing PlayerInput - checking for component on prefab...");

            // Create new PlayerInput if none exists (fallback for testing)
            PlayerInput newPlayerInput = spawnedPlayer.GetComponent<PlayerInput>();
            if (newPlayerInput == null)
            {
                DebugLog($"    Adding new PlayerInput component...");
                newPlayerInput = spawnedPlayer.AddComponent<PlayerInput>();
                // You may need to assign the input actions asset here for testing
            }
            else
            {
                DebugLog($"    PlayerInput component found on prefab");
            }
        }

        // Ensure we're using the Player action map for gameplay
        PlayerInput playerInputComponent = spawnedPlayer.GetComponentInChildren<PlayerInput>();
        if (playerInputComponent != null)
        {
            DebugLog($"  Current action map: {playerInputComponent.currentActionMap?.name}");

            try
            {
                playerInputComponent.SwitchCurrentActionMap("Player");
                DebugLog($"  Switched to Player action map successfully");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"  Failed to switch to Player action map: {e.Message}");
            }
        }
        else
        {
            Debug.LogWarning($"  No PlayerInput component found on spawned character!");
        }

        // Store reference
        spawnedPlayers[playerIndex] = spawnedPlayer;

        // Set up character-specific components
        SetupCharacterSpecificComponents(spawnedPlayer, characterType, playerIndex);

        DebugLog($"  Player {playerIndex + 1} setup complete!");
    }

    private void SetupCharacterSpecificComponents(GameObject player, CharacterType characterType, int playerIndex)
    {
        DebugLog($"Setting up character-specific components for Player {playerIndex + 1}...");

        // Get the appropriate controller component using your actual naming
        MonoBehaviour controller = null;

        if (characterType == CharacterType.Gunner)
        {
            controller = player.GetComponent<PlayerController>();
            DebugLog($"  Looking for PlayerController: {(controller != null ? "Found" : "NOT FOUND")}");
        }
        else if (characterType == CharacterType.Melee)
        {
            controller = player.GetComponent<Player_Melee_Controller1>();
            DebugLog($"  Looking for Player_Melee_Controller1: {(controller != null ? "Found" : "NOT FOUND")}");
        }

        if (controller != null)
        {
            // Set player index on the controller if it has that property
            var playerIndexProperty = controller.GetType().GetProperty("PlayerIndex");
            if (playerIndexProperty != null && playerIndexProperty.CanWrite)
            {
                playerIndexProperty.SetValue(controller, playerIndex);
                DebugLog($"  Set PlayerIndex property to {playerIndex}");
            }
            else
            {
                DebugLog($"  PlayerIndex property not found or not writable on {controller.GetType().Name}");
            }

            // Enable the controller
            controller.enabled = true;
            DebugLog($"  Controller enabled");
        }
        else
        {
            Debug.LogWarning($"  No controller component found for {characterType}!");
        }

        // Set up any UI elements that need to know about the player
        SetupPlayerUI(player, playerIndex, characterType);
    }

    private void SetupPlayerUI(GameObject player, int playerIndex, CharacterType characterType)
    {
        DebugLog($"Setting up player UI for Player {playerIndex + 1}...");

        // Find and configure player-specific UI elements
        var renderers = player.GetComponentsInChildren<SpriteRenderer>();
        DebugLog($"  Found {renderers.Length} SpriteRenderer components");

        Color playerColor = GetPlayerColor(playerIndex);
        DebugLog($"  Player color: {playerColor}");

        int coloredRenderers = 0;
        foreach (var renderer in renderers)
        {
            if (renderer.gameObject.CompareTag("PlayerColorable"))
            {
                renderer.color = playerColor;
                coloredRenderers++;
            }
        }

        DebugLog($"  Colored {coloredRenderers} renderers with player color");

        // You can add more player-specific UI setup here
        // Example: Set up health bars, score displays, etc.
    }

    private Color GetPlayerColor(int playerIndex)
    {
        Color[] playerColors = {
            Color.blue,    // Player 1
            Color.red,     // Player 2
            Color.green,   // Player 3 (if you expand later)
            Color.yellow   // Player 4 (if you expand later)
        };

        return playerIndex < playerColors.Length ? playerColors[playerIndex] : Color.white;
    }

    public GameObject GetPlayer(int playerIndex)
    {
        return spawnedPlayers.ContainsKey(playerIndex) ? spawnedPlayers[playerIndex] : null;
    }

    public int GetActivePlayerCount()
    {
        return spawnedPlayers.Count;
    }

    public CharacterType GetPlayerCharacterType(int playerIndex)
    {
        if (SimplifiedCharacterSelection.SelectedCharacters != null && SimplifiedCharacterSelection.SelectedCharacters.ContainsKey(playerIndex))
        {
            return SimplifiedCharacterSelection.SelectedCharacters[playerIndex];
        }
        if (CharacterSelectionManager.SelectedCharacters.ContainsKey(playerIndex))
        {
            return CharacterSelectionManager.SelectedCharacters[playerIndex];
        }
        return CharacterType.Gunner; // Default fallback
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogging)
        {
            Debug.Log($"[GameplaySpawner] {message}");
        }
    }
}