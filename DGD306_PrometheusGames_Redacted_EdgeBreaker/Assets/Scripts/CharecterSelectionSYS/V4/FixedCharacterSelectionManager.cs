using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

public class FixedCharacterSelectionManager : MonoBehaviour
{
    [Header("Character Data")]
    [SerializeField] private CharacterData[] availableCharacters;

    [Header("UI References")]
    [SerializeField] private Transform player1SelectionUI;
    [SerializeField] private Transform player2SelectionUI;
    [SerializeField] private GameObject readyPrompt;
    [SerializeField] private string gameplaySceneName = "LVL_001";

    [Header("Selection Visuals")]
    [SerializeField] private Color selectedColor = Color.yellow;
    [SerializeField] private Color unselectedColor = Color.white;
    [SerializeField] private Color readyColor = Color.green;

    [Header("Input Settings")]
    [SerializeField] private InputActionAsset playerInputActions;

    [Header("Debug Settings")]
    [SerializeField] private bool enableDebugLogging = true;

    // Player selection states
    private Dictionary<int, PlayerSelectionState> playerStates = new Dictionary<int, PlayerSelectionState>();
    private List<PlayerInput> connectedPlayers = new List<PlayerInput>();

    // Static data to pass to gameplay scene
    public static Dictionary<int, CharacterType> SelectedCharacters { get; private set; } = new Dictionary<int, CharacterType>();

    private class PlayerSelectionState
    {
        public int playerIndex;
        public int selectedCharacterIndex = 0;
        public bool isReady = false;
        public bool isActive = false;
        public Transform uiRoot;
        public UnityEngine.UI.Image[] characterPortraits;
        public UnityEngine.UI.Text playerLabel;
        public UnityEngine.UI.Text characterName;
        public UnityEngine.UI.Text characterDescription;
        public UnityEngine.UI.Text statusText;
        public PlayerInput playerInput;
        public string deviceName = "";
    }

    private void Awake()
    {
        DebugLog("=== FIXED CHARACTER SELECTION MANAGER AWAKENING ===");
        SetupPlayerInputManager();
    }

    private void SetupPlayerInputManager()
    {
        PlayerInputManager inputManager = PlayerInputManager.instance;

        if (inputManager == null)
        {
            Debug.LogError("PlayerInputManager not found! Please add PlayerInputManager to scene with these settings:");
            Debug.LogError("- Max Player Count: 4");
            Debug.LogError("- Player Prefab: None (empty)");
            Debug.LogError("- Joining Behavior: Join Players When Join Action Is Triggered");
            Debug.LogError("- Join Action: None (leave empty - we handle joining manually)");
            Debug.LogError("- Notification Behavior: Send Messages or Invoke Unity Events");
            return;
        }

        DebugLog($"PlayerInputManager found. Max players: {inputManager.maxPlayerCount}");

        // Assign our Input Actions asset if available
        if (playerInputActions != null && inputManager.playerPrefab == null)
        {
            // Create a temporary prefab for the PlayerInputManager to use
            GameObject tempPrefab = new GameObject("TempPlayerPrefab");
            PlayerInput tempPlayerInput = tempPrefab.AddComponent<PlayerInput>();
            tempPlayerInput.actions = playerInputActions;
            tempPlayerInput.defaultActionMap = "UI";

            // This is just for reference - we still handle joining manually
            DebugLog("Configured temporary prefab with our InputActions");
        }

        // Disable automatic joining initially - we'll enable it only when needed
        inputManager.DisableJoining();

        // Set up callbacks
        inputManager.onPlayerJoined += OnPlayerJoined;
        inputManager.onPlayerLeft += OnPlayerLeft;

        DebugLog("PlayerInputManager configured for manual device-specific joining");
        LogAvailableDevices();
    }

    private void LogAvailableDevices()
    {
        DebugLog("=== AVAILABLE INPUT DEVICES ===");

        int gamepadCount = 0;
        foreach (var gamepad in Gamepad.all)
        {
            DebugLog($"  Gamepad {gamepadCount}: {gamepad.name}");
            gamepadCount++;
        }

        if (Keyboard.current != null)
        {
            DebugLog($"  Keyboard: {Keyboard.current.name}");
        }

        DebugLog($"Total: {gamepadCount} gamepads, {(Keyboard.current != null ? 1 : 0)} keyboard");
    }

    private void Start()
    {
        DebugLog("=== CHARACTER SELECTION MANAGER STARTING ===");

        // Validate character data
        if (availableCharacters == null || availableCharacters.Length == 0)
        {
            Debug.LogError("No characters available!");
            return;
        }

        SetupUI();

        if (readyPrompt != null)
        {
            readyPrompt.SetActive(false);
        }

        // Show join instructions
        DebugLog("=== JOIN INSTRUCTIONS ===");
        DebugLog("Press any button on a gamepad to join with that gamepad");
        DebugLog("Press SPACE or ENTER on keyboard to join with keyboard");
    }

    private void Update()
    {
        HandleManualJoining();
    }

    private void HandleManualJoining()
    {
        // Check for gamepad inputs
        for (int i = 0; i < Gamepad.all.Count; i++)
        {
            var gamepad = Gamepad.all[i];

            // Check if this gamepad is already assigned to a player
            bool alreadyAssigned = false;
            foreach (var player in connectedPlayers)
            {
                if (player.devices.Any(device => device == gamepad))
                {
                    alreadyAssigned = true;
                    break;
                }
            }

            if (alreadyAssigned) continue;

            // Check if any button was pressed on this gamepad
            if (gamepad.buttonSouth.wasPressedThisFrame ||
                gamepad.buttonNorth.wasPressedThisFrame ||
                gamepad.buttonEast.wasPressedThisFrame ||
                gamepad.buttonWest.wasPressedThisFrame ||
                gamepad.startButton.wasPressedThisFrame ||
                gamepad.selectButton.wasPressedThisFrame)
            {
                DebugLog($"Gamepad {i} ({gamepad.name}) wants to join!");
                JoinPlayerWithDevice(gamepad);
            }
        }

        // Check for keyboard input
        if (Keyboard.current != null)
        {
            // Check if keyboard is already assigned
            bool keyboardAssigned = false;
            foreach (var player in connectedPlayers)
            {
                if (player.devices.Any(device => device == Keyboard.current))
                {
                    keyboardAssigned = true;
                    break;
                }
            }

            if (!keyboardAssigned &&
                (Keyboard.current.spaceKey.wasPressedThisFrame ||
                 Keyboard.current.enterKey.wasPressedThisFrame))
            {
                DebugLog("Keyboard wants to join!");
                JoinPlayerWithDevice(Keyboard.current);
            }
        }
    }

    private void JoinPlayerWithDevice(InputDevice device)
    {
        if (connectedPlayers.Count >= 4)
        {
            DebugLog("Maximum players reached!");
            return;
        }

        DebugLog($"Joining player with device: {device.name}");

        // Create a GameObject for the PlayerInput
        GameObject playerGO = new GameObject($"Player_{connectedPlayers.Count + 1}_Input");
        PlayerInput playerInput = playerGO.AddComponent<PlayerInput>();

        // Get the InputActionAsset from an existing source or assign it
        // You'll need to assign this in the inspector or get it from somewhere
        var inputActions = GetInputActionAsset();
        if (inputActions == null)
        {
            Debug.LogError("No Input Action Asset found! Please assign it to the PlayerInputManager or this script.");
            DestroyImmediate(playerGO);
            return;
        }

        // Set up the PlayerInput
        playerInput.actions = inputActions;

        // Try to pair the device - this is more complex in newer Input System versions
        try
        {
            // Enable the actions first
            playerInput.actions.Enable();

            // Use the PlayerInputManager to properly join the player
            // This is a simpler approach that works with the Input System
            DebugLog($"PlayerInput created, letting PlayerInputManager handle device assignment");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to set up PlayerInput: {e.Message}");
            DestroyImmediate(playerGO);
            return;
        }

        // Switch to UI action map
        try
        {
            playerInput.SwitchCurrentActionMap("UI");
            DebugLog($"Switched to UI action map for device {device.name}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to switch to UI action map: {e.Message}");
        }

        // The PlayerInputManager will automatically call OnPlayerJoined
        DebugLog($"PlayerInput created for device {device.name}");
    }

    private InputActionAsset GetInputActionAsset()
    {
        // Try to get it from PlayerInputManager first
        if (PlayerInputManager.instance != null && PlayerInputManager.instance.playerPrefab != null)
        {
            var prefabPlayerInput = PlayerInputManager.instance.playerPrefab.GetComponent<PlayerInput>();
            if (prefabPlayerInput != null && prefabPlayerInput.actions != null)
            {
                return prefabPlayerInput.actions;
            }
        }

        // Try to find any PlayerInput in the scene that has actions
        var existingPlayerInput = FindObjectOfType<PlayerInput>();
        if (existingPlayerInput != null && existingPlayerInput.actions != null)
        {
            return existingPlayerInput.actions;
        }

        // Look for InputActionAsset in Resources
        var asset = Resources.Load<InputActionAsset>("PlayerInputActions");
        if (asset != null)
        {
            DebugLog("Found PlayerInputActions asset in Resources folder");
            return asset;
        }

        // Try to find it by name in the project (this searches all loaded assets)
        var allAssets = Resources.FindObjectsOfTypeAll<InputActionAsset>();
        foreach (var inputAsset in allAssets)
        {
            if (inputAsset.name == "PlayerInputActions")
            {
                DebugLog($"Found PlayerInputActions asset: {inputAsset.name}");
                return inputAsset;
            }
        }

        DebugLog("Searched asset names:");

        DebugLog("Searched asset names:");
        foreach (var inputAsset in allAssets)
        {
            DebugLog($"  - {inputAsset.name}");
        }

        Debug.LogError("Could not find 'PlayerInputActions' InputActionAsset! Make sure:");
        Debug.LogError("1. Your Input Action Asset is named 'PlayerInputActions'");
        Debug.LogError("2. It's either in a Resources folder, or");
        Debug.LogError("3. It's assigned to a PlayerInput component in the scene, or");
        Debug.LogError("4. It's assigned to the PlayerInputManager's Player Prefab");
        return null;
    }

    private void SetupUI()
    {
        if (player1SelectionUI != null)
        {
            SetupPlayerUI(player1SelectionUI, 0);
        }

        if (player2SelectionUI != null)
        {
            SetupPlayerUI(player2SelectionUI, 1);
        }
    }

    private void SetupPlayerUI(Transform uiRoot, int playerIndex)
    {
        if (uiRoot == null) return;

        var state = new PlayerSelectionState
        {
            playerIndex = playerIndex,
            uiRoot = uiRoot,
            characterPortraits = uiRoot.GetComponentsInChildren<UnityEngine.UI.Image>(),
            playerLabel = uiRoot.Find("PlayerLabel")?.GetComponent<UnityEngine.UI.Text>(),
            characterName = uiRoot.Find("CharacterName")?.GetComponent<UnityEngine.UI.Text>(),
            characterDescription = uiRoot.Find("CharacterDescription")?.GetComponent<UnityEngine.UI.Text>(),
            statusText = uiRoot.Find("StatusText")?.GetComponent<UnityEngine.UI.Text>()
        };

        playerStates[playerIndex] = state;
        UpdatePlayerUI(state);
        uiRoot.gameObject.SetActive(false);
    }

    private void OnPlayerJoined(PlayerInput playerInput)
    {
        int playerIndex = playerInput.playerIndex;
        DebugLog($"=== PLAYER {playerIndex + 1} JOINED ===");

        // Get device info with more detail
        string deviceInfo = "";
        DebugLog($"  Player {playerIndex + 1} assigned devices:");
        foreach (var device in playerInput.devices)
        {
            deviceInfo += $"{device.name} ";
            DebugLog($"    - {device.name} ({device.GetType().Name}) ID: {device.deviceId}");
        }

        DebugLog($"  Current action map: {playerInput.currentActionMap?.name}");

        connectedPlayers.Add(playerInput);
        DebugLog($"  Total connected players: {connectedPlayers.Count}");

        // Find or create player state
        PlayerSelectionState state = null;
        if (playerStates.ContainsKey(playerIndex))
        {
            state = playerStates[playerIndex];
        }
        else
        {
            // Create new state for dynamic players
            state = new PlayerSelectionState
            {
                playerIndex = playerIndex,
                selectedCharacterIndex = 0
            };
            playerStates[playerIndex] = state;
            DebugLog($"  Created new dynamic state for Player {playerIndex + 1}");
        }

        state.isActive = true;
        state.playerInput = playerInput;
        state.deviceName = deviceInfo.Trim();

        if (state.uiRoot != null)
        {
            state.uiRoot.gameObject.SetActive(true);
            DebugLog($"  Activated UI for Player {playerIndex + 1}");
        }
        else
        {
            DebugLog($"  No UI root for Player {playerIndex + 1} - will work without UI");
        }

        // Set up input callbacks
        SetupInputCallbacks(playerInput, playerIndex);
        UpdatePlayerUI(state);

        DebugLog($"Player {playerIndex + 1} setup complete with {state.deviceName}");

        // Debug: Show all current player assignments
        DebugLog("=== CURRENT PLAYER DEVICE ASSIGNMENTS ===");
        foreach (var player in connectedPlayers)
        {
            string devices = "";
            foreach (var device in player.devices)
            {
                devices += $"{device.name}({device.deviceId}) ";
            }
            DebugLog($"  Player {player.playerIndex + 1}: {devices}");
        }
    }

    private void SetupInputCallbacks(PlayerInput playerInput, int playerIndex)
    {
        var uiActionMap = playerInput.actions.FindActionMap("UI");
        if (uiActionMap == null)
        {
            Debug.LogError($"UI action map not found for Player {playerIndex + 1}!");
            return;
        }

        var navigateAction = uiActionMap.FindAction("Navigate");
        var submitAction = uiActionMap.FindAction("Submit");
        var cancelAction = uiActionMap.FindAction("Cancel");

        if (navigateAction != null)
        {
            navigateAction.performed += (ctx) => OnNavigate(playerIndex, ctx);
        }

        if (submitAction != null)
        {
            submitAction.performed += (ctx) => OnSelect(playerIndex);
        }

        if (cancelAction != null)
        {
            cancelAction.performed += (ctx) => OnBack(playerIndex);
        }

        DebugLog($"Input callbacks set up for Player {playerIndex + 1}");
    }

    private void OnPlayerLeft(PlayerInput playerInput)
    {
        int playerIndex = playerInput.playerIndex;
        DebugLog($"Player {playerIndex + 1} left");

        connectedPlayers.Remove(playerInput);

        if (playerStates.ContainsKey(playerIndex))
        {
            var state = playerStates[playerIndex];
            state.isActive = false;
            state.isReady = false;
            state.playerInput = null;

            if (state.uiRoot != null)
            {
                state.uiRoot.gameObject.SetActive(false);
            }
        }

        UpdateReadyState();
    }

    private void OnNavigate(int playerIndex, InputAction.CallbackContext context)
    {
        if (!playerStates.ContainsKey(playerIndex) || playerStates[playerIndex].isReady)
            return;

        var state = playerStates[playerIndex];
        Vector2 input = context.ReadValue<Vector2>();

        if (Mathf.Abs(input.x) > 0.5f)
        {
            int direction = input.x > 0 ? 1 : -1;
            state.selectedCharacterIndex = (state.selectedCharacterIndex + direction + availableCharacters.Length) % availableCharacters.Length;

            DebugLog($"Player {playerIndex + 1} selected: {availableCharacters[state.selectedCharacterIndex].name}");
            UpdatePlayerUI(state);
        }
    }

    private void OnSelect(int playerIndex)
    {
        if (!playerStates.ContainsKey(playerIndex)) return;

        var state = playerStates[playerIndex];
        if (!state.isReady)
        {
            state.isReady = true;
            DebugLog($"Player {playerIndex + 1} confirmed: {availableCharacters[state.selectedCharacterIndex].name}");
            UpdatePlayerUI(state);
            UpdateReadyState();
        }
    }

    private void OnBack(int playerIndex)
    {
        if (!playerStates.ContainsKey(playerIndex)) return;

        var state = playerStates[playerIndex];
        if (state.isReady)
        {
            state.isReady = false;
            DebugLog($"Player {playerIndex + 1} cancelled ready");
            UpdatePlayerUI(state);
            UpdateReadyState();
        }
    }

    private void UpdatePlayerUI(PlayerSelectionState state)
    {
        if (state.uiRoot == null) return;

        if (state.playerLabel != null)
        {
            string status = state.isActive ? (state.isReady ? "READY" : "SELECT CHARACTER") : "WAITING";
            string deviceDisplay = !string.IsNullOrEmpty(state.deviceName) ? $"({state.deviceName})" : "";
            state.playerLabel.text = $"P{state.playerIndex + 1} {deviceDisplay} - {status}";
        }

        if (state.isActive && availableCharacters != null && availableCharacters.Length > 0)
        {
            var selectedCharacter = availableCharacters[state.selectedCharacterIndex];

            if (state.characterName != null)
                state.characterName.text = selectedCharacter.name.ToUpper();

            if (state.characterDescription != null)
                state.characterDescription.text = selectedCharacter.description;

            // Update portraits
            for (int i = 0; i < state.characterPortraits.Length && i < availableCharacters.Length; i++)
            {
                if (state.characterPortraits[i] != null && availableCharacters[i].portrait != null)
                {
                    state.characterPortraits[i].sprite = availableCharacters[i].portrait;

                    Color targetColor = unselectedColor;
                    if (i == state.selectedCharacterIndex)
                    {
                        targetColor = state.isReady ? readyColor : selectedColor;
                    }
                    state.characterPortraits[i].color = targetColor;
                }
            }
        }
    }

    private void UpdateReadyState()
    {
        int activePlayers = 0;
        int readyPlayers = 0;

        foreach (var kvp in playerStates)
        {
            if (kvp.Value.isActive)
            {
                activePlayers++;
                if (kvp.Value.isReady)
                    readyPlayers++;
            }
        }

        bool allReady = activePlayers > 0 && readyPlayers == activePlayers;

        if (readyPrompt != null)
            readyPrompt.SetActive(allReady);

        if (allReady)
        {
            StartCoroutine(StartGameCountdown());
        }
    }

    private IEnumerator StartGameCountdown()
    {
        DebugLog("Starting game countdown...");
        yield return new WaitForSeconds(1f);

        // Verify still ready
        bool stillReady = true;
        foreach (var kvp in playerStates)
        {
            if (kvp.Value.isActive && !kvp.Value.isReady)
            {
                stillReady = false;
                break;
            }
        }

        if (stillReady)
        {
            StartGame();
        }
    }

    private void StartGame()
    {
        DebugLog("=== STARTING GAME ===");

        // Store selected characters
        SelectedCharacters.Clear();

        foreach (var kvp in playerStates)
        {
            if (kvp.Value.isActive && kvp.Value.isReady)
            {
                var selectedCharacter = availableCharacters[kvp.Value.selectedCharacterIndex];
                SelectedCharacters[kvp.Key] = selectedCharacter.type;
                DebugLog($"Player {kvp.Key + 1} selected: {selectedCharacter.name}");
            }
        }

        // Switch to Player action map
        foreach (var playerInput in connectedPlayers)
        {
            try
            {
                playerInput.SwitchCurrentActionMap("Player");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to switch action map: {e.Message}");
            }
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(gameplaySceneName);
    }

    private void OnDestroy()
    {
        if (PlayerInputManager.instance != null)
        {
            PlayerInputManager.instance.onPlayerJoined -= OnPlayerJoined;
            PlayerInputManager.instance.onPlayerLeft -= OnPlayerLeft;
        }
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogging)
        {
            Debug.Log($"[FixedCharSelection] {message}");
        }
    }
}