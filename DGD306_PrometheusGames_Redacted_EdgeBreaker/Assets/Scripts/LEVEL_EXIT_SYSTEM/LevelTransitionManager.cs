using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// Manages smooth transitions between levels
/// Add this to a persistent GameObject or use as a singleton
/// </summary>
public class LevelTransitionManager : MonoBehaviour
{
    [Header("Transition Settings")]
    [Tooltip("Type of transition effect")]
    public TransitionType transitionType = TransitionType.Fade;
    [Tooltip("Duration of transition effect")]
    public float transitionDuration = 1f;
    [Tooltip("Color for fade transitions")]
    public Color fadeColor = Color.black;

    [Header("UI References")]
    [Tooltip("Canvas for transition overlay - will be created if null")]
    public Canvas transitionCanvas;
    [Tooltip("UI Image for fade effect - will be created if null")]
    public UnityEngine.UI.Image fadeImage;

    [Header("Audio")]
    [Tooltip("Sound to play during transition")]
    public AudioClip transitionSound;
    [Tooltip("Audio source for transition sounds")]
    public AudioSource audioSource;

    [Header("Debug")]
    public bool enableDebugLogs = true;

    public enum TransitionType
    {
        Fade,
        Slide,
        Circle,
        Iris,
        Pixelate
    }

    // Singleton instance
    public static LevelTransitionManager Instance { get; private set; }

    // Events
    public System.Action OnTransitionStart;
    public System.Action OnTransitionComplete;

    private bool isTransitioning = false;
    private Coroutine currentTransition;

    void Awake()
    {
        // Singleton setup
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SetupTransitionUI();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void SetupTransitionUI()
    {
        // Create transition canvas if not assigned
        if (transitionCanvas == null)
        {
            GameObject canvasGO = new GameObject("TransitionCanvas");
            DontDestroyOnLoad(canvasGO);

            transitionCanvas = canvasGO.AddComponent<Canvas>();
            transitionCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            transitionCanvas.sortingOrder = 1000; // Render on top of everything

            canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        }

        // Create fade image if not assigned
        if (fadeImage == null)
        {
            GameObject imageGO = new GameObject("FadeImage");
            imageGO.transform.SetParent(transitionCanvas.transform, false);

            fadeImage = imageGO.AddComponent<UnityEngine.UI.Image>();
            fadeImage.color = fadeColor;
            fadeImage.raycastTarget = false;

            // Make it cover the entire screen
            RectTransform rect = fadeImage.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
        }

        // Start with transparent and HIDDEN
        SetImageAlpha(0f);
        HideTransitionCanvas();

        // Setup audio source if not assigned
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
    }

    /// <summary>
    /// Transition to a specific scene
    /// </summary>
    public void TransitionToScene(string sceneName)
    {
        if (isTransitioning)
        {
            DebugLog("Transition already in progress!");
            return;
        }

        DebugLog($"Starting transition to scene: {sceneName}");
        currentTransition = StartCoroutine(TransitionCoroutine(sceneName));
    }

    /// <summary>
    /// Transition to next level in build order
    /// </summary>
    public void TransitionToNextLevel()
    {
        int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
        int nextSceneIndex = currentSceneIndex + 1;

        if (nextSceneIndex >= SceneManager.sceneCountInBuildSettings)
        {
            DebugLog("No more levels! Returning to main menu or ending game.");
            // You can customize this behavior
            TransitionToScene("MainMenu"); // or whatever your main menu scene is called
            return;
        }

        string nextSceneName = System.IO.Path.GetFileNameWithoutExtension(
            SceneUtility.GetScenePathByBuildIndex(nextSceneIndex));

        TransitionToScene(nextSceneName);
    }

    /// <summary>
    /// Restart current level
    /// </summary>
    public void RestartLevel()
    {
        string currentSceneName = SceneManager.GetActiveScene().name;
        TransitionToScene(currentSceneName);
    }

    /// <summary>
    /// Transition back to main menu
    /// </summary>
    public void TransitionToMainMenu()
    {
        TransitionToScene("MainMenu"); // Adjust scene name as needed
    }

    IEnumerator TransitionCoroutine(string sceneName)
    {
        isTransitioning = true;
        OnTransitionStart?.Invoke();

        // Show transition canvas
        ShowTransitionCanvas();

        // Play transition sound
        if (transitionSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(transitionSound);
        }

        // Transition out (fade in overlay)
        yield return StartCoroutine(TransitionOut());

        // Load new scene
        DebugLog($"Loading scene: {sceneName}");

        // Use async loading for better performance
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);

        // Wait for scene to load
        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        // Small delay to ensure everything is initialized
        yield return new WaitForSeconds(0.1f);

        // Transition in (fade out overlay)
        yield return StartCoroutine(TransitionIn());

        // Hide transition canvas after transition is complete
        HideTransitionCanvas();

        isTransitioning = false;
        OnTransitionComplete?.Invoke();

        DebugLog($"Transition to {sceneName} complete!");
    }

    IEnumerator TransitionOut()
    {
        DebugLog($"Starting {transitionType} transition out");

        switch (transitionType)
        {
            case TransitionType.Fade:
                yield return StartCoroutine(FadeOut());
                break;
            case TransitionType.Slide:
                yield return StartCoroutine(SlideOut());
                break;
            case TransitionType.Circle:
                yield return StartCoroutine(CircleOut());
                break;
            case TransitionType.Iris:
                yield return StartCoroutine(IrisOut());
                break;
            case TransitionType.Pixelate:
                yield return StartCoroutine(PixelateOut());
                break;
        }
    }

    IEnumerator TransitionIn()
    {
        DebugLog($"Starting {transitionType} transition in");

        switch (transitionType)
        {
            case TransitionType.Fade:
                yield return StartCoroutine(FadeIn());
                break;
            case TransitionType.Slide:
                yield return StartCoroutine(SlideIn());
                break;
            case TransitionType.Circle:
                yield return StartCoroutine(CircleIn());
                break;
            case TransitionType.Iris:
                yield return StartCoroutine(IrisIn());
                break;
            case TransitionType.Pixelate:
                yield return StartCoroutine(PixelateIn());
                break;
        }
    }

    // Fade Transitions
    IEnumerator FadeOut()
    {
        float elapsedTime = 0f;

        while (elapsedTime < transitionDuration)
        {
            elapsedTime += Time.deltaTime;
            float alpha = Mathf.Clamp01(elapsedTime / transitionDuration);
            SetImageAlpha(alpha);
            yield return null;
        }

        SetImageAlpha(1f);
    }

    IEnumerator FadeIn()
    {
        float elapsedTime = 0f;

        while (elapsedTime < transitionDuration)
        {
            elapsedTime += Time.deltaTime;
            float alpha = 1f - Mathf.Clamp01(elapsedTime / transitionDuration);
            SetImageAlpha(alpha);
            yield return null;
        }

        SetImageAlpha(0f);
    }

    // Slide Transitions
    IEnumerator SlideOut()
    {
        RectTransform rect = fadeImage.rectTransform;
        Vector2 startPos = new Vector2(-Screen.width, 0);
        Vector2 endPos = Vector2.zero;

        rect.anchoredPosition = startPos;
        SetImageAlpha(1f);

        float elapsedTime = 0f;

        while (elapsedTime < transitionDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / transitionDuration);
            t = Mathf.SmoothStep(0f, 1f, t); // Smooth easing

            rect.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            yield return null;
        }

        rect.anchoredPosition = endPos;
    }

    IEnumerator SlideIn()
    {
        RectTransform rect = fadeImage.rectTransform;
        Vector2 startPos = Vector2.zero;
        Vector2 endPos = new Vector2(Screen.width, 0);

        float elapsedTime = 0f;

        while (elapsedTime < transitionDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / transitionDuration);
            t = Mathf.SmoothStep(0f, 1f, t);

            rect.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            yield return null;
        }

        rect.anchoredPosition = endPos;
        SetImageAlpha(0f);
    }

    // Circle Transitions (requires shader or mask)
    IEnumerator CircleOut()
    {
        // Fallback to fade if no special shader
        yield return StartCoroutine(FadeOut());
    }

    IEnumerator CircleIn()
    {
        // Fallback to fade if no special shader
        yield return StartCoroutine(FadeIn());
    }

    // Iris Transitions
    IEnumerator IrisOut()
    {
        // Fallback to fade if no special shader
        yield return StartCoroutine(FadeOut());
    }

    IEnumerator IrisIn()
    {
        // Fallback to fade if no special shader
        yield return StartCoroutine(FadeIn());
    }

    // Pixelate Transitions
    IEnumerator PixelateOut()
    {
        // Fallback to fade if no special shader
        yield return StartCoroutine(FadeOut());
    }

    IEnumerator PixelateIn()
    {
        // Fallback to fade if no special shader
        yield return StartCoroutine(FadeIn());
    }

    void SetImageAlpha(float alpha)
    {
        if (fadeImage != null)
        {
            Color color = fadeImage.color;
            color.a = alpha;
            fadeImage.color = color;
        }
    }

    // Canvas visibility management
    void ShowTransitionCanvas()
    {
        if (transitionCanvas != null)
        {
            transitionCanvas.gameObject.SetActive(true);
            DebugLog("Transition canvas shown");
        }
    }

    void HideTransitionCanvas()
    {
        if (transitionCanvas != null)
        {
            transitionCanvas.gameObject.SetActive(false);
            DebugLog("Transition canvas hidden");
        }
    }

    // Public utility methods
    public bool IsTransitioning()
    {
        return isTransitioning;
    }

    public void SetTransitionType(TransitionType type)
    {
        transitionType = type;
    }

    public void SetTransitionDuration(float duration)
    {
        transitionDuration = Mathf.Max(0.1f, duration);
    }

    public void SetFadeColor(Color color)
    {
        fadeColor = color;
        if (fadeImage != null)
        {
            Color currentColor = fadeImage.color;
            currentColor.r = color.r;
            currentColor.g = color.g;
            currentColor.b = color.b;
            fadeImage.color = currentColor;
        }
    }

    // Method to manually show/hide transition canvas (for testing)
    public void ShowCanvas()
    {
        ShowTransitionCanvas();
    }

    public void HideCanvas()
    {
        HideTransitionCanvas();
    }

    void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[LevelTransition] {message}");
        }
    }

    // Called when the object is destroyed
    void OnDestroy()
    {
        if (currentTransition != null)
        {
            StopCoroutine(currentTransition);
        }
    }
}