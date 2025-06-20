using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// UI system for showing checkpoint activation notifications
/// Displays animated messages when checkpoints are reached
/// </summary>
public class CheckpointNotificationUI : MonoBehaviour
{
    #region Serialized Fields

    [Header("UI References")]
    [SerializeField] private GameObject notificationPanel;
    [SerializeField] private Text notificationText;
    [SerializeField] private Text checkpointNameText;
    [SerializeField] private Image notificationIcon;
    [SerializeField] private Image notificationBackground;

    [Header("Display Settings")]
    [SerializeField] private float displayDuration = 3f;
    [SerializeField] private float fadeInDuration = 0.5f;
    [SerializeField] private float fadeOutDuration = 0.5f;
    [SerializeField] private bool queueMultipleNotifications = true;

    [Header("Animation")]
    [SerializeField] private AnimationType animationType = AnimationType.SlideFromTop;
    [SerializeField] private float slideDistance = 100f;
    [SerializeField] private AnimationCurve animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Text Content")]
    [SerializeField] private string checkpointMessage = "CHECKPOINT REACHED";
    [SerializeField] private string checkpointNameFormat = "Checkpoint {0}";
    [SerializeField] private string progressSavedMessage = "Progress Saved";

    [Header("Visual Styling")]
    [SerializeField] private Color checkpointColor = Color.green;
    [SerializeField] private Color backgroundStartColor = new Color(0, 1, 0, 0.8f);
    [SerializeField] private Color backgroundEndColor = new Color(0, 1, 0, 0.3f);
    [SerializeField] private Sprite checkpointIcon;

    [Header("Audio")]
    [SerializeField] private AudioClip checkpointSound;
    [SerializeField][Range(0f, 1f)] private float notificationVolume = 0.6f;

    #endregion

    #region Enums

    public enum AnimationType
    {
        Fade,
        SlideFromTop,
        SlideFromLeft,
        SlideFromRight,
        Scale,
        Bounce
    }

    #endregion

    #region Private Fields

    private CheckpointManager checkpointManager;
    private AudioSource audioSource;
    private CanvasGroup canvasGroup;
    private RectTransform panelTransform;

    // Animation state
    private Coroutine currentNotificationCoroutine;
    private Queue<CheckpointNotificationData> notificationQueue = new Queue<CheckpointNotificationData>();
    private bool isShowingNotification = false;

    // Original transform values
    private Vector3 originalPosition;
    private Vector3 originalScale;

    #endregion

    #region Notification Data Structure

    [System.Serializable]
    public struct CheckpointNotificationData
    {
        public string message;
        public string checkpointName;
        public Color iconColor;
        public Sprite icon;

        public CheckpointNotificationData(string msg, string name, Color color, Sprite iconSprite)
        {
            message = msg;
            checkpointName = name;
            iconColor = color;
            icon = iconSprite;
        }
    }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        InitializeComponents();
    }

    private void Start()
    {
        ConnectToCheckpointManager();
        HideNotification(true);
    }

    private void OnDestroy()
    {
        DisconnectFromCheckpointManager();
        StopAllCoroutines();
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
            audioSource.spatialBlend = 0f; // 2D audio
        }

        // Setup canvas group for fading
        if (notificationPanel != null)
        {
            canvasGroup = notificationPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = notificationPanel.AddComponent<CanvasGroup>();
            }

            panelTransform = notificationPanel.GetComponent<RectTransform>();
            if (panelTransform != null)
            {
                originalPosition = panelTransform.anchoredPosition;
                originalScale = panelTransform.localScale;
            }
        }

        // Setup default icon
        if (notificationIcon != null && checkpointIcon != null)
        {
            notificationIcon.sprite = checkpointIcon;
        }
    }

    private void ConnectToCheckpointManager()
    {
        checkpointManager = CheckpointManager.Instance;

        if (checkpointManager != null)
        {
            // Subscribe to checkpoint events - we need to find checkpoints and subscribe to them
            Checkpoint[] checkpoints = FindObjectsOfType<Checkpoint>();
            foreach (Checkpoint checkpoint in checkpoints)
            {
                // We would need to add an event to Checkpoint script, for now we'll use a different approach
                // StartCoroutine(MonitorCheckpointActivation(checkpoint));
            }

            // Alternative: Monitor the active checkpoint change
            StartCoroutine(MonitorActiveCheckpointChanges());
        }
        else
        {
            Debug.LogError("[CheckpointNotificationUI] No CheckpointManager found!");
        }
    }

    private void DisconnectFromCheckpointManager()
    {
        // Cleanup subscriptions if needed
    }

    #endregion

    #region Checkpoint Monitoring

    private IEnumerator MonitorActiveCheckpointChanges()
    {
        Checkpoint lastActiveCheckpoint = null;

        while (checkpointManager != null)
        {
            Checkpoint currentActive = checkpointManager.ActiveCheckpoint;

            if (currentActive != null && currentActive != lastActiveCheckpoint)
            {
                // New checkpoint activated
                OnCheckpointActivated(currentActive);
                lastActiveCheckpoint = currentActive;
            }

            yield return new WaitForSeconds(0.1f); // Check 10 times per second
        }
    }

    private void OnCheckpointActivated(Checkpoint checkpoint)
    {
        string checkpointName = string.Format(checkpointNameFormat, checkpoint.CheckpointID);

        var notificationData = new CheckpointNotificationData(
            checkpointMessage,
            checkpointName,
            checkpointColor,
            checkpointIcon
        );

        ShowNotification(notificationData);
    }

    #endregion

    #region Notification Display

    public void ShowNotification(CheckpointNotificationData data)
    {
        if (queueMultipleNotifications && isShowingNotification)
        {
            // Queue this notification
            notificationQueue.Enqueue(data);
            return;
        }

        // Stop any current notification
        if (currentNotificationCoroutine != null)
        {
            StopCoroutine(currentNotificationCoroutine);
        }

        currentNotificationCoroutine = StartCoroutine(ShowNotificationCoroutine(data));
    }

    public void ShowCheckpointNotification(int checkpointID)
    {
        string checkpointName = string.Format(checkpointNameFormat, checkpointID);

        var data = new CheckpointNotificationData(
            checkpointMessage,
            checkpointName,
            checkpointColor,
            checkpointIcon
        );

        ShowNotification(data);
    }

    public void ShowCustomNotification(string message, string subtitle = "", Color? color = null)
    {
        var data = new CheckpointNotificationData(
            message,
            subtitle,
            color ?? checkpointColor,
            checkpointIcon
        );

        ShowNotification(data);
    }

    private IEnumerator ShowNotificationCoroutine(CheckpointNotificationData data)
    {
        isShowingNotification = true;

        // Setup notification content
        SetupNotificationContent(data);

        // Play sound
        if (checkpointSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(checkpointSound, notificationVolume);
        }

        // Show notification with animation
        yield return StartCoroutine(AnimateNotificationIn());

        // Wait for display duration
        yield return new WaitForSecondsRealtime(displayDuration);

        // Hide notification with animation
        yield return StartCoroutine(AnimateNotificationOut());

        isShowingNotification = false;
        currentNotificationCoroutine = null;

        // Process next notification in queue
        if (notificationQueue.Count > 0)
        {
            var nextNotification = notificationQueue.Dequeue();
            ShowNotification(nextNotification);
        }
    }

    private void SetupNotificationContent(CheckpointNotificationData data)
    {
        // Set main message
        if (notificationText != null)
        {
            notificationText.text = data.message;
            notificationText.color = data.iconColor;
        }

        // Set checkpoint name
        if (checkpointNameText != null)
        {
            checkpointNameText.text = data.checkpointName;
            checkpointNameText.color = data.iconColor;
        }

        // Set icon
        if (notificationIcon != null)
        {
            if (data.icon != null)
            {
                notificationIcon.sprite = data.icon;
            }
            notificationIcon.color = data.iconColor;
        }

        // Set background gradient
        if (notificationBackground != null)
        {
            notificationBackground.color = backgroundStartColor;
        }
    }

    #endregion

    #region Animation System

    private IEnumerator AnimateNotificationIn()
    {
        if (notificationPanel != null)
        {
            notificationPanel.SetActive(true);
        }

        // Reset to starting state
        SetupAnimationStartState();

        float elapsed = 0f;

        while (elapsed < fadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = animationCurve.Evaluate(elapsed / fadeInDuration);

            ApplyAnimationProgress(progress, true);

            yield return null;
        }

        // Ensure final state
        ApplyAnimationProgress(1f, true);
    }

    private IEnumerator AnimateNotificationOut()
    {
        float elapsed = 0f;

        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = animationCurve.Evaluate(elapsed / fadeOutDuration);

            ApplyAnimationProgress(1f - progress, false);

            yield return null;
        }

        // Hide notification
        HideNotification(true);
    }

    private void SetupAnimationStartState()
    {
        if (panelTransform == null || canvasGroup == null) return;

        switch (animationType)
        {
            case AnimationType.Fade:
                canvasGroup.alpha = 0f;
                panelTransform.anchoredPosition = originalPosition;
                panelTransform.localScale = originalScale;
                break;

            case AnimationType.SlideFromTop:
                canvasGroup.alpha = 1f;
                panelTransform.anchoredPosition = originalPosition + Vector3.up * slideDistance;
                panelTransform.localScale = originalScale;
                break;

            case AnimationType.SlideFromLeft:
                canvasGroup.alpha = 1f;
                panelTransform.anchoredPosition = originalPosition + Vector3.left * slideDistance;
                panelTransform.localScale = originalScale;
                break;

            case AnimationType.SlideFromRight:
                canvasGroup.alpha = 1f;
                panelTransform.anchoredPosition = originalPosition + Vector3.right * slideDistance;
                panelTransform.localScale = originalScale;
                break;

            case AnimationType.Scale:
                canvasGroup.alpha = 1f;
                panelTransform.anchoredPosition = originalPosition;
                panelTransform.localScale = Vector3.zero;
                break;

            case AnimationType.Bounce:
                canvasGroup.alpha = 1f;
                panelTransform.anchoredPosition = originalPosition;
                panelTransform.localScale = Vector3.zero;
                break;
        }
    }

    private void ApplyAnimationProgress(float progress, bool isIn)
    {
        if (panelTransform == null || canvasGroup == null) return;

        switch (animationType)
        {
            case AnimationType.Fade:
                canvasGroup.alpha = progress;
                break;

            case AnimationType.SlideFromTop:
                Vector3 topStartPos = originalPosition + Vector3.up * slideDistance;
                panelTransform.anchoredPosition = Vector3.Lerp(topStartPos, originalPosition, progress);
                canvasGroup.alpha = progress;
                break;

            case AnimationType.SlideFromLeft:
                Vector3 leftStartPos = originalPosition + Vector3.left * slideDistance;
                panelTransform.anchoredPosition = Vector3.Lerp(leftStartPos, originalPosition, progress);
                canvasGroup.alpha = progress;
                break;

            case AnimationType.SlideFromRight:
                Vector3 rightStartPos = originalPosition + Vector3.right * slideDistance;
                panelTransform.anchoredPosition = Vector3.Lerp(rightStartPos, originalPosition, progress);
                canvasGroup.alpha = progress;
                break;

            case AnimationType.Scale:
                panelTransform.localScale = Vector3.Lerp(Vector3.zero, originalScale, progress);
                canvasGroup.alpha = progress;
                break;

            case AnimationType.Bounce:
                float bounceScale = isIn ?
                    Mathf.LerpUnclamped(0f, 1.2f, progress) :
                    Mathf.LerpUnclamped(1f, 0f, 1f - progress);

                if (isIn && progress > 0.8f)
                {
                    bounceScale = Mathf.Lerp(1.2f, 1f, (progress - 0.8f) / 0.2f);
                }

                panelTransform.localScale = originalScale * bounceScale;
                canvasGroup.alpha = progress;
                break;
        }

        // Update background color gradient
        if (notificationBackground != null)
        {
            Color bgColor = Color.Lerp(backgroundEndColor, backgroundStartColor, progress);
            notificationBackground.color = bgColor;
        }
    }

    #endregion

    #region Utility Methods

    private void HideNotification(bool immediate = false)
    {
        if (notificationPanel != null)
        {
            if (immediate)
            {
                notificationPanel.SetActive(false);
            }
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }

        if (panelTransform != null)
        {
            panelTransform.anchoredPosition = originalPosition;
            panelTransform.localScale = originalScale;
        }
    }

    public void ClearNotificationQueue()
    {
        notificationQueue.Clear();
    }

    public void ForceHideNotification()
    {
        if (currentNotificationCoroutine != null)
        {
            StopCoroutine(currentNotificationCoroutine);
            currentNotificationCoroutine = null;
        }

        isShowingNotification = false;
        HideNotification(true);
        ClearNotificationQueue();
    }

    #endregion

    #region Public Properties

    public bool IsShowingNotification => isShowingNotification;
    public int QueuedNotificationCount => notificationQueue.Count;

    #endregion

    #region Context Menu Debug

    [ContextMenu("Test Checkpoint Notification")]
    private void TestCheckpointNotification()
    {
        ShowCheckpointNotification(1);
    }

    [ContextMenu("Test Custom Notification")]
    private void TestCustomNotification()
    {
        ShowCustomNotification("TEST MESSAGE", "Test Subtitle", Color.blue);
    }

    [ContextMenu("Test Multiple Notifications")]
    private void TestMultipleNotifications()
    {
        ShowCheckpointNotification(1);
        ShowCheckpointNotification(2);
        ShowCheckpointNotification(3);
    }

    [ContextMenu("Force Hide Notification")]
    private void DebugForceHide()
    {
        ForceHideNotification();
    }

    #endregion
}