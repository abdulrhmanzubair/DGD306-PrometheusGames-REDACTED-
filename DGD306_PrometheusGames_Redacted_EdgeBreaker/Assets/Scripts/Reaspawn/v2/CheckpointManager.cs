using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Singleton manager for all checkpoints in the level
/// Expert Unity implementation with proper singleton pattern
/// </summary>
public class CheckpointManager : MonoBehaviour
{
    #region Singleton Pattern

    private static CheckpointManager instance;
    public static CheckpointManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<CheckpointManager>();

                if (instance == null)
                {
                    GameObject go = new GameObject("CheckpointManager");
                    instance = go.AddComponent<CheckpointManager>();
                    Debug.LogWarning("[CheckpointManager] No CheckpointManager found, creating one automatically.");
                }
            }
            return instance;
        }
    }

    #endregion

    #region Serialized Fields

    [Header("Checkpoint Management")]
    [SerializeField] private List<Checkpoint> checkpoints = new List<Checkpoint>();
    [SerializeField] private bool autoFindCheckpoints = true;
    [SerializeField] private bool sortByID = true;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    #endregion

    #region Private Fields

    private Checkpoint activeCheckpoint;
    private const string CHECKPOINT_PREFS_KEY = "LastCheckpointID";

    #endregion

    #region Properties

    public Checkpoint ActiveCheckpoint => activeCheckpoint;
    public int CheckpointCount => checkpoints.Count;
    public bool HasActiveCheckpoint => activeCheckpoint != null;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        // Singleton pattern
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Debug.LogWarning("[CheckpointManager] Multiple CheckpointManagers detected. Destroying duplicate.");
            Destroy(gameObject);
            return;
        }

        InitializeManager();
    }

    private void Start()
    {
        if (autoFindCheckpoints)
        {
            FindAllCheckpoints();
        }

        SetupStartingCheckpoint();

        if (enableDebugLogs)
        {
            Debug.Log($"[CheckpointManager] Initialized with {checkpoints.Count} checkpoints.");
        }
    }

    #endregion

    #region Initialization

    private void InitializeManager()
    {
        // Validate existing checkpoint list
        if (checkpoints != null)
        {
            checkpoints.RemoveAll(cp => cp == null);
        }
        else
        {
            checkpoints = new List<Checkpoint>();
        }
    }

    private void FindAllCheckpoints()
    {
        Checkpoint[] foundCheckpoints = FindObjectsOfType<Checkpoint>();

        foreach (Checkpoint checkpoint in foundCheckpoints)
        {
            RegisterCheckpoint(checkpoint);
        }

        if (enableDebugLogs)
        {
            Debug.Log($"[CheckpointManager] Auto-found {foundCheckpoints.Length} checkpoints in scene.");
        }
    }

    private void SetupStartingCheckpoint()
    {
        // Don't error if no checkpoints exist yet - they may be added later
        if (checkpoints.Count == 0)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning("[CheckpointManager] No checkpoints found in scene yet. Checkpoints can be added later.");
            }
            return;
        }

        // Find starting checkpoint
        Checkpoint startingCheckpoint = checkpoints.FirstOrDefault(cp => cp.IsStartingCheckpoint);

        if (startingCheckpoint != null)
        {
            activeCheckpoint = startingCheckpoint;
            if (enableDebugLogs)
            {
                Debug.Log($"[CheckpointManager] Starting checkpoint found: ID {startingCheckpoint.CheckpointID}");
            }
        }
        else if (checkpoints.Count > 0)
        {
            // Use first checkpoint as starting point
            activeCheckpoint = checkpoints[0];
            if (enableDebugLogs)
            {
                Debug.LogWarning("[CheckpointManager] No starting checkpoint marked. Using first checkpoint as starting point.");
            }
        }
    }

    #endregion

    #region Public Methods

    public void RegisterCheckpoint(Checkpoint checkpoint)
    {
        if (checkpoint == null)
        {
            Debug.LogError("[CheckpointManager] Attempted to register null checkpoint!");
            return;
        }

        if (!checkpoints.Contains(checkpoint))
        {
            checkpoints.Add(checkpoint);

            if (sortByID)
            {
                SortCheckpointsByID();
            }

            if (enableDebugLogs)
            {
                Debug.Log($"[CheckpointManager] Registered checkpoint ID {checkpoint.CheckpointID}");
            }

            // If this is the first checkpoint and no active checkpoint is set, set up starting checkpoint
            if (activeCheckpoint == null && checkpoints.Count > 0)
            {
                SetupStartingCheckpoint();
            }
        }
    }

    public void UnregisterCheckpoint(Checkpoint checkpoint)
    {
        if (checkpoints.Remove(checkpoint))
        {
            if (activeCheckpoint == checkpoint)
            {
                activeCheckpoint = null;
            }

            if (enableDebugLogs)
            {
                Debug.Log($"[CheckpointManager] Unregistered checkpoint ID {checkpoint.CheckpointID}");
            }
        }
    }

    public void SetActiveCheckpoint(Checkpoint checkpoint)
    {
        if (checkpoint == null)
        {
            Debug.LogError("[CheckpointManager] Attempted to set null checkpoint as active!");
            return;
        }

        // Deactivate previous checkpoint
        if (activeCheckpoint != null && activeCheckpoint != checkpoint)
        {
            activeCheckpoint.DeactivateCheckpoint();
        }

        activeCheckpoint = checkpoint;

        if (enableDebugLogs)
        {
            Debug.Log($"[CheckpointManager] Active checkpoint set to ID {checkpoint.CheckpointID}");
        }

        // Save progress
        SaveCheckpointProgress();
    }

    public Vector3 GetRespawnPosition(int playerIndex = 0)
    {
        if (activeCheckpoint != null)
        {
            return activeCheckpoint.GetSpawnPosition(playerIndex);
        }

        // Fallback: return first checkpoint or world origin
        if (checkpoints.Count > 0)
        {
            return checkpoints[0].GetSpawnPosition(playerIndex);
        }

        Debug.LogWarning("[CheckpointManager] No active checkpoint found! Returning world origin.");
        return Vector3.zero;
    }

    public Checkpoint GetCheckpointByID(int checkpointID)
    {
        return checkpoints.FirstOrDefault(cp => cp.CheckpointID == checkpointID);
    }

    public Checkpoint GetNextCheckpoint()
    {
        if (activeCheckpoint == null || checkpoints.Count == 0) return null;

        int currentIndex = checkpoints.IndexOf(activeCheckpoint);
        if (currentIndex >= 0 && currentIndex < checkpoints.Count - 1)
        {
            return checkpoints[currentIndex + 1];
        }

        return null; // No next checkpoint
    }

    public Checkpoint GetPreviousCheckpoint()
    {
        if (activeCheckpoint == null || checkpoints.Count == 0) return null;

        int currentIndex = checkpoints.IndexOf(activeCheckpoint);
        if (currentIndex > 0)
        {
            return checkpoints[currentIndex - 1];
        }

        return null; // No previous checkpoint
    }

    public void ResetAllCheckpoints()
    {
        foreach (Checkpoint checkpoint in checkpoints)
        {
            if (checkpoint != null)
            {
                checkpoint.DeactivateCheckpoint();
            }
        }

        // Activate starting checkpoint
        Checkpoint startingCheckpoint = checkpoints.FirstOrDefault(cp => cp.IsStartingCheckpoint);
        if (startingCheckpoint != null)
        {
            startingCheckpoint.ActivateCheckpoint(false);
            activeCheckpoint = startingCheckpoint;
        }

        // Clear saved progress for full restart
        ClearSavedProgress();

        if (enableDebugLogs)
        {
            Debug.Log("[CheckpointManager] All checkpoints reset to starting state (progress cleared).");
        }
    }

    public void ResetCheckpointsKeepProgress()
    {
        // This is for soft resets where we want to keep checkpoint progress
        foreach (Checkpoint checkpoint in checkpoints)
        {
            if (checkpoint != null)
            {
                checkpoint.DeactivateCheckpoint();
            }
        }

        // Activate starting checkpoint
        Checkpoint startingCheckpoint = checkpoints.FirstOrDefault(cp => cp.IsStartingCheckpoint);
        if (startingCheckpoint != null)
        {
            startingCheckpoint.ActivateCheckpoint(false);
            activeCheckpoint = startingCheckpoint;
        }

        // Keep saved progress intact
        if (enableDebugLogs)
        {
            Debug.Log("[CheckpointManager] Checkpoints reset but progress preserved.");
        }
    }

    #endregion

    #region Save/Load System

    public void SaveCheckpointProgress()
    {
        if (activeCheckpoint != null)
        {
            PlayerPrefs.SetInt(CHECKPOINT_PREFS_KEY, activeCheckpoint.CheckpointID);
            PlayerPrefs.Save();

            if (enableDebugLogs)
            {
                Debug.Log($"[CheckpointManager] Saved checkpoint progress: ID {activeCheckpoint.CheckpointID}");
            }
        }
    }

    public void LoadCheckpointProgress()
    {
        if (!PlayerPrefs.HasKey(CHECKPOINT_PREFS_KEY))
        {
            if (enableDebugLogs)
            {
                Debug.Log("[CheckpointManager] No saved checkpoint progress found.");
            }
            return;
        }

        int lastCheckpointID = PlayerPrefs.GetInt(CHECKPOINT_PREFS_KEY, 0);
        Checkpoint checkpoint = GetCheckpointByID(lastCheckpointID);

        if (checkpoint != null)
        {
            checkpoint.ActivateCheckpoint(false);
            activeCheckpoint = checkpoint;

            if (enableDebugLogs)
            {
                Debug.Log($"[CheckpointManager] Loaded checkpoint progress: ID {lastCheckpointID}");
            }
        }
        else
        {
            Debug.LogWarning($"[CheckpointManager] Could not find checkpoint with ID {lastCheckpointID}");
        }
    }

    public void ClearSavedProgress()
    {
        PlayerPrefs.DeleteKey(CHECKPOINT_PREFS_KEY);
        PlayerPrefs.Save();

        if (enableDebugLogs)
        {
            Debug.Log("[CheckpointManager] Cleared saved checkpoint progress.");
        }
    }

    #endregion

    #region Private Helper Methods

    private void SortCheckpointsByID()
    {
        checkpoints = checkpoints.OrderBy(cp => cp.CheckpointID).ToList();
    }

    #endregion

    #region Debug Methods

    [ContextMenu("Debug: Print All Checkpoints")]
    public void DebugPrintAllCheckpoints()
    {
        Debug.Log($"[CheckpointManager] Total checkpoints: {checkpoints.Count}");

        for (int i = 0; i < checkpoints.Count; i++)
        {
            Checkpoint cp = checkpoints[i];
            string status = cp.IsActivated ? "ACTIVE" : "inactive";
            string starting = cp.IsStartingCheckpoint ? " (STARTING)" : "";
            Debug.Log($"  [{i}] ID: {cp.CheckpointID} - {status}{starting} at {cp.transform.position}");
        }

        if (activeCheckpoint != null)
        {
            Debug.Log($"[CheckpointManager] Current active: ID {activeCheckpoint.CheckpointID}");
        }
    }

    [ContextMenu("Debug: Reset to Starting Checkpoint")]
    public void DebugResetToStarting()
    {
        ResetAllCheckpoints();
    }

    [ContextMenu("Debug: Clear Saved Progress")]
    public void DebugClearProgress()
    {
        ClearSavedProgress();
    }

    #endregion

    #region Validation

    private void OnValidate()
    {
        if (checkpoints != null)
        {
            // Remove null entries
            checkpoints.RemoveAll(cp => cp == null);

            // Check for duplicate IDs
            var duplicateIDs = checkpoints.GroupBy(cp => cp.CheckpointID)
                                         .Where(g => g.Count() > 1)
                                         .Select(g => g.Key);

            if (duplicateIDs.Any())
            {
                Debug.LogWarning($"[CheckpointManager] Duplicate checkpoint IDs found: {string.Join(", ", duplicateIDs)}");
            }
        }
    }

    #endregion
}