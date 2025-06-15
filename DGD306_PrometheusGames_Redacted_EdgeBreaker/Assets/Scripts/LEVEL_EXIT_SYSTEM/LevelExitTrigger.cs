using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Enhanced LevelExitTrigger with ScoreManagerV2 integration
/// Updated to work with the new timer-based scoring system
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
    [Tooltip("Award bonus based on remaining time (handled by ScoreManagerV2)")]
    public bool timeBonus = true;
    [Tooltip("Points per second remaining (if timeBonus is true) - DEPRECATED: Use ScoreManagerV2 settings")]
    public int pointsPerSecond = 10;
    [Tooltip("Level time limit for bonus calculation - DEPRECATED: Use ScoreManagerV2 settings")]
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

        // Notify ScoreManagerV2 of level start (updated for new system)
        if (ScoreManagerV2.instance != null)
        {
            // Set timer settings if ScoreManagerV2 should use custom values
            if (levelTimeLimit != 300f) // Only if different from default
            {
                ScoreManagerV2.instance.SetTimeLimit(levelTimeLimit);
                DebugLog($"Set custom time limit: {levelTimeLimit} seconds");
            }
        }
        else
        {
            DebugLog("Warning: ScoreManagerV2 not found! Make sure it exists in your first level.");
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

        // Award level completion bonus FIRST
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

        // Start the completion process with proper timing
        StartCoroutine(CompleteAfterDelay());
    }

    void AwardLevelCompletionScore()
    {
        // Updated to use ScoreManagerV2
        if (ScoreManagerV2.instance == null)
        {
            DebugLog("Warning: ScoreManagerV2 not found! Cannot award completion bonus.");
            return;
        }

        int totalBonus = 0;

        // Base completion bonus
        if (levelCompletionBonus > 0)
        {
            totalBonus += levelCompletionBonus;
            DebugLog($"Level completion bonus: {levelCompletionBonus} points");
        }

        // Legacy time bonus calculation (only if ScoreManagerV2 doesn't handle it)
        if (timeBonus && pointsPerSecond > 0 && !ScoreManagerV2.instance.useTimer)
        {
            float levelTime = Time.time - levelStartTime;
            float timeRemaining = Mathf.Max(0, levelTimeLimit - levelTime);
            int timeBonusPoints = Mathf.RoundToInt(timeRemaining * pointsPerSecond);

            if (timeBonusPoints > 0)
            {
                totalBonus += timeBonusPoints;
                DebugLog($"Legacy time bonus: {timeBonusPoints} points ({timeRemaining:F1}s remaining)");
            }
        }
        else if (timeBonus && ScoreManagerV2.instance.useTimer)
        {
            DebugLog("Time bonus will be calculated by ScoreManagerV2 automatically");
        }

        // Award the completion bonus (time bonus handled separately by ScoreManagerV2)
        if (totalBonus > 0)
        {
            ScoreManagerV2.instance.AddPoints(totalBonus);
            DebugLog($"Level completion bonus awarded: {totalBonus} points");
        }
    }

    System.Collections.IEnumerator CompleteAfterDelay()
    {
        yield return new WaitForSeconds(exitDelay);

        OnLevelComplete?.Invoke();

        // Notify ScoreManagerV2 of level completion (WITHOUT automatic scene transition)
        if (ScoreManagerV2.instance != null)
        {
            ScoreManagerV2.instance.OnLevelCompleteNoTransition();
            DebugLog("Level completion sent to ScoreManagerV2 (no auto-transition)");

            // Now handle scene transition based on OUR settings
            yield return StartCoroutine(HandleSceneTransition());
        }
        else
        {
            DebugLog("ScoreManagerV2 not found! Using fallback level transition...");
            // Fallback level transition
            yield return StartCoroutine(FallbackExitAfterDelay());
        }
    }

    System.Collections.IEnumerator HandleSceneTransition()
    {
        // Small delay to show completion effects
        yield return new WaitForSeconds(0.5f);

        DebugLog($"Handling scene transition - UseNextInBuildOrder: {useNextInBuildOrder}, NextSceneName: '{nextSceneName}'");

        // Check if this is the final level
        bool isFinalLevel = false;
        int currentSceneIndex = UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex;
        int nextSceneIndex = currentSceneIndex + 1;

        if (useNextInBuildOrder)
        {
            isFinalLevel = nextSceneIndex >= UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings;

            if (isFinalLevel)
            {
                DebugLog("Final level completed! No more levels in build order.");
                yield break; // Exit without loading another scene
            }

            DebugLog($"Loading next scene by build index: {nextSceneIndex}");
            UnityEngine.SceneManagement.SceneManager.LoadScene(nextSceneIndex);
        }
        else if (!string.IsNullOrEmpty(nextSceneName))
        {
            DebugLog($"Loading scene by name: '{nextSceneName}'");

            // Check if scene exists in build settings
            bool sceneExists = false;
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings; i++)
            {
                string scenePath = UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(i);
                string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
                if (sceneName == nextSceneName)
                {
                    sceneExists = true;
                    break;
                }
            }

            if (sceneExists)
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(nextSceneName);
            }
            else
            {
                DebugLog($"ERROR: Scene '{nextSceneName}' not found in build settings! Check spelling and make sure it's added to build.");
            }
        }
        else
        {
            DebugLog("No next scene specified! Set nextSceneName or enable useNextInBuildOrder.");
        }
    }

    System.Collections.IEnumerator FallbackExitAfterDelay()
    {
        yield return new WaitForSeconds(1f);

        DebugLog("Using fallback scene transition logic...");

        // Use LevelTransitionManager if available
        if (LevelTransitionManager.Instance != null)
        {
            DebugLog("LevelTransitionManager found, using it for transition");

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
            DebugLog("LevelTransitionManager not found! Using direct scene loading...");

            // Direct scene loading with proper scene name support
            if (useNextInBuildOrder)
            {
                int currentSceneIndex = UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex;
                int nextSceneIndex = currentSceneIndex + 1;

                if (nextSceneIndex < UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings)
                {
                    DebugLog($"Loading next scene by build index: {nextSceneIndex}");
                    UnityEngine.SceneManagement.SceneManager.LoadScene(nextSceneIndex);
                }
                else
                {
                    DebugLog("No more scenes in build order!");
                }
            }
            else if (!string.IsNullOrEmpty(nextSceneName))
            {
                DebugLog($"Loading scene by name: '{nextSceneName}'");

                // Check if scene exists in build settings
                bool sceneExists = false;
                for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings; i++)
                {
                    string scenePath = UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(i);
                    string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
                    if (sceneName == nextSceneName)
                    {
                        sceneExists = true;
                        break;
                    }
                }

                if (sceneExists)
                {
                    UnityEngine.SceneManagement.SceneManager.LoadScene(nextSceneName);
                }
                else
                {
                    DebugLog($"ERROR: Scene '{nextSceneName}' not found in build settings! Available scenes:");
                    // List available scenes for debugging
                    for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings; i++)
                    {
                        string scenePath = UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(i);
                        string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
                        DebugLog($"  Scene {i}: {sceneName}");
                    }
                }
            }
            else
            {
                DebugLog("No next scene specified! Set nextSceneName or enable useNextInBuildOrder.");
            }
        }
    }

    void ShowExitMessage(string message)
    {
        // Enhanced message with score info from ScoreManagerV2
        if (ScoreManagerV2.instance != null)
        {
            int currentScore = ScoreManagerV2.instance.GetScore();
            int levelScore = ScoreManagerV2.instance.GetCurrentLevelScore();
            float timeRemaining = ScoreManagerV2.instance.TimeRemaining;
            int potentialTimeBonus = ScoreManagerV2.instance.CalculateTimeBonus();

            message += $"\nLevel Score: {levelScore}";
            if (ScoreManagerV2.instance.useTimer)
            {
                message += $"\nTime Remaining: {timeRemaining:F1}s";
                if (potentialTimeBonus > 0)
                {
                    message += $"\nTime Bonus: +{potentialTimeBonus} points!";
                }
            }
            message += $"\nTotal Score: {currentScore}";

            // Check if new high score
            if (currentScore >= ScoreManagerV2.instance.GetHighscore())
            {
                message += "\nNEW HIGH SCORE!";
            }
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

    // New methods for ScoreManagerV2 integration
    public float GetTimeRemaining()
    {
        if (ScoreManagerV2.instance != null && ScoreManagerV2.instance.useTimer)
        {
            return ScoreManagerV2.instance.TimeRemaining;
        }
        else
        {
            // Fallback calculation
            float levelTime = Time.time - levelStartTime;
            return Mathf.Max(0, levelTimeLimit - levelTime);
        }
    }

    public int GetPotentialTimeBonus()
    {
        if (ScoreManagerV2.instance != null)
        {
            return ScoreManagerV2.instance.CalculateTimeBonus();
        }
        else
        {
            // Fallback calculation
            float timeRemaining = GetTimeRemaining();
            return Mathf.RoundToInt(timeRemaining * pointsPerSecond);
        }
    }

    public bool IsTimerActive()
    {
        if (ScoreManagerV2.instance != null)
        {
            return ScoreManagerV2.instance.IsTimerRunning;
        }
        return false;
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

        // Draw label with updated info
#if UNITY_EDITOR
        Vector3 labelPos = transform.position + Vector3.up * 2f;
        string label = useNextInBuildOrder ? "Exit: Next Level" : $"Exit: {nextSceneName}";
        if (levelCompletionBonus > 0)
        {
            label += $"\nBonus: {levelCompletionBonus} pts";
        }

        // Show timer info if ScoreManagerV2 is available
        if (Application.isPlaying && ScoreManagerV2.instance != null)
        {
            if (ScoreManagerV2.instance.useTimer)
            {
                float timeRemaining = ScoreManagerV2.instance.TimeRemaining;
                int timeBonus = ScoreManagerV2.instance.CalculateTimeBonus();
                label += $"\nTime: {timeRemaining:F1}s";
                if (timeBonus > 0)
                {
                    label += $"\nTime Bonus: +{timeBonus}";
                }
            }
        }

        UnityEditor.Handles.Label(labelPos, label);
#endif
    }
}