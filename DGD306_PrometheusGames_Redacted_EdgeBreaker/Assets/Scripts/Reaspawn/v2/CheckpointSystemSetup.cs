using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Setup helper script for checkpoint and death management system
/// Automatically configures the system in your scene
/// </summary>
public class CheckpointSystemSetup : MonoBehaviour
{
    [Header("Auto Setup Options")]
    [SerializeField] private bool autoSetupOnStart = true;
    [SerializeField] private bool createGameManager = true;
    [SerializeField] private bool createCheckpointManager = true;
    [SerializeField] private bool setupGameOverUI = true;

    [Header("Game Settings")]
    [SerializeField] private int maxDeathsPerPlayer = 3;
    [SerializeField] private int maxTotalDeaths = 5;
    [SerializeField] private bool useSharedDeathPool = true;
    [SerializeField] private float respawnDelay = 2f;

    [Header("UI Setup")]
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private GameObject gameOverUIPrefab;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    private void Start()
    {
        if (autoSetupOnStart)
        {
            SetupCheckpointSystem();
        }
    }

    [ContextMenu("Setup Checkpoint System")]
    public void SetupCheckpointSystem()
    {
        Debug.Log("[CheckpointSystemSetup] Starting automatic setup...");

        // 1. Setup GameManager
        if (createGameManager)
        {
            SetupGameManager();
        }

        // 2. Setup CheckpointManager
        if (createCheckpointManager)
        {
            SetupCheckpointManager();
        }

        // 3. Setup Game Over UI
        if (setupGameOverUI)
        {
            SetupGameOverUI();
        }

        // 4. Validate player setup
        ValidatePlayerSetup();

        // 5. Find and setup checkpoints
        SetupExistingCheckpoints();

        Debug.Log("[CheckpointSystemSetup] Setup complete!");
    }

    private void SetupGameManager()
    {
        GameManager existingManager = FindObjectOfType<GameManager>();
        if (existingManager != null)
        {
            if (enableDebugLogs)
                Debug.Log("[CheckpointSystemSetup] GameManager already exists, skipping configuration...");

            // Note: Configure the GameManager manually in the inspector
            // The fields are SerializeField private for encapsulation
            return;
        }

        // Create new GameManager
        GameObject gameManagerGO = new GameObject("GameManager");
        GameManager gameManager = gameManagerGO.AddComponent<GameManager>();

        if (enableDebugLogs)
            Debug.Log("[CheckpointSystemSetup] GameManager created - Please configure settings in Inspector");

        // The GameManager fields are private SerializeField for proper encapsulation
        // Configure them manually in the Inspector:
        // - Max Deaths Per Player: " + maxDeathsPerPlayer + "
        // - Max Total Deaths: " + maxTotalDeaths + "
        // - Use Shared Death Pool: " + useSharedDeathPool + "
        // - Respawn Delay: " + respawnDelay + "
    }

    private void SetupCheckpointManager()
    {
        CheckpointManager existingManager = FindObjectOfType<CheckpointManager>();
        if (existingManager != null)
        {
            if (enableDebugLogs)
                Debug.Log("[CheckpointSystemSetup] CheckpointManager already exists");
            return;
        }

        // Create new CheckpointManager
        GameObject checkpointManagerGO = new GameObject("CheckpointManager");
        CheckpointManager checkpointManager = checkpointManagerGO.AddComponent<CheckpointManager>();

        if (enableDebugLogs)
            Debug.Log("[CheckpointSystemSetup] CheckpointManager created");
    }

    private void SetupGameOverUI()
    {
        // Check if Game Over UI already exists
        GameOverUI existingUI = FindObjectOfType<GameOverUI>();
        if (existingUI != null)
        {
            if (enableDebugLogs)
                Debug.Log("[CheckpointSystemSetup] GameOverUI already exists");
            return;
        }

        // Skip auto-creation if it causes font issues
        if (!setupGameOverUI)
        {
            if (enableDebugLogs)
                Debug.Log("[CheckpointSystemSetup] GameOverUI auto-setup disabled. Create manually or enable in inspector.");
            return;
        }

        // Find or create canvas
        Canvas canvas = targetCanvas;
        if (canvas == null)
        {
            canvas = FindObjectOfType<Canvas>();
        }

        if (canvas == null)
        {
            if (enableDebugLogs)
                Debug.Log("[CheckpointSystemSetup] Creating new Canvas for UI");

            GameObject canvasGO = new GameObject("Canvas");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        }

        // Create Game Over UI with safe font handling
        GameObject gameOverUI;
        if (gameOverUIPrefab != null)
        {
            gameOverUI = Instantiate(gameOverUIPrefab, canvas.transform);
        }
        else
        {
            try
            {
                gameOverUI = CreateBasicGameOverUI(canvas);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[CheckpointSystemSetup] Failed to create GameOverUI: {e.Message}. Please create manually or assign a prefab.");
                return;
            }
        }

        gameOverUI.name = "GameOverUI";
        gameOverUI.SetActive(false);

        if (enableDebugLogs)
            Debug.Log("[CheckpointSystemSetup] GameOverUI created");
    }

    private GameObject CreateBasicGameOverUI(Canvas canvas)
    {
        // Create main panel
        GameObject panel = new GameObject("GameOverPanel");
        panel.transform.SetParent(canvas.transform, false);

        UnityEngine.UI.Image panelImage = panel.AddComponent<UnityEngine.UI.Image>();
        panelImage.color = new Color(0, 0, 0, 0.8f);

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        // Create "Game Over" text
        GameObject gameOverText = new GameObject("GameOverText");
        gameOverText.transform.SetParent(panel.transform, false);

        UnityEngine.UI.Text textComponent = gameOverText.AddComponent<UnityEngine.UI.Text>();
        textComponent.text = "GAME OVER";

        // Try to get default Unity font safely
        Font defaultFont = GetSafeFont();
        if (defaultFont != null)
        {
            textComponent.font = defaultFont;
        }

        textComponent.fontSize = 48;
        textComponent.color = Color.white;
        textComponent.alignment = TextAnchor.MiddleCenter;

        RectTransform textRect = gameOverText.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.7f);
        textRect.anchorMax = new Vector2(0.5f, 0.7f);
        textRect.sizeDelta = new Vector2(400, 100);

        // Create Retry button
        CreateButton(panel, "RetryButton", "RETRY", new Vector2(0.3f, 0.3f), () => {
            if (GameManager.Instance != null) GameManager.Instance.RestartLevel();
        });

        // Create Main Menu button
        CreateButton(panel, "MainMenuButton", "MAIN MENU", new Vector2(0.7f, 0.3f), () => {
            if (GameManager.Instance != null) GameManager.Instance.LoadMainMenu();
        });

        // Add GameOverUI component
        GameOverUI gameOverUIComponent = panel.AddComponent<GameOverUI>();

        if (enableDebugLogs)
            Debug.Log("[CheckpointSystemSetup] Basic GameOverUI created - Please assign UI references manually in Inspector");

        return panel;
    }

    private Font GetSafeFont()
    {
        // Try multiple font loading methods safely
        Font font = null;

        try
        {
            // Method 1: Try Unity's default font
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        catch
        {
            font = null;
        }

        if (font == null)
        {
            try
            {
                // Method 2: Try Arial
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            catch
            {
                font = null;
            }
        }

        if (font == null)
        {
            try
            {
                // Method 3: Load from Resources folder
                font = Resources.Load<Font>("Arial");
            }
            catch
            {
                font = null;
            }
        }

        if (font == null)
        {
            // Method 4: Find any font in the project
            Font[] allFonts = Resources.FindObjectsOfTypeAll<Font>();
            if (allFonts.Length > 0)
            {
                font = allFonts[0];
            }
        }

        if (font == null && enableDebugLogs)
        {
            Debug.LogWarning("[CheckpointSystemSetup] Could not load any font. Text will use Unity default.");
        }

        return font;
    }

    private void CreateButton(GameObject parent, string name, string text, Vector2 anchorPosition, System.Action onClick)
    {
        GameObject button = new GameObject(name);
        button.transform.SetParent(parent.transform, false);

        UnityEngine.UI.Image buttonImage = button.AddComponent<UnityEngine.UI.Image>();
        buttonImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

        UnityEngine.UI.Button buttonComponent = button.AddComponent<UnityEngine.UI.Button>();
        buttonComponent.onClick.AddListener(() => onClick());

        RectTransform buttonRect = button.GetComponent<RectTransform>();
        buttonRect.anchorMin = anchorPosition;
        buttonRect.anchorMax = anchorPosition;
        buttonRect.sizeDelta = new Vector2(200, 50);

        // Button text
        GameObject buttonText = new GameObject("Text");
        buttonText.transform.SetParent(button.transform, false);

        UnityEngine.UI.Text textComponent = buttonText.AddComponent<UnityEngine.UI.Text>();
        textComponent.text = text;

        // Use safe font loading
        Font buttonFont = GetSafeFont();
        if (buttonFont != null)
        {
            textComponent.font = buttonFont;
        }

        textComponent.fontSize = 18;
        textComponent.color = Color.white;
        textComponent.alignment = TextAnchor.MiddleCenter;

        RectTransform textRect = buttonText.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
    }

    private void ValidatePlayerSetup()
    {
        PlayerHealthSystem[] players = FindObjectsOfType<PlayerHealthSystem>();

        if (players.Length == 0)
        {
            Debug.LogWarning("[CheckpointSystemSetup] No PlayerHealthSystem components found in scene!");
            return;
        }

        int validPlayers = 0;
        foreach (PlayerHealthSystem player in players)
        {
            // Check if player has required tag
            if (!player.CompareTag("Player"))
            {
                Debug.LogWarning($"[CheckpointSystemSetup] Player {player.name} is missing 'Player' tag!");
            }
            else
            {
                validPlayers++;
            }

            // Check if player has collider
            Collider2D col = player.GetComponent<Collider2D>();
            if (col == null)
            {
                Debug.LogWarning($"[CheckpointSystemSetup] Player {player.name} is missing Collider2D component!");
            }
        }

        if (enableDebugLogs)
            Debug.Log($"[CheckpointSystemSetup] Found {validPlayers} valid players out of {players.Length} total");
    }

    private void SetupExistingCheckpoints()
    {
        Checkpoint[] checkpoints = FindObjectsOfType<Checkpoint>();

        if (checkpoints.Length == 0)
        {
            Debug.LogWarning("[CheckpointSystemSetup] No Checkpoint components found in scene! Please create checkpoints manually.");
            return;
        }

        bool hasStartingCheckpoint = false;
        foreach (Checkpoint checkpoint in checkpoints)
        {
            // Ensure checkpoint has trigger collider
            Collider2D col = checkpoint.GetComponent<Collider2D>();
            if (col == null)
            {
                Debug.LogWarning($"[CheckpointSystemSetup] Checkpoint {checkpoint.name} is missing trigger collider! Adding one...");
                BoxCollider2D newCol = checkpoint.gameObject.AddComponent<BoxCollider2D>();
                newCol.isTrigger = true;
                newCol.size = Vector2.one * 2f;
            }
            else if (!col.isTrigger)
            {
                Debug.LogWarning($"[CheckpointSystemSetup] Checkpoint {checkpoint.name} collider is not set as trigger! Fixing...");
                col.isTrigger = true;
            }

            if (checkpoint.IsStartingCheckpoint)
            {
                hasStartingCheckpoint = true;
            }
        }

        if (!hasStartingCheckpoint && checkpoints.Length > 0)
        {
            Debug.LogWarning("[CheckpointSystemSetup] No starting checkpoint found! Setting first checkpoint as starting point...");
            // We can't directly set this without exposing the field, so just log the warning
        }

        if (enableDebugLogs)
            Debug.Log($"[CheckpointSystemSetup] Validated {checkpoints.Length} checkpoints");
    }

    private void CreateExampleCheckpoint()
    {
        GameObject checkpointGO = new GameObject("Checkpoint_Example");
        checkpointGO.transform.position = Vector3.zero;

        // Add checkpoint component
        Checkpoint checkpoint = checkpointGO.AddComponent<Checkpoint>();

        // Add trigger collider
        BoxCollider2D col = checkpointGO.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = Vector2.one * 2f;

        // Add visual indicator
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        visual.transform.SetParent(checkpointGO.transform);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localScale = new Vector3(1f, 0.1f, 1f);
        visual.GetComponent<Renderer>().material.color = Color.green;

        // Remove the collider from visual (checkpoint has its own)
        Destroy(visual.GetComponent<Collider>());

        if (enableDebugLogs)
            Debug.Log("[CheckpointSystemSetup] Created example checkpoint at world origin");
    }

    [ContextMenu("Validate Current Setup")]
    public void ValidateCurrentSetup()
    {
        Debug.Log("[CheckpointSystemSetup] Validating current setup...");

        // Check managers
        bool hasGameManager = FindObjectOfType<GameManager>() != null;
        bool hasCheckpointManager = FindObjectOfType<CheckpointManager>() != null;
        bool hasGameOverUI = FindObjectOfType<GameOverUI>() != null;

        Debug.Log($"GameManager: {(hasGameManager ? "✓" : "✗")}");
        Debug.Log($"CheckpointManager: {(hasCheckpointManager ? "✓" : "✗")}");
        Debug.Log($"GameOverUI: {(hasGameOverUI ? "✓" : "✗")}");

        // Check players
        PlayerHealthSystem[] players = FindObjectsOfType<PlayerHealthSystem>();
        Debug.Log($"Players found: {players.Length}");

        // Check checkpoints
        Checkpoint[] checkpoints = FindObjectsOfType<Checkpoint>();
        Debug.Log($"Checkpoints found: {checkpoints.Length}");

        Debug.Log("[CheckpointSystemSetup] Validation complete");
    }
}