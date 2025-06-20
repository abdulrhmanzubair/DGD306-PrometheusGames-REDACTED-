using UnityEngine;

/// <summary>
/// Individual checkpoint station that players can activate in the level
/// </summary>
public class CheckpointStation : MonoBehaviour
{
    [Header("Checkpoint Settings")]
    public int checkpointIndex = 0;
    public bool isActivated = false;
    public bool isStartingCheckpoint = false;

    [Header("Respawn Position")]
    public Transform respawnPoint; // Optional custom respawn point
    public Vector3 respawnOffset = Vector3.up * 0.5f;

    [Header("Visual Components")]
    public GameObject inactiveVisual;
    public GameObject activeVisual;
    public Light checkpointLight;
    public ParticleSystem idleEffect;
    public ParticleSystem activationEffect;
    public Animator animator;

    [Header("Audio")]
    public AudioClip activationSound;
    public AudioClip ambientSound;
    [Range(0f, 1f)] public float audioVolume = 0.7f;

    [Header("Interaction")]
    public float activationRadius = 3f;
    public LayerMask playerLayer = 1 << 0;
    public bool requiresInteraction = false; // If true, player must press a button
    public KeyCode interactionKey = KeyCode.E;

    private AudioSource audioSource;
    private bool playerInRange = false;
    private int playersInRange = 0;

    void Start()
    {
        Initialize();
    }

    void Initialize()
    {
        // Set up audio
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0.8f; // 3D audio
        audioSource.volume = audioVolume;

        // Set up respawn point if not assigned
        if (respawnPoint == null)
        {
            // Create a child object as respawn point
            GameObject respawnGO = new GameObject("RespawnPoint");
            respawnGO.transform.SetParent(transform);
            respawnGO.transform.localPosition = respawnOffset;
            respawnPoint = respawnGO.transform;
        }

        // Initialize visuals
        UpdateVisuals();

        // Play ambient sound for active checkpoints
        if (isActivated && ambientSound != null)
        {
            audioSource.clip = ambientSound;
            audioSource.loop = true;
            audioSource.Play();
        }

        // Auto-activate if this is a starting checkpoint
        if (isStartingCheckpoint && !isActivated)
        {
            ActivateCheckpoint();
        }
    }

    void Update()
    {
        if (requiresInteraction && playerInRange && !isActivated)
        {
            // Check for interaction input
            if (Input.GetKeyDown(interactionKey))
            {
                ActivateCheckpoint();
            }
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (IsPlayer(other))
        {
            PlayerHealthSystem playerHealth = other.GetComponent<PlayerHealthSystem>();
            if (playerHealth != null && playerHealth.IsAlive)
            {
                playersInRange++;
                playerInRange = true;

                // Automatic activation if not requiring interaction
                if (!requiresInteraction && !isActivated)
                {
                    ActivateCheckpoint();
                }

                OnPlayerEnterRange(playerHealth.PlayerIndex);
            }
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (IsPlayer(other))
        {
            playersInRange = Mathf.Max(0, playersInRange - 1);
            if (playersInRange == 0)
            {
                playerInRange = false;
            }

            PlayerHealthSystem playerHealth = other.GetComponent<PlayerHealthSystem>();
            if (playerHealth != null)
            {
                OnPlayerExitRange(playerHealth.PlayerIndex);
            }
        }
    }

    bool IsPlayer(Collider2D collider)
    {
        return collider.CompareTag("Player") &&
               (playerLayer.value & (1 << collider.gameObject.layer)) != 0;
    }

    public void ActivateCheckpoint()
    {
        if (isActivated) return;

        isActivated = true;

        Debug.Log($"Checkpoint {checkpointIndex} '{gameObject.name}' activated!");

        // Update visuals
        UpdateVisuals();

        // Play activation effects
        PlayActivationEffects();

        // Notify the checkpoint manager
        if (CheckpointManager.Instance != null)
        {
            // The CheckpointManager will handle this through its Update loop
        }

        // Save checkpoint progress
        SaveCheckpointProgress();
    }

    void PlayActivationEffects()
    {
        // Play activation sound
        if (activationSound != null)
        {
            audioSource.PlayOneShot(activationSound, audioVolume);
        }

        // Play activation particle effect
        if (activationEffect != null)
        {
            activationEffect.Play();
        }

        // Start ambient sound loop
        if (ambientSound != null)
        {
            audioSource.clip = ambientSound;
            audioSource.loop = true;
            audioSource.Play();
        }

        // Trigger animation
        if (animator != null)
        {
            animator.SetTrigger("Activate");
            animator.SetBool("IsActive", true);
        }
    }

    void UpdateVisuals()
    {
        // Toggle visual objects
        if (inactiveVisual != null)
        {
            inactiveVisual.SetActive(!isActivated);
        }

        if (activeVisual != null)
        {
            activeVisual.SetActive(isActivated);
        }

        // Update lighting
        if (checkpointLight != null)
        {
            checkpointLight.enabled = isActivated;
            checkpointLight.color = isActivated ? Color.green : Color.blue;
            checkpointLight.intensity = isActivated ? 2f : 1f;
        }

        // Control idle effects
        if (idleEffect != null)
        {
            if (isActivated && !idleEffect.isPlaying)
            {
                idleEffect.Play();
            }
            else if (!isActivated && idleEffect.isPlaying)
            {
                idleEffect.Stop();
            }
        }
    }

    void OnPlayerEnterRange(int playerIndex)
    {
        Debug.Log($"Player {playerIndex} entered checkpoint {checkpointIndex} range");

        // You can add UI prompts here if using interaction-based checkpoints
        if (requiresInteraction && !isActivated)
        {
            // Show interaction prompt UI
            ShowInteractionPrompt(true);
        }
    }

    void OnPlayerExitRange(int playerIndex)
    {
        Debug.Log($"Player {playerIndex} left checkpoint {checkpointIndex} range");

        if (requiresInteraction)
        {
            ShowInteractionPrompt(false);
        }
    }

    void ShowInteractionPrompt(bool show)
    {
        // Implement UI prompt logic here
        // For example, show/hide a "Press E to activate checkpoint" message

        if (show)
        {
            Debug.Log($"Press {interactionKey} to activate checkpoint");
        }
    }

    void SaveCheckpointProgress()
    {
        // Save to PlayerPrefs or your save system
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        PlayerPrefs.SetInt($"Checkpoint_{sceneName}_{checkpointIndex}", 1);
        PlayerPrefs.SetInt($"LastCheckpoint_{sceneName}", checkpointIndex);
        PlayerPrefs.Save();
    }

    public Vector3 GetRespawnPosition()
    {
        if (respawnPoint != null)
        {
            return respawnPoint.position;
        }

        return transform.position + respawnOffset;
    }

    public void ResetCheckpoint()
    {
        isActivated = false;
        UpdateVisuals();

        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }

    // Public method to force activation (for testing or scripted events)
    public void ForceActivate()
    {
        ActivateCheckpoint();
    }

    void OnDrawGizmosSelected()
    {
        // Draw activation radius
        Gizmos.color = isActivated ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, activationRadius);

        // Draw respawn position
        Vector3 respawnPos = GetRespawnPosition();
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(respawnPos, 0.5f);
        Gizmos.DrawLine(transform.position, respawnPos);

        // Draw checkpoint info
        Gizmos.color = Color.white;
        Gizmos.DrawWireCube(transform.position + Vector3.up * 2f, Vector3.one * 0.8f);

#if UNITY_EDITOR
        // Display checkpoint info in scene view
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 3f,
            $"Checkpoint {checkpointIndex}\n{(isActivated ? "ACTIVE" : "INACTIVE")}"
        );
#endif
    }
}