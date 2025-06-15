using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;

public class GameOverUI : MonoBehaviour
{
    [Header("UI References")]
    public GameObject gameOverPanel;
    public TextMeshProUGUI gameOverText;
    public TextMeshProUGUI finalScoreText;
    public Button restartButton;
    public Button mainMenuButton;
    public Button quitButton;

    [Header("Animation Settings")]
    public bool animateIn = true;
    public float animationDuration = 1f;
    public AnimationCurve animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Audio")]
    public AudioClip gameOverSound;
    public AudioSource audioSource;

    [Header("Scene Names")]
    public string currentSceneName; // Auto-detected if empty
    public string mainMenuSceneName = "MainMenu";

    private void Start()
    {
        // Auto-detect current scene if not specified
        if (string.IsNullOrEmpty(currentSceneName))
        {
            currentSceneName = SceneManager.GetActiveScene().name;
        }

        // Set up audio source
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        // Set up button listeners
        if (restartButton != null)
            restartButton.onClick.AddListener(RestartGame);

        if (mainMenuButton != null)
            mainMenuButton.onClick.AddListener(GoToMainMenu);

        if (quitButton != null)
            quitButton.onClick.AddListener(QuitGame);

        // Initialize UI
        InitializeUI();
    }

    private void InitializeUI()
    {
        // Update final score if available
        if (finalScoreText != null && ScoreManagerV2.instance != null)
        {
            finalScoreText.text = $"Final Score: {ScoreManagerV2.instance.GetScore()}";
        }

        // Play game over sound
        if (gameOverSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(gameOverSound);
        }

        // Animate in if enabled
        if (animateIn && gameOverPanel != null)
        {
            StartCoroutine(AnimateGameOverIn());
        }
        else if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }

        // Pause the game
        Time.timeScale = 0f;

        // Show cursor for UI interaction
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private IEnumerator AnimateGameOverIn()
    {
        if (gameOverPanel == null) yield break;

        gameOverPanel.SetActive(true);

        // Start with panel scaled to 0
        gameOverPanel.transform.localScale = Vector3.zero;

        float elapsedTime = 0f;

        while (elapsedTime < animationDuration)
        {
            elapsedTime += Time.unscaledDeltaTime; // Use unscaled time since game is paused
            float progress = elapsedTime / animationDuration;
            float animValue = animationCurve.Evaluate(progress);

            gameOverPanel.transform.localScale = Vector3.one * animValue;

            yield return null;
        }

        gameOverPanel.transform.localScale = Vector3.one;
    }

    public void RestartGame()
    {
        // Resume time scale
        Time.timeScale = 1f;

        // Hide cursor
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        // Reload current scene
        Debug.Log($"Restarting game: {currentSceneName}");
        SceneManager.LoadScene(currentSceneName);
    }

    public void GoToMainMenu()
    {
        // Resume time scale
        Time.timeScale = 1f;

        // Show cursor
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        // Load main menu scene
        Debug.Log($"Going to main menu: {mainMenuSceneName}");
        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void QuitGame()
    {
        Debug.Log("Quitting game");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
    }

    // Public method to show game over (in case you want to trigger it manually)
    public static void ShowGameOver()
    {
        // Find existing game over UI
        GameOverUI gameOverUI = FindObjectOfType<GameOverUI>();

        if (gameOverUI != null)
        {
            gameOverUI.gameObject.SetActive(true);
        }
        else
        {
            Debug.LogWarning("No GameOverUI found in scene!");
        }
    }

    private void OnDestroy()
    {
        // Make sure time scale is restored if UI is destroyed
        Time.timeScale = 1f;
    }
}