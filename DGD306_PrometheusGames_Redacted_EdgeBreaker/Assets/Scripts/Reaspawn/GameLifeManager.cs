using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Manages player lives, respawning, and game over conditions
/// Handles both solo and co-op gameplay scenarios
/// </summary>
public class GameLifeManager : MonoBehaviour
{
    public static GameLifeManager Instance;

    [Header("Life Settings")]
    public int maxLives = 3;
    public float respawnDelay = 3f;
    public float coopRespawnRadius = 5f; // How close to spawn near friend

    [Header("Game Over Settings")]
    public string gameOverScene = "GameOver";
    public string retryScene = ""; // Current scene name, auto-detected
    public float gameOverDelay = 2f;

    [Header("Checkpoint System")]
    public Transform[] checkpoints;
    public int currentCheckpointIndex = 0;
    public float checkpointActivationRadius = 3f;

    [Header("UI References")]
    public Text livesDisplayText;
    public GameObject gameOverUI;
    public Button retryButton;
    public Button mainMenuButton;
    public Image fadeOverlay;

    [Header("Audio")]
    public AudioClip playerDeathSound;
    public AudioClip gameOverSound;
    public AudioClip respawnSound;
    [Range(0f, 1f)] public float audioVolume = 0.7f;

    // Private variables
    private Dictionary<int, int> playerLives = new Dictionary<int, int>();
    private Dictionary<int, PlayerHealthSystem> playerHealthSystems = new Dictionary<int, PlayerHealthSystem>();
    private Dictionary<int, bool> playersDead = new Dictionary<int, bool>();
    private List<PlayerController> allPlayers = new List<PlayerController>();
    private AudioSource audioSource;
    private bool gameIsOver = false;
    private bool isSoloMode = false;

    // Events
    public event System.Action<int, int> OnPlayerLifeChanged; // (playerIndex, livesRemaining)
    public event System.Action OnGameOver;
    public event System.Action<int> OnCheckpointReached; // (checkpointIndex)
    public event System.Action<int> OnPlayerRespawn; // (playerIndex)

    // Public property to access allPlayers safely
    public List<PlayerController> AllPlayers { get { return allPlayers; } }
    public bool IsSoloMode { get { return isSoloMode; } }

    void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Auto-detect current scene for retry
            if (string.IsNullOrEmpty(retryScene))
            {
                retryScene = SceneManager.GetActiveScene().name;
            }
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Set up audio
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        audioSource.volume = audioVolume;
    }

    void Start()
    {
        // Find all players and set up life tracking
        StartCoroutine(InitializePlayersAfterSpawn());

        // Set up UI
        SetupUI();

        // Initialize checkpoints
        InitializeCheckpoints();
    }

    IEnumerator InitializePlayersAfterSpawn()
    {
        // Wait a frame for players to spawn
        yield return new WaitForEndOfFrame();

        // Find all players
        FindAllPlayers();

        // Initialize lives for each player
        foreach (var player in allPlayers)
        {
            int playerIndex = player.PlayerIndex;
            playerLives[playerIndex] = maxLives;
            playersDead[playerIndex] = false;

            // Get and hook up health system
            PlayerHealthSystem healthSystem = player.GetComponent<PlayerHealthSystem>();
            if (healthSystem != null)
            {
                playerHealthSystems[playerIndex] = healthSystem;
                healthSystem.OnPlayerDeath += () => HandlePlayerDeath(playerIndex);
                healthSystem.OnPlayerRespawn += () => HandlePlayerRespawn(playerIndex);
            }
        }

        // Determine if solo mode
        isSoloMode = allPlayers.Count <= 1;

        // Update UI
        UpdateLivesDisplay();

        Debug.Log($"GameLifeManager initialized - Players: {allPlayers.Count}, Solo Mode: {isSoloMode}");
    }

    void FindAllPlayers()
    {
        allPlayers.Clear();

        // Find gunner players - Updated to use new Unity API
        PlayerController[] gunners = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        allPlayers.AddRange(gunners);

        // Find melee players - Updated to use new Unity API
        Player_Melee_Controller1[] meleeControllers = FindObjectsByType<Player_Melee_Controller1>(FindObjectsSortMode.None);
        foreach (var melee in meleeControllers)
        {
            // Create a wrapper or adapter for melee players
            PlayerController adapter = melee.GetComponent<PlayerController>();
            if (adapter == null)
            {
                // If no adapter exists, we'll track melee players separately
                // For now, let's assume they have PlayerIndex property
                Debug.Log($"Found melee player: {melee.name} (Index: {melee.PlayerIndex})");
            }
        }

        Debug.Log($"Found {allPlayers.Count} players");
    }

    void HandlePlayerDeath(int playerIndex)
    {
        if (gameIsOver) return;

        Debug.Log($"Player {playerIndex} died!");

        // Play death sound
        if (playerDeathSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(playerDeathSound, audioVolume);
        }

        // Reduce lives
        if (playerLives.ContainsKey(playerIndex))
        {
            playerLives[playerIndex]--;
            playersDead[playerIndex] = true;

            OnPlayerLifeChanged?.Invoke(playerIndex, playerLives[playerIndex]);

            Debug.Log($"Player {playerIndex} has {playerLives[playerIndex]} lives remaining");
        }

        // Update UI
        UpdateLivesDisplay();

        // Check for game over
        if (ShouldGameEnd())
        {
            StartCoroutine(GameOverSequence());
        }
        else
        {
            // Schedule respawn
            StartCoroutine(RespawnPlayer(playerIndex));
        }
    }

    void HandlePlayerRespawn(int playerIndex)
    {
        playersDead[playerIndex] = false;
        OnPlayerRespawn?.Invoke(playerIndex);

        Debug.Log($"Player {playerIndex} respawned");

        // Play respawn sound
        if (respawnSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(respawnSound, audioVolume);
        }
    }

    bool ShouldGameEnd()
    {
        if (isSoloMode)
        {
            // Solo mode: game over when the single player runs out of lives
            return playerLives.Values.Any(lives => lives <= 0);
        }
        else
        {
            // Co-op mode: game over when ALL players are out of lives
            return playerLives.Values.All(lives => lives <= 0);
        }
    }

    IEnumerator RespawnPlayer(int playerIndex)
    {
        yield return new WaitForSeconds(respawnDelay);

        if (gameIsOver || !playerLives.ContainsKey(playerIndex) || playerLives[playerIndex] <= 0)
        {
            yield break; // Fixed: Use yield break instead of return
        }

        Vector3 respawnPosition = GetRespawnPosition(playerIndex);

        // Get the player's health system and respawn them
        if (playerHealthSystems.ContainsKey(playerIndex))
        {
            PlayerHealthSystem healthSystem = playerHealthSystems[playerIndex];

            // Move player to respawn position
            healthSystem.transform.position = respawnPosition;

            // Manually trigger respawn
            healthSystem.Respawn();
        }
    }

    Vector3 GetRespawnPosition(int playerIndex)
    {
        if (isSoloMode)
        {
            // Solo mode: respawn at current checkpoint
            return GetCurrentCheckpointPosition();
        }
        else
        {
            // Co-op mode: try to respawn near living teammate
            Vector3 teammatePosition = GetLivingTeammatePosition(playerIndex);

            if (teammatePosition != Vector3.zero)
            {
                // Spawn near teammate with some offset
                Vector2 randomOffset = Random.insideUnitCircle * coopRespawnRadius;
                Vector3 respawnPos = teammatePosition + new Vector3(randomOffset.x, randomOffset.y, 0);

                // Make sure spawn position is valid (not inside walls, etc.)
                return ValidateRespawnPosition(respawnPos);
            }
            else
            {
                // No living teammates, use checkpoint
                return GetCurrentCheckpointPosition();
            }
        }
    }

    Vector3 GetLivingTeammatePosition(int deadPlayerIndex)
    {
        foreach (var kvp in playerHealthSystems)
        {
            int playerIndex = kvp.Key;
            PlayerHealthSystem healthSystem = kvp.Value;

            // Skip the dead player and other dead players
            if (playerIndex == deadPlayerIndex || playersDead.ContainsKey(playerIndex) && playersDead[playerIndex])
                continue;

            // Check if this player is alive
            if (healthSystem != null && healthSystem.IsAlive)
            {
                return healthSystem.transform.position;
            }
        }

        return Vector3.zero; // No living teammates found
    }

    Vector3 GetCurrentCheckpointPosition()
    {
        if (checkpoints != null && checkpoints.Length > 0 && currentCheckpointIndex < checkpoints.Length)
        {
            return checkpoints[currentCheckpointIndex].position;
        }

        // Fallback: return spawn point or origin
        return Vector3.zero;
    }

    Vector3 ValidateRespawnPosition(Vector3 desiredPosition)
    {
        // Simple validation - check if position is blocked
        Collider2D obstacle = Physics2D.OverlapCircle(desiredPosition, 0.5f, LayerMask.GetMask("Ground", "Wall"));

        if (obstacle != null)
        {
            // Position is blocked, try nearby positions
            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * 2f;
                Vector3 testPosition = desiredPosition + offset;

                if (Physics2D.OverlapCircle(testPosition, 0.5f, LayerMask.GetMask("Ground", "Wall")) == null)
                {
                    return testPosition;
                }
            }
        }

        return desiredPosition; // Return original if no better position found
    }

    void InitializeCheckpoints()
    {
        if (checkpoints == null || checkpoints.Length == 0)
        {
            // Auto-find checkpoints if not manually assigned - Updated to use new Unity API
            GameObject[] checkpointObjects = GameObject.FindGameObjectsWithTag("Checkpoint");
            checkpoints = new Transform[checkpointObjects.Length];

            for (int i = 0; i < checkpointObjects.Length; i++)
            {
                checkpoints[i] = checkpointObjects[i].transform;
            }

            // Sort checkpoints by name or position if needed
            System.Array.Sort(checkpoints, (a, b) => a.name.CompareTo(b.name));
        }

        Debug.Log($"Initialized {checkpoints.Length} checkpoints");
    }

    void Update()
    {
        if (gameIsOver) return;

        // Check for checkpoint activation
        CheckCheckpointActivation();
    }

    void CheckCheckpointActivation()
    {
        if (checkpoints == null || checkpoints.Length == 0) return;

        // Check if any living player is near the next checkpoint
        int nextCheckpointIndex = currentCheckpointIndex + 1;
        if (nextCheckpointIndex >= checkpoints.Length) return;

        Transform nextCheckpoint = checkpoints[nextCheckpointIndex];

        foreach (var kvp in playerHealthSystems)
        {
            PlayerHealthSystem healthSystem = kvp.Value;
            if (healthSystem != null && healthSystem.IsAlive)
            {
                float distance = Vector3.Distance(healthSystem.transform.position, nextCheckpoint.position);
                if (distance <= checkpointActivationRadius)
                {
                    ActivateCheckpoint(nextCheckpointIndex);
                    break;
                }
            }
        }
    }

    void ActivateCheckpoint(int checkpointIndex)
    {
        currentCheckpointIndex = checkpointIndex;
        OnCheckpointReached?.Invoke(checkpointIndex);

        Debug.Log($"Checkpoint {checkpointIndex} activated!");

        // Optional: Save game state here
        // SaveGameState();
    }

    IEnumerator GameOverSequence()
    {
        if (gameIsOver) yield break; // Fixed: Use yield break instead of return

        gameIsOver = true;

        Debug.Log("Game Over!");

        // Play game over sound
        if (gameOverSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(gameOverSound, audioVolume);
        }

        // Trigger game over event
        OnGameOver?.Invoke();

        // Wait before showing game over UI
        yield return new WaitForSeconds(gameOverDelay);

        // Show game over UI
        ShowGameOverUI();
    }

    void SetupUI()
    {
        // Set up retry button
        if (retryButton != null)
        {
            retryButton.onClick.AddListener(RetryLevel);
        }

        // Set up main menu button
        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.AddListener(GoToMainMenu);
        }

        // Hide game over UI initially
        if (gameOverUI != null)
        {
            gameOverUI.SetActive(false);
        }
    }

    void UpdateLivesDisplay()
    {
        if (livesDisplayText == null) return;

        if (isSoloMode)
        {
            // Solo mode: show single player's lives
            int lives = playerLives.Values.FirstOrDefault();
            livesDisplayText.text = $"Lives: {lives}";
        }
        else
        {
            // Co-op mode: show both players' lives
            string livesText = "Lives: ";
            foreach (var kvp in playerLives.OrderBy(x => x.Key))
            {
                livesText += $"P{kvp.Key + 1}: {kvp.Value}  ";
            }
            livesDisplayText.text = livesText.Trim();
        }
    }

    void ShowGameOverUI()
    {
        if (gameOverUI != null)
        {
            gameOverUI.SetActive(true);
        }

        // Pause the game
        Time.timeScale = 0f;
    }

    public void RetryLevel()
    {
        Time.timeScale = 1f; // Unpause
        StartCoroutine(FadeAndLoadScene(retryScene));
    }

    public void GoToMainMenu()
    {
        Time.timeScale = 1f; // Unpause
        StartCoroutine(FadeAndLoadScene("MainMenu"));
    }

    IEnumerator FadeAndLoadScene(string sceneName)
    {
        // Fade out
        if (fadeOverlay != null)
        {
            fadeOverlay.gameObject.SetActive(true);
            float fadeTime = 1f;
            float timer = 0f;

            while (timer < fadeTime)
            {
                timer += Time.unscaledDeltaTime;
                float alpha = Mathf.Lerp(0f, 1f, timer / fadeTime);
                Color color = fadeOverlay.color;
                color.a = alpha;
                fadeOverlay.color = color;
                yield return null;
            }
        }

        // Load scene
        SceneManager.LoadScene(sceneName);
    }

    // Public methods for external use
    public int GetPlayerLives(int playerIndex)
    {
        return playerLives.ContainsKey(playerIndex) ? playerLives[playerIndex] : 0;
    }

    public bool IsGameOver()
    {
        return gameIsOver;
    }

    public void AddLife(int playerIndex)
    {
        if (playerLives.ContainsKey(playerIndex))
        {
            playerLives[playerIndex]++;
            OnPlayerLifeChanged?.Invoke(playerIndex, playerLives[playerIndex]);
            UpdateLivesDisplay();
        }
    }

    public void SetCheckpoint(int checkpointIndex)
    {
        if (checkpointIndex >= 0 && checkpointIndex < checkpoints.Length)
        {
            currentCheckpointIndex = checkpointIndex;
        }
    }

    void OnDrawGizmosSelected()
    {
        // Draw checkpoints
        if (checkpoints != null)
        {
            for (int i = 0; i < checkpoints.Length; i++)
            {
                if (checkpoints[i] != null)
                {
                    Gizmos.color = i == currentCheckpointIndex ? Color.green : Color.blue;
                    Gizmos.DrawWireSphere(checkpoints[i].position, checkpointActivationRadius);

                    // Draw checkpoint number
                    Gizmos.color = Color.white;
                    Gizmos.DrawWireCube(checkpoints[i].position + Vector3.up * 2f, Vector3.one * 0.5f);
                }
            }
        }

        // Draw co-op respawn radius
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, coopRespawnRadius);
    }
}