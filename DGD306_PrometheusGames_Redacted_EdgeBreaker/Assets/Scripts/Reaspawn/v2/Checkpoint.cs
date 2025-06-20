using UnityEngine;

/// <summary>
/// Individual checkpoint that players can activate
/// Expert Unity implementation with proper error handling
/// </summary>
public class Checkpoint : MonoBehaviour
{
    [Header("Checkpoint Settings")]
    [SerializeField] private int checkpointID = 0;
    [SerializeField] private bool isStartingCheckpoint = false;
    [SerializeField] private Transform[] spawnPoints;

    [Header("Visual Feedback")]
    [SerializeField] private GameObject inactiveVisual;
    [SerializeField] private GameObject activeVisual;
    [SerializeField] private ParticleSystem activationEffect;
    [SerializeField] private SpriteRenderer checkpointSprite;
    [SerializeField] private Color inactiveColor = Color.gray;
    [SerializeField] private Color activeColor = Color.green;

    [Header("Audio")]
    [SerializeField] private AudioClip activationSound;
    [SerializeField][Range(0f, 1f)] private float activationVolume = 0.7f;

    // Private fields
    private bool isActivated = false;
    private AudioSource audioSource;

    // Properties
    public bool IsActivated => isActivated;
    public int CheckpointID => checkpointID;
    public bool IsStartingCheckpoint => isStartingCheckpoint;

    #region Unity Lifecycle

    private void Awake()
    {
        InitializeComponents();
        ValidateColliderSetup();
    }

    private void Start()
    {
        RegisterWithManager();
        SetupSpawnPoints();

        if (isStartingCheckpoint)
        {
            ActivateCheckpoint(false);
        }

        UpdateVisuals();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && !isActivated)
        {
            ActivateCheckpoint(true);
        }
    }

    #endregion

    #region Initialization

    private void InitializeComponents()
    {
        // Setup audio source
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.7f;
        }
    }

    private void ValidateColliderSetup()
    {
        // Ensure we have a trigger collider
        Collider2D col = GetComponent<Collider2D>();
        if (col == null)
        {
            Debug.LogError($"[Checkpoint] {gameObject.name} is missing a Collider2D component!", this);
            BoxCollider2D newCol = gameObject.AddComponent<BoxCollider2D>();
            newCol.isTrigger = true;
            newCol.size = Vector2.one * 2f;
        }
        else if (!col.isTrigger)
        {
            Debug.LogWarning($"[Checkpoint] {gameObject.name} collider should be set as Trigger!", this);
            col.isTrigger = true;
        }
    }

    private void RegisterWithManager()
    {
        if (CheckpointManager.Instance != null)
        {
            CheckpointManager.Instance.RegisterCheckpoint(this);
        }
        else
        {
            Debug.LogError($"[Checkpoint] No CheckpointManager found in scene! Checkpoint {checkpointID} cannot register.", this);
        }
    }

    private void SetupSpawnPoints()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            spawnPoints = new Transform[] { transform };
            Debug.LogWarning($"[Checkpoint] {gameObject.name} has no spawn points assigned. Using checkpoint position.", this);
        }

        // Validate spawn points
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            if (spawnPoints[i] == null)
            {
                Debug.LogWarning($"[Checkpoint] Spawn point {i} is null on {gameObject.name}!", this);
                spawnPoints[i] = transform;
            }
        }
    }

    #endregion

    #region Public Methods

    public void ActivateCheckpoint(bool playEffects = true)
    {
        if (isActivated) return;

        isActivated = true;

        // Update checkpoint manager
        if (CheckpointManager.Instance != null)
        {
            CheckpointManager.Instance.SetActiveCheckpoint(this);
        }

        // Play effects
        if (playEffects)
        {
            PlayActivationEffects();
            Debug.Log($"[Checkpoint] Checkpoint {checkpointID} activated!", this);
        }

        UpdateVisuals();
    }

    public void DeactivateCheckpoint()
    {
        isActivated = false;
        UpdateVisuals();
    }

    public Vector3 GetSpawnPosition(int playerIndex = 0)
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            return transform.position;
        }

        // Clamp player index to available spawn points
        int spawnIndex = Mathf.Clamp(playerIndex, 0, spawnPoints.Length - 1);

        // If we have fewer spawn points than players, cycle through them
        if (playerIndex >= spawnPoints.Length)
        {
            spawnIndex = playerIndex % spawnPoints.Length;
        }

        return spawnPoints[spawnIndex].position;
    }

    #endregion

    #region Private Methods

    private void PlayActivationEffects()
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
    }

    private void UpdateVisuals()
    {
        // Toggle visual objects
        if (inactiveVisual != null)
            inactiveVisual.SetActive(!isActivated);

        if (activeVisual != null)
            activeVisual.SetActive(isActivated);

        // Update sprite color
        if (checkpointSprite != null)
        {
            checkpointSprite.color = isActivated ? activeColor : inactiveColor;
        }
    }

    #endregion

    #region Debug Visualization

    private void OnDrawGizmosSelected()
    {
        // Draw checkpoint indicator
        Gizmos.color = isActivated ? activeColor : inactiveColor;
        Gizmos.DrawWireCube(transform.position, Vector3.one * 1.5f);

        // Draw spawn points
        if (spawnPoints != null)
        {
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                if (spawnPoints[i] != null)
                {
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawWireCube(spawnPoints[i].position, Vector3.one * 0.8f);
                    Gizmos.DrawRay(spawnPoints[i].position, Vector3.up * 2f);

                    // Draw player index
#if UNITY_EDITOR
                    UnityEditor.Handles.Label(spawnPoints[i].position + Vector3.up * 2.5f, $"P{i + 1}");
#endif
                }
            }
        }

        // Draw trigger area
        Collider2D trigger = GetComponent<Collider2D>();
        if (trigger != null)
        {
            Gizmos.color = Color.yellow;
            if (trigger is BoxCollider2D box)
            {
                Gizmos.DrawWireCube(transform.position + (Vector3)box.offset, box.size);
            }
            else if (trigger is CircleCollider2D circle)
            {
                Gizmos.DrawWireSphere(transform.position + (Vector3)circle.offset, circle.radius);
            }
        }

        // Draw checkpoint ID
#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 3f, $"ID: {checkpointID}");
#endif
    }

    #endregion
}