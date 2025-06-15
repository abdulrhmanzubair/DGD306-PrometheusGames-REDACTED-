using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Enhanced ScoreManager with robust persistence and game completion detection
/// </summary>
public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance;

    [Header("Score Settings")]
    public int score = 0;
    public int highScore = 0;

    [Header("UI References")]
    public Text scoreText;
    public Text highScoreText;

    [Header("Score Persistence")]
    public bool saveHighScore = true;
    public string highScoreKey = "HighScore";
    public string finalScoreKey = "FinalScore"; // For final screen

    [Header("Level Tracking")]
    public int currentLevel = 1;
    public int totalLevels = 5;

    [Header("Game State")]
    public bool gameCompleted = false;
    public string finalScoreScene = "FinalScore";

    // Enhanced tracking
    private int scoreAtLevelStart = 0;
    private int currentLevelScore = 0;
    private float gameStartTime;
    private int totalDataCollected = 0;
    private int totalEnemiesDefeated = 0;

    // Events
    public System.Action<int> OnScoreChanged;
    public System.Action<int> OnHighScoreBeaten;
    public System.Action<int, int> OnLevelComplete;
    public System.Action<int> OnGameComplete;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            gameStartTime = Time.time;
            LoadHighScore();

            Debug.Log($"[ScoreManager] Initialized - DontDestroyOnLoad applied");
        }
        else
        {
            Debug.Log($"[ScoreManager] Duplicate destroyed - Instance preserved");
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
        string sceneName = SceneManager.GetActiveScene().name;

        // Don't change level number if we're in final score scene
        if (sceneName == finalScoreScene || sceneName.Contains("Final") || sceneName.Contains("Complete"))
        {
            Debug.Log($"[ScoreManager] In final scene: {sceneName}");
            return;
        }

        // Extract level number from scene name
        if (sceneName.Contains("LVL_"))
        {
            string levelPart = sceneName.Replace("LVL_", "").Replace("Level_", "").Replace("Level", "");
            if (int.TryParse(levelPart, out int levelNum))
            {
                SetCurrentLevel(levelNum);
            }
        }
        else if (sceneName.Contains("Level"))
        {
            // Handle "Level1", "Level2" format
            string levelPart = sceneName.Replace("Level", "");
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

        Debug.Log($"[ScoreManager] Level {currentLevel}/{totalLevels} - Starting Score: {score}");
    }

    public void AddScore(int amount, string source = "")
    {
        int oldScore = score;
        score += amount;
        currentLevelScore += amount;

        // Track what gave us points
        if (source == "data") totalDataCollected++;
        if (source == "enemy") totalEnemiesDefeated++;

        // Check for high score
        if (score > highScore)
        {
            int oldHighScore = highScore;
            highScore = score;
            SaveHighScore();

            if (oldHighScore > 0)
            {
                OnHighScoreBeaten?.Invoke(highScore);
            }

            Debug.Log($"[ScoreManager] 🏆 NEW HIGH SCORE: {highScore}!");
        }

        UpdateScoreDisplay();
        OnScoreChanged?.Invoke(score);

        Debug.Log($"[ScoreManager] +{amount} points {source} | Score: {score} | Level: {currentLevelScore}");
    }

    public void OnLevelCompleted()
    {
        Debug.Log($"[ScoreManager] ✅ Level {currentLevel} Complete! Level Score: {currentLevelScore}");

        OnLevelComplete?.Invoke(score, currentLevel);

        // Check if this completes the game
        if (currentLevel >= totalLevels)
        {
            OnGameCompleted();
        }
    }

    public void OnGameCompleted()
    {
        gameCompleted = true;

        Debug.Log($"[ScoreManager] 🎉 GAME COMPLETED! Final Score: {score}");

        // Save final score for the final screen
        PlayerPrefs.SetInt(finalScoreKey, score);
        PlayerPrefs.SetFloat("GameCompletionTime", Time.time - gameStartTime);
        PlayerPrefs.SetInt("TotalDataCollected", totalDataCollected);
        PlayerPrefs.SetInt("TotalEnemiesDefeated", totalEnemiesDefeated);
        PlayerPrefs.Save();

        OnGameComplete?.Invoke(score);

        // Transition to final score scene
        if (LevelTransitionManager.Instance != null)
        {
            LevelTransitionManager.Instance.TransitionToScene(finalScoreScene);
        }
        else
        {
            Debug.LogWarning("[ScoreManager] No LevelTransitionManager found! Loading final scene directly.");
            SceneManager.LoadScene(finalScoreScene);
        }
    }

    // Public getters with enhanced data
    public int GetScore() => score;
    public int GetHighScore() => highScore;
    public int GetCurrentLevelScore() => currentLevelScore;
    public int GetCurrentLevel() => currentLevel;
    public bool IsGameComplete() => gameCompleted || currentLevel >= totalLevels;
    public float GetGameProgress() => (float)currentLevel / totalLevels;
    public float GetPlayTime() => Time.time - gameStartTime;
    public int GetTotalDataCollected() => totalDataCollected;
    public int GetTotalEnemiesDefeated() => totalEnemiesDefeated;

    // Game state management
    public void ResetGame()
    {
        score = 0;
        currentLevel = 1;
        currentLevelScore = 0;
        scoreAtLevelStart = 0;
        gameCompleted = false;
        gameStartTime = Time.time;
        totalDataCollected = 0;
        totalEnemiesDefeated = 0;

        UpdateScoreDisplay();
        Debug.Log("[ScoreManager] 🔄 Game Reset");
    }

    void UpdateScoreDisplay()
    {
        if (scoreText != null)
            scoreText.text = $"Score: {score:N0}";

        if (highScoreText != null)
            highScoreText.text = $"Best: {highScore:N0}";
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
        }
    }

    // Auto-save important events
    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus) SaveHighScore();
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus) SaveHighScore();
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            SaveHighScore();
            Instance = null;
            Debug.Log("[ScoreManager] Instance destroyed and saved");
        }
    }

    // Debug method to manually complete game (for testing)
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void DebugCompleteGame()
    {
        currentLevel = totalLevels;
        OnGameCompleted();
    }
}