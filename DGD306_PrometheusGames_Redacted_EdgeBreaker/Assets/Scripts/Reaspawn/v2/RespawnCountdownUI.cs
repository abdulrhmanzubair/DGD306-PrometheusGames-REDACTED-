using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// UI component that shows respawn countdown for dead players
/// Displays countdown timer and player-specific messages
/// </summary>
public class RespawnCountdownUI : MonoBehaviour
{
    #region Serialized Fields

    [Header("UI References")]
    [SerializeField] private GameObject countdownPanel;
    [SerializeField] private Text countdownText;
    [SerializeField] private Text playerNameText;
    [SerializeField] private Text instructionText;
    [SerializeField] private Image countdownBackground;
    [SerializeField] private Slider countdownProgressBar;

    [Header("Display Settings")]
    [SerializeField] private bool showForAllPlayers = true;
    [SerializeField] private int targetPlayerIndex = 0;
    [SerializeField] private bool showProgressBar = true;
    [SerializeField] private bool showPlayerName = true;
    [SerializeField] private bool showInstructions = true;

    [Header("Text Content")]
    [SerializeField] private string countdownFormat = "{0:F0}";
    [SerializeField] private string playerNameFormat = "Player {0}";
    [SerializeField] private string instructionMessage = "Respawning at checkpoint...";
    [SerializeField] private string readyMessage = "Ready!";

    [Header("Visual Settings")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color urgentColor = Color.red;
    [SerializeField] private float urgentThreshold = 3f;
    [SerializeField] private bool pulseOnUrgent = true;

    [Header("Audio")]
    [SerializeField] private AudioClip countdownBeep;
    [SerializeField] private AudioClip respawnSound;
    [SerializeField][Range(0f, 1f)] private float beepVolume = 0.5f;
    [SerializeField][Range(0f, 1f)] private float respawnVolume = 0.7f;

    #endregion

    #region Private Fields

    private GameManager gameManager;
    private AudioSource audioSource;
    private Coroutine countdownCoroutine;
    private Coroutine pulseCoroutine;
    private RectTransform panelTransform;
    private Vector3 originalScale;

    // Countdown state
    private bool isCountingDown = false;
    private float currentCountdownTime = 0f;
    private float totalCountdownTime = 0f;
    private int currentPlayerIndex = -1;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        InitializeComponents();
    }

    private void Start()
    {
        ConnectToGameManager();
        HideCountdown();
    }

    private void OnDestroy()
    {
        DisconnectFromGameManager();
        StopAllCoroutines();
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

        // Get panel transform for scaling
        if (countdownPanel != null)
        {
            panelTransform = countdownPanel.GetComponent<RectTransform>();
            if (panelTransform != null)
            {
                originalScale = panelTransform.localScale;
            }
        }

        // Configure UI elements
        if (countdownProgressBar != null)
        {
            countdownProgressBar.gameObject.SetActive(showProgressBar);
        }

        if (playerNameText != null)
        {
            playerNameText.gameObject.SetActive(showPlayerName);
        }

        if (instructionText != null)
        {
            instructionText.gameObject.SetActive(showInstructions);
            instructionText.text = instructionMessage;
        }
    }

    private void ConnectToGameManager()
    {
        gameManager = GameManager.Instance;

        if (gameManager != null)
        {
            gameManager.OnPlayerDeath += OnPlayerDeath;
            gameManager.OnPlayerRespawn += OnPlayerRespawn;
            gameManager.OnGameOver += OnGameOver;
        }
        else
        {
            Debug.LogError("[RespawnCountdownUI] No GameManager found!");
        }
    }

    private void DisconnectFromGameManager()
    {
        if (gameManager != null)
        {
            gameManager.OnPlayerDeath -= OnPlayerDeath;
            gameManager.OnPlayerRespawn -= OnPlayerRespawn;
            gameManager.OnGameOver -= OnGameOver;
        }
    }

    #endregion

    #region Event Handlers

    private void OnPlayerDeath(int playerIndex, int deathCount)
    {
        // Only show countdown for target player if not showing for all
        if (!showForAllPlayers && playerIndex != targetPlayerIndex)
        {
            return;
        }

        // Don't show countdown if game is over
        if (gameManager.IsGameOver)
        {
            return;
        }

        StartRespawnCountdown(playerIndex);
    }

    private void OnPlayerRespawn(PlayerHealthSystem player)
    {
        // Hide countdown when player respawns
        if (isCountingDown && (showForAllPlayers || player.PlayerIndex == targetPlayerIndex))
        {
            StopRespawnCountdown(true);
        }
    }

    private void OnGameOver()
    {
        // Stop countdown immediately on game over
        if (isCountingDown)
        {
            StopRespawnCountdown(false);
        }
    }

    #endregion

    #region Countdown Control

    private void StartRespawnCountdown(int playerIndex)
    {
        // Stop any existing countdown
        if (countdownCoroutine != null)
        {
            StopCoroutine(countdownCoroutine);
        }

        currentPlayerIndex = playerIndex;
        totalCountdownTime = gameManager != null ? 2f : 3f; // Get respawn delay from GameManager
        currentCountdownTime = totalCountdownTime;
        isCountingDown = true;

        // Update player-specific UI
        UpdatePlayerInfo(playerIndex);

        // Show countdown panel
        ShowCountdown();

        // Start countdown coroutine
        countdownCoroutine = StartCoroutine(CountdownCoroutine());
    }

    private void StopRespawnCountdown(bool playRespawnSound = false)
    {
        isCountingDown = false;

        if (countdownCoroutine != null)
        {
            StopCoroutine(countdownCoroutine);
            countdownCoroutine = null;
        }

        if (pulseCoroutine != null)
        {
            StopCoroutine(pulseCoroutine);
            pulseCoroutine = null;
        }

        // Play respawn sound if requested
        if (playRespawnSound && respawnSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(respawnSound, respawnVolume);
        }

        // Hide countdown with delay to show "Ready!" message
        StartCoroutine(HideCountdownDelayed(playRespawnSound ? 0.5f : 0f));
    }

    private IEnumerator CountdownCoroutine()
    {
        while (currentCountdownTime > 0f && isCountingDown)
        {
            // Update countdown display
            UpdateCountdownDisplay();

            // Play beep on each second (if enabled)
            if (countdownBeep != null && audioSource != null)
            {
                float previousTime = currentCountdownTime + Time.unscaledDeltaTime;
                if (Mathf.Floor(previousTime) > Mathf.Floor(currentCountdownTime))
                {
                    audioSource.PlayOneShot(countdownBeep, beepVolume);
                }
            }

            // Start pulsing when urgent
            if (currentCountdownTime <= urgentThreshold && pulseOnUrgent && pulseCoroutine == null)
            {
                pulseCoroutine = StartCoroutine(PulseAnimation());
            }

            currentCountdownTime -= Time.unscaledDeltaTime;
            yield return null;
        }

        // Countdown finished
        if (isCountingDown)
        {
            ShowReadyMessage();
            yield return new WaitForSecondsRealtime(0.5f);
            StopRespawnCountdown(true);
        }
    }

    private IEnumerator HideCountdownDelayed(float delay)
    {
        if (delay > 0f)
        {
            yield return new WaitForSecondsRealtime(delay);
        }

        HideCountdown();
    }

    #endregion

    #region UI Updates

    private void UpdatePlayerInfo(int playerIndex)
    {
        if (playerNameText != null && showPlayerName)
        {
            playerNameText.text = string.Format(playerNameFormat, playerIndex + 1);
        }
    }

    private void UpdateCountdownDisplay()
    {
        // Update countdown text
        if (countdownText != null)
        {
            countdownText.text = string.Format(countdownFormat, Mathf.Ceil(currentCountdownTime));

            // Update text color based on urgency
            bool isUrgent = currentCountdownTime <= urgentThreshold;
            countdownText.color = isUrgent ? urgentColor : normalColor;
        }

        // Update progress bar
        if (countdownProgressBar != null && showProgressBar)
        {
            float progress = currentCountdownTime / totalCountdownTime;
            countdownProgressBar.value = 1f - progress; // Invert so it fills up as time decreases

            // Update progress bar color
            Image fillImage = countdownProgressBar.fillRect?.GetComponent<Image>();
            if (fillImage != null)
            {
                bool isUrgent = currentCountdownTime <= urgentThreshold;
                fillImage.color = isUrgent ? urgentColor : normalColor;
            }
        }

        // Update background color
        if (countdownBackground != null)
        {
            bool isUrgent = currentCountdownTime <= urgentThreshold;
            Color bgColor = isUrgent ? urgentColor : normalColor;
            bgColor.a = 0.3f;
            countdownBackground.color = bgColor;
        }
    }

    private void ShowReadyMessage()
    {
        if (countdownText != null)
        {
            countdownText.text = readyMessage;
            countdownText.color = normalColor;
        }

        if (instructionText != null)
        {
            instructionText.text = "Get ready!";
        }
    }

    private void ShowCountdown()
    {
        if (countdownPanel != null)
        {
            countdownPanel.SetActive(true);
        }

        // Reset instruction text
        if (instructionText != null && showInstructions)
        {
            instructionText.text = instructionMessage;
        }
    }

    private void HideCountdown()
    {
        if (countdownPanel != null)
        {
            countdownPanel.SetActive(false);
        }

        // Reset scale
        if (panelTransform != null)
        {
            panelTransform.localScale = originalScale;
        }
    }

    #endregion

    #region Animation

    private IEnumerator PulseAnimation()
    {
        if (panelTransform == null) yield break;

        float pulseSpeed = 2f;
        float pulseAmount = 0.1f;

        while (isCountingDown && currentCountdownTime <= urgentThreshold)
        {
            float scale = 1f + Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseAmount;
            panelTransform.localScale = originalScale * scale;
            yield return null;
        }

        panelTransform.localScale = originalScale;
        pulseCoroutine = null;
    }

    #endregion

    #region Public Methods

    public void SetTargetPlayer(int playerIndex)
    {
        targetPlayerIndex = playerIndex;
    }

    public void SetShowForAllPlayers(bool showForAll)
    {
        showForAllPlayers = showForAll;
    }

    public void ManualStartCountdown(int playerIndex, float duration)
    {
        totalCountdownTime = duration;
        StartRespawnCountdown(playerIndex);
    }

    public void ManualStopCountdown()
    {
        StopRespawnCountdown(false);
    }

    public bool IsCountingDown => isCountingDown;
    public float RemainingTime => currentCountdownTime;

    #endregion

    #region Context Menu Debug

    [ContextMenu("Test Countdown (3 seconds)")]
    private void TestCountdown()
    {
        ManualStartCountdown(0, 3f);
    }

    [ContextMenu("Test Urgent Countdown (2 seconds)")]
    private void TestUrgentCountdown()
    {
        ManualStartCountdown(0, 2f);
    }

    [ContextMenu("Stop Test Countdown")]
    private void StopTestCountdown()
    {
        ManualStopCountdown();
    }

    #endregion
}