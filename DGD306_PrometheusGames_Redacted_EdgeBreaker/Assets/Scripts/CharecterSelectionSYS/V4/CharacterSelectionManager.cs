using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Collections;

public class CharacterSelectionManager : MonoBehaviour
{
    [Header("Character Data")]
    [SerializeField] private CharacterData[] availableCharacters;

    [Header("UI References")]
    [SerializeField] private Transform player1SelectionUI;
    [SerializeField] private Transform player2SelectionUI;
    [SerializeField] private GameObject readyPrompt;
    [SerializeField] private string gameplaySceneName = "Level1";

    [Header("Selection Visuals")]
    [SerializeField] private Color selectedColor = Color.yellow;
    [SerializeField] private Color unselectedColor = Color.white;
    [SerializeField] private Color readyColor = Color.green;

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
    }

    private void Awake()
    {
        DebugLog("=== CHARACTER SELECTION MANAGER AWAKENING ===");

        // Check if PlayerInputManager exists
        if (PlayerInputManager.instance == null)
        {
            Debug.LogError("PlayerInputManager.instance is NULL! Make sure you have a PlayerInputManager in the scene.");
            return;
        }

        DebugLog($"PlayerInputManager found. Max players: {PlayerInputManager.instance.maxPlayerCount}");

        // Enable joining for new players
        PlayerInputManager.instance.EnableJoining();
        PlayerInputManager.instance.onPlayerJoined += OnPlayerJoined;
        PlayerInputManager.instance.onPlayerLeft += OnPlayerLeft;

        DebugLog("Player joining enabled. Event listeners attached.");
    }

    private void Start()
    {
        DebugLog("=== CHARACTER SELECTION MANAGER STARTING ===");

        // Validate character data
        if (availableCharacters == null || availableCharacters.Length == 0)
        {
            Debug.LogError("No characters available! Please assign character data in the inspector.");
            return;
        }

        DebugLog($"Available characters: {availableCharacters.Length}");
        for (int i = 0; i < availableCharacters.Length; i++)
        {
            DebugLog($"  Character {i}: {availableCharacters[i].name} ({availableCharacters[i].type})");
        }

        SetupUI();

        if (readyPrompt != null)
        {
            readyPrompt.SetActive(false);
            DebugLog("Ready prompt initialized and hidden.");
        }
        else
        {
            Debug.LogWarning("Ready prompt is not assigned!");
        }

        DebugLog("Character Selection Manager setup complete.");
    }

    private void SetupUI()
    {
        DebugLog("Setting up UI...");

        if (player1SelectionUI == null)
        {
            Debug.LogError("Player1SelectionUI is not assigned!");
        }
        else
        {
            DebugLog("Setting up Player 1 UI...");
            SetupPlayerUI(player1SelectionUI, 0);
        }

        if (player2SelectionUI == null)
        {
            Debug.LogError("Player2SelectionUI is not assigned!");
        }
        else
        {
            DebugLog("Setting up Player 2 UI...");
            SetupPlayerUI(player2SelectionUI, 1);
        }
    }

    private void SetupPlayerUI(Transform uiRoot, int playerIndex)
    {
        if (uiRoot == null)
        {
            Debug.LogError($"UI Root for player {playerIndex} is null!");
            return;
        }

        DebugLog($"Setting up UI for Player {playerIndex + 1}");

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

        // Debug UI component findings
        DebugLog($"  Player {playerIndex + 1} UI Components:");
        DebugLog($"    Character portraits found: {state.characterPortraits.Length}");
        DebugLog($"    Player label: {(state.playerLabel != null ? "Found" : "NOT FOUND")}");
        DebugLog($"    Character name: {(state.characterName != null ? "Found" : "NOT FOUND")}");
        DebugLog($"    Character description: {(state.characterDescription != null ? "Found" : "NOT FOUND")}");
        DebugLog($"    Status text: {(state.statusText != null ? "Found" : "NOT FOUND")}");

        playerStates[playerIndex] = state;
        UpdatePlayerUI(state);

        // Initially inactive until player joins
        uiRoot.gameObject.SetActive(false);
        DebugLog($"  Player {playerIndex + 1} UI initially disabled (waiting for player to join)");
    }

    private void OnPlayerJoined(PlayerInput playerInput)
    {
        int playerIndex = playerInput.playerIndex;
        DebugLog($"=== PLAYER {playerIndex + 1} JOINED ===");
        DebugLog($"  Player device: {playerInput.devices}");
        DebugLog($"  Current action map: {playerInput.currentActionMap?.name}");

        connectedPlayers.Add(playerInput);
        DebugLog($"  Total connected players: {connectedPlayers.Count}");

        if (playerStates.ContainsKey(playerIndex))
        {
            var state = playerStates[playerIndex];
            state.isActive = true;
            state.uiRoot.gameObject.SetActive(true);

            DebugLog($"  Activating UI for Player {playerIndex + 1}");

            // Switch to UI action map for character selection
            try
            {
                playerInput.SwitchCurrentActionMap("UI");
                DebugLog($"  Switched to UI action map successfully");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"  Failed to switch to UI action map: {e.Message}");
                return;
            }

            // Set up input callbacks using your existing UI actions
            var uiActionMap = playerInput.actions.FindActionMap("UI");
            if (uiActionMap != null)
            {
                DebugLog($"  UI action map found. Setting up input callbacks...");

                var navigateAction = uiActionMap.FindAction("Navigate");
                var submitAction = uiActionMap.FindAction("Submit");
                var cancelAction = uiActionMap.FindAction("Cancel");

                if (navigateAction != null)
                {
                    navigateAction.performed += (ctx) => OnNavigate(playerIndex, ctx);
                    DebugLog($"    Navigate action bound");
                }
                else
                {
                    Debug.LogError($"    Navigate action NOT FOUND in UI action map!");
                }

                if (submitAction != null)
                {
                    submitAction.performed += (ctx) => OnSelect(playerIndex);
                    DebugLog($"    Submit action bound");
                }
                else
                {
                    Debug.LogError($"    Submit action NOT FOUND in UI action map!");
                }

                if (cancelAction != null)
                {
                    cancelAction.performed += (ctx) => OnBack(playerIndex);
                    DebugLog($"    Cancel action bound");
                }
                else
                {
                    Debug.LogError($"    Cancel action NOT FOUND in UI action map!");
                }
            }
            else
            {
                Debug.LogError($"  UI action map NOT FOUND! Available action maps:");
                foreach (var map in playerInput.actions.actionMaps)
                {
                    Debug.LogError($"    - {map.name}");
                }
            }

            UpdatePlayerUI(state);
            DebugLog($"  Player {playerIndex + 1} setup complete");
        }
        else
        {
            Debug.LogError($"  No UI state found for player index {playerIndex}!");
        }
    }

    private void OnPlayerLeft(PlayerInput playerInput)
    {
        int playerIndex = playerInput.playerIndex;
        DebugLog($"=== PLAYER {playerIndex + 1} LEFT ===");

        connectedPlayers.Remove(playerInput);
        DebugLog($"  Remaining connected players: {connectedPlayers.Count}");

        if (playerStates.ContainsKey(playerIndex))
        {
            var state = playerStates[playerIndex];
            state.isActive = false;
            state.isReady = false;
            state.uiRoot.gameObject.SetActive(false);
            DebugLog($"  Player {playerIndex + 1} UI deactivated");
        }

        UpdateReadyState();
    }

    private void OnNavigate(int playerIndex, InputAction.CallbackContext context)
    {
        DebugLog($"Player {playerIndex + 1} navigating...");

        if (!playerStates.ContainsKey(playerIndex) || playerStates[playerIndex].isReady)
        {
            DebugLog($"  Navigation ignored (not found or already ready)");
            return;
        }

        var state = playerStates[playerIndex];
        Vector2 input = context.ReadValue<Vector2>();

        DebugLog($"  Navigation input: {input}");

        if (Mathf.Abs(input.x) > 0.5f)
        {
            int direction = input.x > 0 ? 1 : -1;
            int oldIndex = state.selectedCharacterIndex;
            state.selectedCharacterIndex = (state.selectedCharacterIndex + direction + availableCharacters.Length) % availableCharacters.Length;

            DebugLog($"  Character selection changed from {oldIndex} to {state.selectedCharacterIndex}");
            DebugLog($"  Now selecting: {availableCharacters[state.selectedCharacterIndex].name}");

            UpdatePlayerUI(state);
        }
    }

    private void OnSelect(int playerIndex)
    {
        DebugLog($"Player {playerIndex + 1} pressed SELECT");

        if (!playerStates.ContainsKey(playerIndex))
        {
            DebugLog($"  Selection ignored (player state not found)");
            return;
        }

        var state = playerStates[playerIndex];

        if (!state.isReady)
        {
            // Confirm selection
            state.isReady = true;
            DebugLog($"  Player {playerIndex + 1} confirmed character: {availableCharacters[state.selectedCharacterIndex].name}");
            UpdatePlayerUI(state);
            UpdateReadyState();
        }
        else
        {
            DebugLog($"  Player {playerIndex + 1} is already ready");
        }
    }

    private void OnBack(int playerIndex)
    {
        DebugLog($"Player {playerIndex + 1} pressed BACK/CANCEL");

        if (!playerStates.ContainsKey(playerIndex))
        {
            DebugLog($"  Cancel ignored (player state not found)");
            return;
        }

        var state = playerStates[playerIndex];

        if (state.isReady)
        {
            // Cancel ready state
            state.isReady = false;
            DebugLog($"  Player {playerIndex + 1} cancelled ready state");
            UpdatePlayerUI(state);
            UpdateReadyState();
        }
        else
        {
            DebugLog($"  Player {playerIndex + 1} was not ready, nothing to cancel");
        }
    }

    private void UpdatePlayerUI(PlayerSelectionState state)
    {
        DebugLog($"Updating UI for Player {state.playerIndex + 1}");

        if (state.playerLabel != null)
        {
            string status = state.isActive ? (state.isReady ? "READY" : "SELECT CHARACTER") : "PRESS ANY BUTTON TO JOIN";
            state.playerLabel.text = $"PLAYER {state.playerIndex + 1} - {status}";
        }

        if (state.isActive && availableCharacters != null && availableCharacters.Length > 0)
        {
            var selectedCharacter = availableCharacters[state.selectedCharacterIndex];

            if (state.characterName != null)
                state.characterName.text = selectedCharacter.name.ToUpper();

            if (state.characterDescription != null)
                state.characterDescription.text = selectedCharacter.description;

            DebugLog($"  Displaying character: {selectedCharacter.name}");

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

    private void UpdateReadyState()
    {
        DebugLog("=== CHECKING READY STATE ===");

        bool allPlayersReady = false;
        int activePlayers = 0;
        int readyPlayers = 0;

        foreach (var kvp in playerStates)
        {
            if (kvp.Value.isActive)
            {
                activePlayers++;
                if (kvp.Value.isReady)
                {
                    readyPlayers++;
                }
                DebugLog($"  Player {kvp.Key + 1}: Active={kvp.Value.isActive}, Ready={kvp.Value.isReady}");
            }
        }

        DebugLog($"  Active players: {activePlayers}, Ready players: {readyPlayers}");

        // Check if all active players are ready
        if (activePlayers > 0)
        {
            allPlayersReady = (readyPlayers == activePlayers);
        }

        DebugLog($"  All players ready: {allPlayersReady}");

        if (readyPrompt != null)
        {
            readyPrompt.SetActive(allPlayersReady);
            if (allPlayersReady)
            {
                DebugLog("  READY PROMPT ACTIVATED - Starting countdown...");
            }
        }

        if (allPlayersReady)
        {
            StartCoroutine(StartGameCountdown());
        }
    }

    private IEnumerator StartGameCountdown()
    {
        DebugLog("=== GAME COUNTDOWN STARTED ===");
        yield return new WaitForSeconds(1f);

        // Check if still all ready (in case someone cancelled)
        bool stillAllReady = true;
        int activePlayers = 0;

        foreach (var kvp in playerStates)
        {
            if (kvp.Value.isActive)
            {
                activePlayers++;
                if (!kvp.Value.isReady)
                {
                    stillAllReady = false;
                    break;
                }
            }
        }

        DebugLog($"After countdown - Still all ready: {stillAllReady}, Active players: {activePlayers}");

        if (stillAllReady && activePlayers > 0)
        {
            DebugLog("CONDITIONS MET - Starting game!");
            StartGame();
        }
        else
        {
            DebugLog("CONDITIONS NOT MET - Countdown cancelled");
        }
    }

    private void StartGame()
    {
        DebugLog("=== STARTING GAME ===");

        // Store selected characters for the gameplay scene
        SelectedCharacters.Clear();

        foreach (var kvp in playerStates)
        {
            if (kvp.Value.isActive && kvp.Value.isReady)
            {
                var selectedCharacter = availableCharacters[kvp.Value.selectedCharacterIndex];
                SelectedCharacters[kvp.Key] = selectedCharacter.type;
                DebugLog($"  Player {kvp.Key + 1} selected: {selectedCharacter.name} ({selectedCharacter.type})");
            }
        }

        DebugLog($"Total selected characters: {SelectedCharacters.Count}");

        // Switch all players back to Player action map before transitioning
        foreach (var playerInput in connectedPlayers)
        {
            try
            {
                playerInput.SwitchCurrentActionMap("Player");
                DebugLog($"  Switched Player {playerInput.playerIndex + 1} to Player action map");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"  Failed to switch Player {playerInput.playerIndex + 1} to Player action map: {e.Message}");
            }
        }

        // Ensure game is not paused and time scale is normal
        Time.timeScale = 1f;

        // Disable joining before switching scenes
        if (PlayerInputManager.instance != null)
        {
            PlayerInputManager.instance.DisableJoining();
            DebugLog("  Player joining disabled");
        }

        // Load gameplay scene
        DebugLog($"  Loading scene: {gameplaySceneName}");
        SceneManager.LoadScene(gameplaySceneName);
    }

    private void OnDestroy()
    {
        DebugLog("=== CHARACTER SELECTION MANAGER DESTROYED ===");

        if (PlayerInputManager.instance != null)
        {
            PlayerInputManager.instance.onPlayerJoined -= OnPlayerJoined;
            PlayerInputManager.instance.onPlayerLeft -= OnPlayerLeft;
            DebugLog("Event listeners removed");
        }
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogging)
        {
            Debug.Log($"[CharacterSelection] {message}");
        }
    }
}