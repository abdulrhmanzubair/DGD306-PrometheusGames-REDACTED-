using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIConnector : MonoBehaviour
{
    [Header("Debug")]
    public bool showDebugInfo = true;

    [Header("Testing - Create ScoreManager if Missing")]
    public bool createScoreManagerIfMissing = true;
    public int testStartingScore = 0;
    public int testHighScore = 500;

    void Start()
    {
        DebugLog("UIConnector starting...");
        // Wait a frame to make sure everything is ready
        Invoke("ConnectUI", 0.1f);
    }

    void ConnectUI()
    {
        // Check if ScoreManager exists
        if (ScoreManagerV2.instance == null)
        {
            if (createScoreManagerIfMissing)
            {
                DebugLog("ScoreManager not found! Creating one for testing...");
                CreateScoreManager();
            }
            else
            {
                DebugLog("ERROR: ScoreManager not found! Make sure it exists in your first level and has DontDestroyOnLoad.");
                return;
            }
        }

        DebugLog("ScoreManager found! Attempting to connect UI...");

        // Find the UI elements in this scene
        GameObject scoreTextObj = GameObject.Find("ScoreText");
        GameObject highscoreTextObj = GameObject.Find("HighscoreText");
        GameObject timerTextObj = GameObject.Find("TimerText");

        if (scoreTextObj == null || highscoreTextObj == null)
        {
            DebugLog("ERROR: Could not find ScoreText or HighscoreText GameObjects! Make sure they are named exactly 'ScoreText' and 'HighscoreText'");
            return;
        }

        DebugLog("Found ScoreText and HighscoreText GameObjects");
        if (timerTextObj != null) DebugLog("Found TimerText GameObject");

        // Try regular Text components first
        Text scoreText = scoreTextObj.GetComponent<Text>();
        Text highscoreText = highscoreTextObj.GetComponent<Text>();
        Text timerText = timerTextObj?.GetComponent<Text>();

        // Try TextMeshPro components
        TextMeshProUGUI scoreTextTMP = scoreTextObj.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI highscoreTextTMP = highscoreTextObj.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI timerTextTMP = timerTextObj?.GetComponent<TextMeshProUGUI>();

        // Connect based on what we found
        if (scoreText != null && highscoreText != null)
        {
            DebugLog("Found regular Text components, connecting...");
            ScoreManagerV2.instance.SetUIReferences(scoreText, highscoreText, timerText);
            DebugLog("Regular Text UI connected successfully!");
        }
        else if (scoreTextTMP != null && highscoreTextTMP != null)
        {
            DebugLog("Found TextMeshPro components, connecting...");
            ScoreManagerV2.instance.SetUIReferencesTMP(scoreTextTMP, highscoreTextTMP, timerTextTMP);
            DebugLog("TextMeshPro UI connected successfully!");
        }
        else
        {
            DebugLog("ERROR: Could not find Text or TextMeshPro components on the GameObjects!");
            DebugLog($"ScoreText has Text: {scoreText != null}, has TMP: {scoreTextTMP != null}");
            DebugLog($"HighscoreText has Text: {highscoreText != null}, has TMP: {highscoreTextTMP != null}");
        }

        // Test the connection
        DebugLog($"Current score after connection: {ScoreManagerV2.instance.GetScore()}");
        DebugLog($"Timer status: {(ScoreManagerV2.instance.IsTimerRunning ? "Running" : "Stopped")} - Time remaining: {ScoreManagerV2.instance.TimeRemaining:F1}s");
    }

    void CreateScoreManager()
    {
        // Create a new GameObject for ScoreManager
        GameObject scoreManagerObj = new GameObject("ScoreManager (Auto-Created)");

        // Add the ScoreManagerV2 component
        ScoreManagerV2 scoreManager = scoreManagerObj.AddComponent<ScoreManagerV2>();

        // Set some test values
        if (testStartingScore > 0)
        {
            // We'll set the score after the ScoreManager initializes
            Invoke("SetTestScore", 0.2f);
        }

        DebugLog($"ScoreManager created for testing! Starting score: {testStartingScore}, Test high score: {testHighScore}");
    }

    void SetTestScore()
    {
        if (ScoreManagerV2.instance != null && testStartingScore > 0)
        {
            ScoreManagerV2.instance.SetScore(testStartingScore);
            DebugLog($"Set test starting score to: {testStartingScore}");
        }
    }

    private void DebugLog(string message)
    {
        if (showDebugInfo)
        {
            Debug.Log($"[UIConnector] {message}");
        }
    }
}