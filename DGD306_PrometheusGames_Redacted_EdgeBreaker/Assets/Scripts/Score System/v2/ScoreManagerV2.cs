using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class ScoreManagerV2 : MonoBehaviour
{
    public static ScoreManagerV2 instance;

    [Header("UI References")]
    public Text ScoreText;
    public Text highscoreText;

    [Header("Score Settings")]
    public bool resetScoreOnGameRestart = true;

    // Score variables
    private int currentScore = 0;
    private int highscore = 0;
    private int levelStartScore = 0; // Score at the beginning of current level

    // Properties for external access
    public int CurrentScore => currentScore;
    public int Highscore => highscore;
    public int CurrentLevelScore => currentScore - levelStartScore;

    private void Awake()
    {
        // Singleton pattern with persistence across scenes
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);

            // Load saved data
            LoadData();
        }
        else if (instance != this)
        {
            // If another instance exists, copy UI references and destroy this one
            if (ScoreText != null) instance.ScoreText = ScoreText;
            if (highscoreText != null) instance.highscoreText = highscoreText;

            Destroy(gameObject);
            return;
        }

        // Listen for scene changes
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void Start()
    {
        UpdateUI();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    // Called when a new scene loads
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Record score at level start for level-specific calculations
        levelStartScore = currentScore;

        // Try to find UI elements if they're not assigned
        FindUIElements();

        // Update UI
        UpdateUI();

        Debug.Log($"Level loaded: {scene.name}, Current Score: {currentScore}, Level Start Score: {levelStartScore}");
    }

    // Try to automatically find UI elements in the scene
    private void FindUIElements()
    {
        if (ScoreText == null)
        {
            // Try to find by name or tag
            GameObject scoreTextObj = GameObject.Find("ScoreText");
            if (scoreTextObj != null)
                ScoreText = scoreTextObj.GetComponent<Text>();
        }

        if (highscoreText == null)
        {
            GameObject highscoreTextObj = GameObject.Find("HighscoreText");
            if (highscoreTextObj != null)
                highscoreText = highscoreTextObj.GetComponent<Text>();
        }
    }

    public void AddPoint(int points = 1)
    {
        currentScore += points;
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
            highscore = currentScore;
            SaveHighscore();

            Debug.Log($"New High Score: {highscore}!");

            // You can add visual feedback here for new high score
            OnNewHighScore();
        }
    }

    // Called when transitioning to next level
    public void OnLevelComplete()
    {
        CheckAndUpdateHighscore();

        // Save current progress
        SaveData();

        Debug.Log($"Level Complete! Current Score: {currentScore}, Level Score: {CurrentLevelScore}");
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

        Debug.Log("Game Restarted");
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
        if (ScoreText != null)
        {
            ScoreText.text = currentScore.ToString() + " POINTS";
        }

        if (highscoreText != null)
        {
            highscoreText.text = "HIGHSCORE: " + highscore.ToString();
        }
    }

    // Override UI references (useful when UI elements change between scenes)
    public void SetUIReferences(Text scoreText, Text highscoreTextRef)
    {
        ScoreText = scoreText;
        highscoreText = highscoreTextRef;
        UpdateUI();
    }

    private void LoadData()
    {
        highscore = PlayerPrefs.GetInt("highscore", 0);

        // Optionally save current session score (if you want it to persist through app restarts)
        // currentScore = PlayerPrefs.GetInt("currentScore", 0);
        // levelStartScore = PlayerPrefs.GetInt("levelStartScore", 0);
    }

    private void SaveData()
    {
        SaveHighscore();

        // Optionally save current session data
        // PlayerPrefs.SetInt("currentScore", currentScore);
        // PlayerPrefs.SetInt("levelStartScore", levelStartScore);
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
        // Example: if you have a UI animation controller
        // GetComponent<Animator>()?.SetTrigger("NewHighScore");
    }

    // Public methods for external use
    public void SetScore(int newScore)
    {
        currentScore = newScore;
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

    void Update()
    {
        // You can add any per-frame logic here if needed
    }
}