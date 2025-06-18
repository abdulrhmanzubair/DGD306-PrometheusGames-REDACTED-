using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Player count selection screen - Choose 1 or 2 players before character selection
/// </summary>
public class PlayerCountSelector : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject playerCountUI;
    [SerializeField] private Button onePlayerButton;
    [SerializeField] private Button twoPlayerButton;
    [SerializeField] private Text instructionText;
    [SerializeField] private Text selectedCountText;

    [Header("Visual Feedback")]
    [SerializeField] private Color selectedColor = Color.green;
    [SerializeField] private Color unselectedColor = Color.white;
    [SerializeField] private Color highlightColor = Color.yellow;

    [Header("Character Selection Integration")]
    [SerializeField] private SimplifiedCharacterSelection characterSelection;

    private int selectedPlayerCount = 1;
    private int currentSelection = 0; // 0 = 1 player, 1 = 2 players
    private bool hasConfirmed = false;

    void Start()
    {
        // Show player count selection first
        ShowPlayerCountSelection();

        // Hide character selection initially
        if (characterSelection != null)
        {
            characterSelection.gameObject.SetActive(false);
        }

        // Setup button events
        if (onePlayerButton != null)
        {
            onePlayerButton.onClick.AddListener(() => SelectPlayerCount(1));
        }
        if (twoPlayerButton != null)
        {
            twoPlayerButton.onClick.AddListener(() => SelectPlayerCount(2));
        }

        UpdateUI();
    }

    void Update()
    {
        if (hasConfirmed) return;

        HandleInput();
        UpdateUI();
    }

    void HandleInput()
    {
        // Handle gamepad/keyboard navigation
        bool leftPressed = false;
        bool rightPressed = false;
        bool confirmPressed = false;

        // Check keyboard input
        if (Keyboard.current != null)
        {
            leftPressed = Keyboard.current.leftArrowKey.wasPressedThisFrame ||
                         Keyboard.current.aKey.wasPressedThisFrame;
            rightPressed = Keyboard.current.rightArrowKey.wasPressedThisFrame ||
                          Keyboard.current.dKey.wasPressedThisFrame;
            confirmPressed = Keyboard.current.spaceKey.wasPressedThisFrame ||
                           Keyboard.current.enterKey.wasPressedThisFrame;
        }

        // Check gamepad input (any gamepad)
        foreach (var gamepad in Gamepad.all)
        {
            leftPressed |= gamepad.dpad.left.wasPressedThisFrame ||
                          gamepad.leftStick.left.wasPressedThisFrame;
            rightPressed |= gamepad.dpad.right.wasPressedThisFrame ||
                           gamepad.leftStick.right.wasPressedThisFrame;
            confirmPressed |= gamepad.buttonSouth.wasPressedThisFrame;
        }

        // Handle navigation
        if (leftPressed && currentSelection > 0)
        {
            currentSelection--;
            selectedPlayerCount = currentSelection + 1;
        }
        else if (rightPressed && currentSelection < 1)
        {
            currentSelection++;
            selectedPlayerCount = currentSelection + 1;
        }

        // Handle confirmation
        if (confirmPressed)
        {
            ConfirmPlayerCount();
        }
    }

    void SelectPlayerCount(int count)
    {
        selectedPlayerCount = count;
        currentSelection = count - 1;
        ConfirmPlayerCount();
    }

    void ConfirmPlayerCount()
    {
        if (hasConfirmed) return;

        hasConfirmed = true;

        Debug.Log($"Player count selected: {selectedPlayerCount}");

        // Store the player count choice
        PlayerPrefs.SetInt("SelectedPlayerCount", selectedPlayerCount);
        PlayerPrefs.Save();

        // Hide player count UI
        if (playerCountUI != null)
        {
            playerCountUI.SetActive(false);
        }

        // Start character selection with the chosen player count
        StartCharacterSelection();
    }

    void StartCharacterSelection()
    {
        if (characterSelection != null)
        {
            characterSelection.gameObject.SetActive(true);

            // Configure character selection for the chosen player count
            ConfigureCharacterSelection();
        }
        else
        {
            Debug.LogError("No SimplifiedCharacterSelection component assigned!");
        }
    }

    void ConfigureCharacterSelection()
    {
        // You can add specific configuration here if needed
        // For example, disable Player 2 UI if solo play is selected

        if (selectedPlayerCount == 1)
        {
            Debug.Log("Configuring for SOLO play");
            // Could hide Player 2 UI elements here
        }
        else
        {
            Debug.Log("Configuring for DUO play");
            // Ensure both player UIs are available
        }
    }

    void ShowPlayerCountSelection()
    {
        if (playerCountUI != null)
        {
            playerCountUI.SetActive(true);
        }
    }

    void UpdateUI()
    {
        // Update instruction text
        if (instructionText != null)
        {
            instructionText.text = "Choose number of players:";
        }

        // Update selected count display
        if (selectedCountText != null)
        {
            selectedCountText.text = $"{selectedPlayerCount} PLAYER{(selectedPlayerCount > 1 ? "S" : "")}";
        }

        // Update button colors
        UpdateButtonVisuals();
    }

    void UpdateButtonVisuals()
    {
        if (onePlayerButton != null)
        {
            var colors = onePlayerButton.colors;
            colors.normalColor = (currentSelection == 0) ? highlightColor : unselectedColor;
            colors.selectedColor = selectedColor;
            onePlayerButton.colors = colors;

            // Update button text color
            var buttonText = onePlayerButton.GetComponentInChildren<Text>();
            if (buttonText != null)
            {
                buttonText.color = (currentSelection == 0) ? Color.black : Color.white;
            }
        }

        if (twoPlayerButton != null)
        {
            var colors = twoPlayerButton.colors;
            colors.normalColor = (currentSelection == 1) ? highlightColor : unselectedColor;
            colors.selectedColor = selectedColor;
            twoPlayerButton.colors = colors;

            // Update button text color
            var buttonText = twoPlayerButton.GetComponentInChildren<Text>();
            if (buttonText != null)
            {
                buttonText.color = (currentSelection == 1) ? Color.black : Color.white;
            }
        }
    }

    // Public method to get the selected player count (for other scripts)
    public static int GetSelectedPlayerCount()
    {
        return PlayerPrefs.GetInt("SelectedPlayerCount", 1);
    }

    // Reset selection (useful for testing)
    [ContextMenu("Reset Player Count Selection")]
    public void ResetSelection()
    {
        hasConfirmed = false;
        selectedPlayerCount = 1;
        currentSelection = 0;

        if (playerCountUI != null)
        {
            playerCountUI.SetActive(true);
        }

        if (characterSelection != null)
        {
            characterSelection.gameObject.SetActive(false);
        }

        UpdateUI();
    }
}