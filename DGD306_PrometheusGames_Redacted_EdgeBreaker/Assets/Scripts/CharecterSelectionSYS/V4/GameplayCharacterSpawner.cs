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
    private Dictionary<int, InputDevice> playerDevices = new Dictionary<int, InputDevice>();
    private bool hasSpawned = false;

    private void Start()
    {
        DebugLog("=== DIRECT INPUT GAMEPLAY SPAWNER STARTING ===");

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
        }

        // Assign devices to players - DIRECT DEVICE ASSIGNMENT
        AssignDevicesToPlayers();

        // Wait a frame to ensure everything is ready
        Invoke(nameof(SpawnSelectedCharacters), 0.1f);
    }

    private void AssignDevicesToPlayers()
    {
        DebugLog("=== ASSIGNING DEVICES TO PLAYERS (DIRECT INPUT) ===");
        DebugLog($"Total gamepads: {Gamepad.all.Count}");

        for (int i = 0; i < Gamepad.all.Count; i++)
        {
            DebugLog($"  Gamepad {i}: {Gamepad.all[i].name}");
        }

        // DIRECT DEVICE ASSIGNMENT - No PlayerInputManager interference
        // Player 0 gets first gamepad
        if (Gamepad.all.Count > 0)
        {
            playerDevices[0] = Gamepad.all[0];
            DebugLog($"DIRECT: Assigned Gamepad 1 ({Gamepad.all[0].name}) to Player 1");
        }

        // Player 1 gets second gamepad  
        if (Gamepad.all.Count > 1)
        {
            playerDevices[1] = Gamepad.all[1];
            DebugLog($"DIRECT: Assigned Gamepad 2 ({Gamepad.all[1].name}) to Player 2");
        }

        // If not enough gamepads, use keyboard for remaining players
        if (Keyboard.current != null)
        {
            if (!playerDevices.ContainsKey(0))
            {
                playerDevices[0] = Keyboard.current;
                DebugLog($"DIRECT: Assigned Keyboard to Player 1");
            }
            else if (!playerDevices.ContainsKey(1))
            {
                playerDevices[1] = Keyboard.current;
                DebugLog($"DIRECT: Assigned Keyboard to Player 2 (shared with P1)");
            }
        }
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

        // Get the selected characters
        var selectedCharacters = new Dictionary<int, CharacterType>();

        if (SimplifiedCharacterSelection.SelectedCharacters != null && SimplifiedCharacterSelection.SelectedCharacters.Count > 0)
        {
            selectedCharacters = SimplifiedCharacterSelection.SelectedCharacters;
            DebugLog($"Using SimplifiedCharacterSelection data with {selectedCharacters.Count} characters");
        }
        else
        {
            DebugLog("NO CHARACTER DATA FOUND! Trying PlayerPrefs backup...");

            int playerCount = PlayerPrefs.GetInt("PlayerCount", -1);
            DebugLog($"PlayerPrefs backup - PlayerCount: {playerCount}");

            if (playerCount > 0)
            {
                for (int i = 0; i < playerCount; i++)
                {
                    string characterTypeString = PlayerPrefs.GetString($"Player{i}Character", "");
                    if (!string.IsNullOrEmpty(characterTypeString))
                    {
                        if (System.Enum.TryParse<CharacterType>(characterTypeString, out CharacterType characterType))
                        {
                            selectedCharacters[i] = characterType;
                            DebugLog($"  Restored from backup - Player {i}: {characterType}");
                        }
                    }
                }
            }
        }

        if (selectedCharacters.Count == 0)
        {
            Debug.LogWarning("No characters were selected! Creating test players.");
            SpawnPlayerCharacter(0, CharacterType.Gunner);
            SpawnPlayerCharacter(1, CharacterType.Melee);
            return;
        }

        // Spawn each selected character
        foreach (var kvp in selectedCharacters)
        {
            int playerIndex = kvp.Key;
            CharacterType characterType = kvp.Value;

            DebugLog($"Processing Player {playerIndex + 1} ({characterType})...");
            SpawnPlayerCharacter(playerIndex, characterType);
        }

        DebugLog($"=== SPAWNING COMPLETE - {spawnedPlayers.Count} players spawned ===");
    }

    private void SpawnPlayerCharacter(int playerIndex, CharacterType characterType)
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
            spawnPosition = new Vector3(playerIndex * 3f - 1.5f, 0, 0);
            DebugLog($"  Using default spawn position: {spawnPosition}");
        }

        // Spawn the player character
        GameObject spawnedPlayer = Instantiate(prefabToSpawn, spawnPosition, Quaternion.identity);
        spawnedPlayer.name = $"Player_{playerIndex + 1}_{characterType}";

        DebugLog($"  Character spawned: {spawnedPlayer.name}");

        // REMOVE any existing PlayerInput components to avoid conflicts
        PlayerInput existingPlayerInput = spawnedPlayer.GetComponent<PlayerInput>();
        if (existingPlayerInput != null)
        {
            DebugLog($"  Removing existing PlayerInput component to avoid conflicts");
            DestroyImmediate(existingPlayerInput);
        }

        // Set up DIRECT device assignment ONLY
        SetupDirectDeviceInput(spawnedPlayer, playerIndex);

        // Store reference
        spawnedPlayers[playerIndex] = spawnedPlayer;

        DebugLog($"  Player {playerIndex + 1} setup complete!");
    }

    private void SetupDirectDeviceInput(GameObject player, int playerIndex)
    {
        DebugLog($"Setting up DIRECT device input for Player {playerIndex + 1}...");

        // Add our device info component
        var deviceInfo = player.GetComponent<PlayerDeviceInfo>();
        if (deviceInfo == null)
        {
            deviceInfo = player.AddComponent<PlayerDeviceInfo>();
        }

        // Set device info
        deviceInfo.PlayerIndex = playerIndex;
        if (playerDevices.ContainsKey(playerIndex))
        {
            deviceInfo.AssignedDevice = playerDevices[playerIndex];
            DebugLog($"  DIRECT: Assigned device {deviceInfo.AssignedDevice.name} to Player {playerIndex + 1}");
        }
        else
        {
            Debug.LogError($"  No device found for Player {playerIndex + 1}!");
        }

        // Initialize controllers - they will use DIRECT device input only
        var meleeController = player.GetComponent<Player_Melee_Controller1>();
        if (meleeController != null)
        {
            meleeController.Initialize(playerIndex);
            DebugLog($"  Initialized melee controller for DIRECT input - Player {playerIndex + 1}");
        }

        var gunnerController = player.GetComponent<PlayerController>();
        if (gunnerController != null)
        {
            gunnerController.Initialize(playerIndex);
            DebugLog($"  Initialized gunner controller for DIRECT input - Player {playerIndex + 1}");
        }

        DebugLog($"  DIRECT device input setup complete for Player {playerIndex + 1}");
    }

    public GameObject GetPlayer(int playerIndex)
    {
        return spawnedPlayers.ContainsKey(playerIndex) ? spawnedPlayers[playerIndex] : null;
    }

    public int GetActivePlayerCount()
    {
        return spawnedPlayers.Count;
    }

    public InputDevice GetPlayerDevice(int playerIndex)
    {
        return playerDevices.ContainsKey(playerIndex) ? playerDevices[playerIndex] : null;
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogging)
        {
            Debug.Log($"[DirectInputSpawner] {message}");
        }
    }
}

// Simplified device info component
public class PlayerDeviceInfo : MonoBehaviour
{
    public InputDevice AssignedDevice { get; set; }
    public int PlayerIndex { get; set; }
}