using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

/// <summary>
/// Singleton Game Manager handling death counting and game over logic
/// Expert Unity implementation with proper architecture
/// </summary>
public class GameManager : MonoBehaviour
{
    #region Singleton Pattern

    private static GameManager instance;
    public static GameManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<GameManager>();

                if (instance == null)
                {
                    GameObject go = new GameObject("GameManager");
                    instance = go.AddComponent<GameManager>();
                    Debug.LogWarning("[GameManager] No GameManager found, creating one automatically.");
                }
            }
            return instance;
        }
    }

    #endregion

    #region Serialized Fields

    [Header("Death Management")]
    [SerializeField] private int maxDeathsPerPlayer = 3;
    [SerializeField] private int maxTotalDeaths = 5;
    [SerializeField] private bool useSharedDeathPool = true;

    [Header("Respawn Settings")]
    [SerializeField] private float respawnDelay = 2f;
    [SerializeField] private bool showRespawnCountdown = true;
    [SerializeField] private bool invulnerabilityAfterRespawn = true;
    [SerializeField] private float respawnInvulnerabilityTime = 2f;

    [Header("Game Over Settings")]
    [SerializeField] private GameObject gameOverUI;
    [SerializeField] private bool pauseGameOnGameOver = true;
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Audio")]
    [SerializeField] private AudioClip gameOverSound;
    [SerializeField][Range(0f, 1f)] private float gameOverVolume = 0.8f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    #endregion

    #region Private Fields

    // Death tracking
    private Dictionary<int, int> playerDeaths = new Dictionary<int, int>();
    private int totalDeaths = 0;
    private bool gameIsOver = false;

    // Components
    private AudioSource audioSource;
    private List<PlayerHealthSystem> registeredPlayers = new List<PlayerHealthSystem>();

    // Coroutine tracking
    private Dictionary<PlayerHealthSystem, Coroutine> activeRespawnCoroutines = new Dictionary<PlayerHealthSystem, Coroutine>();

    #endregion

    #region Events

    public event System.Action<int, int> OnPlayerDeath; // (playerIndex, deathCount)
    public event System.Action<int> OnTotalDeathsChanged; // (totalDeaths)
    public event System.Action OnGameOver;
    public event System.Action<PlayerHealthSystem> OnPlayerRespawn; // (player)

    #endregion

    #region Properties

    public int MaxDeathsPerPlayer => maxDeathsPerPlayer;
    public int MaxTotalDeaths => maxTotalDeaths;
    public bool UseSharedDeathPool => useSharedDeathPool;
    public int TotalDeaths => totalDeaths;
    public bool IsGameOver => gameIsOver;
    public int RegisteredPlayerCount => registeredPlayers.Count;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        // Singleton pattern with proper cleanup
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeManager();
        }
        else if (instance != this)
        {
            Debug.LogWarning("[GameManager] Multiple GameManagers detected. Destroying duplicate.");
            Destroy(gameObject);
            return;
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        // Handle cleanup when application loses focus
        if (!hasFocus && gameIsOver)
        {
            CleanupManager();
        }
    }

    private void OnDestroy()
    {
        // Clean up singleton reference
        if (instance == this)
        {
            instance = null;
        }
    }

    private void Start()
    {
        FindAndRegisterPlayers();
        SetupUI();

        if (enableDebugLogs)
        {
            Debug.Log($"[GameManager] Initialized - Max deaths per player: {maxDeathsPerPlayer}, Max total: {maxTotalDeaths}, Shared pool: {useSharedDeathPool}");
        }
    }

    #endregion

    #region Initialization

    private void InitializeManager()
    {
        // Setup audio source
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D audio for UI sounds
        }

        // Initialize collections
        playerDeaths = new Dictionary<int, int>();
        registeredPlayers = new List<PlayerHealthSystem>();
        activeRespawnCoroutines = new Dictionary<PlayerHealthSystem, Coroutine>();
    }

    private void FindAndRegisterPlayers()
    {
        PlayerHealthSystem[] foundPlayers = FindObjectsOfType<PlayerHealthSystem>();

        foreach (PlayerHealthSystem player in foundPlayers)
        {
            RegisterPlayer(player);
        }

        if (enableDebugLogs)
        {
            Debug.Log($"[GameManager] Found and registered {foundPlayers.Length} players");
        }
    }

    private void SetupUI()
    {
        if (gameOverUI != null)
        {
            gameOverUI.SetActive(false);
        }
    }

    #endregion

    #region Player Management

    public void RegisterPlayer(PlayerHealthSystem player)
    {
        if (player == null)
        {
            Debug.LogError("[GameManager] Attempted to register null player!");
            return;
        }

        if (!registeredPlayers.Contains(player))
        {
            registeredPlayers.Add(player);

            // Initialize death count
            if (!playerDeaths.ContainsKey(player.PlayerIndex))
            {
                playerDeaths[player.PlayerIndex] = 0;
            }

            // Subscribe to death event
            player.OnPlayerDeath += () => HandlePlayerDeath(player);

            if (enableDebugLogs)
            {
                Debug.Log($"[GameManager] Player {player.PlayerIndex} registered");
            }
        }
    }

    public void UnregisterPlayer(PlayerHealthSystem player)
    {
        if (registeredPlayers.Remove(player))
        {
            // Unsubscribe from events
            player.OnPlayerDeath -= () => HandlePlayerDeath(player);

            // Stop any active respawn coroutine
            if (activeRespawnCoroutines.ContainsKey(player))
            {
                if (activeRespawnCoroutines[player] != null)
                {
                    StopCoroutine(activeRespawnCoroutines[player]);
                }
                activeRespawnCoroutines.Remove(player);
            }

            if (enableDebugLogs)
            {
                Debug.Log($"[GameManager] Player {player.PlayerIndex} unregistered");
            }
        }
    }

    #endregion

    #region Death Management

    private void HandlePlayerDeath(PlayerHealthSystem player)
    {
        if (gameIsOver) return;

        // Increment death counters
        playerDeaths[player.PlayerIndex]++;
        totalDeaths++;

        if (enableDebugLogs)
        {
            Debug.Log($"[GameManager] Player {player.PlayerIndex} died! Individual deaths: {playerDeaths[player.PlayerIndex]}, Total deaths: {totalDeaths}");
        }

        // Trigger events
        OnPlayerDeath?.Invoke(player.PlayerIndex, playerDeaths[player.PlayerIndex]);
        OnTotalDeathsChanged?.Invoke(totalDeaths);

        // Check game over conditions
        if (ShouldGameOver())
        {
            TriggerGameOver();
        }
        else
        {
            // Start respawn process
            StartRespawnProcess(player);
        }
    }

    private bool ShouldGameOver()
    {
        if (useSharedDeathPool)
        {
            // Coop mode: check total deaths
            return totalDeaths >= maxTotalDeaths;
        }
        else
        {
            // Individual mode: check if any player exceeded their limit
            foreach (var deathCount in playerDeaths.Values)
            {
                if (deathCount >= maxDeathsPerPlayer)
                {
                    return true;
                }
            }
            return false;
        }
    }

    #endregion

    #region Respawn System

    private void StartRespawnProcess(PlayerHealthSystem player)
    {
        // Stop any existing respawn coroutine for this player
        if (activeRespawnCoroutines.ContainsKey(player) && activeRespawnCoroutines[player] != null)
        {
            StopCoroutine(activeRespawnCoroutines[player]);
        }

        // Start new respawn coroutine
        activeRespawnCoroutines[player] = StartCoroutine(RespawnPlayerCoroutine(player));
    }

    private IEnumerator RespawnPlayerCoroutine(PlayerHealthSystem player)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[GameManager] Starting respawn sequence for Player {player.PlayerIndex}");
        }

        // Wait for respawn delay with optional countdown
        if (showRespawnCountdown)
        {
            for (float t = respawnDelay; t > 0; t -= 1f)
            {
                if (enableDebugLogs)
                {
                    Debug.Log($"[GameManager] Respawning Player {player.PlayerIndex} in {t:F0}...");
                }
                yield return new WaitForSeconds(1f);
            }
        }
        else
        {
            yield return new WaitForSeconds(respawnDelay);
        }

        // Get respawn position from checkpoint manager
        Vector3 respawnPos = GetRespawnPosition(player.PlayerIndex);

        // Respawn the player
        RespawnPlayer(player, respawnPos);

        // Remove from active respawn tracking
        if (activeRespawnCoroutines.ContainsKey(player))
        {
            activeRespawnCoroutines.Remove(player);
        }
    }

    private void RespawnPlayer(PlayerHealthSystem player, Vector3 position)
    {
        if (player == null) return;

        // Use the player's built-in respawn method
        player.RespawnAtPosition(position);

        // Apply post-respawn invulnerability if enabled
        if (invulnerabilityAfterRespawn && respawnInvulnerabilityTime > 0f)
        {
            player.SetInvulnerable(true, respawnInvulnerabilityTime);
        }

        // Trigger respawn event
        OnPlayerRespawn?.Invoke(player);

        if (enableDebugLogs)
        {
            Debug.Log($"[GameManager] Player {player.PlayerIndex} respawned at {position}");
        }
    }

    private Vector3 GetRespawnPosition(int playerIndex)
    {
        if (CheckpointManager.Instance != null)
        {
            return CheckpointManager.Instance.GetRespawnPosition(playerIndex);
        }

        Debug.LogWarning("[GameManager] No CheckpointManager found! Using world origin for respawn.");
        return Vector3.zero;
    }

    #endregion

    #region Game Over System

    private void TriggerGameOver()
    {
        if (gameIsOver) return;

        gameIsOver = true;

        if (enableDebugLogs)
        {
            Debug.Log("[GameManager] GAME OVER triggered!");
        }

        // Stop all active respawn coroutines
        foreach (var kvp in activeRespawnCoroutines)
        {
            if (kvp.Value != null)
            {
                StopCoroutine(kvp.Value);
            }
        }
        activeRespawnCoroutines.Clear();

        // Play game over sound
        if (gameOverSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(gameOverSound, gameOverVolume);
        }

        // Pause game if specified
        if (pauseGameOnGameOver)
        {
            Time.timeScale = 0f;
        }

        // Show game over UI
        if (gameOverUI != null)
        {
            gameOverUI.SetActive(true);
        }

        // Trigger event
        OnGameOver?.Invoke();
    }

    #endregion

    #region Public Control Methods

    public void RestartLevel()
    {
        if (enableDebugLogs)
        {
            Debug.Log("[GameManager] Restarting level (full scene reload)...");
        }

        // Reset game state first
        gameIsOver = false;
        Time.timeScale = 1f;

        // Clean up before scene reload
        CleanupManager();

        // Hide game over UI
        if (gameOverUI != null)
        {
            gameOverUI.SetActive(false);
        }

        // Get current scene and reload it completely
        int currentSceneIndex = UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex;
        string currentSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        if (enableDebugLogs)
        {
            Debug.Log($"[GameManager] Reloading scene: {currentSceneName} (Index: {currentSceneIndex})");
        }

        // Option 1: Reload by build index (preferred if scene is in build settings)
        if (currentSceneIndex >= 0)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(currentSceneIndex);
        }
        // Option 2: Reload by name (fallback)
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(currentSceneName);
        }
    }

    public void RestartLevelSoft()
    {
        if (enableDebugLogs)
        {
            Debug.Log("[GameManager] Soft restart (reset state only)...");
        }

        // This is the old restart method - keeps enemies dead, pickups collected
        // Reset game state
        gameIsOver = false;
        Time.timeScale = 1f;

        // Reset death counters
        var playerIndices = playerDeaths.Keys.ToList();
        foreach (int playerIndex in playerIndices)
        {
            playerDeaths[playerIndex] = 0;
        }
        totalDeaths = 0;

        // Clear active respawn coroutines
        foreach (var kvp in activeRespawnCoroutines)
        {
            if (kvp.Value != null)
            {
                StopCoroutine(kvp.Value);
            }
        }
        activeRespawnCoroutines.Clear();

        // Reset checkpoints
        if (CheckpointManager.Instance != null)
        {
            CheckpointManager.Instance.ResetAllCheckpoints();
        }

        // Respawn all players at starting checkpoint
        foreach (PlayerHealthSystem player in registeredPlayers)
        {
            if (player != null)
            {
                Vector3 startPos = GetRespawnPosition(player.PlayerIndex);
                player.RespawnAtPosition(startPos);
            }
        }

        // Hide game over UI
        if (gameOverUI != null)
        {
            gameOverUI.SetActive(false);
        }

        // Trigger events
        OnTotalDeathsChanged?.Invoke(totalDeaths);
    }

    public void LoadMainMenu()
    {
        Time.timeScale = 1f;

        if (string.IsNullOrEmpty(mainMenuSceneName))
        {
            Debug.LogError("[GameManager] Main menu scene name not set!");
            return;
        }

        CleanupManager();
        UnityEngine.SceneManagement.SceneManager.LoadScene(mainMenuSceneName);
    }

    private void CleanupManager()
    {
        // Stop all active coroutines
        StopAllCoroutines();

        // Clear active respawn coroutines
        foreach (var kvp in activeRespawnCoroutines)
        {
            if (kvp.Value != null)
            {
                StopCoroutine(kvp.Value);
            }
        }
        activeRespawnCoroutines.Clear();

        // Reset time scale
        Time.timeScale = 1f;

        if (enableDebugLogs)
        {
            Debug.Log("[GameManager] Manager cleaned up");
        }
    }

    public void QuitGame()
    {
        if (enableDebugLogs)
        {
            Debug.Log("[GameManager] Quitting game...");
        }

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    #endregion

    #region Public Accessors

    public int GetPlayerDeaths(int playerIndex)
    {
        return playerDeaths.ContainsKey(playerIndex) ? playerDeaths[playerIndex] : 0;
    }

    public int GetRemainingDeaths()
    {
        if (useSharedDeathPool)
        {
            return Mathf.Max(0, maxTotalDeaths - totalDeaths);
        }
        else
        {
            // Return minimum remaining deaths across all players
            int minRemaining = maxDeathsPerPlayer;
            foreach (var deathCount in playerDeaths.Values)
            {
                minRemaining = Mathf.Min(minRemaining, maxDeathsPerPlayer - deathCount);
            }
            return Mathf.Max(0, minRemaining);
        }
    }

    public int GetRemainingDeaths(int playerIndex)
    {
        if (useSharedDeathPool)
        {
            return GetRemainingDeaths();
        }
        else
        {
            int playerDeathCount = GetPlayerDeaths(playerIndex);
            return Mathf.Max(0, maxDeathsPerPlayer - playerDeathCount);
        }
    }

    public bool IsPlayerRegistered(PlayerHealthSystem player)
    {
        return registeredPlayers.Contains(player);
    }

    #endregion

    #region Debug Methods

    [ContextMenu("Debug: Print Death Statistics")]
    public void DebugPrintDeathStats()
    {
        Debug.Log($"[GameManager] Death Statistics:");
        Debug.Log($"  Total Deaths: {totalDeaths}/{maxTotalDeaths}");
        Debug.Log($"  Game Mode: {(useSharedDeathPool ? "Shared Pool" : "Individual")}");

        foreach (var kvp in playerDeaths)
        {
            Debug.Log($"  Player {kvp.Key}: {kvp.Value}/{maxDeathsPerPlayer} deaths");
        }

        Debug.Log($"  Remaining Deaths: {GetRemainingDeaths()}");
        Debug.Log($"  Game Over: {gameIsOver}");
    }

    [ContextMenu("Debug: Trigger Game Over")]
    public void DebugTriggerGameOver()
    {
        TriggerGameOver();
    }

    [ContextMenu("Debug: Full Restart Level")]
    public void DebugFullRestart()
    {
        RestartLevel();
    }

    [ContextMenu("Debug: Soft Restart Level")]
    public void DebugSoftRestart()
    {
        RestartLevelSoft();
    }

    [ContextMenu("Debug: Test Scene Reload")]
    public void DebugTestSceneReload()
    {
        string currentSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        int currentSceneIndex = UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex;

        Debug.Log($"[GameManager] Current Scene: {currentSceneName} (Build Index: {currentSceneIndex})");

        if (currentSceneIndex >= 0)
        {
            Debug.Log("[GameManager] Scene is in Build Settings - can use index reload");
        }
        else
        {
            Debug.Log("[GameManager] Scene not in Build Settings - will use name reload");
            Debug.LogWarning("[GameManager] Add scene to File → Build Settings for more reliable reloading");
        }
    }

    #endregion
}