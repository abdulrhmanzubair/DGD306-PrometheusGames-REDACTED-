using UnityEngine;
using System.Collections;

/// <summary>
/// EdgeBreaker - Special completion item that triggers game victory
/// When collected, sends players to the game completion scene
/// </summary>
public class EdgeBreaker : MonoBehaviour
{
    [Header("Completion Settings")]
    [SerializeField] private string completionSceneName = "GameComplete";
    [SerializeField] private bool requireAllPlayersAlive = true;
    [SerializeField] private bool requireAllPlayersNearby = false;
    [SerializeField] private float nearbyRadius = 5f;

    [Header("Pickup Behavior")]
    [SerializeField] private bool autoPickup = true;
    [SerializeField] private bool requireInteraction = false;
    [SerializeField] private KeyCode interactionKey = KeyCode.E;
    [SerializeField] private float pickupRange = 2f;

    [Header("Visual Effects")]
    [SerializeField] private SpriteRenderer itemSprite;
    [SerializeField] private GameObject completionEffect;
    [SerializeField] private ParticleSystem idleParticles;
    [SerializeField] private ParticleSystem victoryParticles;
    [SerializeField] private Color glowColor = Color.yellow;
    [SerializeField] private float bobSpeed = 1.5f;
    [SerializeField] private float bobHeight = 0.8f;
    [SerializeField] private float pulseSpeed = 3f;

    [Header("Audio")]
    [SerializeField] private AudioClip pickupSound;
    [SerializeField] private AudioClip victorySound;
    [SerializeField] private AudioClip ambientHum;
    [SerializeField][Range(0f, 1f)] private float pickupVolume = 1f;
    [SerializeField][Range(0f, 1f)] private float victoryVolume = 0.8f;
    [SerializeField][Range(0f, 1f)] private float ambientVolume = 0.3f;

    [Header("Transition Settings")]
    [SerializeField] private float transitionDelay = 2f;
    [SerializeField] private bool showCompletionMessage = true;
    [SerializeField] private string completionMessage = "LEVEL COMPLETE!";
    [SerializeField] private bool pauseGameDuringTransition = true;

    [Header("DEBUG")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool showDebugGizmos = true;

    // Private variables
    private Vector3 startPosition;
    private bool isBeingCollected = false;
    private AudioSource audioSource;
    private AudioSource ambientAudioSource;
    private Color originalColor;
    private GameManager gameManager;
    private bool playerInRange = false;
    private GameObject nearbyPlayer;

    // Animation variables
    private float animationTime = 0f;

    #region Unity Lifecycle

    private void Awake()
    {
        InitializeComponents();
    }

    private void Start()
    {
        Initialize();
    }

    private void Update()
    {
        if (isBeingCollected) return;

        // Update animation
        UpdateAnimation();

        // Check for nearby players
        CheckForPlayers();

        // Handle interaction input
        if (requireInteraction && playerInRange && Input.GetKeyDown(interactionKey))
        {
            TriggerCompletion(nearbyPlayer);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isBeingCollected) return;

        if (IsValidPlayer(other) && autoPickup && !requireInteraction)
        {
            TriggerCompletion(other.gameObject);
        }
    }

    #endregion

    #region Initialization

    private void InitializeComponents()
    {
        // Setup main audio source
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0.7f;

        // Setup ambient audio source for humming sound
        if (ambientHum != null)
        {
            GameObject ambientAudioGO = new GameObject("AmbientAudio");
            ambientAudioGO.transform.SetParent(transform);
            ambientAudioGO.transform.localPosition = Vector3.zero;

            ambientAudioSource = ambientAudioGO.AddComponent<AudioSource>();
            ambientAudioSource.clip = ambientHum;
            ambientAudioSource.loop = true;
            ambientAudioSource.volume = ambientVolume;
            ambientAudioSource.spatialBlend = 0.8f;
            ambientAudioSource.Play();
        }

        // Store original sprite color
        if (itemSprite == null)
        {
            itemSprite = GetComponent<SpriteRenderer>();
        }
        if (itemSprite != null)
        {
            originalColor = itemSprite.color;
        }

        // Verify collider setup
        VerifyColliderSetup();
    }

    private void Initialize()
    {
        startPosition = transform.position;
        animationTime = 0f;

        // Find game manager
        gameManager = GameManager.Instance;

        // Start idle particles
        if (idleParticles != null && !idleParticles.isPlaying)
        {
            idleParticles.Play();
        }

        if (enableDebugLogs)
        {
            Debug.Log($"[EdgeBreaker] Initialized at position: {transform.position}");
            Debug.Log($"[EdgeBreaker] Completion scene: {completionSceneName}");
            Debug.Log($"[EdgeBreaker] Require all players alive: {requireAllPlayersAlive}");
            Debug.Log($"[EdgeBreaker] Require all players nearby: {requireAllPlayersNearby}");
        }
    }

    private void VerifyColliderSetup()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col == null)
        {
            Debug.LogError($"[EdgeBreaker] {gameObject.name} has NO COLLIDER! Adding BoxCollider2D...");
            BoxCollider2D boxCol = gameObject.AddComponent<BoxCollider2D>();
            boxCol.isTrigger = true;
            boxCol.size = Vector2.one * 1.5f;
        }
        else if (!col.isTrigger)
        {
            Debug.LogWarning($"[EdgeBreaker] {gameObject.name} collider is NOT set as trigger! Fixing...");
            col.isTrigger = true;
        }
    }

    #endregion

    #region Animation & Visual Effects

    private void UpdateAnimation()
    {
        animationTime += Time.deltaTime;

        // Bobbing animation
        if (itemSprite != null)
        {
            float newY = startPosition.y + Mathf.Sin(animationTime * bobSpeed) * bobHeight;
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);

            // Pulsing glow effect
            float pulseIntensity = Mathf.Sin(animationTime * pulseSpeed) * 0.4f + 0.6f;
            Color glowedColor = Color.Lerp(originalColor, glowColor, pulseIntensity);
            itemSprite.color = glowedColor;
        }

        // Slow rotation
        transform.Rotate(0, 0, 30f * Time.deltaTime);
    }

    #endregion

    #region Player Detection

    private void CheckForPlayers()
    {
        // Find all players
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        PlayerHealthSystem[] playerHealthSystems = FindObjectsOfType<PlayerHealthSystem>();

        // Combine detection methods
        System.Collections.Generic.List<GameObject> allPlayers = new System.Collections.Generic.List<GameObject>();
        allPlayers.AddRange(players);

        foreach (PlayerHealthSystem phs in playerHealthSystems)
        {
            if (!allPlayers.Contains(phs.gameObject))
                allPlayers.Add(phs.gameObject);
        }

        GameObject closestPlayer = null;
        float closestDistance = Mathf.Infinity;

        foreach (GameObject player in allPlayers)
        {
            if (player == null) continue;

            PlayerHealthSystem playerHealth = player.GetComponent<PlayerHealthSystem>();
            if (playerHealth == null || !playerHealth.IsAlive) continue;

            float distance = Vector2.Distance(transform.position, player.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestPlayer = player;
            }
        }

        nearbyPlayer = closestPlayer;
        playerInRange = (closestDistance <= pickupRange);
    }

    private bool IsValidPlayer(Collider2D other)
    {
        // Check tag
        if (other.CompareTag("Player"))
        {
            return true;
        }

        // Check for PlayerHealthSystem component
        PlayerHealthSystem healthSystem = other.GetComponent<PlayerHealthSystem>();
        if (healthSystem != null)
        {
            return true;
        }

        // Check for player controllers
        if (other.GetComponent<PlayerController>() != null || other.GetComponent<Player_Melee_Controller1>() != null)
        {
            return true;
        }

        return false;
    }

    #endregion

    #region Completion Logic

    public void TriggerCompletion(GameObject triggeringPlayer)
    {
        if (isBeingCollected) return;

        if (enableDebugLogs)
        {
            Debug.Log($"[EdgeBreaker] Completion triggered by: {triggeringPlayer.name}");
        }

        // Validate completion conditions
        if (!ValidateCompletionConditions(triggeringPlayer))
        {
            return;
        }

        isBeingCollected = true;

        // Start completion sequence
        StartCoroutine(CompletionSequence(triggeringPlayer));
    }

    private bool ValidateCompletionConditions(GameObject triggeringPlayer)
    {
        // Check if triggering player is alive
        PlayerHealthSystem triggeringPlayerHealth = triggeringPlayer.GetComponent<PlayerHealthSystem>();
        if (triggeringPlayerHealth == null || !triggeringPlayerHealth.IsAlive)
        {
            if (enableDebugLogs)
                Debug.Log("[EdgeBreaker] Triggering player is not alive!");
            return false;
        }

        // Get all players
        PlayerHealthSystem[] allPlayers = FindObjectsOfType<PlayerHealthSystem>();

        if (requireAllPlayersAlive)
        {
            foreach (PlayerHealthSystem player in allPlayers)
            {
                if (!player.IsAlive)
                {
                    if (enableDebugLogs)
                        Debug.Log($"[EdgeBreaker] Player {player.PlayerIndex} is not alive! All players must be alive for completion.");

                    ShowFailureMessage("All players must be alive!");
                    return false;
                }
            }
        }

        if (requireAllPlayersNearby)
        {
            foreach (PlayerHealthSystem player in allPlayers)
            {
                if (!player.IsAlive) continue; // Skip dead players if not requiring all alive

                float distance = Vector2.Distance(transform.position, player.transform.position);
                if (distance > nearbyRadius)
                {
                    if (enableDebugLogs)
                        Debug.Log($"[EdgeBreaker] Player {player.PlayerIndex} is too far away ({distance:F1} > {nearbyRadius})!");

                    ShowFailureMessage("All players must be nearby!");
                    return false;
                }
            }
        }

        return true;
    }

    private void ShowFailureMessage(string message)
    {
        // This could trigger a UI message or sound effect
        Debug.LogWarning($"[EdgeBreaker] Completion failed: {message}");

        // Play a failure sound or visual effect here if desired
        if (audioSource != null && pickupSound != null)
        {
            audioSource.pitch = 0.7f; // Lower pitch for failure
            audioSource.PlayOneShot(pickupSound, pickupVolume * 0.5f);
            audioSource.pitch = 1f; // Reset pitch
        }
    }

    #endregion

    #region Completion Sequence

    private IEnumerator CompletionSequence(GameObject triggeringPlayer)
    {
        if (enableDebugLogs)
        {
            Debug.Log("[EdgeBreaker] Starting completion sequence!");
        }

        // Stop ambient audio
        if (ambientAudioSource != null)
        {
            ambientAudioSource.Stop();
        }

        // Play pickup sound
        if (pickupSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(pickupSound, pickupVolume);
        }

        // Stop idle particles and start victory particles
        if (idleParticles != null)
        {
            idleParticles.Stop();
        }

        if (victoryParticles != null)
        {
            victoryParticles.Play();
        }

        // Spawn completion effect
        if (completionEffect != null)
        {
            GameObject effect = Instantiate(completionEffect, transform.position, Quaternion.identity);
            Destroy(effect, 5f);
        }

        // Show completion message
        if (showCompletionMessage)
        {
            Debug.Log($"[EdgeBreaker] {completionMessage}");
            // This could trigger a UI popup with the completion message
        }

        // Enhanced visual effects during transition
        yield return StartCoroutine(VictoryAnimation());

        // Play victory sound
        if (victorySound != null && audioSource != null)
        {
            audioSource.PlayOneShot(victorySound, victoryVolume);
        }

        // Pause game if specified
        if (pauseGameDuringTransition)
        {
            Time.timeScale = 0f;
        }

        // Wait for transition delay (use unscaled time if game is paused)
        if (pauseGameDuringTransition)
        {
            yield return new WaitForSecondsRealtime(transitionDelay);
        }
        else
        {
            yield return new WaitForSeconds(transitionDelay);
        }

        // Restore time scale
        Time.timeScale = 1f;

        // Notify game manager of completion
        if (gameManager != null)
        {
            NotifyGameManager();
        }

        // Load completion scene
        LoadCompletionScene();
    }

    private IEnumerator VictoryAnimation()
    {
        float animDuration = 1.5f;
        float elapsed = 0f;
        Vector3 originalScale = transform.localScale;
        Vector3 targetScale = originalScale * 1.5f;

        while (elapsed < animDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = elapsed / animDuration;

            // Scale up
            transform.localScale = Vector3.Lerp(originalScale, targetScale, progress);

            // Increase glow intensity
            if (itemSprite != null)
            {
                Color brightGlow = Color.Lerp(originalColor, Color.white, progress);
                itemSprite.color = brightGlow;
            }

            yield return null;
        }
    }

    private void NotifyGameManager()
    {
        // Save completion state, update statistics, etc.
        if (enableDebugLogs)
        {
            Debug.Log("[EdgeBreaker] Notifying GameManager of level completion");
        }


    }

    private void LoadCompletionScene()
    {
        if (string.IsNullOrEmpty(completionSceneName))
        {
            Debug.LogError("[EdgeBreaker] Completion scene name is not set!");
            return;
        }

        if (enableDebugLogs)
        {
            Debug.Log($"[EdgeBreaker] Loading completion scene: {completionSceneName}");
        }

        try
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(completionSceneName);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[EdgeBreaker] Failed to load completion scene '{completionSceneName}': {e.Message}");
            Debug.LogError("[EdgeBreaker] Make sure the scene is added to Build Settings!");
        }
    }

    #endregion

    #region Public Methods

    public void SetCompletionScene(string sceneName)
    {
        completionSceneName = sceneName;
        if (enableDebugLogs)
        {
            Debug.Log($"[EdgeBreaker] Completion scene set to: {sceneName}");
        }
    }

    public void SetRequireAllPlayersAlive(bool require)
    {
        requireAllPlayersAlive = require;
    }

    public void SetRequireAllPlayersNearby(bool require)
    {
        requireAllPlayersNearby = require;
    }

    [ContextMenu("Test Completion")]
    public void TestCompletion()
    {
        PlayerHealthSystem[] players = FindObjectsOfType<PlayerHealthSystem>();
        if (players.Length > 0)
        {
            TriggerCompletion(players[0].gameObject);
        }
        else
        {
            Debug.LogError("[EdgeBreaker] No players found for testing!");
        }
    }

    [ContextMenu("Validate Scene Name")]
    public void ValidateSceneName()
    {
        if (string.IsNullOrEmpty(completionSceneName))
        {
            Debug.LogError("[EdgeBreaker] Completion scene name is not set!");
            return;
        }

        // Check if scene exists in build settings
        bool sceneExists = false;
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings; i++)
        {
            string scenePath = UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(i);
            string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);

            if (sceneName == completionSceneName)
            {
                sceneExists = true;
                break;
            }
        }

        if (sceneExists)
        {
            Debug.Log($"[EdgeBreaker] ✅ Scene '{completionSceneName}' found in Build Settings");
        }
        else
        {
            Debug.LogError($"[EdgeBreaker] ❌ Scene '{completionSceneName}' NOT found in Build Settings! Add it to File → Build Settings");
        }
    }

    #endregion

    #region Debug Visualization

    private void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos) return;

        // Draw pickup range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, pickupRange);

        // Draw nearby radius if required
        if (requireAllPlayersNearby)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, nearbyRadius);
        }

        // Draw completion indicator
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, Vector3.one * 2f);

        // Draw labels
#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 3f, "EDGE BREAKER");
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2.5f, $"→ {completionSceneName}");

        if (requireAllPlayersAlive)
        {
            UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, "All Alive Required");
        }

        if (requireAllPlayersNearby)
        {
            UnityEditor.Handles.Label(transform.position + Vector3.up * 1.5f, $"All Nearby ({nearbyRadius}u)");
        }
#endif
    }

    #endregion
}