using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class CoopCameraController : MonoBehaviour
{
    [Header("Camera Settings")]
    public Camera mainCamera;
    public float smoothTime = 0.3f;
    public float minZoom = 3f;
    public float maxZoom = 10f;
    public float zoomBorder = 2f;
    public Vector2 offset = Vector2.zero;

    [Header("Single Player Settings")]
    public float singlePlayerZoom = 5f;
    public float singlePlayerSmoothTime = 0.2f;

    [Header("Bounds (Optional)")]
    public bool useBounds = false;
    public Bounds cameraBounds = new Bounds(Vector3.zero, Vector3.one * 20f);

    [Header("Debug")]
    public bool showDebugGizmos = true;

    // Private variables
    private List<Transform> playerTargets = new List<Transform>();
    private Vector3 velocity;
    private bool isSinglePlayer = false;

    // Character selection integration
    private Dictionary<int, CharacterType> selectedCharacters;
    private bool hasInitialized = false;

    void Start()
    {
        // Get camera if not assigned
        if (mainCamera == null)
            mainCamera = GetComponent<Camera>();

        if (mainCamera == null)
            mainCamera = Camera.main;

        if (mainCamera == null)
        {
            Debug.LogError("CoopCameraController: No camera found! Please assign a camera.");
            return;
        }

        // Initialize from character selection but don't search for players yet
        InitializeFromCharacterSelection();

        // Start checking for spawned players
        StartCoroutine(WaitForPlayersToSpawn());
    }

    void InitializeFromCharacterSelection()
    {
        // Get selected characters from the character selection system
        selectedCharacters = SimplifiedCharacterSelection.SelectedCharacters;

        if (selectedCharacters == null || selectedCharacters.Count == 0)
        {
            // Fallback: Try to get from PlayerPrefs
            Debug.LogWarning("No characters found in static data, trying PlayerPrefs backup...");
            LoadFromPlayerPrefs();
        }

        Debug.Log($"CoopCameraController: Found {selectedCharacters?.Count ?? 0} selected characters");
        Debug.Log("Waiting for character spawner to create players...");
    }

    void LoadFromPlayerPrefs()
    {
        selectedCharacters = new Dictionary<int, CharacterType>();
        int playerCount = PlayerPrefs.GetInt("PlayerCount", 0);

        for (int i = 0; i < playerCount; i++)
        {
            string characterString = PlayerPrefs.GetString($"Player{i}Character", "");
            if (System.Enum.TryParse<CharacterType>(characterString, out CharacterType characterType))
            {
                selectedCharacters[i] = characterType;
            }
        }

        Debug.Log($"Loaded {selectedCharacters.Count} characters from PlayerPrefs");
    }

    System.Collections.IEnumerator WaitForPlayersToSpawn()
    {
        float timeoutTime = 10f; // Maximum wait time
        float elapsedTime = 0f;

        Debug.Log("CoopCamera: Waiting for players to spawn...");

        while (elapsedTime < timeoutTime)
        {
            // Check if any players have been spawned
            if (CheckForSpawnedPlayers())
            {
                Debug.Log("CoopCamera: Players detected! Initializing camera tracking.");
                FindPlayerTargets();
                yield break; // Exit the coroutine
            }

            elapsedTime += 0.5f;
            yield return new WaitForSeconds(0.5f); // Check every 0.5 seconds
        }

        Debug.LogWarning("CoopCamera: Timeout waiting for players to spawn. Trying fallback search...");
        FindPlayerTargets(); // Try anyway as fallback
    }

    bool CheckForSpawnedPlayers()
    {
        // Check for PlayerDeviceInfo components (best indicator)
        PlayerDeviceInfo[] deviceInfos = FindObjectsByType<PlayerDeviceInfo>(FindObjectsSortMode.None);
        if (deviceInfos.Length > 0)
        {
            Debug.Log($"Found {deviceInfos.Length} PlayerDeviceInfo components");
            return true;
        }

        // Check for player controllers
        PlayerController[] gunners = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        Player_Melee_Controller1[] melee = FindObjectsByType<Player_Melee_Controller1>(FindObjectsSortMode.None);

        if (gunners.Length > 0 || melee.Length > 0)
        {
            Debug.Log($"Found {gunners.Length} gunners and {melee.Length} melee players");
            return true;
        }

        // Check for GameObjects tagged as Player
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        if (players.Length > 0)
        {
            Debug.Log($"Found {players.Length} player-tagged objects");
            return true;
        }

        return false;
    }

    void FindPlayerTargets()
    {
        playerTargets.Clear();

        if (selectedCharacters == null || selectedCharacters.Count == 0)
        {
            Debug.LogWarning("No selected characters found. Camera will search for any player objects.");
            // Fallback: Find any GameObject with "Player" tag or containing "Player" in name
            FindFallbackPlayers();
            return;
        }

        // Find players based on character selection
        foreach (var kvp in selectedCharacters)
        {
            int playerIndex = kvp.Key;
            CharacterType characterType = kvp.Value;

            Transform playerTransform = FindPlayerByIndex(playerIndex, characterType);
            if (playerTransform != null)
            {
                playerTargets.Add(playerTransform);
                Debug.Log($"Found Player {playerIndex} ({characterType}): {playerTransform.name}");
            }
            else
            {
                Debug.LogWarning($"Could not find Player {playerIndex} with character type {characterType}");
            }
        }

        // Determine if single player
        isSinglePlayer = playerTargets.Count == 1;

        if (playerTargets.Count == 0)
        {
            Debug.LogError("No player targets found! Camera will not function properly.");
        }
        else
        {
            Debug.Log($"CoopCamera initialized with {playerTargets.Count} players (Single Player: {isSinglePlayer})");
        }

        hasInitialized = true;
    }

    Transform FindPlayerByIndex(int playerIndex, CharacterType characterType)
    {
        // Strategy 1: Look for objects with PlayerDeviceInfo component
        PlayerDeviceInfo[] allPlayerDeviceInfos = FindObjectsByType<PlayerDeviceInfo>(FindObjectsSortMode.None);
        foreach (var deviceInfo in allPlayerDeviceInfos)
        {
            if (deviceInfo.PlayerIndex == playerIndex)
            {
                return deviceInfo.transform;
            }
        }

        // Strategy 2: Look for player controllers with matching PlayerIndex
        PlayerController[] gunnerControllers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var controller in gunnerControllers)
        {
            if (controller.PlayerIndex == playerIndex && characterType == CharacterType.Gunner)
            {
                return controller.transform;
            }
        }

        Player_Melee_Controller1[] meleeControllers = FindObjectsByType<Player_Melee_Controller1>(FindObjectsSortMode.None);
        foreach (var controller in meleeControllers)
        {
            if (controller.PlayerIndex == playerIndex && characterType == CharacterType.Melee)
            {
                return controller.transform;
            }
        }

        // Strategy 3: Look by name patterns
        string[] possibleNames = {
            $"Player{playerIndex}",
            $"Player {playerIndex}",
            $"Player_{playerIndex}",
            $"P{playerIndex}",
            $"Player{playerIndex + 1}",
            $"Player {playerIndex + 1}"
        };

        foreach (string name in possibleNames)
        {
            GameObject found = GameObject.Find(name);
            if (found != null)
            {
                return found.transform;
            }
        }

        return null;
    }

    void FindFallbackPlayers()
    {
        // Find any GameObjects tagged as "Player"
        GameObject[] playerObjects = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject player in playerObjects)
        {
            playerTargets.Add(player.transform);
        }

        // If no tagged players, look for objects with player controllers
        if (playerTargets.Count == 0)
        {
            PlayerController[] gunners = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            Player_Melee_Controller1[] melee = FindObjectsByType<Player_Melee_Controller1>(FindObjectsSortMode.None);

            foreach (var gunner in gunners)
                playerTargets.Add(gunner.transform);

            foreach (var meleeController in melee)
                playerTargets.Add(meleeController.transform);
        }

        isSinglePlayer = playerTargets.Count == 1;
        Debug.Log($"Fallback player search found {playerTargets.Count} players");
    }

    void LateUpdate()
    {
        if (!hasInitialized || playerTargets.Count == 0)
            return;

        // Remove null targets (destroyed players)
        playerTargets.RemoveAll(target => target == null);

        if (playerTargets.Count == 0)
            return;

        if (isSinglePlayer)
        {
            HandleSinglePlayerCamera();
        }
        else
        {
            HandleMultiPlayerCamera();
        }
    }

    void HandleSinglePlayerCamera()
    {
        if (playerTargets.Count == 0) return;

        Vector3 targetPosition = playerTargets[0].position + (Vector3)offset;
        targetPosition.z = transform.position.z;

        // Apply bounds if enabled
        if (useBounds)
        {
            targetPosition = ApplyBounds(targetPosition);
        }

        // Smooth movement
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, singlePlayerSmoothTime);

        // Set single player zoom
        if (mainCamera.orthographic)
        {
            mainCamera.orthographicSize = Mathf.Lerp(mainCamera.orthographicSize, singlePlayerZoom, Time.deltaTime * 2f);
        }
    }

    void HandleMultiPlayerCamera()
    {
        if (playerTargets.Count == 0) return;

        // Get center point of all players
        Vector3 centerPoint = GetCenterPoint();
        Vector3 targetPosition = centerPoint + (Vector3)offset;
        targetPosition.z = transform.position.z;

        // Apply bounds if enabled
        if (useBounds)
        {
            targetPosition = ApplyBounds(targetPosition);
        }

        // Smooth movement
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, smoothTime);

        // Dynamic zoom based on player spread
        if (mainCamera.orthographic)
        {
            float targetZoom = GetRequiredZoom();
            mainCamera.orthographicSize = Mathf.Lerp(mainCamera.orthographicSize, targetZoom, Time.deltaTime * 2f);
        }
    }

    Vector3 GetCenterPoint()
    {
        if (playerTargets.Count == 1)
            return playerTargets[0].position;

        var bounds = new Bounds(playerTargets[0].position, Vector3.zero);
        foreach (var target in playerTargets)
        {
            bounds.Encapsulate(target.position);
        }

        return bounds.center;
    }

    float GetRequiredZoom()
    {
        if (playerTargets.Count <= 1)
            return singlePlayerZoom;

        // Get bounds of all players
        var bounds = new Bounds(playerTargets[0].position, Vector3.zero);
        foreach (var target in playerTargets)
        {
            bounds.Encapsulate(target.position);
        }

        // Calculate required zoom to fit all players
        float distance = Mathf.Max(bounds.size.x, bounds.size.y);
        float targetZoom = (distance / 2f) + zoomBorder;

        return Mathf.Clamp(targetZoom, minZoom, maxZoom);
    }

    Vector3 ApplyBounds(Vector3 targetPosition)
    {
        // Keep camera within bounds
        float halfHeight = mainCamera.orthographicSize;
        float halfWidth = halfHeight * mainCamera.aspect;

        targetPosition.x = Mathf.Clamp(targetPosition.x,
            cameraBounds.min.x + halfWidth,
            cameraBounds.max.x - halfWidth);

        targetPosition.y = Mathf.Clamp(targetPosition.y,
            cameraBounds.min.y + halfHeight,
            cameraBounds.max.y - halfHeight);

        return targetPosition;
    }

    // Public method to be called by the character spawner
    public void ManualInitialize(List<Transform> spawnedPlayers)
    {
        Debug.Log($"CoopCamera: Manually initialized with {spawnedPlayers.Count} spawned players");

        playerTargets.Clear();
        playerTargets.AddRange(spawnedPlayers);

        isSinglePlayer = playerTargets.Count == 1;
        hasInitialized = true;

        Debug.Log($"CoopCamera: Successfully initialized with {playerTargets.Count} players");
        foreach (var player in playerTargets)
        {
            Debug.Log($"  - Tracking: {player.name}");
        }
    }

    // Public methods for external control
    public void AddPlayer(Transform playerTransform)
    {
        if (!playerTargets.Contains(playerTransform))
        {
            playerTargets.Add(playerTransform);
            isSinglePlayer = playerTargets.Count == 1;
            Debug.Log($"Added player to camera tracking: {playerTransform.name}");

            // Mark as initialized if we weren't before
            if (!hasInitialized && playerTargets.Count > 0)
            {
                hasInitialized = true;
            }
        }
    }

    public void RemovePlayer(Transform playerTransform)
    {
        if (playerTargets.Contains(playerTransform))
        {
            playerTargets.Remove(playerTransform);
            isSinglePlayer = playerTargets.Count == 1;
            Debug.Log($"Removed player from camera tracking: {playerTransform.name}");
        }
    }

    public void SetBounds(Bounds newBounds)
    {
        cameraBounds = newBounds;
        useBounds = true;
    }

    public void DisableBounds()
    {
        useBounds = false;
    }

    void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;

        // Draw camera bounds
        if (useBounds)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(cameraBounds.center, cameraBounds.size);
        }

        // Draw player targets
        if (playerTargets != null)
        {
            Gizmos.color = Color.red;
            foreach (var target in playerTargets)
            {
                if (target != null)
                {
                    Gizmos.DrawWireSphere(target.position, 0.5f);
                }
            }

            // Draw center point for multiplayer
            if (playerTargets.Count > 1)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(GetCenterPoint(), 0.3f);
            }
        }

        // Draw current camera view
        if (mainCamera != null && mainCamera.orthographic)
        {
            Gizmos.color = Color.blue;
            float halfHeight = mainCamera.orthographicSize;
            float halfWidth = halfHeight * mainCamera.aspect;
            Vector3 pos = transform.position;

            Gizmos.DrawWireCube(pos, new Vector3(halfWidth * 2, halfHeight * 2, 0));
        }
    }
}