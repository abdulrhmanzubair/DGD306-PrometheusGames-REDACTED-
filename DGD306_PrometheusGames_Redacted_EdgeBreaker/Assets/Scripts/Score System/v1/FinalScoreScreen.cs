using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// Final score screen to display at the end of the game
/// Create a scene called "FinalScore" or "GameComplete" and add this
/// </summary>
public class FinalScoreScreen : MonoBehaviour
{
    [Header("UI References")]
    public Text finalScoreText;
    public Text highScoreText;
    public Text newHighScoreText;
    public Text gameCompleteText;
    public Text playTimeText;

    [Header("Buttons")]
    public Button playAgainButton;
    public Button mainMenuButton;
    public Button quitButton;

    [Header("Scene Names")]
    public string firstLevelScene = "LVL_001";
    public string mainMenuScene = "MainMenu";

    [Header("Animation")]
    public bool animateScore = true;
    public float scoreAnimationDuration = 2f;
    public AudioClip scoreCountSound;
    public AudioClip newHighScoreSound;

    [Header("Visual Effects")]
    public GameObject celebrationEffect;
    public GameObject newHighScoreEffect;

    private bool isNewHighScore = false;
    private AudioSource audioSource;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        SetupButtons();
        DisplayFinalScore();
    }

    void SetupButtons()
    {
        if (playAgainButton != null)
        {
            playAgainButton.onClick.AddListener(PlayAgain);
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.AddListener(GoToMainMenu);
        }

        if (quitButton != null)
        {
            quitButton.onClick.AddListener(QuitGame);
        }
    }

    void DisplayFinalScore()
    {
        if (ScoreManager.Instance == null)
        {
            Debug.LogError("FinalScoreScreen: No ScoreManager found!");
            ShowFallbackScore();
            return;
        }

        int finalScore = ScoreManager.Instance.GetScore();
        int highScore = ScoreManager.Instance.GetHighScore();

        // Check if this is a new high score
        isNewHighScore = finalScore >= highScore && finalScore > 0;

        // Set up texts
        if (gameCompleteText != null)
        {
            gameCompleteText.text = "GAME COMPLETE!";
        }

        // Show high score
        if (highScoreText != null)
        {
            if (isNewHighScore)
            {
                highScoreText.text = $"Previous Best: {(highScore == finalScore ? "None" : highScore.ToString("N0"))}";
            }
            else
            {
                highScoreText.text = $"High Score: {highScore:N0}";
            }
        }

        // Handle new high score indicator
        if (newHighScoreText != null)
        {
            newHighScoreText.gameObject.SetActive(isNewHighScore);
            if (isNewHighScore)
            {
                newHighScoreText.text = "NEW HIGH SCORE!";
            }
        }

        // Animate score or show immediately
        if (animateScore && finalScore > 0)
        {
            StartCoroutine(AnimateScoreDisplay(finalScore));
        }
        else
        {
            ShowFinalScore(finalScore);
        }

        // Show celebration effects
        if (isNewHighScore)
        {
            ShowNewHighScoreEffects();
        }
        else if (celebrationEffect != null)
        {
            Instantiate(celebrationEffect, transform.position, Quaternion.identity);
        }

        // Display play time if available
        ShowPlayTime();
    }

    IEnumerator AnimateScoreDisplay(int targetScore)
    {
        if (finalScoreText == null) yield break;

        float elapsedTime = 0f;
        int currentDisplayScore = 0;

        // Play counting sound
        if (scoreCountSound != null && audioSource != null)
        {
            audioSource.clip = scoreCountSound;
            audioSource.loop = true;
            audioSource.Play();
        }

        while (elapsedTime < scoreAnimationDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / scoreAnimationDuration;

            // Use easing for smooth animation
            progress = Mathf.SmoothStep(0f, 1f, progress);

            currentDisplayScore = Mathf.RoundToInt(targetScore * progress);
            finalScoreText.text = $"Final Score: {currentDisplayScore:N0}";

            yield return null;
        }

        // Stop counting sound
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
            audioSource.loop = false;
        }

        // Show final score
        ShowFinalScore(targetScore);

        // Play high score sound if applicable
        if (isNewHighScore && newHighScoreSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(newHighScoreSound);
        }
    }

    void ShowFinalScore(int score)
    {
        if (finalScoreText != null)
        {
            finalScoreText.text = $"Final Score: {score:N0}";
        }
    }

    void ShowNewHighScoreEffects()
    {
        if (newHighScoreEffect != null)
        {
            Instantiate(newHighScoreEffect, transform.position, Quaternion.identity);
        }

        // Flash the new high score text
        if (newHighScoreText != null)
        {
            StartCoroutine(FlashText(newHighScoreText));
        }
    }

    IEnumerator FlashText(Text text)
    {
        Color originalColor = text.color;

        for (int i = 0; i < 6; i++) // Flash 3 times
        {
            text.color = Color.yellow;
            yield return new WaitForSeconds(0.2f);
            text.color = originalColor;
            yield return new WaitForSeconds(0.2f);
        }
    }

    void ShowPlayTime()
    {
        if (playTimeText == null) return;

        // Try to calculate total play time
        // This is a simple implementation - you could enhance this with a proper time tracking system
        float sessionTime = Time.time;

        int minutes = Mathf.FloorToInt(sessionTime / 60f);
        int seconds = Mathf.FloorToInt(sessionTime % 60f);

        playTimeText.text = $"Session Time: {minutes:00}:{seconds:00}";
    }

    void ShowFallbackScore()
    {
        // Fallback if no ScoreManager
        if (finalScoreText != null)
        {
            finalScoreText.text = "Final Score: N/A";
        }

        if (gameCompleteText != null)
        {
            gameCompleteText.text = "GAME COMPLETE!";
        }
    }

    // Button methods
    void PlayAgain()
    {
        Debug.Log("FinalScoreScreen: Play Again pressed");

        // Reset the game
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.ResetGame();
        }

        // Load first level
        if (LevelTransitionManager.Instance != null)
        {
            LevelTransitionManager.Instance.TransitionToScene(firstLevelScene);
        }
        else
        {
            SceneManager.LoadScene(firstLevelScene);
        }
    }

    void GoToMainMenu()
    {
        Debug.Log("FinalScoreScreen: Main Menu pressed");

        // Don't reset score - keep it for high score purposes

        // Load main menu
        if (LevelTransitionManager.Instance != null)
        {
            LevelTransitionManager.Instance.TransitionToScene(mainMenuScene);
        }
        else
        {
            SceneManager.LoadScene(mainMenuScene);
        }
    }

    void QuitGame()
    {
        Debug.Log("FinalScoreScreen: Quit pressed");

        // Save any final data
        if (ScoreManager.Instance != null)
        {
            // High score is already saved, but just to be sure
            PlayerPrefs.Save();
        }

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // Public method to be called by external systems
    public void ShowScoreBreakdown()
    {
        if (ScoreManager.Instance == null) return;

        // You can expand this to show detailed score breakdown
        Debug.Log("=== FINAL SCORE BREAKDOWN ===");
        Debug.Log($"Total Score: {ScoreManager.Instance.GetScore()}");
        Debug.Log($"Levels Completed: {ScoreManager.Instance.GetCurrentLevel()}");
        Debug.Log($"High Score: {ScoreManager.Instance.GetHighScore()}");
    }

    // Method to manually trigger score animation
    public void AnimateScoreAgain()
    {
        if (ScoreManager.Instance != null && animateScore)
        {
            StartCoroutine(AnimateScoreDisplay(ScoreManager.Instance.GetScore()));
        }
    }
}