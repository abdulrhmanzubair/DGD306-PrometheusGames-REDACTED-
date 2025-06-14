using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Enhanced LevelExitTrigger with score system integration
/// Replaces your existing LevelExitTrigger
/// </summary>
public class LevelExitTrigger : MonoBehaviour
{
    [Header("Exit Settings")]
    [Tooltip("Scene to load when all players reach the exit")]
    public string nextSceneName = "";
    [Tooltip("If true, loads next scene in build order")]
    public bool useNextInBuildOrder = true;
    [Tooltip("Require all players to be in trigger area")]
    public bool requireAllPlayers = true;
    [Tooltip("Time to wait before transitioning (allows players to see they reached the exit)")]
    public float exitDelay = 1f;

    [Header("Score Integration")]
    [Tooltip("Bonus points for completing the level")]
    public int levelCompletionBonus = 500;
    [Tooltip("Award bonus based on remaining time")]
    public bool timeBonus = false;
    [Tooltip("Points per second remaining (if timeBonus is true)")]
    public int pointsPerSecond = 10;
    [Tooltip("Level time limit for bonus calculation")]
    public float levelTimeLimit = 300f; // 5 minutes

    [Header("Visual Feedback")]
    [Tooltip("Effect to spawn when triggered")]
    public GameObject exitEffect;
    [Tooltip("Sound to play when triggered")]
    public AudioClip exitSound;
    [Tooltip("Message to show when players reach exit")]
    public string exitMessage = "Level Complete!";

    [Header("Player Detection")]
    [Tooltip("Tags that count as players")]
    public string[] playerTags = { "Player" };
    [Tooltip("Specific player objects (alternative to tags)")]
    public GameObject[] specificPlayers;

    [Header("Debug")]
    public bool showDebugInfo = true;
    public Color gizmoColor = Color.green;

    private HashSet<GameObject> playersInTrigger = new HashSet<GameObject>();
    private bool hasTriggered = false;
    private int requiredPlayerCount = 0;
    private float levelStartTime;

    // Events
    public System.Action<GameObject> OnPlayerEnter;
    public System.Action<GameObject> OnPlayerExit;
    public System.Action OnAllPlayersReached;
    public System.Action OnLevelComplete;

    void Start()
    {
        // Record level start time for bonus calculations
        levelStartTime = Time.time;

        // Make sure we have a trigger collider
        Collider2D col = GetComponent<Collider2D>();
        if (col == null)
        {
            col = gameObject.AddComponent<BoxCollider2D>();
            DebugLog("Added BoxCollider2D component automatically");
        }
        col.isTrigger = true;

        // Determine required player count
        if (specificPlayers != null && specificPlayers.Length > 0)
        {
            requiredPlayerCount = specificPlayers.Length;
        }
        else
        {
            // Count players in scene by tags
            requiredPlayerCount = CountPlayersInScene();
        }

        DebugLog($"Level exit initialized. Required players: {requiredPlayerCount}");

        // Notify ScoreManager of level start
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.SetCurrentLevel(GetCurrentLevelNumber());
        }
    }

    int GetCurrentLevelNumber()
    {
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        // Try to extract level number from scene name
        if (sceneName.Contains("LVL_"))
        {
            string levelPart = sceneName.Replace("LVL_", "");
            if (int.TryParse(levelPart, out int levelNum))
            {
                return levelNum;
            }
        }

        // Fallback to build index
        return UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex;
    }

    int CountPlayersInScene()
    {
        int count = 0;
        foreach (string tag in playerTags)
        {
            GameObject[] players = GameObject.FindGameObjectsWithTag(tag);
            count += players.Length;
        }

        // Also check for player controllers if no tagged objects found
        if (count == 0)
        {
            PlayerController[] gunners = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            Player_Melee_Controller1[] melee = FindObjectsByType<Player_Melee_Controller1>(FindObjectsSortMode.None);
            count = gunners.Length + melee.Length;
        }

        return count;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (hasTriggered) return;

        if (IsPlayer(other.gameObject))
        {
            playersInTrigger.Add(other.gameObject);
            OnPlayerEnter?.Invoke(other.gameObject);

            DebugLog($"Player {other.name} entered exit area. Players in area: {playersInTrigger.Count}/{requiredPlayerCount}");

            // Check if we should trigger the exit
            CheckForExit();
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (hasTriggered) return;

        if (IsPlayer(other.gameObject))
        {
            playersInTrigger.Remove(other.gameObject);
            OnPlayerExit?.Invoke(other.gameObject);

            DebugLog($"Player {other.name} left exit area. Players in area: {playersInTrigger.Count}/{requiredPlayerCount}");
        }
    }

    bool IsPlayer(GameObject obj)
    {
        // Check specific players first
        if (specificPlayers != null)
        {
            foreach (GameObject player in specificPlayers)
            {
                if (player == obj) return true;
            }
        }

        // Check by tags
        foreach (string tag in playerTags)
        {
            if (obj.CompareTag(tag)) return true;
        }

        // Check by components
        if (obj.GetComponent<PlayerController>() != null ||
            obj.GetComponent<Player_Melee_Controller1>() != null)
        {
            return true;
        }

        return false;
    }

    void CheckForExit()
    {
        bool shouldExit = false;

        if (requireAllPlayers)
        {
            // Need all players in the trigger area
            shouldExit = playersInTrigger.Count >= requiredPlayerCount;
        }
        else
        {
            // Just need at least one player
            shouldExit = playersInTrigger.Count > 0;
        }

        if (shouldExit && !hasTriggered)
        {
            TriggerExit();
        }
    }

    void TriggerExit()
    {
        hasTriggered = true;

        DebugLog("Level exit triggered!");
        OnAllPlayersReached?.Invoke();

        // Calculate and award score bonuses
        AwardLevelCompletionScore();

        // Visual/Audio feedback
        if (exitEffect != null)
        {
            Instantiate(exitEffect, transform.position, Quaternion.identity);
        }

        if (exitSound != null)
        {
            AudioSource.PlayClipAtPoint(exitSound, transform.position);
        }

        // Show exit message (you can customize this)
        if (!string.IsNullOrEmpty(exitMessage))
        {
            ShowExitMessage(exitMessage);
        }

        // Notify ScoreManager of level completion
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.OnLevelCompleted();
        }

        // Start transition after delay
        StartCoroutine(ExitAfterDelay());
    }

    void AwardLevelCompletionScore()
    {
        if (ScoreManager.Instance == null) return;

        int totalBonus = 0;

        // Base completion bonus
        if (levelCompletionBonus > 0)
        {
            totalBonus += levelCompletionBonus;
            DebugLog($"Level completion bonus: {levelCompletionBonus} points");
        }

        // Time bonus
        if (timeBonus && pointsPerSecond > 0)
        {
            float levelTime = Time.time - levelStartTime;
            float timeRemaining = Mathf.Max(0, levelTimeLimit - levelTime);
            int timeBonusPoints = Mathf.RoundToInt(timeRemaining * pointsPerSecond);

            if (timeBonusPoints > 0)
            {
                totalBonus += timeBonusPoints;
                DebugLog($"Time bonus: {timeBonusPoints} points ({timeRemaining:F1}s remaining)");
            }
        }

        // Award the total bonus
        if (totalBonus > 0)
        {
            ScoreManager.Instance.AddScore(totalBonus);
            DebugLog($"Total level bonus awarded: {totalBonus} points");
        }
    }

    System.Collections.IEnumerator ExitAfterDelay()
    {
        yield return new WaitForSeconds(exitDelay);

        OnLevelComplete?.Invoke();

        // Check if this is the final level
        bool isFinalLevel = false;
        if (ScoreManager.Instance != null)
        {
            isFinalLevel = ScoreManager.Instance.IsGameComplete();
        }

        if (isFinalLevel)
        {
            DebugLog("Final level completed! Showing final score...");
            // You can add special end-game logic here
            // For now, we'll still transition to the next scene (could be credits/score screen)
        }

        // Trigger level transition
        if (LevelTransitionManager.Instance != null)
        {
            if (useNextInBuildOrder)
            {
                LevelTransitionManager.Instance.TransitionToNextLevel();
            }
            else if (!string.IsNullOrEmpty(nextSceneName))
            {
                LevelTransitionManager.Instance.TransitionToScene(nextSceneName);
            }
            else
            {
                DebugLog("No next scene specified!");
            }
        }
        else
        {
            DebugLog("LevelTransitionManager not found! Loading scene directly...");

            // Fallback to direct scene loading
            if (useNextInBuildOrder)
            {
                int nextScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex + 1;
                UnityEngine.SceneManagement.SceneManager.LoadScene(nextScene);
            }
            else if (!string.IsNullOrEmpty(nextSceneName))
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(nextSceneName);
            }
        }
    }

    void ShowExitMessage(string message)
    {
        // Enhanced message with score info
        if (ScoreManager.Instance != null)
        {
            int currentScore = ScoreManager.Instance.GetScore();
            int levelScore = ScoreManager.Instance.GetCurrentLevelScore();
            message += $"\nLevel Score: {levelScore}\nTotal Score: {currentScore}";
        }

        DebugLog($"EXIT MESSAGE: {message}");

        // If you have TransitionUI, show the message
        if (TransitionUI.Instance != null)
        {
            TransitionUI.Instance.ShowMessage(message, exitDelay);
        }
    }

    // Public methods for external control
    public void ForceExit()
    {
        if (!hasTriggered)
        {
            TriggerExit();
        }
    }

    public void ResetTrigger()
    {
        hasTriggered = false;
        playersInTrigger.Clear();
    }

    public int GetPlayersInArea()
    {
        return playersInTrigger.Count;
    }

    public bool AllPlayersInArea()
    {
        return playersInTrigger.Count >= requiredPlayerCount;
    }

    public float GetLevelTime()
    {
        return Time.time - levelStartTime;
    }

    void DebugLog(string message)
    {
        if (showDebugInfo)
        {
            Debug.Log($"[LevelExit] {message}");
        }
    }

    void OnDrawGizmos()
    {
        // Draw the trigger area
        Gizmos.color = gizmoColor;

        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            if (col is BoxCollider2D box)
            {
                Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
                Gizmos.DrawWireCube(box.offset, box.size);

                // Fill with transparent color
                Color fillColor = gizmoColor;
                fillColor.a = 0.2f;
                Gizmos.color = fillColor;
                Gizmos.DrawCube(box.offset, box.size);
            }
        }

        // Draw label
#if UNITY_EDITOR
        Vector3 labelPos = transform.position + Vector3.up * 2f;
        string label = useNextInBuildOrder ? "Exit: Next Level" : $"Exit: {nextSceneName}";
        if (levelCompletionBonus > 0)
        {
            label += $"\nBonus: {levelCompletionBonus} pts";
        }
        UnityEditor.Handles.Label(labelPos, label);
#endif
    }
}