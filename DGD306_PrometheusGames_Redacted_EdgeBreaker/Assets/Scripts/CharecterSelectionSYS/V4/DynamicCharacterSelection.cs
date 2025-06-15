using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// Dynamic Character Selection with flexible device assignment
/// Any player can use any available input device
/// </summary>
public class DynamicCharacterSelection : MonoBehaviour
{
    [Header("Character Data")]
    [SerializeField] private CharacterData[] availableCharacters;

    [Header("UI References")]
    [SerializeField] private Transform[] playerUISlots; // Array of UI slots for players
    [SerializeField] private GameObject readyPrompt;
    [SerializeField] private string gameplaySceneName = "LVL_001";

    [Header("Selection Visuals")]
    [SerializeField] private Color selectedColor = Color.yellow;
    [SerializeField] private Color unselectedColor = Color.white;
    [SerializeField] private Color readyColor = Color.green;

    [Header("Dynamic Settings")]
    [SerializeField] private int maxPlayers = 4;
    [SerializeField] private bool allowKeyboardSharing = true; // Multiple players on same keyboard

    // Available input devices
    private List<InputDevice> availableDevices = new List<InputDevice>();
    private Dictionary<InputDevice, int> deviceToPlayerMapping = new Dictionary<InputDevice, int>();

    // Keyboard schemes for shared keyboard usage
    private KeyboardScheme[] keyboardSchemes = new KeyboardScheme[]
    {
        new KeyboardScheme("WASD", Key.W, Key.S, Key.A, Key.D, Key.Space, Key.LeftShift, Key.LeftCtrl),
        new KeyboardScheme("Arrows", Key.UpArrow, Key.DownArrow, Key.LeftArrow, Key.RightArrow, Key.Enter, Key.RightShift, Key.RightCtrl),
        new KeyboardScheme("IJKL", Key.I, Key.K, Key.J, Key.L, Key.U, Key.O, Key.P),
        new KeyboardScheme("Numpad", Key.Numpad8, Key.Numpad2, Key.Numpad4, Key.Numpad6, Key.Numpad0, Key.NumpadPlus, Key.NumpadEnter)
    };

    [System.Serializable]
    public class KeyboardScheme
    {
        public string name;
        public Key up, down, left, right, confirm, cancel, action;

        public KeyboardScheme(string name, Key up, Key down, Key left, Key right, Key confirm, Key cancel, Key action)
        {
            this.name = name;
            this.up = up; this.down = down; this.left = left; this.right = right;
            this.confirm = confirm; this.cancel = cancel; this.action = action;
        }
    }

    // Player states
    private List<PlayerState> players = new List<PlayerState>();

    // Static data to pass to gameplay scene
    public static Dictionary<int, CharacterSelectionData> SelectedPlayers { get; private set; } = new Dictionary<int, CharacterSelectionData>();

    [System.Serializable]
    public class CharacterSelectionData
    {
        public CharacterType characterType;
        public string inputDevice;
        public int keyboardScheme = -1; // -1 if not keyboard
    }

    private class PlayerState
    {
        public bool isJoined = false;
        public bool isReady = false;
        public int selectedCharacterIndex = 0;
        public InputDevice assignedDevice;
        public int keyboardSchemeIndex = -1; // For keyboard users
        public Transform uiRoot;
        public UnityEngine.UI.Image[] characterPortraits;
        public UnityEngine.UI.Text playerLabel;
        public UnityEngine.UI.Text characterName;
        public UnityEngine.UI.Text deviceInfo;
        public UnityEngine.UI.Text statusText;
    }

    void Start()
    {
        RefreshAvailableDevices();
        SetupPlayerSlots();

        Debug.Log($"🎮 Dynamic Character Selection Started");
        Debug.Log($"   Max Players: {maxPlayers}");
        Debug.Log($"   Available Devices: {availableDevices.Count}");
        LogAvailableDevices();
    }

    void RefreshAvailableDevices()
    {
        availableDevices.Clear();

        // Add all gamepads
        foreach (var gamepad in Gamepad.all)
        {
            availableDevices.Add(gamepad);
        }

        // Add keyboard(s) - treat as separate "devices" for each scheme if sharing enabled
        if (Keyboard.current != null)
        {
            if (allowKeyboardSharing)
            {
                // Each keyboard scheme counts as a separate "device"
                for (int i = 0; i < keyboardSchemes.Length; i++)
                {
                    availableDevices.Add(Keyboard.current); // We'll differentiate by scheme index
                }
            }
            else
            {
                availableDevices.Add(Keyboard.current);
            }
        }
    }

    void SetupPlayerSlots()
    {
        players.Clear();

        for (int i = 0; i < maxPlayers && i < playerUISlots.Length; i++)
        {
            var playerState = new PlayerState();
            SetupPlayerState(playerState, playerUISlots[i], i);
            players.Add(playerState);
        }
    }

    void SetupPlayerState(PlayerState state, Transform uiRoot, int playerIndex)
    {
        if (uiRoot == null) return;

        state.uiRoot = uiRoot;
        state.characterPortraits = uiRoot.GetComponentsInChildren<UnityEngine.UI.Image>();
        state.playerLabel = uiRoot.Find("PlayerLabel")?.GetComponent<UnityEngine.UI.Text>();
        state.characterName = uiRoot.Find("CharacterName")?.GetComponent<UnityEngine.UI.Text>();
        state.deviceInfo = uiRoot.Find("DeviceInfo")?.GetComponent<UnityEngine.UI.Text>();
        state.statusText = uiRoot.Find("StatusText")?.GetComponent<UnityEngine.UI.Text>();

        UpdatePlayerUI(state, playerIndex);
        uiRoot.gameObject.SetActive(false); // Hide until joined
    }

    void Update()
    {
        HandleDynamicInput();
        CheckReadyState();

        // Debug: Force all players ready
        if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
        {
            ForceAllPlayersReady();
        }
    }

    void HandleDynamicInput()
    {
        // Check for new players joining with any available device
        HandleJoinInput();

        // Handle input for existing players
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].isJoined)
            {
                HandlePlayerInput(players[i], i);
            }
        }
    }

    void HandleJoinInput()
    {
        // Check gamepads
        foreach (var gamepad in Gamepad.all)
        {
            if (gamepad.buttonSouth.wasPressedThisFrame && !IsDeviceAssigned(gamepad))
            {
                JoinPlayerWithDevice(gamepad, -1);
            }
        }

        // Check keyboard schemes
        if (Keyboard.current != null && allowKeyboardSharing)
        {
            for (int schemeIndex = 0; schemeIndex < keyboardSchemes.Length; schemeIndex++)
            {
                var scheme = keyboardSchemes[schemeIndex];
                if (Keyboard.current[scheme.confirm].wasPressedThisFrame && !IsKeyboardSchemeAssigned(schemeIndex))
                {
                    JoinPlayerWithDevice(Keyboard.current, schemeIndex);
                }
            }
        }
        else if (Keyboard.current != null && !IsDeviceAssigned(Keyboard.current))
        {
            // Single keyboard mode - check primary confirm key
            if (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame)
            {
                JoinPlayerWithDevice(Keyboard.current, 0); // Default to first scheme
            }
        }
    }

    void JoinPlayerWithDevice(InputDevice device, int keyboardSchemeIndex)
    {
        // Find first available player slot
        for (int i = 0; i < players.Count; i++)
        {
            if (!players[i].isJoined)
            {
                var playerState = players[i];
                playerState.isJoined = true;
                playerState.assignedDevice = device;
                playerState.keyboardSchemeIndex = keyboardSchemeIndex;
                playerState.uiRoot.gameObject.SetActive(true);

                // Track device assignment
                if (device is Keyboard && keyboardSchemeIndex >= 0)
                {
                    // For keyboard, we track scheme instead of device directly
                    Debug.Log($"🎮 Player {i + 1} joined with {keyboardSchemes[keyboardSchemeIndex].name} keyboard scheme");
                }
                else
                {
                    deviceToPlayerMapping[device] = i;
                    Debug.Log($"🎮 Player {i + 1} joined with {device.name}");
                }

                UpdatePlayerUI(playerState, i);
                break;
            }
        }
    }

    void HandlePlayerInput(PlayerState playerState, int playerIndex)
    {
        if (playerState.assignedDevice is Gamepad gamepad)
        {
            HandleGamepadInput(playerState, playerIndex, gamepad);
        }
        else if (playerState.assignedDevice is Keyboard)
        {
            HandleKeyboardInput(playerState, playerIndex, playerState.keyboardSchemeIndex);
        }
    }

    void HandleGamepadInput(PlayerState playerState, int playerIndex, Gamepad gamepad)
    {
        if (playerState.isReady)
        {
            // Cancel ready
            if (gamepad.buttonEast.wasPressedThisFrame)
            {
                CancelReady(playerState, playerIndex);
            }
            return;
        }

        // Navigation
        if (gamepad.dpad.left.wasPressedThisFrame || gamepad.leftStick.left.wasPressedThisFrame)
        {
            ChangeCharacter(playerState, playerIndex, -1);
        }
        else if (gamepad.dpad.right.wasPressedThisFrame || gamepad.leftStick.right.wasPressedThisFrame)
        {
            ChangeCharacter(playerState, playerIndex, 1);
        }
        else if (gamepad.buttonSouth.wasPressedThisFrame)
        {
            ConfirmSelection(playerState, playerIndex);
        }
        else if (gamepad.buttonEast.wasPressedThisFrame)
        {
            LeaveGame(playerState, playerIndex);
        }
    }

    void HandleKeyboardInput(PlayerState playerState, int playerIndex, int schemeIndex)
    {
        if (schemeIndex < 0 || schemeIndex >= keyboardSchemes.Length) return;

        var scheme = keyboardSchemes[schemeIndex];
        var keyboard = Keyboard.current;

        if (playerState.isReady)
        {
            // Cancel ready
            if (keyboard[scheme.cancel].wasPressedThisFrame)
            {
                CancelReady(playerState, playerIndex);
            }
            return;
        }

        // Navigation
        if (keyboard[scheme.left].wasPressedThisFrame)
        {
            ChangeCharacter(playerState, playerIndex, -1);
        }
        else if (keyboard[scheme.right].wasPressedThisFrame)
        {
            ChangeCharacter(playerState, playerIndex, 1);
        }
        else if (keyboard[scheme.confirm].wasPressedThisFrame)
        {
            ConfirmSelection(playerState, playerIndex);
        }
        else if (keyboard[scheme.cancel].wasPressedThisFrame)
        {
            LeaveGame(playerState, playerIndex);
        }
    }

    bool IsDeviceAssigned(InputDevice device)
    {
        return deviceToPlayerMapping.ContainsKey(device);
    }

    bool IsKeyboardSchemeAssigned(int schemeIndex)
    {
        foreach (var player in players)
        {
            if (player.isJoined && player.assignedDevice is Keyboard && player.keyboardSchemeIndex == schemeIndex)
            {
                return true;
            }
        }
        return false;
    }

    void ChangeCharacter(PlayerState playerState, int playerIndex, int direction)
    {
        if (availableCharacters == null || availableCharacters.Length == 0) return;

        playerState.selectedCharacterIndex = (playerState.selectedCharacterIndex + direction + availableCharacters.Length) % availableCharacters.Length;
        UpdatePlayerUI(playerState, playerIndex);
        Debug.Log($"Player {playerIndex + 1} selected: {availableCharacters[playerState.selectedCharacterIndex].name}");
    }

    void ConfirmSelection(PlayerState playerState, int playerIndex)
    {
        if (availableCharacters == null || availableCharacters.Length == 0) return;

        playerState.isReady = true;
        UpdatePlayerUI(playerState, playerIndex);
        Debug.Log($"Player {playerIndex + 1} confirmed: {availableCharacters[playerState.selectedCharacterIndex].name}");
    }

    void CancelReady(PlayerState playerState, int playerIndex)
    {
        playerState.isReady = false;
        UpdatePlayerUI(playerState, playerIndex);
        Debug.Log($"Player {playerIndex + 1} cancelled ready state");
    }

    void LeaveGame(PlayerState playerState, int playerIndex)
    {
        // Remove device assignment
        if (deviceToPlayerMapping.ContainsKey(playerState.assignedDevice))
        {
            deviceToPlayerMapping.Remove(playerState.assignedDevice);
        }

        // Reset player state
        playerState.isJoined = false;
        playerState.isReady = false;
        playerState.assignedDevice = null;
        playerState.keyboardSchemeIndex = -1;
        playerState.uiRoot.gameObject.SetActive(false);

        Debug.Log($"Player {playerIndex + 1} left the game");
    }

    void UpdatePlayerUI(PlayerState playerState, int playerIndex)
    {
        if (!playerState.isJoined)
        {
            // Show available devices to join
            string availableInputs = GetAvailableInputsText();
            if (playerState.playerLabel != null)
            {
                playerState.playerLabel.text = $"PRESS ANY BUTTON TO JOIN\n{availableInputs}";
            }
            return;
        }

        // Show player info
        if (playerState.playerLabel != null)
        {
            string status = playerState.isReady ? "READY!" : "SELECT CHARACTER";
            playerState.playerLabel.text = $"PLAYER {playerIndex + 1} - {status}";
        }

        // Show device info
        if (playerState.deviceInfo != null)
        {
            string deviceText;
            if (playerState.assignedDevice is Keyboard && playerState.keyboardSchemeIndex >= 0)
            {
                deviceText = $"Keyboard: {keyboardSchemes[playerState.keyboardSchemeIndex].name}";
            }
            else
            {
                deviceText = $"Controller: {playerState.assignedDevice.name}";
            }
            playerState.deviceInfo.text = deviceText;
        }

        // Update character selection UI
        if (availableCharacters != null && availableCharacters.Length > 0 && playerState.selectedCharacterIndex < availableCharacters.Length)
        {
            var selectedCharacter = availableCharacters[playerState.selectedCharacterIndex];

            if (playerState.characterName != null)
                playerState.characterName.text = selectedCharacter.name.ToUpper();

            if (playerState.statusText != null)
            {
                string controls = GetControlsText(playerState);
                playerState.statusText.text = playerState.isReady ? "READY!" : controls;
            }

            // Update character portraits
            for (int i = 0; i < playerState.characterPortraits.Length && i < availableCharacters.Length; i++)
            {
                if (playerState.characterPortraits[i] != null && availableCharacters[i].portrait != null)
                {
                    playerState.characterPortraits[i].sprite = availableCharacters[i].portrait;

                    Color targetColor = unselectedColor;
                    if (i == playerState.selectedCharacterIndex)
                    {
                        targetColor = playerState.isReady ? readyColor : selectedColor;
                    }
                    playerState.characterPortraits[i].color = targetColor;
                }
            }
        }
    }

    string GetAvailableInputsText()
    {
        List<string> available = new List<string>();

        // Check available gamepads
        foreach (var gamepad in Gamepad.all)
        {
            if (!IsDeviceAssigned(gamepad))
            {
                available.Add($"{gamepad.name} (A Button)");
            }
        }

        // Check available keyboard schemes
        if (allowKeyboardSharing && Keyboard.current != null)
        {
            for (int i = 0; i < keyboardSchemes.Length; i++)
            {
                if (!IsKeyboardSchemeAssigned(i))
                {
                    available.Add($"Keyboard {keyboardSchemes[i].name}");
                }
            }
        }
        else if (Keyboard.current != null && !IsDeviceAssigned(Keyboard.current))
        {
            available.Add("Keyboard (Space/Enter)");
        }

        return available.Count > 0 ? string.Join(", ", available) : "No devices available";
    }

    string GetControlsText(PlayerState playerState)
    {
        if (playerState.assignedDevice is Gamepad)
        {
            return "D-Pad/Stick: Select | A: Confirm | B: Cancel/Leave";
        }
        else if (playerState.assignedDevice is Keyboard && playerState.keyboardSchemeIndex >= 0)
        {
            var scheme = keyboardSchemes[playerState.keyboardSchemeIndex];
            return $"{scheme.left}/{scheme.right}: Select | {scheme.confirm}: Confirm | {scheme.cancel}: Cancel";
        }
        return "Unknown controls";
    }

    void CheckReadyState()
    {
        int joinedPlayers = 0;
        int readyPlayers = 0;

        foreach (var player in players)
        {
            if (player.isJoined)
            {
                joinedPlayers++;
                if (player.isReady) readyPlayers++;
            }
        }

        bool allReady = joinedPlayers > 0 && joinedPlayers == readyPlayers;

        if (readyPrompt != null)
            readyPrompt.SetActive(allReady);

        if (allReady)
        {
            StartCoroutine(StartGameCountdown());
        }
    }

    IEnumerator StartGameCountdown()
    {
        yield return new WaitForSeconds(1.5f);

        // Double-check still ready
        bool stillReady = true;
        int joinedCount = 0;
        foreach (var player in players)
        {
            if (player.isJoined)
            {
                joinedCount++;
                if (!player.isReady) stillReady = false;
            }
        }

        if (stillReady && joinedCount > 0)
        {
            StartGame();
        }
    }

    void StartGame()
    {
        Debug.Log("🚀 === STARTING DYNAMIC GAME ===");

        SelectedPlayers.Clear();

        for (int i = 0; i < players.Count; i++)
        {
            var player = players[i];
            if (player.isJoined && player.isReady)
            {
                var selectionData = new CharacterSelectionData
                {
                    characterType = availableCharacters[player.selectedCharacterIndex].type,
                    inputDevice = player.assignedDevice.name,
                    keyboardScheme = player.keyboardSchemeIndex
                };

                SelectedPlayers[i] = selectionData;

                Debug.Log($"📝 Player {i + 1}: {selectionData.characterType} on {selectionData.inputDevice}" +
                         (selectionData.keyboardScheme >= 0 ? $" ({keyboardSchemes[selectionData.keyboardScheme].name})" : ""));
            }
        }

        // Save to PlayerPrefs as backup
        SaveToPlayerPrefs();

        SceneManager.LoadScene(gameplaySceneName);
    }

    void SaveToPlayerPrefs()
    {
        PlayerPrefs.SetInt("DynamicPlayerCount", SelectedPlayers.Count);

        foreach (var kvp in SelectedPlayers)
        {
            PlayerPrefs.SetString($"Player{kvp.Key}Character", kvp.Value.characterType.ToString());
            PlayerPrefs.SetString($"Player{kvp.Key}Device", kvp.Value.inputDevice);
            PlayerPrefs.SetInt($"Player{kvp.Key}KeyboardScheme", kvp.Value.keyboardScheme);
        }

        PlayerPrefs.Save();
        Debug.Log("💾 Dynamic selection saved to PlayerPrefs");
    }

    void LogAvailableDevices()
    {
        Debug.Log("🎮 Available Input Devices:");
        foreach (var gamepad in Gamepad.all)
        {
            Debug.Log($"   🎮 {gamepad.name}");
        }

        if (Keyboard.current != null)
        {
            if (allowKeyboardSharing)
            {
                foreach (var scheme in keyboardSchemes)
                {
                    Debug.Log($"   ⌨️ Keyboard ({scheme.name})");
                }
            }
            else
            {
                Debug.Log($"   ⌨️ Keyboard (Single User)");
            }
        }
    }

    void ForceAllPlayersReady()
    {
        Debug.Log("🧪 FORCING ALL PLAYERS READY FOR TESTING");

        // Join up to 2 players with different devices for testing
        if (Gamepad.all.Count > 0 && !players[0].isJoined)
        {
            JoinPlayerWithDevice(Gamepad.all[0], -1);
            players[0].selectedCharacterIndex = 0;
            players[0].isReady = true;
            UpdatePlayerUI(players[0], 0);
        }

        if (Keyboard.current != null && !players[1].isJoined)
        {
            JoinPlayerWithDevice(Keyboard.current, 0); // WASD scheme
            players[1].selectedCharacterIndex = 1;
            players[1].isReady = true;
            UpdatePlayerUI(players[1], 1);
        }
    }
}