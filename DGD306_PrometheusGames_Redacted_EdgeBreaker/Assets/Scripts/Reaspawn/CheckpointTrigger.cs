using UnityEngine;

/// <summary>
/// Checkpoint trigger that activates when players reach it
/// Place this on checkpoint GameObjects
/// </summary>
public class CheckpointTrigger : MonoBehaviour
{
    [Header("Checkpoint Settings")]
    public int checkpointIndex = 0;
    public bool isActivated = false;
    public bool saveOnActivation = true;

    [Header("Visual Feedback")]
    public GameObject inactiveVisual;
    public GameObject activeVisual;
    public ParticleSystem activationEffect;
    public Light checkpointLight;

    [Header("Audio")]
    public AudioClip activationSound;
    [Range(0f, 1f)] public float activationVolume = 0.6f;

    private AudioSource audioSource;
    private bool hasBeenActivated = false;

    void Start()
    {
        // Set up audio
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0.7f; // 3D audio

        // Set initial visual state
        UpdateVisuals();

        // Subscribe to checkpoint events
        if (GameLifeManager.Instance != null)
        {
            GameLifeManager.Instance.OnCheckpointReached += OnCheckpointReached;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // Check if a player entered
        if (other.CompareTag("Player") && !hasBeenActivated)
        {
            // Verify the player is alive
            PlayerHealthSystem playerHealth = other.GetComponent<PlayerHealthSystem>();
            if (playerHealth != null && playerHealth.IsAlive)
            {
                ActivateCheckpoint();
            }
        }
    }

    void ActivateCheckpoint()
    {
        if (hasBeenActivated) return;

        hasBeenActivated = true;
        isActivated = true;

        Debug.Log($"Checkpoint {checkpointIndex} activated!");

        // Notify the game life manager
        if (GameLifeManager.Instance != null)
        {
            GameLifeManager.Instance.SetCheckpoint(checkpointIndex);
        }

        // Visual and audio feedback
        PlayActivationFeedback();

        // Update visuals
        UpdateVisuals();

        // Save game if enabled
        if (saveOnActivation)
        {
            SaveCheckpointProgress();
        }
    }

    void OnCheckpointReached(int reachedCheckpointIndex)
    {
        // Update our state if this checkpoint was reached
        if (reachedCheckpointIndex == checkpointIndex && !hasBeenActivated)
        {
            hasBeenActivated = true;
            isActivated = true;
            UpdateVisuals();
        }
    }

    void PlayActivationFeedback()
    {
        // Play sound
        if (activationSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(activationSound, activationVolume);
        }

        // Play particle effect
        if (activationEffect != null)
        {
            activationEffect.Play();
        }

        // Update light
        if (checkpointLight != null)
        {
            checkpointLight.enabled = true;
            checkpointLight.color = Color.green;
        }
    }

    void UpdateVisuals()
    {
        // Switch visual representations
        if (inactiveVisual != null)
        {
            inactiveVisual.SetActive(!isActivated);
        }

        if (activeVisual != null)
        {
            activeVisual.SetActive(isActivated);
        }

        // Update light color
        if (checkpointLight != null)
        {
            checkpointLight.color = isActivated ? Color.green : Color.blue;
            checkpointLight.intensity = isActivated ? 2f : 1f;
        }
    }

    void SaveCheckpointProgress()
    {
        // Save checkpoint progress to PlayerPrefs or save system
        PlayerPrefs.SetInt($"Checkpoint_{UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}", checkpointIndex);
        PlayerPrefs.SetInt($"CheckpointActivated_{checkpointIndex}", 1);
        PlayerPrefs.Save();

        Debug.Log($"Checkpoint {checkpointIndex} progress saved");
    }

    // Public method to manually activate (for testing or special cases)
    public void ForceActivate()
    {
        ActivateCheckpoint();
    }

    // Public method to reset checkpoint
    public void ResetCheckpoint()
    {
        hasBeenActivated = false;
        isActivated = false;
        UpdateVisuals();
    }

    void OnDestroy()
    {
        // Unsubscribe from events
        if (GameLifeManager.Instance != null)
        {
            GameLifeManager.Instance.OnCheckpointReached -= OnCheckpointReached;
        }
    }

    void OnDrawGizmosSelected()
    {
        // Draw checkpoint activation area
        Gizmos.color = isActivated ? Color.green : Color.blue;
        Gizmos.DrawWireSphere(transform.position, 3f);

        // Draw checkpoint index
        Gizmos.color = Color.white;
        Gizmos.DrawWireCube(transform.position + Vector3.up * 3f, Vector3.one * 0.8f);

#if UNITY_EDITOR
        // Draw checkpoint number in scene view
        UnityEditor.Handles.Label(transform.position + Vector3.up * 4f, $"Checkpoint {checkpointIndex}");
#endif
    }
}