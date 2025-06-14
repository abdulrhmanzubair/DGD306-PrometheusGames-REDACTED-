using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Enhanced ScoreManager that persists across all levels
/// Replaces your existing ScoreManager.cs
/// </summary>
public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance;

    [Header("Score Settings")]
    public int score = 0;
    public int highScore = 0;

    [Header("UI References")]
    public Text scoreText; // Optional, for displaying in UI
    public Text highScoreText; // Optional, for displaying high score

    [Header("Score Persistence")]
    [Tooltip("Save score to PlayerPrefs")]
    public bool saveHighScore = true;
    [Tooltip("PlayerPrefs key for high score")]
    public string highScoreKey = "HighScore";

    [Header("Level Tracking")]
    public int currentLevel = 1;
    public int totalLevels = 5; // Set this to your total number of levels

    [Header("Score Events")]
    public bool enableScoreEvents = true;

    // Events for score changes
    public System.Action<int> OnScoreChanged;
    public System.Action<int> OnHighScoreBeaten;
    public System.Action<int, int> OnLevelComplete; // currentScore, levelNumber
    public System.Action<int> OnGameComplete; // finalScore

    // Level score tracking
    private int scoreAtLevelStart = 0;
    private int currentLevelScore = 0;

    void Awake()
    {
        // Singleton pattern with persistence
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // This keeps the score across scenes!
            LoadHighScore();
            Debug.Log("[ScoreManager] Persistent ScoreManager initialized");
        }
        else
        {
            Debug.Log("[ScoreManager] Duplicate ScoreManager destroyed");
            Destroy(gameObject);
        }
    }

    void Start()
    {
        UpdateScoreDisplay();
        DetectCurrentLevel();
    }

    void DetectCurrentLevel()
    {
        // Auto-detect current level from scene name
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        // Try to extract level number from scene name (e.g., "LVL_001" -> 1)
        if (sceneName.Contains("LVL_"))
        {
            string levelPart = sceneName.Replace("LVL_", "").Replace("Level_", "").Replace("Level", "");
            if (int.TryParse(levelPart, out int levelNum))
            {
                SetCurrentLevel(levelNum);
            }
        }
    }

    public void SetCurrentLevel(int levelNumber)
    {
        currentLevel = levelNumber;
        scoreAtLevelStart = score;
        currentLevelScore = 0;

        Debug.Log($"[ScoreManager] Starting Level {currentLevel} with score: {score}");
    }

    public void AddScore(int amount)
    {
        int oldScore = score;
        score += amount;
        currentLevelScore += amount;

        // Check for high score
        if (score > highScore)
        {
            int oldHighScore = highScore;
            highScore = score;

            if (saveHighScore)
            {
                SaveHighScore();
            }

            if (enableScoreEvents && oldHighScore > 0) // Don't trigger on first time
            {
                OnHighScoreBeaten?.Invoke(highScore);
            }

            Debug.Log($"[ScoreManager] NEW HIGH SCORE: {highScore}!");
        }

        UpdateScoreDisplay();

        if (enableScoreEvents)
        {
            OnScoreChanged?.Invoke(score);
        }

        Debug.Log($"[ScoreManager] Score: {oldScore} + {amount} = {score} (Level Score: {currentLevelScore})");
    }

    public void SubtractScore(int amount)
    {
        score = Mathf.Max(0, score - amount); // Don't go below 0
        currentLevelScore = Mathf.Max(0, currentLevelScore - amount);

        UpdateScoreDisplay();

        if (enableScoreEvents)
        {
            OnScoreChanged?.Invoke(score);
        }

        Debug.Log($"[ScoreManager] Score reduced by {amount}. New score: {score}");
    }

    public void OnLevelCompleted()
    {
        Debug.Log($"[ScoreManager] Level {currentLevel} completed! Level Score: {currentLevelScore}, Total Score: {score}");

        if (enableScoreEvents)
        {
            OnLevelComplete?.Invoke(score, currentLevel);
        }

        // Check if this was the final level
        if (currentLevel >= totalLevels)
        {
            OnGameCompleted();
        }
    }

    public void OnGameCompleted()
    {
        Debug.Log($"[ScoreManager] GAME COMPLETED! Final Score: {score}");

        if (enableScoreEvents)
        {
            OnGameComplete?.Invoke(score);
        }

        // Save final score as high score if it's better
        if (score > highScore)
        {
            highScore = score;
            if (saveHighScore)
            {
                SaveHighScore();
            }
        }
    }

    void UpdateScoreDisplay()
    {
        if (scoreText != null)
        {
            scoreText.text = "Score: " + score.ToString("N0"); // Formatted with commas
        }

        if (highScoreText != null)
        {
            highScoreText.text = "High Score: " + highScore.ToString("N0");
        }
    }

    void LoadHighScore()
    {
        if (saveHighScore)
        {
            highScore = PlayerPrefs.GetInt(highScoreKey, 0);
            Debug.Log($"[ScoreManager] Loaded high score: {highScore}");
        }
    }

    void SaveHighScore()
    {
        if (saveHighScore)
        {
            PlayerPrefs.SetInt(highScoreKey, highScore);
            PlayerPrefs.Save();
            Debug.Log($"[ScoreManager] Saved high score: {highScore}");
        }
    }

    // Public utility methods
    public int GetScore()
    {
        return score;
    }

    public int GetHighScore()
    {
        return highScore;
    }

    public int GetCurrentLevelScore()
    {
        return currentLevelScore;
    }

    public int GetCurrentLevel()
    {
        return currentLevel;
    }

    public bool IsGameComplete()
    {
        return currentLevel >= totalLevels;
    }

    public float GetGameProgress()
    {
        return (float)currentLevel / totalLevels;
    }

    public void ResetScore()
    {
        score = 0;
        currentLevelScore = 0;
        scoreAtLevelStart = 0;
        UpdateScoreDisplay();

        if (enableScoreEvents)
        {
            OnScoreChanged?.Invoke(score);
        }

        Debug.Log("[ScoreManager] Score reset to 0");
    }

    public void ResetGame()
    {
        score = 0;
        currentLevel = 1;
        currentLevelScore = 0;
        scoreAtLevelStart = 0;
        UpdateScoreDisplay();

        Debug.Log("[ScoreManager] Game reset - Score: 0, Level: 1");
    }

    // Method to manually set total levels if needed
    public void SetTotalLevels(int total)
    {
        totalLevels = total;
        Debug.Log($"[ScoreManager] Total levels set to: {totalLevels}");
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && saveHighScore)
        {
            SaveHighScore(); // Save when app is paused
        }
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus && saveHighScore)
        {
            SaveHighScore(); // Save when app loses focus
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            if (saveHighScore)
            {
                SaveHighScore(); // Save before destruction
            }
            Instance = null;
        }
    }
}