using UnityEngine;
using System.Collections;

/// <summary>
/// FIXED Health pickup item that players can collect to restore health
/// Enhanced debugging and more robust player detection
/// Updated for new PlayerHealthSystem properties
/// </summary>
public class HealthPickup : MonoBehaviour
{
    [Header("Health Settings")]
    public int healAmount = 25;
    public bool percentageHeal = false;
    [Range(0f, 100f)] public float healPercentage = 25f;

    [Header("Pickup Behavior")]
    public bool autoPickup = true;
    public bool requireInteraction = false;
    public KeyCode interactionKey = KeyCode.E;
    public float pickupRange = 1.5f;

    [Header("Lifetime")]
    public float lifetime = 30f;
    public bool fadeBeforeDestroy = true;
    public float fadeStartTime = 5f;

    [Header("Visual Effects")]
    public SpriteRenderer itemSprite;
    public GameObject pickupEffect;
    public ParticleSystem idleParticles;
    public Color glowColor = Color.green;
    public float bobSpeed = 2f;
    public float bobHeight = 0.5f;

    [Header("Audio")]
    public AudioClip pickupSound;
    public AudioClip spawnSound;
    [Range(0f, 1f)] public float pickupVolume = 0.8f;
    [Range(0f, 1f)] public float spawnVolume = 0.5f;

    [Header("Magnetism")]
    public bool magneticPickup = true;
    public float magnetRange = 3f;
    public float magnetStrength = 5f;

    [Header("DEBUG - Enhanced Debugging")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool showDebugGizmos = true;

    // Private variables
    private Vector3 startPosition;
    private float spawnTime;
    private bool isBeingCollected = false;
    private AudioSource audioSource;
    private Color originalColor;
    private GameObject nearbyPlayer;
    private bool playerInRange = false;

    void Start()
    {
        Initialize();
    }

    void Initialize()
    {
        spawnTime = Time.time;
        startPosition = transform.position;

        // VERIFY COLLIDER SETUP
        VerifyColliderSetup();

        // Setup audio
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0.7f;

        // Store original sprite color
        if (itemSprite == null)
        {
            itemSprite = GetComponent<SpriteRenderer>();
        }
        if (itemSprite != null)
        {
            originalColor = itemSprite.color;
        }

        // Play spawn sound
        if (spawnSound != null)
        {
            audioSource.PlayOneShot(spawnSound, spawnVolume);
        }

        // Start idle particles
        if (idleParticles != null && !idleParticles.isPlaying)
        {
            idleParticles.Play();
        }

        // Setup lifetime destruction
        if (lifetime > 0)
        {
            Invoke(nameof(DestroyPickup), lifetime);
        }

        if (enableDebugLogs)
        {
            Debug.Log($"[HealthPickup] Initialized - Heal: {GetHealAmount()} HP, Lifetime: {lifetime}s, Position: {transform.position}");
        }
    }

    // NEW: Verify that collider is properly set up
    void VerifyColliderSetup()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col == null)
        {
            Debug.LogError($"[HealthPickup] {gameObject.name} has NO COLLIDER! Adding BoxCollider2D...");
            BoxCollider2D boxCol = gameObject.AddComponent<BoxCollider2D>();
            boxCol.isTrigger = true;
            boxCol.size = Vector2.one;
        }
        else if (!col.isTrigger)
        {
            Debug.LogWarning($"[HealthPickup] {gameObject.name} collider is NOT set as trigger! Fixing...");
            col.isTrigger = true;
        }
        else if (enableDebugLogs)
        {
            Debug.Log($"[HealthPickup] Collider setup verified - Type: {col.GetType().Name}, IsTrigger: {col.isTrigger}");
        }
    }

    void Update()
    {
        if (isBeingCollected) return;

        // Bobbing animation
        BobAnimation();

        // Check for nearby players - ENHANCED
        CheckForPlayersEnhanced();

        // Handle magnetic attraction
        if (magneticPickup && nearbyPlayer != null)
        {
            MagneticAttraction();
        }

        // Handle interaction input
        if (requireInteraction && playerInRange && Input.GetKeyDown(interactionKey))
        {
            CollectPickup(nearbyPlayer);
        }

        // Handle fading near end of lifetime
        if (fadeBeforeDestroy && lifetime > 0)
        {
            float timeRemaining = lifetime - (Time.time - spawnTime);
            if (timeRemaining <= fadeStartTime)
            {
                FadeItem(timeRemaining / fadeStartTime);
            }
        }
    }

    void BobAnimation()
    {
        if (itemSprite != null)
        {
            float newY = startPosition.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);

            // Add glow effect
            float glowIntensity = Mathf.Sin(Time.time * bobSpeed * 2f) * 0.3f + 0.7f;
            Color glowedColor = Color.Lerp(originalColor, glowColor, glowIntensity);
            itemSprite.color = glowedColor;
        }
    }

    // ENHANCED: More robust player detection
    void CheckForPlayersEnhanced()
    {
        // Method 1: Try finding by tag
        GameObject[] playersByTag = GameObject.FindGameObjectsWithTag("Player");

        // Method 2: Try finding by PlayerHealthSystem component
        PlayerHealthSystem[] allPlayerHealthSystems = FindObjectsOfType<PlayerHealthSystem>();

        // Method 3: Try finding by PlayerController or Player_Melee_Controller1
        PlayerController[] gunnerControllers = FindObjectsOfType<PlayerController>();
        Player_Melee_Controller1[] meleeControllers = FindObjectsOfType<Player_Melee_Controller1>();

        if (enableDebugLogs && Time.frameCount % 60 == 0) // Log every 60 frames to avoid spam
        {
            Debug.Log($"[HealthPickup] Player Detection Results:");
            Debug.Log($"  - Players by tag: {playersByTag.Length}");
            Debug.Log($"  - PlayerHealthSystems: {allPlayerHealthSystems.Length}");
            Debug.Log($"  - Gunner Controllers: {gunnerControllers.Length}");
            Debug.Log($"  - Melee Controllers: {meleeControllers.Length}");
        }

        GameObject closestPlayer = null;
        float closestDistance = Mathf.Infinity;

        // Check all potential player sources
        System.Collections.Generic.List<GameObject> allPlayers = new System.Collections.Generic.List<GameObject>();

        // Add players found by tag
        allPlayers.AddRange(playersByTag);

        // Add players found by health system
        foreach (PlayerHealthSystem phs in allPlayerHealthSystems)
        {
            if (!allPlayers.Contains(phs.gameObject))
                allPlayers.Add(phs.gameObject);
        }

        // Add players found by controllers
        foreach (PlayerController pc in gunnerControllers)
        {
            if (!allPlayers.Contains(pc.gameObject))
                allPlayers.Add(pc.gameObject);
        }

        foreach (Player_Melee_Controller1 pmc in meleeControllers)
        {
            if (!allPlayers.Contains(pmc.gameObject))
                allPlayers.Add(pmc.gameObject);
        }

        foreach (GameObject player in allPlayers)
        {
            if (player == null) continue;

            // Check if player is alive
            PlayerHealthSystem playerHealth = player.GetComponent<PlayerHealthSystem>();
            if (playerHealth == null)
            {
                if (enableDebugLogs)
                    Debug.LogWarning($"[HealthPickup] Player {player.name} has no PlayerHealthSystem component!");
                continue;
            }

            if (!playerHealth.IsAlive)
            {
                if (enableDebugLogs)
                    Debug.Log($"[HealthPickup] Player {player.name} is dead, skipping");
                continue;
            }

            float distance = Vector2.Distance(transform.position, player.transform.position);

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestPlayer = player;
            }
        }

        nearbyPlayer = (closestDistance <= magnetRange) ? closestPlayer : null;
        playerInRange = (closestDistance <= pickupRange);

        if (enableDebugLogs && nearbyPlayer != null && Time.frameCount % 30 == 0)
        {
            Debug.Log($"[HealthPickup] Nearest player: {nearbyPlayer.name} at distance {closestDistance:F2}");
        }
    }

    void MagneticAttraction()
    {
        if (nearbyPlayer == null) return;

        float distance = Vector2.Distance(transform.position, nearbyPlayer.transform.position);

        if (distance <= magnetRange && distance > 0.5f)
        {
            Vector2 direction = (nearbyPlayer.transform.position - transform.position).normalized;
            float magnetForce = magnetStrength * (1f - distance / magnetRange);
            transform.position = Vector2.MoveTowards(transform.position, nearbyPlayer.transform.position, magnetForce * Time.deltaTime);
        }
    }

    void FadeItem(float fadeRatio)
    {
        if (itemSprite != null)
        {
            Color fadeColor = originalColor;
            fadeColor.a = fadeRatio;
            itemSprite.color = fadeColor;
        }

        if (idleParticles != null)
        {
            var main = idleParticles.main;
            Color particleColor = main.startColor.color;
            particleColor.a = fadeRatio;
            main.startColor = particleColor;
        }
    }

    // ENHANCED: Better collision detection
    void OnTriggerEnter2D(Collider2D other)
    {
        if (isBeingCollected) return;

        if (enableDebugLogs)
        {
            Debug.Log($"[HealthPickup] Collision detected with: {other.name} (Tag: {other.tag})");
        }

        // Check multiple ways to identify a player
        bool isPlayer = false;

        // Method 1: Check tag
        if (other.CompareTag("Player"))
        {
            isPlayer = true;
            if (enableDebugLogs) Debug.Log($"[HealthPickup] Identified as player by tag");
        }

        // Method 2: Check for PlayerHealthSystem component
        PlayerHealthSystem healthSystem = other.GetComponent<PlayerHealthSystem>();
        if (healthSystem != null)
        {
            isPlayer = true;
            if (enableDebugLogs) Debug.Log($"[HealthPickup] Identified as player by PlayerHealthSystem component");
        }

        // Method 3: Check for player controllers
        if (other.GetComponent<PlayerController>() != null || other.GetComponent<Player_Melee_Controller1>() != null)
        {
            isPlayer = true;
            if (enableDebugLogs) Debug.Log($"[HealthPickup] Identified as player by controller component");
        }

        if (isPlayer && autoPickup && !requireInteraction)
        {
            CollectPickup(other.gameObject);
        }
        else if (enableDebugLogs)
        {
            Debug.Log($"[HealthPickup] Not collecting - IsPlayer: {isPlayer}, AutoPickup: {autoPickup}, RequireInteraction: {requireInteraction}");
        }
    }

    // ENHANCED: Better collection logic with more debugging
    void CollectPickup(GameObject player)
    {
        if (isBeingCollected) return;

        if (enableDebugLogs)
        {
            Debug.Log($"[HealthPickup] Attempting to collect pickup for player: {player.name}");
        }

        PlayerHealthSystem playerHealth = player.GetComponent<PlayerHealthSystem>();
        if (playerHealth == null)
        {
            if (enableDebugLogs)
                Debug.LogError($"[HealthPickup] Player {player.name} has no PlayerHealthSystem component!");
            return;
        }

        if (!playerHealth.IsAlive)
        {
            if (enableDebugLogs)
                Debug.Log($"[HealthPickup] Player {player.name} is dead, cannot heal");
            return;
        }

        // ✅ FIXED: Check if player needs healing using proper properties
        if (playerHealth.CurrentHealth >= playerHealth.MaxHealth)
        {
            if (enableDebugLogs)
                Debug.Log($"[HealthPickup] Player {playerHealth.PlayerIndex} health is already full ({playerHealth.CurrentHealth}/{playerHealth.MaxHealth})!");
            return; // Don't collect if health is full
        }

        isBeingCollected = true;

        // Calculate heal amount
        int actualHealAmount = GetHealAmount();
        int healthBefore = playerHealth.CurrentHealth;

        // Heal the player
        playerHealth.Heal(actualHealAmount);

        int healthAfter = playerHealth.CurrentHealth;
        int actualHealed = healthAfter - healthBefore;

        if (enableDebugLogs)
        {
            Debug.Log($"[HealthPickup] SUCCESS! Player {playerHealth.PlayerIndex} healed from {healthBefore} to {healthAfter} (Expected: {actualHealAmount}, Actual: {actualHealed})");
        }

        // Play pickup sound
        if (pickupSound != null)
        {
            audioSource.PlayOneShot(pickupSound, pickupVolume);
        }

        // Spawn pickup effect
        if (pickupEffect != null)
        {
            GameObject effect = Instantiate(pickupEffect, transform.position, Quaternion.identity);
            Destroy(effect, 3f);
        }

        // Start collection sequence
        StartCoroutine(CollectionSequence());
    }

    int GetHealAmount()
    {
        if (percentageHeal)
        {
            // Find a player to get max health
            GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
            if (players.Length == 0)
            {
                // Try alternative method
                PlayerHealthSystem[] healthSystems = FindObjectsOfType<PlayerHealthSystem>();
                if (healthSystems.Length > 0)
                {
                    return Mathf.RoundToInt(healthSystems[0].MaxHealth * (healPercentage / 100f));
                }
            }
            else
            {
                PlayerHealthSystem playerHealth = players[0].GetComponent<PlayerHealthSystem>();
                if (playerHealth != null)
                {
                    return Mathf.RoundToInt(playerHealth.MaxHealth * (healPercentage / 100f));
                }
            }
            return 25; // Fallback
        }
        else
        {
            return healAmount;
        }
    }

    IEnumerator CollectionSequence()
    {
        // Cancel lifetime destruction
        CancelInvoke(nameof(DestroyPickup));

        // Stop idle particles
        if (idleParticles != null)
        {
            idleParticles.Stop();
        }

        // Scale down animation
        Vector3 originalScale = transform.localScale;
        float scaleTime = 0.3f;
        float timer = 0f;

        while (timer < scaleTime)
        {
            timer += Time.deltaTime;
            float progress = timer / scaleTime;
            transform.localScale = Vector3.Lerp(originalScale, Vector3.zero, progress);
            transform.Rotate(0, 0, 720f * Time.deltaTime); // Spin while scaling
            yield return null;
        }

        // Wait for pickup sound to finish
        if (pickupSound != null)
        {
            yield return new WaitForSeconds(pickupSound.length);
        }

        if (enableDebugLogs)
        {
            Debug.Log($"[HealthPickup] Pickup destroyed after successful collection");
        }

        // Destroy the pickup
        Destroy(gameObject);
    }

    void DestroyPickup()
    {
        if (!isBeingCollected)
        {
            if (enableDebugLogs)
                Debug.Log("[HealthPickup] Pickup expired and was destroyed");
            Destroy(gameObject);
        }
    }

    // Public methods for customization
    public void SetHealAmount(int amount)
    {
        healAmount = amount;
        percentageHeal = false;
    }

    public void SetHealPercentage(float percentage)
    {
        healPercentage = Mathf.Clamp(percentage, 0f, 100f);
        percentageHeal = true;
    }

    public void SetLifetime(float newLifetime)
    {
        CancelInvoke(nameof(DestroyPickup));
        lifetime = newLifetime;
        spawnTime = Time.time;

        if (lifetime > 0)
        {
            Invoke(nameof(DestroyPickup), lifetime);
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos) return;

        // Draw pickup range
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, pickupRange);

        // Draw magnetic range
        if (magneticPickup)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, magnetRange);
        }

        // Draw lifetime indicator
        if (lifetime > 0 && Application.isPlaying)
        {
            float timeRemaining = lifetime - (Time.time - spawnTime);
            float lifePercentage = Mathf.Clamp01(timeRemaining / lifetime);
            Gizmos.color = Color.Lerp(Color.red, Color.green, lifePercentage);
            Gizmos.DrawWireCube(transform.position + Vector3.up * 2f, Vector3.one * 0.5f);
        }

        // Draw nearby player indicator
        if (Application.isPlaying && nearbyPlayer != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, nearbyPlayer.transform.position);
        }
    }

    // ✅ FIXED: Manual test method using proper properties
    [ContextMenu("Test Pickup Collection")]
    public void TestPickupCollection()
    {
        PlayerHealthSystem[] players = FindObjectsOfType<PlayerHealthSystem>();
        if (players.Length > 0)
        {
            // Damage the first player a bit so they can be healed
            players[0].TakeDamage(30);
            Debug.Log($"[HealthPickup] Damaged player to {players[0].CurrentHealth}/{players[0].MaxHealth} for testing");

            // Try to collect
            CollectPickup(players[0].gameObject);
        }
        else
        {
            Debug.LogError("[HealthPickup] No players found for testing!");
        }
    }
}