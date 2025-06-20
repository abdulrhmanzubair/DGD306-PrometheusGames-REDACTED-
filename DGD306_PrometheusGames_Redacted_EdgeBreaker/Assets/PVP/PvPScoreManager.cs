using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

// Add this component to player GameObjects for identification
public class PlayerIdentifier : MonoBehaviour
{
    public int playerNumber; // 1 or 2
    public PvPScoreManager scoreManager;

    void Start()
    {
        if (scoreManager == null)
            scoreManager = FindObjectOfType<PvPScoreManager>();

        // Auto-register this player
        if (scoreManager != null)
            scoreManager.RegisterPlayer(playerNumber, gameObject);
    }

    // Easy scoring methods you can call directly on the player
    public void AddScore(int points = 1)
    {
        if (scoreManager != null)
            scoreManager.AddScore(playerNumber, points);
    }

    public void RecordKill(PlayerIdentifier victim)
    {
        if (scoreManager != null && victim != null)
            scoreManager.AddKill(playerNumber, victim.playerNumber);
    }

    public void RecordDeath(PlayerIdentifier killer)
    {
        if (scoreManager != null && killer != null)
            scoreManager.AddKill(killer.playerNumber, playerNumber);
    }
}

[System.Serializable]
public class Player
{
    public string playerName;
    public int score;
    public int kills;
    public int deaths;
    public int roundsWon;
    public Color playerColor = Color.white;
    public GameObject playerObject; // Reference to the actual player GameObject
    public string playerTag = ""; // Optional tag for identification

    public Player(string name)
    {
        playerName = name;
        ResetStats();
    }

    public void ResetStats()
    {
        score = 0;
        kills = 0;
        deaths = 0;
        roundsWon = 0;
    }

    public float GetKDRatio()
    {
        return deaths > 0 ? (float)kills / deaths : kills;
    }
}

[System.Serializable]
public class ScoreEvents
{
    public UnityEvent<Player> OnScoreChanged;
    public UnityEvent<Player> OnPlayerKill;
    public UnityEvent<Player> OnPlayerDeath;
    public UnityEvent<Player> OnRoundWon;
    public UnityEvent<Player> OnMatchWon;
    public UnityEvent OnMatchReset;
}

public class PvPScoreManager : MonoBehaviour
{
    [Header("Players")]
    public Player player1 = new Player("Player 1");
    public Player player2 = new Player("Player 2");

    [Header("Game Settings")]
    public int scoreToWin = 10;
    public int roundsToWinMatch = 3;
    public bool resetScoreEachRound = false;

    [Header("UI Settings")]
    public bool autoCreateUI = true;
    public Vector2 canvasSize = new Vector2(1920, 1080);
    public int uiFontSize = 24;
    public Font customFont; // Optional custom font

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip scoreSound;
    public AudioClip killSound;
    public AudioClip roundWinSound;
    public AudioClip matchWinSound;

    [Header("Events")]
    public ScoreEvents scoreEvents;

    // UI References (created automatically)
    private Canvas mainCanvas;
    private Text player1NameText;
    private Text player1ScoreText;
    private Text player1KillsText;
    private Text player1DeathsText;
    private Text player1RoundsText;

    private Text player2NameText;
    private Text player2ScoreText;
    private Text player2KillsText;
    private Text player2DeathsText;
    private Text player2RoundsText;

    private Text gameStatusText;
    private GameObject matchEndPanel;
    private Text matchWinnerText;

    // Game State
    private bool gameActive = true;
    private bool matchInProgress = true;
    private Player currentRoundWinner;
    private Player matchWinner;

    void Start()
    {
        if (autoCreateUI)
        {
            CreateUI();
        }
        InitializeGame();
        UpdateUI();
    }

    void CreateUI()
    {
        // Create or find Canvas
        mainCanvas = FindObjectOfType<Canvas>();
        if (mainCanvas == null)
        {
            GameObject canvasGO = new GameObject("PvP Canvas");
            mainCanvas = canvasGO.AddComponent<Canvas>();
            mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // Add Canvas Scaler
            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = canvasSize;
            scaler.matchWidthOrHeight = 0.5f;

            // Add GraphicRaycaster
            canvasGO.AddComponent<GraphicRaycaster>();
        }

        // Create EventSystem if it doesn't exist
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject eventSystemGO = new GameObject("EventSystem");
            eventSystemGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystemGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        CreatePlayerPanels();
        CreateGameStatusUI();
        CreateMatchEndPanel();
    }

    void CreatePlayerPanels()
    {
        // Player 1 Panel (Top Left)
        GameObject p1Panel = CreatePanel("Player1Panel", new Vector2(300, 150),
            new Vector2(-660, 390), mainCanvas.transform);
        p1Panel.GetComponent<Image>().color = new Color(0, 0, 1, 0.3f); // Blue tint

        player1NameText = CreateText("P1Name", player1.playerName, p1Panel.transform,
            new Vector2(0, 50), uiFontSize + 4, FontStyle.Bold);
        player1NameText.color = Color.white;

        player1ScoreText = CreateText("P1Score", "0", p1Panel.transform,
            new Vector2(0, 15), uiFontSize + 8, FontStyle.Bold);
        player1ScoreText.color = Color.white;

        player1KillsText = CreateText("P1Kills", "K: 0", p1Panel.transform,
            new Vector2(-50, -15), uiFontSize - 2);

        player1DeathsText = CreateText("P1Deaths", "D: 0", p1Panel.transform,
            new Vector2(50, -15), uiFontSize - 2);

        player1RoundsText = CreateText("P1Rounds", "R: 0", p1Panel.transform,
            new Vector2(0, -45), uiFontSize);

        // Player 2 Panel (Top Right)
        GameObject p2Panel = CreatePanel("Player2Panel", new Vector2(300, 150),
            new Vector2(660, 390), mainCanvas.transform);
        p2Panel.GetComponent<Image>().color = new Color(1, 0, 0, 0.3f); // Red tint

        player2NameText = CreateText("P2Name", player2.playerName, p2Panel.transform,
            new Vector2(0, 50), uiFontSize + 4, FontStyle.Bold);
        player2NameText.color = Color.white;

        player2ScoreText = CreateText("P2Score", "0", p2Panel.transform,
            new Vector2(0, 15), uiFontSize + 8, FontStyle.Bold);
        player2ScoreText.color = Color.white;

        player2KillsText = CreateText("P2Kills", "K: 0", p2Panel.transform,
            new Vector2(-50, -15), uiFontSize - 2);

        player2DeathsText = CreateText("P2Deaths", "D: 0", p2Panel.transform,
            new Vector2(50, -15), uiFontSize - 2);

        player2RoundsText = CreateText("P2Rounds", "R: 0", p2Panel.transform,
            new Vector2(0, -45), uiFontSize);
    }

    void CreateGameStatusUI()
    {
        // Game Status Text (Center Top)
        gameStatusText = CreateText("GameStatus", "Match Started!", mainCanvas.transform,
            new Vector2(0, 450), uiFontSize + 6, FontStyle.Bold);
        gameStatusText.color = Color.yellow;
        gameStatusText.alignment = TextAnchor.MiddleCenter;
    }

    void CreateMatchEndPanel()
    {
        // Match End Panel (Center)
        matchEndPanel = CreatePanel("MatchEndPanel", new Vector2(500, 300),
            Vector2.zero, mainCanvas.transform);
        matchEndPanel.GetComponent<Image>().color = new Color(0, 0, 0, 0.9f);
        matchEndPanel.SetActive(false);

        // Title Text
        Text titleText = CreateText("MatchEndTitle", "MATCH COMPLETE!", matchEndPanel.transform,
            new Vector2(0, 100), uiFontSize + 14, FontStyle.Bold);
        titleText.color = Color.yellow;
        titleText.alignment = TextAnchor.MiddleCenter;

        // Winner Text
        matchWinnerText = CreateText("WinnerText", "Player Wins!", matchEndPanel.transform,
            new Vector2(0, 50), uiFontSize + 8, FontStyle.Bold);
        matchWinnerText.color = Color.yellow;
        matchWinnerText.alignment = TextAnchor.MiddleCenter;

        // Match Stats Text
        Text statsText = CreateText("StatsText", "", matchEndPanel.transform,
            new Vector2(0, 10), uiFontSize - 2);
        statsText.color = Color.white;
        statsText.alignment = TextAnchor.MiddleCenter;

        // Rematch Button
        Button rematchButton = CreateButton("RematchButton", "PLAY AGAIN", matchEndPanel.transform,
            new Vector2(-80, -50), new Vector2(140, 50));
        rematchButton.onClick.AddListener(RestartMatch);

        // Enhance rematch button appearance
        ColorBlock rematchColors = rematchButton.colors;
        rematchColors.normalColor = new Color(0.2f, 0.8f, 0.2f, 0.8f); // Green
        rematchColors.highlightedColor = new Color(0.3f, 0.9f, 0.3f, 0.9f);
        rematchButton.colors = rematchColors;

        // Main Menu Button
        Button mainMenuButton = CreateButton("MainMenuButton", "MAIN MENU", matchEndPanel.transform,
            new Vector2(80, -50), new Vector2(140, 50));
        mainMenuButton.onClick.AddListener(GoToMainMenu);

        // Enhance main menu button appearance
        ColorBlock mainMenuColors = mainMenuButton.colors;
        mainMenuColors.normalColor = new Color(0.8f, 0.2f, 0.2f, 0.8f); // Red
        mainMenuColors.highlightedColor = new Color(0.9f, 0.3f, 0.3f, 0.9f);
        mainMenuButton.colors = mainMenuColors;

        // Controls hint
        Text controlsText = CreateText("ControlsHint", "ESC - Main Menu | SPACE - Play Again", matchEndPanel.transform,
            new Vector2(0, -100), uiFontSize - 4);
        controlsText.color = Color.gray;
        controlsText.alignment = TextAnchor.MiddleCenter;
    }

    GameObject CreatePanel(string name, Vector2 size, Vector2 position, Transform parent)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent, false);

        RectTransform rect = panel.AddComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        Image image = panel.AddComponent<Image>();
        image.color = new Color(0, 0, 0, 0.5f);

        return panel;
    }

    Text CreateText(string name, string content, Transform parent, Vector2 position,
        int fontSize = 24, FontStyle fontStyle = FontStyle.Normal)
    {
        GameObject textGO = new GameObject(name);
        textGO.transform.SetParent(parent, false);

        RectTransform rect = textGO.AddComponent<RectTransform>();
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(200, 30);

        Text text = textGO.AddComponent<Text>();
        text.text = content;
        text.font = customFont ? customFont : Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;

        return text;
    }

    Button CreateButton(string name, string buttonText, Transform parent, Vector2 position, Vector2 size)
    {
        GameObject buttonGO = new GameObject(name);
        buttonGO.transform.SetParent(parent, false);

        RectTransform rect = buttonGO.AddComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        Image image = buttonGO.AddComponent<Image>();
        image.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

        Button button = buttonGO.AddComponent<Button>();

        // Button text
        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(buttonGO.transform, false);

        RectTransform textRect = textGO.AddComponent<RectTransform>();
        textRect.sizeDelta = size;
        textRect.anchoredPosition = Vector2.zero;

        Text text = textGO.AddComponent<Text>();
        text.text = buttonText;
        text.font = customFont ? customFont : Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = uiFontSize;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;

        return button;
    }

    void InitializeGame()
    {
        // Set player colors if not set
        if (player1.playerColor == Color.white)
            player1.playerColor = Color.cyan;
        if (player2.playerColor == Color.white)
            player2.playerColor = Color.red;

        gameActive = true;
        matchInProgress = true;

        UpdateGameStatus("Match Started!");
    }

    #region Score Management

    public void AddScore(int playerNumber, int points)
    {
        Player player = GetPlayer(playerNumber);
        if (player != null && gameActive)
        {
            player.score += points;
            PlaySound(scoreSound);
            scoreEvents.OnScoreChanged?.Invoke(player);

            UpdateUI();
            CheckForRoundWin();
        }
    }

    public void AddKill(int killerPlayerNumber, int victimPlayerNumber)
    {
        Player killer = GetPlayer(killerPlayerNumber);
        Player victim = GetPlayer(victimPlayerNumber);

        if (killer != null && victim != null && gameActive)
        {
            killer.kills++;
            killer.score += 1; // Add score for kill
            victim.deaths++;

            PlaySound(killSound);
            scoreEvents.OnPlayerKill?.Invoke(killer);
            scoreEvents.OnPlayerDeath?.Invoke(victim);

            UpdateUI();
            CheckForRoundWin();

            Debug.Log($"{killer.playerName} killed {victim.playerName}!");
        }
    }

    public void SetScore(int playerNumber, int newScore)
    {
        Player player = GetPlayer(playerNumber);
        if (player != null && gameActive)
        {
            player.score = newScore;
            scoreEvents.OnScoreChanged?.Invoke(player);
            UpdateUI();
        }
    }

    #endregion

    #region Game Flow

    void CheckForRoundWin()
    {
        if (!gameActive) return;

        Player roundWinner = null;

        if (player1.score >= scoreToWin)
            roundWinner = player1;
        else if (player2.score >= scoreToWin)
            roundWinner = player2;

        if (roundWinner != null)
        {
            EndRound(roundWinner);
        }
    }

    void EndRound(Player winner)
    {
        gameActive = false;
        currentRoundWinner = winner;
        winner.roundsWon++;

        PlaySound(roundWinSound);
        scoreEvents.OnRoundWon?.Invoke(winner);

        UpdateGameStatus($"{winner.playerName} wins the round!");
        UpdateUI();

        // Check for match win
        if (winner.roundsWon >= roundsToWinMatch)
        {
            EndMatch(winner);
        }
        else
        {
            // Start next round after delay
            StartCoroutine(StartNextRoundDelay());
        }
    }

    void EndMatch(Player winner)
    {
        matchInProgress = false;
        matchWinner = winner;

        PlaySound(matchWinSound);
        scoreEvents.OnMatchWon?.Invoke(winner);

        UpdateGameStatus($"{winner.playerName} wins the match!");

        if (matchEndPanel)
        {
            matchEndPanel.SetActive(true);
            if (matchWinnerText)
                matchWinnerText.text = $"{winner.playerName} WINS!";

            // Update match statistics
            UpdateMatchStats();
        }
    }

    void UpdateMatchStats()
    {
        Text statsText = matchEndPanel.transform.Find("StatsText")?.GetComponent<Text>();
        if (statsText != null)
        {
            string stats = $"Final Score: {player1.playerName} {player1.roundsWon} - {player2.roundsWon} {player2.playerName}\n";
            stats += $"K/D: {player1.playerName} {player1.kills}/{player1.deaths} - {player2.playerName} {player2.kills}/{player2.deaths}";
            statsText.text = stats;
        }
    }

    // NEW: Restart match (play again)
    public void RestartMatch()
    {
        Debug.Log("Restarting match...");

        // Reset all player stats
        player1.ResetStats();
        player2.ResetStats();

        // Reset game state
        gameActive = true;
        matchInProgress = true;
        currentRoundWinner = null;
        matchWinner = null;

        // Hide match end panel
        if (matchEndPanel)
            matchEndPanel.SetActive(false);

        // Update UI and status
        UpdateGameStatus("New Match Started!");
        UpdateUI();

        // Trigger reset event
        scoreEvents.OnMatchReset?.Invoke();

        Debug.Log("Match restarted successfully!");
    }

    // NEW: Go to main menu
    public void GoToMainMenu()
    {
        Debug.Log("Going to main menu...");

        // Try to load main menu scene
        try
        {
            // Common main menu scene names
            if (Application.CanStreamedLevelBeLoaded("MainMenu"))
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
            }
            else if (Application.CanStreamedLevelBeLoaded("Main"))
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("Main");
            }
            else if (Application.CanStreamedLevelBeLoaded("Menu"))
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("Menu");
            }
            else
            {
                // Fallback: Load first scene in build settings
                UnityEngine.SceneManagement.SceneManager.LoadScene(0);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Could not load main menu scene: {e.Message}");
            Debug.LogWarning("Add your main menu scene to Build Settings or set the correct scene name.");

            // Alternative: Restart current scene as fallback
            RestartMatch();
        }
    }

    IEnumerator StartNextRoundDelay()
    {
        yield return new WaitForSeconds(3f);
        StartNextRound();
    }

    public void StartNextRound()
    {
        if (resetScoreEachRound)
        {
            player1.score = 0;
            player2.score = 0;
        }

        gameActive = true;
        UpdateGameStatus("Next round started!");
        UpdateUI();
    }

    // Legacy method for backwards compatibility
    public void ResetMatch()
    {
        RestartMatch();
    }

    #endregion

    #region Debug Methods

    void HandleDebugControls()
    {
        // Debug controls (remove in production)
        if (Input.GetKeyDown(KeyCode.Alpha1))
            Player1Score();
        if (Input.GetKeyDown(KeyCode.Alpha2))
            Player2Score();
        if (Input.GetKeyDown(KeyCode.Q))
            Player1Kill();
        if (Input.GetKeyDown(KeyCode.W))
            Player2Kill();
        if (Input.GetKeyDown(KeyCode.R))
            RestartMatch();
    }

    #region UI Management

    void UpdateUI()
    {
        if (!autoCreateUI) return;

        // Player 1 UI
        if (player1NameText) player1NameText.text = player1.playerName;
        if (player1ScoreText) player1ScoreText.text = player1.score.ToString();
        if (player1KillsText) player1KillsText.text = $"K: {player1.kills}";
        if (player1DeathsText) player1DeathsText.text = $"D: {player1.deaths}";
        if (player1RoundsText) player1RoundsText.text = $"R: {player1.roundsWon}";

        // Player 2 UI
        if (player2NameText) player2NameText.text = player2.playerName;
        if (player2ScoreText) player2ScoreText.text = player2.score.ToString();
        if (player2KillsText) player2KillsText.text = $"K: {player2.kills}";
        if (player2DeathsText) player2DeathsText.text = $"D: {player2.deaths}";
        if (player2RoundsText) player2RoundsText.text = $"R: {player2.roundsWon}";

        // Color coding for UI elements
        SetUIColors();
    }

    void SetUIColors()
    {
        if (player1ScoreText) player1ScoreText.color = player1.playerColor;
        if (player2ScoreText) player2ScoreText.color = player2.playerColor;
    }

    void UpdateGameStatus(string message)
    {
        if (gameStatusText)
        {
            gameStatusText.text = message;
            StartCoroutine(ClearStatusAfterDelay(3f));
        }
        Debug.Log($"[PvP] {message}");
    }

    IEnumerator ClearStatusAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (gameStatusText && gameActive)
            gameStatusText.text = "";
    }

    #endregion

    #region Player Registration & Identification

    // Register player GameObjects for identification
    public void RegisterPlayer(int playerNumber, GameObject playerGO)
    {
        Player player = GetPlayer(playerNumber);
        if (player != null)
        {
            player.playerObject = playerGO;
            Debug.Log($"Registered {player.playerName} GameObject: {playerGO.name}");
        }
    }

    // Identify which player a GameObject belongs to
    public int IdentifyPlayer(GameObject obj)
    {
        // Method 1: Check PlayerIdentifier component
        PlayerIdentifier identifier = obj.GetComponent<PlayerIdentifier>();
        if (identifier != null)
            return identifier.playerNumber;

        // Method 2: Check registered GameObjects
        if (player1.playerObject == obj)
            return 1;
        if (player2.playerObject == obj)
            return 2;

        // Method 3: Check by tag if set
        if (!string.IsNullOrEmpty(player1.playerTag) && obj.CompareTag(player1.playerTag))
            return 1;
        if (!string.IsNullOrEmpty(player2.playerTag) && obj.CompareTag(player2.playerTag))
            return 2;

        // Method 4: Check by name contains
        string objName = obj.name.ToLower();
        if (objName.Contains("player1") || objName.Contains("p1"))
            return 1;
        if (objName.Contains("player2") || objName.Contains("p2"))
            return 2;

        return 0; // Unknown player
    }

    // Easy collision-based scoring
    public void OnPlayerCollision(GameObject player, GameObject other, int scoreValue = 1)
    {
        int playerNum = IdentifyPlayer(player);
        if (playerNum != 0)
        {
            AddScore(playerNum, scoreValue);
        }
    }

    // Easy kill detection for same-type players
    public void OnPlayerKilled(GameObject killer, GameObject victim)
    {
        int killerNum = IdentifyPlayer(killer);
        int victimNum = IdentifyPlayer(victim);

        if (killerNum != 0 && victimNum != 0 && killerNum != victimNum)
        {
            AddKill(killerNum, victimNum);
        }
    }

    #endregion

    Player GetPlayer(int playerNumber)
    {
        switch (playerNumber)
        {
            case 1: return player1;
            case 2: return player2;
            default:
                Debug.LogWarning($"Invalid player number: {playerNumber}");
                return null;
        }
    }

    public Player GetOpponent(int playerNumber)
    {
        return playerNumber == 1 ? player2 : player1;
    }

    public bool IsGameActive()
    {
        return gameActive && matchInProgress;
    }

    public Player GetLeader()
    {
        if (player1.score > player2.score) return player1;
        if (player2.score > player1.score) return player2;
        return null; // Tie
    }

    public string GetMatchSummary()
    {
        return $"Player 1: {player1.roundsWon} rounds | Player 2: {player2.roundsWon} rounds";
    }

    void PlaySound(AudioClip clip)
    {
        if (audioSource && clip)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    #endregion

    #region Public API Methods (Call these from your game)

    // Easy methods to call from your game code
    public void Player1Score(int points = 1) => AddScore(1, points);
    public void Player2Score(int points = 1) => AddScore(2, points);

    public void Player1Kill() => AddKill(1, 2);
    public void Player2Kill() => AddKill(2, 1);

    public void RecordKill(int killer, int victim) => AddKill(killer, victim);

    // NEW: GameObject-based methods for same-type players
    public void ScoreForPlayer(GameObject player, int points = 1)
    {
        int playerNum = IdentifyPlayer(player);
        if (playerNum != 0)
            AddScore(playerNum, points);
    }

    public void KillRecorded(GameObject killer, GameObject victim)
    {
        OnPlayerKilled(killer, victim);
    }

    // NEW: Component-based methods
    public void ScoreForPlayer(PlayerIdentifier player, int points = 1)
    {
        if (player != null)
            AddScore(player.playerNumber, points);
    }

    public void KillRecorded(PlayerIdentifier killer, PlayerIdentifier victim)
    {
        if (killer != null && victim != null)
            AddKill(killer.playerNumber, victim.playerNumber);
    }

    #endregion

    #region Debug Methods

    void Update()
    {
        // Debug controls (remove in production)
        if (Input.GetKeyDown(KeyCode.Alpha1))
            Player1Score();
        if (Input.GetKeyDown(KeyCode.Alpha2))
            Player2Score();
        if (Input.GetKeyDown(KeyCode.Q))
            Player1Kill();
        if (Input.GetKeyDown(KeyCode.W))
            Player2Kill();
        if (Input.GetKeyDown(KeyCode.R))
            ResetMatch();
    }

    #endregion
}