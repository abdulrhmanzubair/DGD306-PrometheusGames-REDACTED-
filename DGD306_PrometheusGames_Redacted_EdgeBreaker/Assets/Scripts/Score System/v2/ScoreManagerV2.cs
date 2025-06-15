using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class ScoreManagerV2 : MonoBehaviour
{
    public static ScoreManagerV2 instance;

    [Header("UI References - Regular Text")]
    public Text ScoreText;
    public Text highscoreText;
    public Text timerText; // Timer display

    [Header("UI References - TextMeshPro")]
    public TextMeshProUGUI ScoreTextTMP;
    public TextMeshProUGUI highscoreTextTMP;
    public TextMeshProUGUI timerTextTMP; // Timer display

    [Header("Score Settings")]
    public bool resetScoreOnGameRestart = true;

    [Header("Timer Settings")]
    public bool useTimer = true;
    public float levelTimeLimit = 300f; // 5 minutes default

    [Header("Time Bonus Settings")]
    public bool giveTimeBonus = true;
    public int pointsPerSecondRemaining = 10;
    public int maximumTimeBonus = 2000;
    public float bonusMultiplier = 1f; // Multiplier for harder levels

    [Header("Timer Visual Settings")]
    public Color normalTimeColor = Color.white;
    public Color warningTimeColor = Color.yellow;
    public Color criticalTimeColor = Color.red;
    public float warningTimeThreshold = 60f; // Seconds
    public float criticalTimeThreshold = 30f; // Seconds

    [Header("Debug")]
    public bool showDebugInfo = true;

    // Score variables
    private int currentScore = 0;
    private int highscore = 0;
    private int levelStartScore = 0; // Score at the beginning of current level

    // Timer variables
    private float currentTime;
    private bool timerRunning = false;
    private bool timeUp = false;
    private float levelStartTime;

    // Properties for external access
    public int CurrentScore => currentScore;
    public int Highscore => highscore;
    public int CurrentLevelScore => currentScore - levelStartScore;
    public float TimeRemaining => Mathf.Max(0, currentTime);
    public float TimeElapsed => levelTimeLimit - currentTime;
    public bool IsTimerRunning => timerRunning;

    private void Awake()
    {
        // Singleton pattern with persistence across scenes
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);

            // Load saved data
            LoadData();
            DebugLog("ScoreManager created and persisted across scenes");
        }
        else if (instance != this)
        {
            DebugLog("Another ScoreManager found, copying UI references and destroying this one");

            // If another instance exists, copy UI references and destroy this one
            if (ScoreText != null) instance.ScoreText = ScoreText;
            if (highscoreText != null) instance.highscoreText = highscoreText;
            if (timerText != null) instance.timerText = timerText;
            if (ScoreTextTMP != null) instance.ScoreTextTMP = ScoreTextTMP;
            if (highscoreTextTMP != null) instance.highscoreTextTMP = highscoreTextTMP;
            if (timerTextTMP != null) instance.timerTextTMP = timerTextTMP;

            Destroy(gameObject);
            return;
        }

        // Listen for scene changes
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void Start()
    {
        DebugLog($"ScoreManager Start - Current Score: {currentScore}, Highscore: {highscore}");
        UpdateUI();

        // Start timer if enabled
        if (useTimer)
        {
            StartLevelTimer();
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            DebugLog("ScoreManager destroyed");
        }
    }

    void Update()
    {
        // Update timer
        if (timerRunning && useTimer)
        {
            UpdateTimer();
        }
    }

    // Called when a new scene loads
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Record score at level start for level-specific calculations
        levelStartScore = currentScore;

        DebugLog($"Scene loaded: {scene.name}, Current Score: {currentScore}, Level Start Score: {levelStartScore}");

        // Try to find UI elements if they're not assigned
        FindUIElements();

        // Update UI
        UpdateUI();

        // Restart timer for new level
        if (useTimer)
        {
            StartLevelTimer();
        }
    }

    #region Timer System

    public void StartLevelTimer()
    {
        currentTime = levelTimeLimit;
        timerRunning = true;
        timeUp = false;
        levelStartTime = Time.time;

        DebugLog($"Timer started - Level time limit: {levelTimeLimit} seconds");
        UpdateTimerUI();
    }

    public void PauseTimer()
    {
        timerRunning = false;
        DebugLog("Timer paused");
    }

    public void ResumeTimer()
    {
        timerRunning = true;
        DebugLog("Timer resumed");
    }

    public void StopTimer()
    {
        timerRunning = false;
        DebugLog("Timer stopped");
    }

    public void AddTime(float seconds)
    {
        currentTime += seconds;
        currentTime = Mathf.Min(currentTime, levelTimeLimit); // Cap at max time
        DebugLog($"Added {seconds} seconds to timer. Current time: {currentTime}");
        UpdateTimerUI();
    }

    public void SetTimeLimit(float newTimeLimit)
    {
        levelTimeLimit = newTimeLimit;
        if (!timerRunning)
        {
            currentTime = levelTimeLimit;
        }
        DebugLog($"Time limit set to: {newTimeLimit} seconds");
        UpdateTimerUI();
    }

    void UpdateTimer()
    {
        if (timeUp) return;

        currentTime -= Time.deltaTime;

        if (currentTime <= 0)
        {
            currentTime = 0;
            timeUp = true;
            timerRunning = false;

            DebugLog("TIME UP! Timer stopped.");
            // Timer just stops - no auto-transition
        }

        UpdateTimerUI();
    }


    #endregion

    #region Time Bonus System

    public int CalculateTimeBonus()
    {
        if (!giveTimeBonus || timeUp) return 0;

        float timeRemaining = Mathf.Max(0, currentTime);
        int baseBonus = Mathf.RoundToInt(timeRemaining * pointsPerSecondRemaining);
        int finalBonus = Mathf.RoundToInt(baseBonus * bonusMultiplier);

        // Cap at maximum
        finalBonus = Mathf.Min(finalBonus, maximumTimeBonus);

        return finalBonus;
    }

    public void AwardTimeBonus()
    {
        int timeBonus = CalculateTimeBonus();

        if (timeBonus > 0)
        {
            DebugLog($"Awarding time bonus: {timeBonus} points ({currentTime:F1} seconds remaining)");

            // Add the time bonus to the score
            currentScore += timeBonus;

            // Update UI immediately to show new score
            UpdateUI();

            // Show time bonus message
            ShowTimeBonusMessage(timeBonus);

            DebugLog($"Time bonus added! New total score: {currentScore}");
        }
        else
        {
            DebugLog("No time bonus awarded (time ran out or disabled)");
        }
    }

    void ShowTimeBonusMessage(int bonus)
    {
        DebugLog($"TIME BONUS: +{bonus} points!");

        // You can customize this to show in UI
        // Example: UIManager.Instance?.ShowMessage($"Time Bonus: +{bonus} points!", 2f);
    }

    #endregion

    // Try to automatically find UI elements in the scene
    private void FindUIElements()
    {
        DebugLog("Looking for UI elements...");

        // Look for regular Text components
        if (ScoreText == null)
        {
            GameObject scoreTextObj = GameObject.Find("ScoreText");
            if (scoreTextObj != null)
            {
                ScoreText = scoreTextObj.GetComponent<Text>();
                if (ScoreText != null) DebugLog("Found regular Text ScoreText component");
            }
        }

        if (highscoreText == null)
        {
            GameObject highscoreTextObj = GameObject.Find("HighscoreText");
            if (highscoreTextObj != null)
            {
                highscoreText = highscoreTextObj.GetComponent<Text>();
                if (highscoreText != null) DebugLog("Found regular Text HighscoreText component");
            }
        }

        if (timerText == null)
        {
            GameObject timerTextObj = GameObject.Find("TimerText");
            if (timerTextObj != null)
            {
                timerText = timerTextObj.GetComponent<Text>();
                if (timerText != null) DebugLog("Found regular Text TimerText component");
            }
        }

        // Look for TextMeshPro components
        if (ScoreTextTMP == null)
        {
            GameObject scoreTextObj = GameObject.Find("ScoreText");
            if (scoreTextObj != null)
            {
                ScoreTextTMP = scoreTextObj.GetComponent<TextMeshProUGUI>();
                if (ScoreTextTMP != null) DebugLog("Found TextMeshPro ScoreText component");
            }
        }

        if (highscoreTextTMP == null)
        {
            GameObject highscoreTextObj = GameObject.Find("HighscoreText");
            if (highscoreTextObj != null)
            {
                highscoreTextTMP = highscoreTextObj.GetComponent<TextMeshProUGUI>();
                if (highscoreTextTMP != null) DebugLog("Found TextMeshPro HighscoreText component");
            }
        }

        if (timerTextTMP == null)
        {
            GameObject timerTextObj = GameObject.Find("TimerText");
            if (timerTextObj != null)
            {
                timerTextTMP = timerTextObj.GetComponent<TextMeshProUGUI>();
                if (timerTextTMP != null) DebugLog("Found TextMeshPro TimerText component");
            }
        }

        // Debug what we found
        DebugLog($"UI Elements found - Regular Text: {ScoreText != null && highscoreText != null}, TextMeshPro: {ScoreTextTMP != null && highscoreTextTMP != null}, Timer: {timerText != null || timerTextTMP != null}");
    }

    public void AddPoint(int points = 1)
    {
        currentScore += points;
        DebugLog($"Added {points} points. New score: {currentScore}");
        UpdateUI();

        // Check for new high score
        CheckAndUpdateHighscore();
    }

    public void AddPoints(int points)
    {
        AddPoint(points);
    }

    private void CheckAndUpdateHighscore()
    {
        if (currentScore > highscore)
        {
            int oldHighscore = highscore;
            highscore = currentScore;
            SaveHighscore();

            DebugLog($"NEW HIGH SCORE! Old: {oldHighscore}, New: {highscore}");

            // Update UI to show new high score immediately
            UpdateUI();

            // You can add visual feedback here for new high score
            OnNewHighScore();
        }
        else
        {
            DebugLog($"Current score {currentScore} did not beat high score {highscore}");
        }
    }

    // Called when transitioning to next level
    public void OnLevelComplete()
    {
        DebugLog("Level completed by player!");

        // Stop timer first
        StopTimer();

        // Award time bonus BEFORE checking high score
        AwardTimeBonus();

        // NOW check for high score with the bonus included
        CheckAndUpdateHighscore();

        // Save current progress
        SaveData();

        DebugLog($"Level Complete! Current Score: {currentScore}, Level Score: {CurrentLevelScore}");

        // Start transition to next level (automatic)
        StartCoroutine(LevelCompleteTransition());
    }

    // New method: Complete level without automatic scene transition
    public void OnLevelCompleteNoTransition()
    {
        DebugLog("Level completed by player (no auto-transition)!");

        // Stop timer first
        StopTimer();

        // Award time bonus BEFORE checking high score
        AwardTimeBonus();

        // NOW check for high score with the bonus included
        CheckAndUpdateHighscore();

        // Save current progress
        SaveData();

        DebugLog($"Level Complete! Current Score: {currentScore}, Level Score: {CurrentLevelScore}");

        // No automatic scene transition - let caller handle it
    }

    IEnumerator LevelCompleteTransition()
    {
        yield return new WaitForSeconds(2f); // Show bonus for a moment
        GoToNextLevel();
    }

    void GoToNextLevel()
    {
        int nextSceneIndex = SceneManager.GetActiveScene().buildIndex + 1;

        if (nextSceneIndex < SceneManager.sceneCountInBuildSettings)
        {
            DebugLog($"Loading next level: {nextSceneIndex}");
            SceneManager.LoadScene(nextSceneIndex);
        }
        else
        {
            DebugLog("No more levels! Game complete!");
            // Handle game completion
            OnGameComplete();
        }
    }

    void OnGameComplete()
    {
        DebugLog("GAME COMPLETE!");
        CheckAndUpdateHighscore();
        SaveData();

        // You can load a game complete scene or restart
        // SceneManager.LoadScene("GameComplete");
        // Or restart from first level
        SceneManager.LoadScene(0);
    }

    // Called when the game is completely restarted (like from main menu)
    public void RestartGame()
    {
        if (resetScoreOnGameRestart)
        {
            currentScore = 0;
            levelStartScore = 0;
        }

        UpdateUI();
        SaveData();

        DebugLog("Game Restarted");
    }

    // Reset only the current session (keeps high score)
    public void ResetCurrentScore()
    {
        currentScore = 0;
        levelStartScore = 0;
        UpdateUI();
        SaveData();
    }

    private void UpdateUI()
    {
        string scoreString = currentScore.ToString() + " POINTS";
        string highscoreString = "HIGHSCORE: " + highscore.ToString();

        DebugLog($"Updating UI - Score: {scoreString}, Highscore: {highscoreString}");

        // Update regular Text components
        if (ScoreText != null)
        {
            ScoreText.text = scoreString;
            DebugLog("Updated regular Text ScoreText");
        }

        if (highscoreText != null)
        {
            highscoreText.text = highscoreString;
            DebugLog("Updated regular Text HighscoreText");
        }

        // Update TextMeshPro components
        if (ScoreTextTMP != null)
        {
            ScoreTextTMP.text = scoreString;
            DebugLog("Updated TextMeshPro ScoreText");
        }

        if (highscoreTextTMP != null)
        {
            highscoreTextTMP.text = highscoreString;
            DebugLog("Updated TextMeshPro HighscoreText");
        }

        // Update timer
        UpdateTimerUI();

        // If no UI found, warn user
        if (ScoreText == null && highscoreText == null && ScoreTextTMP == null && highscoreTextTMP == null)
        {
            DebugLog("WARNING: No UI elements found to update!");
        }
    }

    private void UpdateTimerUI()
    {
        if (!useTimer) return;

        string timeString = FormatTime(currentTime);
        Color timeColor = GetTimerColor();

        // Update regular Text timer
        if (timerText != null)
        {
            timerText.text = timeString;
            timerText.color = timeColor;
        }

        // Update TextMeshPro timer
        if (timerTextTMP != null)
        {
            timerTextTMP.text = timeString;
            timerTextTMP.color = timeColor;
        }
    }

    string FormatTime(float time)
    {
        int minutes = Mathf.FloorToInt(time / 60);
        int seconds = Mathf.FloorToInt(time % 60);
        return $"{minutes:00}:{seconds:00}";
    }

    Color GetTimerColor()
    {
        if (currentTime <= criticalTimeThreshold)
            return criticalTimeColor;
        else if (currentTime <= warningTimeThreshold)
            return warningTimeColor;
        else
            return normalTimeColor;
    }

    // Override UI references (useful when UI elements change between scenes)
    public void SetUIReferences(Text scoreText, Text highText, Text timerTextRef = null)
    {
        ScoreText = scoreText;
        highscoreText = highText;
        timerText = timerTextRef;
        DebugLog("Regular Text UI references set manually");
        UpdateUI();
    }

    public void SetUIReferencesTMP(TextMeshProUGUI scoreTextTMP, TextMeshProUGUI highTextTMP, TextMeshProUGUI timerTextRef = null)
    {
        ScoreTextTMP = scoreTextTMP;
        highscoreTextTMP = highTextTMP;
        timerTextTMP = timerTextRef;
        DebugLog("TextMeshPro UI references set manually");
        UpdateUI();
    }

    private void LoadData()
    {
        highscore = PlayerPrefs.GetInt("highscore", 0);
        DebugLog($"Loaded data - Highscore: {highscore}");
    }

    private void SaveData()
    {
        SaveHighscore();
        DebugLog("Data saved");
    }

    private void SaveHighscore()
    {
        PlayerPrefs.SetInt("highscore", highscore);
        PlayerPrefs.Save();
    }

    // Event for new high score (you can use this to trigger effects)
    private void OnNewHighScore()
    {
        // Add particle effects, sound, screen flash, etc.
    }

    // Public methods for external use
    public void SetScore(int newScore)
    {
        currentScore = newScore;
        DebugLog($"Score set to: {newScore}");
        UpdateUI();
        CheckAndUpdateHighscore();
    }

    public int GetScore()
    {
        return currentScore;
    }

    public int GetHighscore()
    {
        return highscore;
    }

    public int GetCurrentLevelScore()
    {
        return currentScore - levelStartScore;
    }

    // Force save (useful before quitting)
    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
            SaveData();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
            SaveData();
    }

    private void DebugLog(string message)
    {
        if (showDebugInfo)
        {
            Debug.Log($"[ScoreManager] {message}");
        }
    }
}