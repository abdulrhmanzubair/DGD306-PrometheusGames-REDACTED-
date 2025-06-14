using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Simple UI controller for transition messages and effects
/// Optional component to enhance level transitions
/// </summary>
public class TransitionUI : MonoBehaviour
{
    [Header("UI References")]
    public Canvas uiCanvas;
    public Text messageText;
    public Text levelNameText;
    public Image progressBar;
    public GameObject messagePanel;

    [Header("Animation Settings")]
    public float fadeInDuration = 0.5f;
    public float displayDuration = 2f;
    public float fadeOutDuration = 0.5f;

    [Header("Level Names")]
    [Tooltip("Display names for each level (by scene name)")]
    public LevelNameMapping[] levelNames;

    [System.Serializable]
    public class LevelNameMapping
    {
        public string sceneName;
        public string displayName;
    }

    // Singleton for easy access
    public static TransitionUI Instance { get; private set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SetupUI();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void SetupUI()
    {
        // Create UI if not assigned
        if (uiCanvas == null)
        {
            CreateTransitionUI();
        }

        // Hide UI initially
        if (messagePanel != null)
            messagePanel.SetActive(false);
    }

    void CreateTransitionUI()
    {
        // Create canvas
        GameObject canvasGO = new GameObject("TransitionUI");
        DontDestroyOnLoad(canvasGO);

        uiCanvas = canvasGO.AddComponent<Canvas>();
        uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        uiCanvas.sortingOrder = 999; // Below transition overlay but above game UI

        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        // Create message panel
        GameObject panelGO = new GameObject("MessagePanel");
        panelGO.transform.SetParent(uiCanvas.transform, false);

        messagePanel = panelGO;
        Image panelImage = panelGO.AddComponent<Image>();
        panelImage.color = new Color(0, 0, 0, 0.7f); // Semi-transparent black

        RectTransform panelRect = panelGO.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.sizeDelta = Vector2.zero;
        panelRect.anchoredPosition = Vector2.zero;

        // Create message text
        GameObject messageGO = new GameObject("MessageText");
        messageGO.transform.SetParent(panelGO.transform, false);

        messageText = messageGO.AddComponent<Text>();
        messageText.text = "Level Complete!";
        messageText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        messageText.fontSize = 48;
        messageText.color = Color.white;
        messageText.alignment = TextAnchor.MiddleCenter;

        RectTransform messageRect = messageGO.GetComponent<RectTransform>();
        messageRect.anchorMin = new Vector2(0.1f, 0.4f);
        messageRect.anchorMax = new Vector2(0.9f, 0.6f);
        messageRect.sizeDelta = Vector2.zero;
        messageRect.anchoredPosition = Vector2.zero;

        // Create level name text
        GameObject levelGO = new GameObject("LevelNameText");
        levelGO.transform.SetParent(panelGO.transform, false);

        levelNameText = levelGO.AddComponent<Text>();
        levelNameText.text = "Loading Next Level...";
        levelNameText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        levelNameText.fontSize = 24;
        levelNameText.color = Color.yellow;
        levelNameText.alignment = TextAnchor.MiddleCenter;

        RectTransform levelRect = levelGO.GetComponent<RectTransform>();
        levelRect.anchorMin = new Vector2(0.1f, 0.3f);
        levelRect.anchorMax = new Vector2(0.9f, 0.4f);
        levelRect.sizeDelta = Vector2.zero;
        levelRect.anchoredPosition = Vector2.zero;

        // Hide panel initially
        messagePanel.SetActive(false);
    }

    /// <summary>
    /// Show a transition message
    /// </summary>
    public void ShowMessage(string message, float duration = -1f)
    {
        if (duration < 0f)
            duration = displayDuration;

        StartCoroutine(ShowMessageCoroutine(message, duration));
    }

    /// <summary>
    /// Show level complete message with next level name
    /// </summary>
    public void ShowLevelComplete(string nextSceneName = "")
    {
        string message = "Level Complete!";
        string levelName = GetLevelDisplayName(nextSceneName);

        if (!string.IsNullOrEmpty(levelName))
        {
            StartCoroutine(ShowLevelCompleteCoroutine(message, levelName));
        }
        else
        {
            ShowMessage(message);
        }
    }

    /// <summary>
    /// Show loading message
    /// </summary>
    public void ShowLoading(string sceneName = "")
    {
        string message = "Loading...";
        string levelName = GetLevelDisplayName(sceneName);

        if (!string.IsNullOrEmpty(levelName))
        {
            message = $"Loading {levelName}...";
        }

        ShowMessage(message, 0.5f); // Shorter duration for loading
    }

    string GetLevelDisplayName(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return "";

        foreach (var mapping in levelNames)
        {
            if (mapping.sceneName == sceneName)
                return mapping.displayName;
        }

        // Default formatting if no mapping found
        return sceneName.Replace("_", " ").Replace("LVL", "Level ");
    }

    IEnumerator ShowMessageCoroutine(string message, float duration)
    {
        if (messageText != null)
            messageText.text = message;

        if (levelNameText != null)
            levelNameText.text = "";

        // Show panel
        if (messagePanel != null)
        {
            messagePanel.SetActive(true);

            // Fade in
            yield return StartCoroutine(FadePanel(0f, 1f, fadeInDuration));

            // Display
            yield return new WaitForSeconds(duration);

            // Fade out
            yield return StartCoroutine(FadePanel(1f, 0f, fadeOutDuration));

            // Hide panel
            messagePanel.SetActive(false);
        }
    }

    IEnumerator ShowLevelCompleteCoroutine(string message, string levelName)
    {
        if (messageText != null)
            messageText.text = message;

        if (levelNameText != null)
            levelNameText.text = $"Next: {levelName}";

        // Show panel
        if (messagePanel != null)
        {
            messagePanel.SetActive(true);

            // Fade in
            yield return StartCoroutine(FadePanel(0f, 1f, fadeInDuration));

            // Display
            yield return new WaitForSeconds(displayDuration);

            // Fade out
            yield return StartCoroutine(FadePanel(1f, 0f, fadeOutDuration));

            // Hide panel
            messagePanel.SetActive(false);
        }
    }

    IEnumerator FadePanel(float startAlpha, float endAlpha, float duration)
    {
        if (messagePanel == null) yield break;

        CanvasGroup canvasGroup = messagePanel.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = messagePanel.AddComponent<CanvasGroup>();
        }

        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / duration);

            canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, t);
            yield return null;
        }

        canvasGroup.alpha = endAlpha;
    }

    /// <summary>
    /// Hide any currently displayed message
    /// </summary>
    public void HideMessage()
    {
        StopAllCoroutines();
        if (messagePanel != null)
            messagePanel.SetActive(false);
    }

    /// <summary>
    /// Show a simple progress bar (optional)
    /// </summary>
    public void ShowProgress(float progress)
    {
        if (progressBar != null)
        {
            progressBar.fillAmount = Mathf.Clamp01(progress);
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}