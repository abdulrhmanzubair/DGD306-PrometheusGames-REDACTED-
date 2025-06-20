using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// UI component that displays death count in real-time
/// Shows individual player deaths or shared pool based on game mode
/// </summary>
public class DeathCounterUI : MonoBehaviour
{
    #region Serialized Fields

    [Header("UI References")]
    [SerializeField] private Text deathCountText;
    [SerializeField] private Text remainingDeathsText;
    [SerializeField] private Image deathCountBackground;
    [SerializeField] private Slider deathProgressBar;

    [Header("Display Settings")]
    [SerializeField] private bool showPlayerSpecific = false;
    [SerializeField] private int targetPlayerIndex = 0;
    [SerializeField] private bool showRemainingDeaths = true;
    [SerializeField] private bool showProgressBar = true;

    [Header("Visual Feedback")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color warningColor = Color.yellow;
    [SerializeField] private Color dangerColor = Color.red;
    [SerializeField] private float warningThreshold = 0.6f;
    [SerializeField] private float dangerThreshold = 0.8f;

    [Header("Animation")]
    [SerializeField] private bool animateOnDeathChange = true;
    [SerializeField] private float punchScale = 1.2f;
    [SerializeField] private float punchDuration = 0.3f;

    [Header("Text Formats")]
    [SerializeField] private string soloFormat = "P{0}: {1}/{2}";
    [SerializeField] private string coopFormat = "Deaths: {0}/{1}";
    [SerializeField] private string remainingFormat = "Remaining: {0}";

    #endregion

    #region Private Fields

    private GameManager gameManager;
    private RectTransform rectTransform;
    private Vector3 originalScale;
    private Coroutine animationCoroutine;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            originalScale = rectTransform.localScale;
        }
    }

    private void Start()
    {
        ConnectToGameManager();
        InitializeUI();
        UpdateDisplay();
    }

    private void OnDestroy()
    {
        DisconnectFromGameManager();
    }

    #endregion

    #region Initialization

    private void ConnectToGameManager()
    {
        gameManager = GameManager.Instance;

        if (gameManager != null)
        {
            gameManager.OnPlayerDeath += OnPlayerDeath;
            gameManager.OnTotalDeathsChanged += OnTotalDeathsChanged;
            gameManager.OnGameOver += OnGameOver;
        }
        else
        {
            Debug.LogError("[DeathCounterUI] No GameManager found!");
        }
    }

    private void DisconnectFromGameManager()
    {
        if (gameManager != null)
        {
            gameManager.OnPlayerDeath -= OnPlayerDeath;
            gameManager.OnTotalDeathsChanged -= OnTotalDeathsChanged;
            gameManager.OnGameOver -= OnGameOver;
        }
    }

    private void InitializeUI()
    {
        // Hide progress bar if not needed
        if (deathProgressBar != null)
        {
            deathProgressBar.gameObject.SetActive(showProgressBar);
        }

        // Hide remaining deaths text if not needed
        if (remainingDeathsText != null)
        {
            remainingDeathsText.gameObject.SetActive(showRemainingDeaths);
        }
    }

    #endregion

    #region Event Handlers

    private void OnPlayerDeath(int playerIndex, int deathCount)
    {
        UpdateDisplay();

        if (animateOnDeathChange)
        {
            PlayDeathAnimation();
        }
    }

    private void OnTotalDeathsChanged(int totalDeaths)
    {
        UpdateDisplay();

        if (animateOnDeathChange)
        {
            PlayDeathAnimation();
        }
    }

    private void OnGameOver()
    {
        UpdateDisplay();

        // Flash red on game over
        if (deathCountBackground != null)
        {
            StartCoroutine(FlashBackground(Color.red, 0.5f));
        }
    }

    #endregion

    #region Display Update

    private void UpdateDisplay()
    {
        if (gameManager == null) return;

        UpdateDeathCountText();
        UpdateRemainingDeathsText();
        UpdateProgressBar();
        UpdateVisualFeedback();
    }

    private void UpdateDeathCountText()
    {
        if (deathCountText == null) return;

        if (showPlayerSpecific && !gameManager.UseSharedDeathPool)
        {
            // Show specific player's deaths
            int playerDeaths = gameManager.GetPlayerDeaths(targetPlayerIndex);
            int maxDeaths = gameManager.MaxDeathsPerPlayer;
            deathCountText.text = string.Format(soloFormat, targetPlayerIndex + 1, playerDeaths, maxDeaths);
        }
        else
        {
            // Show total deaths (coop mode)
            int totalDeaths = gameManager.TotalDeaths;
            int maxDeaths = gameManager.UseSharedDeathPool ? gameManager.MaxTotalDeaths : gameManager.MaxDeathsPerPlayer;
            deathCountText.text = string.Format(coopFormat, totalDeaths, maxDeaths);
        }
    }

    private void UpdateRemainingDeathsText()
    {
        if (remainingDeathsText == null || !showRemainingDeaths) return;

        int remainingDeaths = showPlayerSpecific && !gameManager.UseSharedDeathPool ?
            gameManager.GetRemainingDeaths(targetPlayerIndex) :
            gameManager.GetRemainingDeaths();

        remainingDeathsText.text = string.Format(remainingFormat, remainingDeaths);
    }

    private void UpdateProgressBar()
    {
        if (deathProgressBar == null || !showProgressBar) return;

        float progress = GetDeathProgress();
        deathProgressBar.value = progress;

        // Update progress bar color
        Image fillImage = deathProgressBar.fillRect?.GetComponent<Image>();
        if (fillImage != null)
        {
            fillImage.color = GetProgressColor(progress);
        }
    }

    private void UpdateVisualFeedback()
    {
        float progress = GetDeathProgress();
        Color currentColor = GetProgressColor(progress);

        // Update text color
        if (deathCountText != null)
        {
            deathCountText.color = currentColor;
        }

        if (remainingDeathsText != null)
        {
            remainingDeathsText.color = currentColor;
        }

        // Update background color
        if (deathCountBackground != null)
        {
            Color bgColor = currentColor;
            bgColor.a = 0.3f; // Semi-transparent background
            deathCountBackground.color = bgColor;
        }
    }

    #endregion

    #region Helper Methods

    private float GetDeathProgress()
    {
        if (gameManager == null) return 0f;

        if (showPlayerSpecific && !gameManager.UseSharedDeathPool)
        {
            int playerDeaths = gameManager.GetPlayerDeaths(targetPlayerIndex);
            return (float)playerDeaths / gameManager.MaxDeathsPerPlayer;
        }
        else
        {
            int totalDeaths = gameManager.TotalDeaths;
            int maxDeaths = gameManager.UseSharedDeathPool ? gameManager.MaxTotalDeaths : gameManager.MaxDeathsPerPlayer;
            return (float)totalDeaths / maxDeaths;
        }
    }

    private Color GetProgressColor(float progress)
    {
        if (progress >= dangerThreshold)
        {
            return dangerColor;
        }
        else if (progress >= warningThreshold)
        {
            return warningColor;
        }
        else
        {
            return normalColor;
        }
    }

    #endregion

    #region Animation

    private void PlayDeathAnimation()
    {
        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
        }

        animationCoroutine = StartCoroutine(PunchScaleAnimation());
    }

    private IEnumerator PunchScaleAnimation()
    {
        if (rectTransform == null) yield break;

        Vector3 targetScale = originalScale * punchScale;
        float elapsed = 0f;
        float halfDuration = punchDuration * 0.5f;

        // Scale up
        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = elapsed / halfDuration;
            rectTransform.localScale = Vector3.Lerp(originalScale, targetScale, progress);
            yield return null;
        }

        // Scale back down
        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = elapsed / halfDuration;
            rectTransform.localScale = Vector3.Lerp(targetScale, originalScale, progress);
            yield return null;
        }

        rectTransform.localScale = originalScale;
        animationCoroutine = null;
    }

    private IEnumerator FlashBackground(Color flashColor, float duration)
    {
        if (deathCountBackground == null) yield break;

        Color originalColor = deathCountBackground.color;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float alpha = Mathf.PingPong(elapsed * 4f, 1f);
            deathCountBackground.color = Color.Lerp(originalColor, flashColor, alpha);
            yield return null;
        }

        deathCountBackground.color = originalColor;
    }

    #endregion

    #region Public Methods

    public void SetTargetPlayer(int playerIndex)
    {
        targetPlayerIndex = playerIndex;
        UpdateDisplay();
    }

    public void SetDisplayMode(bool playerSpecific)
    {
        showPlayerSpecific = playerSpecific;
        UpdateDisplay();
    }

    public void ForceUpdateDisplay()
    {
        UpdateDisplay();
    }

    #endregion

    #region Context Menu Debug

    [ContextMenu("Test Death Animation")]
    private void TestDeathAnimation()
    {
        PlayDeathAnimation();
    }

    [ContextMenu("Test Flash Background")]
    private void TestFlashBackground()
    {
        StartCoroutine(FlashBackground(Color.red, 1f));
    }

    [ContextMenu("Force Update")]
    private void ForceUpdate()
    {
        ForceUpdateDisplay();
    }

    #endregion
}