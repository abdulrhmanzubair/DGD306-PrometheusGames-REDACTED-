using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Collections;

public class SimpleMultiGamepadSelection : MonoBehaviour
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

    // Player states - using simple approach
    private PlayerState player1State = new PlayerState();
    private PlayerState player2State = new PlayerState();

    // Device assignments
    private InputDevice player1Device = null;
    private InputDevice player2Device = null;

    // Static data to pass to gameplay scene
    public static Dictionary<int, CharacterType> SelectedCharacters { get; private set; } = new Dictionary<int, CharacterType>();

    private class PlayerState
    {
        public bool isJoined = false;
        public bool isReady = false;
        public int selectedCharacterIndex = 0;
        public Transform uiRoot;
        public UnityEngine.UI.Image[] characterPortraits;
        public UnityEngine.UI.Text playerLabel;
        public UnityEngine.UI.Text characterName;
        public UnityEngine.UI.Text characterDescription;
        public UnityEngine.UI.Text statusText;
        public string deviceName = "";
    }

    private void Start()
    {
        DebugLog("=== SIMPLE MULTI-GAMEPAD CHARACTER SELECTION ===");

        // Validate character data
        if (availableCharacters == null || availableCharacters.Length == 0)
        {
            Debug.LogError("No characters available!");
            return;
        }

        // Setup player states
        SetupPlayerState(player1State, player1SelectionUI, 0);
        SetupPlayerState(player2State, player2SelectionUI, 1);

        if (readyPrompt != null)
            readyPrompt.SetActive(false);

        Time.timeScale = 1f;

        LogAvailableDevices();
        DebugLog("=== JOIN INSTRUCTIONS ===");
        DebugLog("Press any button on Gamepad 1 for Player 1");
        DebugLog("Press any button on Gamepad 2 for Player 2");
        DebugLog("Or press SPACE (P1) / ENTER (P2) for keyboard");
    }

    private void LogAvailableDevices()
    {
        DebugLog("=== AVAILABLE DEVICES ===");
        for (int i = 0; i < Gamepad.all.Count; i++)
        {
            DebugLog($"  Gamepad {i}: {Gamepad.all[i].name}");
        }
        if (Keyboard.current != null)
        {
            DebugLog($"  Keyboard: {Keyboard.current.name}");
        }
    }

    private void SetupPlayerState(PlayerState state, Transform uiRoot, int playerIndex)
    {
        if (uiRoot == null)
        {
            Debug.LogError($"Player {playerIndex + 1} UI Root is null!");
            return;
        }

        state.uiRoot = uiRoot;
        state.characterPortraits = uiRoot.GetComponentsInChildren<UnityEngine.UI.Image>();
        state.playerLabel = uiRoot.Find("PlayerLabel")?.GetComponent<UnityEngine.UI.Text>();
        state.characterName = uiRoot.Find("CharacterName")?.GetComponent<UnityEngine.UI.Text>();
        state.characterDescription = uiRoot.Find("CharacterDescription")?.GetComponent<UnityEngine.UI.Text>();
        state.statusText = uiRoot.Find("StatusText")?.GetComponent<UnityEngine.UI.Text>();

        UpdatePlayerUI(state, playerIndex);
        uiRoot.gameObject.SetActive(false); // Hide until joined
    }

    private void Update()
    {
        HandlePlayer1Input();
        HandlePlayer2Input();
        CheckReadyState();
    }

    private void HandlePlayer1Input()
    {
        if (!player1State.isJoined)
        {
            // Check for gamepad 0 input
            if (Gamepad.all.Count > 0)
            {
                var gamepad = Gamepad.all[0];
                if (gamepad.buttonSouth.wasPressedThisFrame ||
                    gamepad.buttonNorth.wasPressedThisFrame ||
                    gamepad.buttonEast.wasPressedThisFrame ||
                    gamepad.buttonWest.wasPressedThisFrame ||
                    gamepad.startButton.wasPressedThisFrame)
                {
                    JoinPlayer1WithDevice(gamepad);
                    return;
                }
            }

            // Check for keyboard space
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                JoinPlayer1WithDevice(Keyboard.current);
                return;
            }
        }
        else if (!player1State.isReady)
        {
            // Handle navigation and selection
            bool leftPressed = false;
            bool rightPressed = false;
            bool confirmPressed = false;
            bool cancelPressed = false;

            // Check player 1's assigned device
            if (player1Device is Gamepad gamepad)
            {
                leftPressed = gamepad.dpad.left.wasPressedThisFrame || gamepad.leftStick.left.wasPressedThisFrame;
                rightPressed = gamepad.dpad.right.wasPressedThisFrame || gamepad.leftStick.right.wasPressedThisFrame;
                confirmPressed = gamepad.buttonSouth.wasPressedThisFrame;
                cancelPressed = gamepad.buttonEast.wasPressedThisFrame;
            }
            else if (player1Device is Keyboard)
            {
                leftPressed = Keyboard.current.aKey.wasPressedThisFrame;
                rightPressed = Keyboard.current.dKey.wasPressedThisFrame;
                confirmPressed = Keyboard.current.spaceKey.wasPressedThisFrame;
                cancelPressed = Keyboard.current.escapeKey.wasPressedThisFrame;
            }

            if (leftPressed) ChangeCharacter(player1State, 0, -1);
            if (rightPressed) ChangeCharacter(player1State, 0, 1);
            if (confirmPressed) ConfirmSelection(player1State, 0);
            if (cancelPressed) CancelReady(player1State, 0);
        }
        else
        {
            // Handle cancel when ready
            bool cancelPressed = false;

            if (player1Device is Gamepad gamepad)
            {
                cancelPressed = gamepad.buttonEast.wasPressedThisFrame;
            }
            else if (player1Device is Keyboard)
            {
                cancelPressed = Keyboard.current.escapeKey.wasPressedThisFrame;
            }

            if (cancelPressed) CancelReady(player1State, 0);
        }
    }

    private void HandlePlayer2Input()
    {
        if (!player2State.isJoined)
        {
            // Check for gamepad 1 input (second gamepad)
            if (Gamepad.all.Count > 1)
            {
                var gamepad = Gamepad.all[1];
                if (gamepad.buttonSouth.wasPressedThisFrame ||
                    gamepad.buttonNorth.wasPressedThisFrame ||
                    gamepad.buttonEast.wasPressedThisFrame ||
                    gamepad.buttonWest.wasPressedThisFrame ||
                    gamepad.startButton.wasPressedThisFrame)
                {
                    JoinPlayer2WithDevice(gamepad);
                    return;
                }
            }

            // Check for keyboard enter (if not used by player 1)
            if (Keyboard.current != null && player1Device != Keyboard.current &&
                Keyboard.current.enterKey.wasPressedThisFrame)
            {
                JoinPlayer2WithDevice(Keyboard.current);
                return;
            }
        }
        else if (!player2State.isReady)
        {
            // Handle navigation and selection
            bool leftPressed = false;
            bool rightPressed = false;
            bool confirmPressed = false;
            bool cancelPressed = false;

            // Check player 2's assigned device
            if (player2Device is Gamepad gamepad)
            {
                leftPressed = gamepad.dpad.left.wasPressedThisFrame || gamepad.leftStick.left.wasPressedThisFrame;
                rightPressed = gamepad.dpad.right.wasPressedThisFrame || gamepad.leftStick.right.wasPressedThisFrame;
                confirmPressed = gamepad.buttonSouth.wasPressedThisFrame;
                cancelPressed = gamepad.buttonEast.wasPressedThisFrame;
            }
            else if (player2Device is Keyboard)
            {
                leftPressed = Keyboard.current.leftArrowKey.wasPressedThisFrame;
                rightPressed = Keyboard.current.rightArrowKey.wasPressedThisFrame;
                confirmPressed = Keyboard.current.enterKey.wasPressedThisFrame;
                cancelPressed = Keyboard.current.escapeKey.wasPressedThisFrame;
            }

            if (leftPressed) ChangeCharacter(player2State, 1, -1);
            if (rightPressed) ChangeCharacter(player2State, 1, 1);
            if (confirmPressed) ConfirmSelection(player2State, 1);
            if (cancelPressed) CancelReady(player2State, 1);
        }
        else
        {
            // Handle cancel when ready
            bool cancelPressed = false;

            if (player2Device is Gamepad gamepad)
            {
                cancelPressed = gamepad.buttonEast.wasPressedThisFrame;
            }
            else if (player2Device is Keyboard)
            {
                cancelPressed = Keyboard.current.escapeKey.wasPressedThisFrame;
            }

            if (cancelPressed) CancelReady(player2State, 1);
        }
    }

    private void JoinPlayer1WithDevice(InputDevice device)
    {
        player1Device = device;
        player1State.isJoined = true;
        player1State.deviceName = device.name;
        player1State.uiRoot.gameObject.SetActive(true);
        UpdatePlayerUI(player1State, 0);
        DebugLog($"Player 1 joined with {device.name}!");
    }

    private void JoinPlayer2WithDevice(InputDevice device)
    {
        player2Device = device;
        player2State.isJoined = true;
        player2State.deviceName = device.name;
        player2State.uiRoot.gameObject.SetActive(true);
        UpdatePlayerUI(player2State, 1);
        DebugLog($"Player 2 joined with {device.name}!");
    }

    private void ChangeCharacter(PlayerState state, int playerIndex, int direction)
    {
        if (availableCharacters == null || availableCharacters.Length == 0) return;

        state.selectedCharacterIndex = (state.selectedCharacterIndex + direction + availableCharacters.Length) % availableCharacters.Length;
        UpdatePlayerUI(state, playerIndex);
        DebugLog($"Player {playerIndex + 1} selected: {availableCharacters[state.selectedCharacterIndex].name}");
    }

    private void ConfirmSelection(PlayerState state, int playerIndex)
    {
        if (availableCharacters == null || availableCharacters.Length == 0) return;

        state.isReady = true;
        UpdatePlayerUI(state, playerIndex);
        DebugLog($"Player {playerIndex + 1} confirmed: {availableCharacters[state.selectedCharacterIndex].name}");
    }

    private void CancelReady(PlayerState state, int playerIndex)
    {
        state.isReady = false;
        UpdatePlayerUI(state, playerIndex);
        DebugLog($"Player {playerIndex + 1} cancelled ready state");
    }

    private void UpdatePlayerUI(PlayerState state, int playerIndex)
    {
        if (!state.isJoined)
        {
            if (state.playerLabel != null)
            {
                string joinPrompt = playerIndex == 0 ? "GAMEPAD 1 or SPACE" : "GAMEPAD 2 or ENTER";
                state.playerLabel.text = $"PLAYER {playerIndex + 1} - PRESS {joinPrompt} TO JOIN";
            }
            return;
        }

        if (state.playerLabel != null)
        {
            string status = state.isReady ? "READY!" : "SELECT CHARACTER";
            state.playerLabel.text = $"P{playerIndex + 1} ({state.deviceName}) - {status}";
        }

        if (availableCharacters != null && availableCharacters.Length > 0 && state.selectedCharacterIndex < availableCharacters.Length)
        {
            var selectedCharacter = availableCharacters[state.selectedCharacterIndex];

            if (state.characterName != null)
                state.characterName.text = selectedCharacter.name.ToUpper();

            if (state.characterDescription != null)
                state.characterDescription.text = selectedCharacter.description;

            if (state.statusText != null)
            {
                string controls = state.isReady ? "READY!" : GetControlsText(playerIndex);
                state.statusText.text = controls;
            }

            // Update character portraits
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

    private string GetControlsText(int playerIndex)
    {
        if (playerIndex == 0)
        {
            return player1Device is Gamepad ? "D-Pad/Stick to select, A to confirm, B to cancel"
                                          : "A/D to select, SPACE to confirm, ESC to cancel";
        }
        else
        {
            return player2Device is Gamepad ? "D-Pad/Stick to select, A to confirm, B to cancel"
                                          : "← → to select, ENTER to confirm, ESC to cancel";
        }
    }

    private void CheckReadyState()
    {
        // Need at least one player joined
        if (!player1State.isJoined && !player2State.isJoined)
        {
            if (readyPrompt != null) readyPrompt.SetActive(false);
            return;
        }

        // Check if all joined players are ready
        bool allReady = true;
        if (player1State.isJoined && !player1State.isReady) allReady = false;
        if (player2State.isJoined && !player2State.isReady) allReady = false;

        if (readyPrompt != null)
            readyPrompt.SetActive(allReady);

        if (allReady)
        {
            StartCoroutine(StartGameCountdown());
        }
    }

    private IEnumerator StartGameCountdown()
    {
        yield return new WaitForSeconds(1.5f);

        // Double-check still ready
        bool stillReady = true;
        if (player1State.isJoined && !player1State.isReady) stillReady = false;
        if (player2State.isJoined && !player2State.isReady) stillReady = false;

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

        if (player1State.isJoined && player1State.isReady)
        {
            SelectedCharacters[0] = availableCharacters[player1State.selectedCharacterIndex].type;
            DebugLog($"Player 1 selected: {availableCharacters[player1State.selectedCharacterIndex].name}");
        }

        if (player2State.isJoined && player2State.isReady)
        {
            SelectedCharacters[1] = availableCharacters[player2State.selectedCharacterIndex].type;
            DebugLog($"Player 2 selected: {availableCharacters[player2State.selectedCharacterIndex].name}");
        }

        // Store device info for gameplay scene (optional)
        if (player1Device != null)
            PlayerPrefs.SetString("Player1Device", player1Device.name);
        if (player2Device != null)
            PlayerPrefs.SetString("Player2Device", player2Device.name);

        PlayerPrefs.Save();

        Time.timeScale = 1f;
        SceneManager.LoadScene(gameplaySceneName);
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogging)
        {
            Debug.Log($"[SimpleMultiGamepad] {message}");
        }
    }

    // TEMPORARY TEST - Remove this later!
    private void OnGUI()
    {
        if (!enableDebugLogging) return;

        GUILayout.BeginArea(new Rect(10, 10, 400, 200));
        GUILayout.Label("DEBUG INFO:");
        GUILayout.Label($"Gamepads connected: {Gamepad.all.Count}");

        for (int i = 0; i < Gamepad.all.Count; i++)
        {
            var gamepad = Gamepad.all[i];
            GUILayout.Label($"  Gamepad {i}: {gamepad.name}");
        }

        GUILayout.Label($"Player 1: {(player1State.isJoined ? $"Joined with {player1State.deviceName}" : "Not joined")}");
        GUILayout.Label($"Player 2: {(player2State.isJoined ? $"Joined with {player2State.deviceName}" : "Not joined")}");

        if (GUILayout.Button("Force Start Game (Test)"))
        {
            // Force both players ready for testing
            if (!player1State.isJoined)
            {
                JoinPlayer1WithDevice(Gamepad.all.Count > 0 ? Gamepad.all[0] : Keyboard.current);
            }
            player1State.isReady = true;

            if (!player2State.isJoined && Gamepad.all.Count > 1)
            {
                JoinPlayer2WithDevice(Gamepad.all[1]);
            }
            player2State.isReady = true;

            UpdatePlayerUI(player1State, 0);
            UpdatePlayerUI(player2State, 1);
        }

        GUILayout.EndArea();
    }
}