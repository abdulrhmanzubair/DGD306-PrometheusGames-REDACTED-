using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Collections;

public class SimplifiedCharacterSelection : MonoBehaviour
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

    // Player states
    private PlayerState player1State = new PlayerState();
    private PlayerState player2State = new PlayerState();


    private string player1InputMethod = "none";
    private string player2InputMethod = "none";
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
    }

    private void Start()
    {
        // Setup player states
        SetupPlayerState(player1State, player1SelectionUI, 0);
        SetupPlayerState(player2State, player2SelectionUI, 1);

        if (readyPrompt != null)
            readyPrompt.SetActive(false);

        // Ensure time scale is normal
        Time.timeScale = 1f;

        Debug.Log("[CharSelection] Simple character selection started.");
        Debug.Log("GAMEPAD SETUP:");
        Debug.Log($"  Total gamepads connected: {Gamepad.all.Count}");
        for (int i = 0; i < Gamepad.all.Count; i++)
        {
            Debug.Log($"  Gamepad {i}: {Gamepad.all[i].name}");
        }
        Debug.Log("CONTROLS:");
        Debug.Log("  Player 1: SPACE (keyboard) or A button (Gamepad 1)");
        Debug.Log("  Player 2: ENTER (keyboard) or A button (Gamepad 2)");
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

        Debug.Log($"Player {playerIndex + 1} UI - Portraits: {state.characterPortraits.Length}, Label: {state.playerLabel != null}, Name: {state.characterName != null}");

        UpdatePlayerUI(state, playerIndex);
        uiRoot.gameObject.SetActive(false); // Hide until joined
    }

    private void Update()
    {
        HandlePlayer1Input();
        HandlePlayer2Input();
        CheckReadyState();

        // TEMPORARY TEST - Remove this later!
        if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
        {
            Debug.Log("MANUAL TEST - Forcing both players ready...");

            // Force both players to join and be ready
            if (!player1State.isJoined)
            {
                JoinPlayer(player1State, 0);
            }
            player1State.selectedCharacterIndex = 0; // Gunner
            player1State.isReady = true;
            UpdatePlayerUI(player1State, 0);

            if (!player2State.isJoined)
            {
                JoinPlayer(player2State, 1);
            }
            player2State.selectedCharacterIndex = 1; // Melee
            player2State.isReady = true;
            UpdatePlayerUI(player2State, 1);

            Debug.Log("Both players forced ready - should start game now");
        }
    }

    private void HandlePlayer1Input()
    {
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            player1InputMethod = "keyboard";
            JoinPlayer(player1State, 0);
        }
        else if (Gamepad.all.Count > 0 && Gamepad.all[0].buttonSouth.wasPressedThisFrame)
        {
            player1InputMethod = "gamepad";
            JoinPlayer(player1State, 0);
        }

        if (!player1State.isJoined)
        {
            // Player 1 joins with SPACE or FIRST GAMEPAD (index 0)
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                JoinPlayer(player1State, 0);
            }
            else if (Gamepad.all.Count > 0 && Gamepad.all[0].buttonSouth.wasPressedThisFrame)
            {
                JoinPlayer(player1State, 0);
            }
        }
        else if (!player1State.isReady)
        {
            // Navigation with A/D or FIRST GAMEPAD (index 0)
            if (Keyboard.current != null)
            {
                if (Keyboard.current.aKey.wasPressedThisFrame)
                {
                    ChangeCharacter(player1State, 0, -1);
                }
                else if (Keyboard.current.dKey.wasPressedThisFrame)
                {
                    ChangeCharacter(player1State, 0, 1);
                }
                else if (Keyboard.current.spaceKey.wasPressedThisFrame)
                {
                    ConfirmSelection(player1State, 0);
                }
                else if (Keyboard.current.escapeKey.wasPressedThisFrame)
                {
                    CancelReady(player1State, 0);
                }
            }

            // Check FIRST gamepad (index 0) for Player 1
            if (Gamepad.all.Count > 0)
            {
                var gamepad1 = Gamepad.all[0];
                if (gamepad1.dpad.left.wasPressedThisFrame || gamepad1.leftStick.left.wasPressedThisFrame)
                {
                    ChangeCharacter(player1State, 0, -1);
                }
                else if (gamepad1.dpad.right.wasPressedThisFrame || gamepad1.leftStick.right.wasPressedThisFrame)
                {
                    ChangeCharacter(player1State, 0, 1);
                }
                else if (gamepad1.buttonSouth.wasPressedThisFrame)
                {
                    ConfirmSelection(player1State, 0);
                }
                else if (gamepad1.buttonEast.wasPressedThisFrame)
                {
                    CancelReady(player1State, 0);
                }
            }
        }
        else
        {
            // Cancel ready with ESC or gamepad B
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CancelReady(player1State, 0);
            }
            else if (Gamepad.all.Count > 0 && Gamepad.all[0].buttonEast.wasPressedThisFrame)
            {
                CancelReady(player1State, 0);
            }
        }
    }

    private void HandlePlayer2Input()
    {
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            player2InputMethod = "keyboard";
            JoinPlayer(player2State, 0);
        }
        else if (Gamepad.all.Count > 0 && Gamepad.all[0].buttonSouth.wasPressedThisFrame)
        {
            player2InputMethod = "gamepad";
            JoinPlayer(player2State, 0);
        }
        if (!player2State.isJoined)
        {
            // Player 2 joins with ENTER or SECOND GAMEPAD (index 1)
            bool shouldJoin = false;

            if (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame)
                shouldJoin = true;

            // Check for second gamepad specifically (index 1)
            if (Gamepad.all.Count > 1 && Gamepad.all[1].buttonSouth.wasPressedThisFrame)
                shouldJoin = true;

            if (shouldJoin)
            {
                JoinPlayer(player2State, 1);
            }
        }
        else if (!player2State.isReady)
        {
            // Navigation with Arrow keys or SECOND GAMEPAD (index 1)
            bool leftPressed = false;
            bool rightPressed = false;
            bool confirmPressed = false;
            bool cancelPressed = false;

            if (Keyboard.current != null)
            {
                leftPressed = Keyboard.current.leftArrowKey.wasPressedThisFrame;
                rightPressed = Keyboard.current.rightArrowKey.wasPressedThisFrame;
                confirmPressed = Keyboard.current.enterKey.wasPressedThisFrame;
                cancelPressed = Keyboard.current.escapeKey.wasPressedThisFrame;
            }

            // Use SECOND gamepad (index 1) for Player 2
            if (Gamepad.all.Count > 1)
            {
                var gamepad2 = Gamepad.all[1];
                leftPressed |= gamepad2.dpad.left.wasPressedThisFrame || gamepad2.leftStick.left.wasPressedThisFrame;
                rightPressed |= gamepad2.dpad.right.wasPressedThisFrame || gamepad2.leftStick.right.wasPressedThisFrame;
                confirmPressed |= gamepad2.buttonSouth.wasPressedThisFrame;
                cancelPressed |= gamepad2.buttonEast.wasPressedThisFrame;
            }

            if (leftPressed) ChangeCharacter(player2State, 1, -1);
            if (rightPressed) ChangeCharacter(player2State, 1, 1);
            if (confirmPressed) ConfirmSelection(player2State, 1);
            if (cancelPressed) CancelReady(player2State, 1);
        }
        else
        {
            // Cancel ready
            bool cancelPressed = false;

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                cancelPressed = true;

            // Use SECOND gamepad (index 1) for Player 2
            if (Gamepad.all.Count > 1 && Gamepad.all[1].buttonEast.wasPressedThisFrame)
                cancelPressed = true;

            if (cancelPressed)
            {
                CancelReady(player2State, 1);
            }
        }
    }

    private void JoinPlayer(PlayerState state, int playerIndex)
    {
        state.isJoined = true;
        state.uiRoot.gameObject.SetActive(true);
        UpdatePlayerUI(state, playerIndex);
        Debug.Log($"Player {playerIndex + 1} joined!");
    }

    private void ChangeCharacter(PlayerState state, int playerIndex, int direction)
    {
        if (availableCharacters == null || availableCharacters.Length == 0)
        {
            Debug.LogWarning("No available characters assigned!");
            return;
        }

        state.selectedCharacterIndex = (state.selectedCharacterIndex + direction + availableCharacters.Length) % availableCharacters.Length;
        UpdatePlayerUI(state, playerIndex);
        Debug.Log($"Player {playerIndex + 1} selected: {availableCharacters[state.selectedCharacterIndex].name}");
    }

    private void ConfirmSelection(PlayerState state, int playerIndex)
    {
        if (availableCharacters == null || availableCharacters.Length == 0)
        {
            Debug.LogWarning("No available characters assigned!");
            return;
        }

        state.isReady = true;
        UpdatePlayerUI(state, playerIndex);
        Debug.Log($"Player {playerIndex + 1} confirmed: {availableCharacters[state.selectedCharacterIndex].name}");
    }

    private void CancelReady(PlayerState state, int playerIndex)
    {
        state.isReady = false;
        UpdatePlayerUI(state, playerIndex);
        Debug.Log($"Player {playerIndex + 1} cancelled ready state");
    }

    private void UpdatePlayerUI(PlayerState state, int playerIndex)
    {
        if (!state.isJoined)
        {
            // Show join prompt
            if (state.playerLabel != null)
            {
                string joinPrompt = playerIndex == 0 ? "GAMEPAD 1 or SPACE" : "GAMEPAD 2 or ENTER";
                state.playerLabel.text = $"PLAYER {playerIndex + 1} - PRESS {joinPrompt} TO JOIN";
            }
            else
            {
                string joinPrompt = playerIndex == 0 ? "GAMEPAD 1 or SPACE" : "GAMEPAD 2 or ENTER";
                Debug.Log($"Player {playerIndex + 1} - Press {joinPrompt} to join!");
            }
            return;
        }

        if (state.playerLabel != null)
        {
            string status = state.isReady ? "READY!" : "SELECT CHARACTER";
            state.playerLabel.text = $"PLAYER {playerIndex + 1} - {status}";
        }
        else
        {
            string status = state.isReady ? "READY!" : "SELECT CHARACTER";
            Debug.Log($"Player {playerIndex + 1} - {status}");
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
                string controls = GetControlsText(playerIndex);
                state.statusText.text = state.isReady ? "READY!" : controls;
            }

            // Update character portraits
            for (int i = 0; i < state.characterPortraits.Length && i < availableCharacters.Length; i++)
            {
                if (state.characterPortraits[i] != null && availableCharacters[i].portrait != null)
                {
                    state.characterPortraits[i].sprite = availableCharacters[i].portrait;

                    // Highlight selected character
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
            return Gamepad.all.Count > 0 ? "D-Pad/Stick to select, A to confirm, B to cancel"
                                        : "A/D to select, SPACE to confirm, ESC to cancel";
        }
        else
        {
            return Gamepad.all.Count > 1 ? "D-Pad/Stick to select, A to confirm, B to cancel"
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

        PlayerPrefs.SetString("Player0InputMethod", player1InputMethod);
        PlayerPrefs.SetString("Player1InputMethod", player2InputMethod);
        Debug.Log("=== STARTING GAME ===");

        // Store selected characters
        SelectedCharacters.Clear();

        if (player1State.isJoined && player1State.isReady)
        {
            SelectedCharacters[0] = availableCharacters[player1State.selectedCharacterIndex].type;
            Debug.Log($"STORED: Player 1 = {availableCharacters[player1State.selectedCharacterIndex].name} ({availableCharacters[player1State.selectedCharacterIndex].type})");
        }

        if (player2State.isJoined && player2State.isReady)
        {
            SelectedCharacters[1] = availableCharacters[player2State.selectedCharacterIndex].type;
            Debug.Log($"STORED: Player 2 = {availableCharacters[player2State.selectedCharacterIndex].name} ({availableCharacters[player2State.selectedCharacterIndex].type})");
        }

        Debug.Log($"TOTAL STORED CHARACTERS: {SelectedCharacters.Count}");
        foreach (var kvp in SelectedCharacters)
        {
            Debug.Log($"  Player {kvp.Key}: {kvp.Value}");
        }

        // BACKUP: Store in PlayerPrefs as fallback
        PlayerPrefs.SetInt("PlayerCount", SelectedCharacters.Count);
        Debug.Log($"BACKUP: Stored PlayerCount = {SelectedCharacters.Count}");

        foreach (var kvp in SelectedCharacters)
        {
            PlayerPrefs.SetString($"Player{kvp.Key}Character", kvp.Value.ToString());
            Debug.Log($"BACKUP STORED: Player{kvp.Key}Character = {kvp.Value}");
        }
        PlayerPrefs.Save();
        Debug.Log("PlayerPrefs saved successfully");

        // Ensure time scale is normal
        Time.timeScale = 1f;

        // Load gameplay scene
        SceneManager.LoadScene(gameplaySceneName);
    }
}