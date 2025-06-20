using UnityEngine;
using UnityEngine.UI;
using System.Text;

/// <summary>
/// Expert Unity Game Over UI implementation
/// Handles all game over screen functionality with proper error handling
/// </summary>
public class GameOverUI : MonoBehaviour
{
    #region Serialized Fields

    [Header("UI References")]
    [SerializeField] private Text gameOverText;
    [SerializeField] private Text deathStatsText;
    [SerializeField] private Text finalScoreText;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private Button quitButton;

    [Header("Animation")]
    [SerializeField] private Animator uiAnimator;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float fadeInDuration = 1f;

    [Header("Messages")]
    [SerializeField] private string gameOverMessage = "GAME OVER";
    [SerializeField] private string soloDeathMessage = "Player {0}: {1}/{2} Deaths";
    [SerializeField] private string coopDeathMessage = "Total Deaths: {0}/{1}";
    [SerializeField] private string victoryMessage = "LEVEL COMPLETE!";

    [Header("Audio")]
    [SerializeField] private AudioClip buttonClickSound;
    [SerializeField][Range(0f, 1f)] private float buttonVolume = 0.7f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    #endregion

    #region Private Fields

    private GameManager gameManager;
    private AudioSource audioSource;
    private bool isInitialized = false;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        InitializeComponents();
        SetupButtons();
    }

    private void Start()
    {
        ConnectToGameManager();
        ValidateSetup();

        // Ensure UI starts hidden
        SetUIVisible(false);

        isInitialized = true;
    }

    private void OnDestroy()
    {
        DisconnectFromGameManager();
    }

    #endregion

    #region Initialization

    private void InitializeComponents()
    {
        // Setup audio source
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D audio
        }

        // Setup canvas group for fading
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }
    }

    private void SetupButtons()
    {
        // Setup retry button
        if (retryButton != null)
        {
            retryButton.onClick.RemoveAllListeners();
            retryButton.onClick.AddListener(OnRetryClicked);
        }

        // Setup main menu button
        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.RemoveAllListeners();
            mainMenuButton.onClick.AddListener(OnMainMenuClicked);
        }

        // Setup quit button
        if (quitButton != null)
        {
            quitButton.onClick.RemoveAllListeners();
            quitButton.onClick.AddListener(OnQuitClicked);
        }
    }

    private void ConnectToGameManager()
    {
        gameManager = GameManager.Instance;

        if (gameManager != null)
        {
            // Subscribe to game over event
            gameManager.OnGameOver += ShowGameOverScreen;

            if (enableDebugLogs)
            {
                Debug.Log("[GameOverUI] Connected to GameManager");
            }
        }
        else
        {
            Debug.LogError("[GameOverUI] No GameManager found in scene!");
        }
    }

    private void DisconnectFromGameManager()
    {
        if (gameManager != null)
        {
            gameManager.OnGameOver -= ShowGameOverScreen;
        }
    }

    private void ValidateSetup()
    {
        StringBuilder warnings = new StringBuilder();

        if (gameOverText == null)
            warnings.AppendLine("- Game Over Text is not assigned");

        if (retryButton == null)
            warnings.AppendLine("- Retry Button is not assigned");

        if (mainMenuButton == null)
            warnings.AppendLine("- Main Menu Button is not assigned");

        if (warnings.Length > 0)
        {
            Debug.LogWarning($"[GameOverUI] Setup issues found:\n{warnings.ToString()}");
        }
    }

    #endregion

    #region UI Display Methods

    public void ShowGameOverScreen()
    {
        if (!isInitialized) return;

        if (enableDebugLogs)
        {
            Debug.Log("[GameOverUI] Showing game over screen");
        }

        // Update all text elements
        UpdateGameOverDisplay();

        // Show UI with animation
        SetUIVisible(true);

        // Play animation if available
        if (uiAnimator != null)
        {
            uiAnimator.SetTrigger("ShowGameOver");
        }
        else
        {
            // Fallback fade in
            StartCoroutine(FadeInCoroutine());
        }
    }

    public void HideGameOverScreen()
    {
        if (!isInitialized) return;

        if (enableDebugLogs)
        {
            Debug.Log("[GameOverUI] Hiding game over screen");
        }

        SetUIVisible(false);

        if (uiAnimator != null)
        {
            uiAnimator.SetTrigger("HideGameOver");
        }
    }

    private void SetUIVisible(bool visible)
    {
        gameObject.SetActive(visible);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }
    }

    #endregion

    #region Content Update

    private void UpdateGameOverDisplay()
    {
        if (gameManager == null) return;

        // Update main game over text
        if (gameOverText != null)
        {
            gameOverText.text = gameOverMessage;
        }

        // Update death statistics
        UpdateDeathStatsDisplay();

        // Update final score if available
        UpdateFinalScoreDisplay();
    }

    private void UpdateDeathStatsDisplay()
    {
        if (deathStatsText == null || gameManager == null) return;

        StringBuilder statsText = new StringBuilder();

        if (gameManager.UseSharedDeathPool)
        {
            // Coop mode: show total deaths
            statsText.AppendFormat(coopDeathMessage,
                gameManager.TotalDeaths,
                gameManager.MaxTotalDeaths);
        }
        else
        {
            // Individual mode: show each player's deaths
            bool hasAnyDeaths = false;

            for (int i = 0; i < 4; i++) // Check up to 4 players
            {
                int deaths = gameManager.GetPlayerDeaths(i);
                if (deaths > 0 || (i < gameManager.RegisteredPlayerCount))
                {
                    if (hasAnyDeaths)
                    {
                        statsText.AppendLine();
                    }

                    statsText.AppendFormat(soloDeathMessage,
                        i + 1, deaths, gameManager.MaxDeathsPerPlayer);
                    hasAnyDeaths = true;
                }
            }

            if (!hasAnyDeaths)
            {
                statsText.Append("No player data available");
            }
        }

        deathStatsText.text = statsText.ToString();
    }

    private void UpdateFinalScoreDisplay()
    {
        if (finalScoreText == null) return;

        // This could be connected to a scoring system
        // For now, just show remaining deaths as a "score"
        if (gameManager != null)
        {
            int remainingDeaths = gameManager.GetRemainingDeaths();
            finalScoreText.text = $"Deaths Remaining: {remainingDeaths}";
        }
        else
        {
            finalScoreText.text = "";
        }
    }

    #endregion

    #region Button Handlers

    private void OnRetryClicked()
    {
        PlayButtonSound();

        if (enableDebugLogs)
        {
            Debug.Log("[GameOverUI] Retry button clicked - Full level restart");
        }

        if (gameManager != null)
        {
            gameManager.RestartLevel(); // This now does a full scene reload
        }
        else
        {
            Debug.LogError("[GameOverUI] Cannot restart - GameManager is null!");
        }

        HideGameOverScreen();
    }

    private void OnMainMenuClicked()
    {
        PlayButtonSound();

        if (enableDebugLogs)
        {
            Debug.Log("[GameOverUI] Main Menu button clicked");
        }

        if (gameManager != null)
        {
            gameManager.LoadMainMenu();
        }
        else
        {
            Debug.LogError("[GameOverUI] Cannot load main menu - GameManager is null!");
        }
    }

    private void OnQuitClicked()
    {
        PlayButtonSound();

        if (enableDebugLogs)
        {
            Debug.Log("[GameOverUI] Quit button clicked");
        }

        if (gameManager != null)
        {
            gameManager.QuitGame();
        }
        else
        {
            // Fallback quit
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }

    private void PlayButtonSound()
    {
        if (buttonClickSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(buttonClickSound, buttonVolume);
        }
    }

    #endregion

    #region Animation Coroutines

    private System.Collections.IEnumerator FadeInCoroutine()
    {
        if (canvasGroup == null) yield break;

        float elapsed = 0f;
        float startAlpha = canvasGroup.alpha;

        while (elapsed < fadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime; // Use unscaled time in case game is paused
            float progress = elapsed / fadeInDuration;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, progress);
            yield return null;
        }

        canvasGroup.alpha = 1f;
    }

    #endregion

    #region Public Methods

    public void ShowVictoryScreen()
    {
        if (gameOverText != null)
        {
            gameOverText.text = victoryMessage;
        }

        ShowGameOverScreen();
    }

    public void UpdateCustomMessage(string message)
    {
        if (gameOverText != null)
        {
            gameOverText.text = message;
        }
    }

    public void SetButtonInteractable(bool retry, bool mainMenu, bool quit)
    {
        if (retryButton != null)
            retryButton.interactable = retry;

        if (mainMenuButton != null)
            mainMenuButton.interactable = mainMenu;

        if (quitButton != null)
            quitButton.interactable = quit;
    }

    #endregion

    #region Debug Methods

    [ContextMenu("Debug: Show Game Over")]
    public void DebugShowGameOver()
    {
        ShowGameOverScreen();
    }

    [ContextMenu("Debug: Hide Game Over")]
    public void DebugHideGameOver()
    {
        HideGameOverScreen();
    }

    [ContextMenu("Debug: Show Victory")]
    public void DebugShowVictory()
    {
        ShowVictoryScreen();
    }

    #endregion
}