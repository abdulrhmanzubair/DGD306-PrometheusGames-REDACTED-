using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using System.Collections;

public class ComicCutsceneManager : MonoBehaviour
{
    [Header("Comic Pages")]
    public Sprite[] comicPages; // Array to hold your PNG comic pages

    [Header("UI Elements")]
    public Image pageDisplay; // The main image component that shows the current page
    public Button nextButton;
    public Button previousButton;
    public Button skipButton;
    public Button startLevelButton;

    [Header("Scene Settings")]
    public string level1SceneName = "Level1"; // Name of your Level 1 scene
    public string mainMenuSceneName = "MainMenu"; // Name of your Main Menu scene

    [Header("Animation Settings")]
    public float pageTransitionTime = 0.3f; // Time for fade in/out
    public AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Input Settings")]
    public bool acceptAnyPlayerInput = true; // Accept input from any connected controller
    public bool showInputHints = true; // Show control hints on screen

    [Header("UI Settings")]
    public bool autoCreateUI = true;
    public Vector2 canvasSize = new Vector2(1920, 1080);
    public int uiFontSize = 24;
    public Font customFont; // Optional custom font

    [Header("Page Display Settings")]
    [Tooltip("Page size as percentage of screen (0.1 = 10%, 1.0 = 100%)")]
    public Vector2 pageSize = new Vector2(0.8f, 0.7f); // Width and height as screen percentage

    [Tooltip("Page position offset from center (-0.5 to 0.5)")]
    public Vector2 pagePosition = new Vector2(0f, 0.05f); // X and Y offset from center

    [Tooltip("Page margins from screen edges (0.0 to 0.5)")]
    public Vector4 pageMargins = new Vector4(0.1f, 0.15f, 0.1f, 0.15f); // Left, Top, Right, Bottom

    [Tooltip("Preserve aspect ratio of comic pages")]
    public bool preserveAspectRatio = true;

    [Tooltip("Page background color")]
    public Color pageBackgroundColor = Color.clear;

    [Tooltip("Page border settings")]
    public bool showPageBorder = false;
    public Color pageBorderColor = Color.white;
    public float pageBorderWidth = 2f;

    [Header("Page Layout Presets")]
    [Tooltip("Quick preset layouts")]
    public PageLayoutPreset layoutPreset = PageLayoutPreset.Custom;

    public enum PageLayoutPreset
    {
        Custom,
        Fullscreen,
        Standard,
        Widescreen,
        Portrait,
        Small,
        Large
    }

    [Header("Audio (Optional)")]
    public AudioSource audioSource;
    public AudioClip pageFlipSound;
    public AudioClip buttonClickSound;

    private int currentPageIndex = 0;
    private bool isTransitioning = false;

    // UI References (created automatically)
    private Canvas mainCanvas;
    private Text gameStatusText;
    private GameObject matchEndPanel;
    private Text matchWinnerText;

    void Start()
    {
        // Validate page settings
        ValidatePageSettings();

        if (autoCreateUI)
        {
            CreateUI();
        }
        InitializeCutscene();
    }

    void ValidatePageSettings()
    {
        // Clamp page size to valid range
        pageSize.x = Mathf.Clamp(pageSize.x, 0.1f, 1f);
        pageSize.y = Mathf.Clamp(pageSize.y, 0.1f, 1f);

        // Clamp page position to reasonable range
        pagePosition.x = Mathf.Clamp(pagePosition.x, -0.5f, 0.5f);
        pagePosition.y = Mathf.Clamp(pagePosition.y, -0.5f, 0.5f);

        // Clamp margins to valid range
        pageMargins.x = Mathf.Clamp(pageMargins.x, 0f, 0.5f); // Left
        pageMargins.y = Mathf.Clamp(pageMargins.y, 0f, 0.5f); // Top
        pageMargins.z = Mathf.Clamp(pageMargins.z, 0f, 0.5f); // Right
        pageMargins.w = Mathf.Clamp(pageMargins.w, 0f, 0.5f); // Bottom

        // Ensure margins don't exceed 100% of screen
        float totalHorizontalMargin = pageMargins.x + pageMargins.z;
        float totalVerticalMargin = pageMargins.y + pageMargins.w;

        if (totalHorizontalMargin >= 1f)
        {
            float scale = 0.9f / totalHorizontalMargin;
            pageMargins.x *= scale;
            pageMargins.z *= scale;
            Debug.LogWarning("Horizontal margins too large, auto-scaled to fit");
        }

        if (totalVerticalMargin >= 1f)
        {
            float scale = 0.9f / totalVerticalMargin;
            pageMargins.y *= scale;
            pageMargins.w *= scale;
            Debug.LogWarning("Vertical margins too large, auto-scaled to fit");
        }
    }

    void CreateUI()
    {
        // Create or find Canvas
        mainCanvas = FindObjectOfType<Canvas>();
        if (mainCanvas == null)
        {
            GameObject canvasGO = new GameObject("Comic Canvas");
            mainCanvas = canvasGO.AddComponent<Canvas>();
            mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // Add Canvas Scaler
            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = canvasSize;
            scaler.matchWidthOrHeight = 0.5f;

            // Add GraphicRaycaster
            canvasGO.AddComponent<GraphicRaycaster>();
        }

        // Create EventSystem if it doesn't exist
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject eventSystemGO = new GameObject("EventSystem");
            eventSystemGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystemGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        CreatePageDisplay();
        CreateNavigationButtons();

        if (showInputHints)
        {
            CreateInputHints();
        }
    }

    void CreatePageDisplay()
    {
        // Main page display with customizable size and position
        GameObject pageGO = new GameObject("PageDisplay");
        pageGO.transform.SetParent(mainCanvas.transform, false);

        RectTransform pageRect = pageGO.AddComponent<RectTransform>();

        // Calculate anchors based on page size and margins
        float leftMargin = pageMargins.x;   // Left
        float topMargin = pageMargins.y;    // Top  
        float rightMargin = pageMargins.z;  // Right
        float bottomMargin = pageMargins.w; // Bottom

        // Set anchors using margins
        pageRect.anchorMin = new Vector2(leftMargin, bottomMargin);
        pageRect.anchorMax = new Vector2(1f - rightMargin, 1f - topMargin);

        // Apply page size scaling
        Vector2 currentSize = pageRect.sizeDelta;
        pageRect.sizeDelta = new Vector2(
            currentSize.x * pageSize.x,
            currentSize.y * pageSize.y
        );

        // Apply position offset
        pageRect.anchoredPosition = new Vector2(
            pagePosition.x * canvasSize.x,
            pagePosition.y * canvasSize.y
        );

        // Set margins to zero since we're using anchors
        pageRect.offsetMin = Vector2.zero;
        pageRect.offsetMax = Vector2.zero;

        // Add Image component
        pageDisplay = pageGO.AddComponent<Image>();
        pageDisplay.preserveAspect = preserveAspectRatio;
        pageDisplay.color = pageBackgroundColor;

        // Add border if enabled
        if (showPageBorder)
        {
            CreatePageBorder(pageGO);
        }

        Debug.Log($"Page Display created - Size: {pageSize}, Position: {pagePosition}, Margins: {pageMargins}");
    }

    void CreatePageBorder(GameObject pageParent)
    {
        // Create border outline
        GameObject borderGO = new GameObject("PageBorder");
        borderGO.transform.SetParent(pageParent.transform, false);

        RectTransform borderRect = borderGO.AddComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.offsetMin = Vector2.zero;
        borderRect.offsetMax = Vector2.zero;

        // Add Outline component for border effect
        var outline = borderGO.AddComponent<UnityEngine.UI.Outline>();
        outline.effectColor = pageBorderColor;
        outline.effectDistance = new Vector2(pageBorderWidth, pageBorderWidth);

        // Alternative: Create border using UI Image if Outline doesn't work well
        Image borderImage = borderGO.AddComponent<Image>();
        borderImage.color = Color.clear; // Transparent fill
        borderImage.raycastTarget = false; // Don't block input
    }

    void CreateNavigationButtons()
    {
        // Previous Button (Left side)
        previousButton = CreateButton("PreviousButton", "◀ PREV", mainCanvas.transform,
            new Vector2(-650, -400), new Vector2(120, 50));
        previousButton.onClick.AddListener(PreviousPage);

        // Next Button (Right side)
        nextButton = CreateButton("NextButton", "NEXT ▶", mainCanvas.transform,
            new Vector2(650, -400), new Vector2(120, 50));
        nextButton.onClick.AddListener(NextPage);

        // Skip Button (Top right)
        skipButton = CreateButton("SkipButton", "SKIP", mainCanvas.transform,
            new Vector2(650, 400), new Vector2(100, 40));
        skipButton.onClick.AddListener(SkipCutscene);

        // Main Menu Button (Top left)
        Button mainMenuButton = CreateButton("MainMenuButton", "MAIN MENU", mainCanvas.transform,
            new Vector2(-650, 400), new Vector2(120, 40));
        mainMenuButton.onClick.AddListener(GoToMainMenu);

        // Enhance main menu button appearance
        ColorBlock mainMenuColors = mainMenuButton.colors;
        mainMenuColors.normalColor = new Color(0.8f, 0.2f, 0.2f, 0.8f); // Red
        mainMenuColors.highlightedColor = new Color(0.9f, 0.3f, 0.3f, 0.9f);
        mainMenuButton.colors = mainMenuColors;

        // Start Level Button (Center bottom, initially hidden)
        startLevelButton = CreateButton("StartLevelButton", "START GAME", mainCanvas.transform,
            new Vector2(0, -400), new Vector2(200, 60));
        startLevelButton.onClick.AddListener(StartLevel1);
        startLevelButton.gameObject.SetActive(false);
    }

    void CreateInputHints()
    {
        // Input hints panel (bottom of screen)
        GameObject hintsPanel = CreatePanel("InputHints", new Vector2(800, 80),
            new Vector2(0, -450), mainCanvas.transform);
        hintsPanel.GetComponent<Image>().color = new Color(0, 0, 0, 0.7f);

        // Gamepad hints
        Text gamepadHints = CreateText("GamepadHints", "🎮 A: Next | B/→: Next | L1/←: Prev | Y: Skip | Select/L2: Menu",
            hintsPanel.transform, new Vector2(0, 15), uiFontSize - 4);
        gamepadHints.color = Color.cyan;
        gamepadHints.alignment = TextAnchor.MiddleCenter;

        // Keyboard hints  
        Text keyboardHints = CreateText("KeyboardHints", "⌨️ Space: Next | Arrows/WASD: Navigate | Esc: Skip | Backspace/F1: Menu",
            hintsPanel.transform, new Vector2(0, -15), uiFontSize - 4);
        keyboardHints.color = Color.yellow;
        keyboardHints.alignment = TextAnchor.MiddleCenter;
    }

    GameObject CreatePanel(string name, Vector2 size, Vector2 position, Transform parent)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent, false);

        RectTransform rect = panel.AddComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        Image image = panel.AddComponent<Image>();
        image.color = new Color(0, 0, 0, 0.5f);

        return panel;
    }

    Text CreateText(string name, string content, Transform parent, Vector2 position,
        int fontSize = 24, FontStyle fontStyle = FontStyle.Normal)
    {
        GameObject textGO = new GameObject(name);
        textGO.transform.SetParent(parent, false);

        RectTransform rect = textGO.AddComponent<RectTransform>();
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(200, 30);

        Text text = textGO.AddComponent<Text>();
        text.text = content;
        text.font = customFont ? customFont : Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;

        return text;
    }

    Button CreateButton(string name, string buttonText, Transform parent, Vector2 position, Vector2 size)
    {
        GameObject buttonGO = new GameObject(name);
        buttonGO.transform.SetParent(parent, false);

        RectTransform rect = buttonGO.AddComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        Image image = buttonGO.AddComponent<Image>();
        image.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

        Button button = buttonGO.AddComponent<Button>();

        // Button text
        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(buttonGO.transform, false);

        RectTransform textRect = textGO.AddComponent<RectTransform>();
        textRect.sizeDelta = size;
        textRect.anchoredPosition = Vector2.zero;

        Text text = textGO.AddComponent<Text>();
        text.text = buttonText;
        text.font = customFont ? customFont : Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = uiFontSize;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;

        return button;
    }

    void InitializeCutscene()
    {
        // Apply layout preset if not custom
        if (layoutPreset != PageLayoutPreset.Custom)
        {
            ApplyLayoutPreset(layoutPreset);
        }

        // Ensure we have pages to display
        if (comicPages.Length == 0)
        {
            Debug.LogError("No comic pages assigned!");
            return;
        }

        // Set up initial page
        currentPageIndex = 0;
        pageDisplay.sprite = comicPages[currentPageIndex];

        // Update button states
        UpdateButtonStates();

        // Debug: Log available input devices
        Debug.Log($"Comic Cutscene: {Gamepad.all.Count} gamepads connected");
        Debug.Log("Input Controls: Any gamepad or keyboard can navigate the cutscene");
        Debug.Log($"Page Layout: {layoutPreset} - Size: {pageSize}, Position: {pagePosition}");
    }

    // Apply preset page layouts
    void ApplyLayoutPreset(PageLayoutPreset preset)
    {
        switch (preset)
        {
            case PageLayoutPreset.Fullscreen:
                pageSize = new Vector2(1f, 1f);
                pagePosition = Vector2.zero;
                pageMargins = Vector4.zero;
                break;

            case PageLayoutPreset.Standard:
                pageSize = new Vector2(0.8f, 0.7f);
                pagePosition = new Vector2(0f, 0.05f);
                pageMargins = new Vector4(0.1f, 0.15f, 0.1f, 0.15f);
                break;

            case PageLayoutPreset.Widescreen:
                pageSize = new Vector2(0.9f, 0.6f);
                pagePosition = new Vector2(0f, 0.1f);
                pageMargins = new Vector4(0.05f, 0.2f, 0.05f, 0.2f);
                break;

            case PageLayoutPreset.Portrait:
                pageSize = new Vector2(0.6f, 0.85f);
                pagePosition = Vector2.zero;
                pageMargins = new Vector4(0.2f, 0.075f, 0.2f, 0.075f);
                break;

            case PageLayoutPreset.Small:
                pageSize = new Vector2(0.6f, 0.5f);
                pagePosition = new Vector2(0f, 0.1f);
                pageMargins = new Vector4(0.2f, 0.25f, 0.2f, 0.25f);
                break;

            case PageLayoutPreset.Large:
                pageSize = new Vector2(0.95f, 0.85f);
                pagePosition = Vector2.zero;
                pageMargins = new Vector4(0.025f, 0.075f, 0.025f, 0.075f);
                break;

            case PageLayoutPreset.Custom:
            default:
                // Keep current settings
                break;
        }

        // Update the page display if it exists
        if (pageDisplay != null)
        {
            UpdatePageDisplayLayout();
        }
    }

    // Update page display layout with current settings
    public void UpdatePageDisplayLayout()
    {
        if (pageDisplay == null) return;

        RectTransform pageRect = pageDisplay.GetComponent<RectTransform>();
        if (pageRect == null) return;

        // Calculate anchors based on page size and margins
        float leftMargin = pageMargins.x;
        float topMargin = pageMargins.y;
        float rightMargin = pageMargins.z;
        float bottomMargin = pageMargins.w;

        // Set anchors using margins
        pageRect.anchorMin = new Vector2(leftMargin, bottomMargin);
        pageRect.anchorMax = new Vector2(1f - rightMargin, 1f - topMargin);

        // Apply position offset
        pageRect.anchoredPosition = new Vector2(
            pagePosition.x * canvasSize.x,
            pagePosition.y * canvasSize.y
        );

        // Update display properties
        pageDisplay.preserveAspect = preserveAspectRatio;
        pageDisplay.color = pageBackgroundColor;

        Debug.Log($"Page layout updated - Preset: {layoutPreset}, Size: {pageSize}, Position: {pagePosition}");
    }

    // Context menu methods for easy testing in editor
    [ContextMenu("Apply Current Preset")]
    void ApplyCurrentPreset()
    {
        ApplyLayoutPreset(layoutPreset);
        Debug.Log($"Applied preset: {layoutPreset}");
    }

    [ContextMenu("Reset to Standard Layout")]
    void ResetToStandardLayout()
    {
        layoutPreset = PageLayoutPreset.Standard;
        ApplyLayoutPreset(layoutPreset);
    }

    [ContextMenu("Test Fullscreen Layout")]
    void TestFullscreenLayout()
    {
        layoutPreset = PageLayoutPreset.Fullscreen;
        ApplyLayoutPreset(layoutPreset);
    }

    [ContextMenu("Update Page Layout")]
    void UpdatePageLayoutFromInspector()
    {
        UpdatePageDisplayLayout();
    }

    void Update()
    {
        HandleInput();
    }

    void HandleInput()
    {
        if (isTransitioning) return;

        // Handle input from any connected gamepad or keyboard
        bool nextPressed = false;
        bool previousPressed = false;
        bool skipPressed = false;
        bool confirmPressed = false;
        bool mainMenuPressed = false;

        // Check all connected gamepads
        foreach (var gamepad in Gamepad.all)
        {
            // Navigation - Next
            if (gamepad.rightShoulder.wasPressedThisFrame ||
                gamepad.buttonEast.wasPressedThisFrame ||
                gamepad.dpad.right.wasPressedThisFrame)
            {
                nextPressed = true;
            }

            // Navigation - Previous  
            if (gamepad.leftShoulder.wasPressedThisFrame ||
                gamepad.dpad.left.wasPressedThisFrame)
            {
                previousPressed = true;
            }

            // Skip
            if (gamepad.buttonNorth.wasPressedThisFrame ||
                gamepad.startButton.wasPressedThisFrame)
            {
                skipPressed = true;
            }

            // Main Menu
            if (gamepad.selectButton.wasPressedThisFrame ||
                gamepad.leftTrigger.wasPressedThisFrame)
            {
                mainMenuPressed = true;
            }

            // Confirm/Next
            if (gamepad.buttonSouth.wasPressedThisFrame ||
                gamepad.rightTrigger.wasPressedThisFrame)
            {
                confirmPressed = true;
            }
        }

        // Check keyboard input
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            // Navigation keys
            if (keyboard.rightArrowKey.wasPressedThisFrame ||
                keyboard.dKey.wasPressedThisFrame ||
                keyboard.pageDownKey.wasPressedThisFrame)
            {
                nextPressed = true;
            }

            if (keyboard.leftArrowKey.wasPressedThisFrame ||
                keyboard.aKey.wasPressedThisFrame ||
                keyboard.pageUpKey.wasPressedThisFrame)
            {
                previousPressed = true;
            }

            // Skip keys
            if (keyboard.escapeKey.wasPressedThisFrame ||
                keyboard.tabKey.wasPressedThisFrame)
            {
                skipPressed = true;
            }

            // Main Menu keys
            if (keyboard.backspaceKey.wasPressedThisFrame ||
                keyboard.f1Key.wasPressedThisFrame)
            {
                mainMenuPressed = true;
            }

            // Confirm keys
            if (keyboard.spaceKey.wasPressedThisFrame ||
                keyboard.enterKey.wasPressedThisFrame)
            {
                confirmPressed = true;
            }
        }

        // Process input
        if (nextPressed || confirmPressed)
        {
            if (currentPageIndex >= comicPages.Length - 1)
            {
                StartLevel1();
            }
            else
            {
                NextPage();
            }
        }
        else if (previousPressed)
        {
            PreviousPage();
        }
        else if (skipPressed)
        {
            SkipCutscene();
        }
        else if (mainMenuPressed)
        {
            GoToMainMenu();
        }
    }

    public void NextPage()
    {
        if (isTransitioning) return;

        PlayPageFlipSound(); // Add audio feedback

        if (currentPageIndex < comicPages.Length - 1)
        {
            StartCoroutine(TransitionToPage(currentPageIndex + 1));
        }
        else
        {
            // Last page reached, show start level button
            ShowStartLevelButton();
        }
    }

    public void PreviousPage()
    {
        if (isTransitioning) return;

        PlayPageFlipSound(); // Add audio feedback

        if (currentPageIndex > 0)
        {
            StartCoroutine(TransitionToPage(currentPageIndex - 1));
        }
    }

    public void SkipCutscene()
    {
        // Go directly to the last page and show start level button
        if (!isTransitioning)
        {
            PlayButtonClickSound(); // Add audio feedback
            StartCoroutine(TransitionToPage(comicPages.Length - 1, true));
        }
    }

    public void StartLevel1()
    {
        PlayButtonClickSound(); // Add audio feedback
        StartCoroutine(LoadLevel1());
    }

    public void GoToMainMenu()
    {
        PlayButtonClickSound(); // Add audio feedback
        StartCoroutine(LoadMainMenu());
    }

    IEnumerator TransitionToPage(int newPageIndex, bool isSkipping = false)
    {
        isTransitioning = true;

        // Fade out current page
        yield return StartCoroutine(FadePage(false));

        // Change page
        currentPageIndex = newPageIndex;
        pageDisplay.sprite = comicPages[currentPageIndex];

        // Update button states
        UpdateButtonStates();

        // If this is the last page or we're skipping, show start level button
        if (currentPageIndex >= comicPages.Length - 1 || isSkipping)
        {
            ShowStartLevelButton();
        }

        // Fade in new page
        yield return StartCoroutine(FadePage(true));

        isTransitioning = false;
    }

    IEnumerator FadePage(bool fadeIn)
    {
        float startAlpha = fadeIn ? 0f : 1f;
        float endAlpha = fadeIn ? 1f : 0f;
        float elapsed = 0f;

        Color pageColor = pageDisplay.color;

        while (elapsed < pageTransitionTime)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = elapsed / pageTransitionTime;
            float curveValue = fadeCurve.Evaluate(normalizedTime);

            pageColor.a = Mathf.Lerp(startAlpha, endAlpha, curveValue);
            pageDisplay.color = pageColor;

            yield return null;
        }

        pageColor.a = endAlpha;
        pageDisplay.color = pageColor;
    }

    void UpdateButtonStates()
    {
        // Update previous button
        if (previousButton != null)
            previousButton.interactable = currentPageIndex > 0;

        // Update next button
        if (nextButton != null)
            nextButton.interactable = currentPageIndex < comicPages.Length - 1;

        // Hide start level button if not on last page
        if (startLevelButton != null && currentPageIndex < comicPages.Length - 1)
        {
            startLevelButton.gameObject.SetActive(false);
        }
    }

    void ShowStartLevelButton()
    {
        if (startLevelButton != null)
        {
            startLevelButton.gameObject.SetActive(true);
            // Optional: Animate the button appearance
            StartCoroutine(AnimateButtonAppearance());
        }
    }

    IEnumerator AnimateButtonAppearance()
    {
        startLevelButton.transform.localScale = Vector3.zero;

        float elapsed = 0f;
        float animTime = 0.5f;

        while (elapsed < animTime)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = elapsed / animTime;
            float scale = fadeCurve.Evaluate(normalizedTime);

            startLevelButton.transform.localScale = Vector3.one * scale;
            yield return null;
        }

        startLevelButton.transform.localScale = Vector3.one;
    }

    IEnumerator LoadLevel1()
    {
        // Optional: Add a fade to black transition
        yield return StartCoroutine(FadeToBlack());

        // Load the level
        SceneManager.LoadScene(level1SceneName);
    }

    IEnumerator LoadMainMenu()
    {
        // Optional: Add a fade to black transition
        yield return StartCoroutine(FadeToBlack());

        // Try to load main menu scene
        try
        {
            // Try configured main menu scene name first
            if (!string.IsNullOrEmpty(mainMenuSceneName) && Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
            {
                SceneManager.LoadScene(mainMenuSceneName);
            }
            // Common main menu scene names
            else if (Application.CanStreamedLevelBeLoaded("MainMenu"))
            {
                SceneManager.LoadScene("MainMenu");
            }
            else if (Application.CanStreamedLevelBeLoaded("Main"))
            {
                SceneManager.LoadScene("Main");
            }
            else if (Application.CanStreamedLevelBeLoaded("Menu"))
            {
                SceneManager.LoadScene("Menu");
            }
            else
            {
                // Fallback: Load first scene in build settings
                SceneManager.LoadScene(0);
                Debug.Log("Loaded first scene as main menu fallback");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Could not load main menu scene: {e.Message}");
            Debug.LogWarning("Add your main menu scene to Build Settings or set the correct scene name.");

            // Alternative: Load first scene as fallback
            SceneManager.LoadScene(0);
        }
    }

    IEnumerator FadeToBlack()
    {
        // You can implement a screen fade here if you have a fade panel
        // For now, just a simple delay
        yield return new WaitForSeconds(0.5f);
    }

    void PlayPageFlipSound()
    {
        if (audioSource && pageFlipSound)
        {
            audioSource.PlayOneShot(pageFlipSound);
        }
    }

    void PlayButtonClickSound()
    {
        if (audioSource && buttonClickSound)
        {
            audioSource.PlayOneShot(buttonClickSound);
        }
    }

    // Debug: Show input device status
    void OnGUI()
    {
        if (!Application.isEditor) return;

        GUI.Label(new Rect(10, 10, 400, 20), $"Comic Cutscene - Connected Gamepads: {Gamepad.all.Count}");
        GUI.Label(new Rect(10, 30, 400, 20), $"Current Page: {currentPageIndex + 1}/{comicPages.Length}");
        GUI.Label(new Rect(10, 50, 400, 20), $"Accept Any Input: {acceptAnyPlayerInput}");
        GUI.Label(new Rect(10, 70, 400, 20), $"Layout Preset: {layoutPreset}");
        GUI.Label(new Rect(10, 90, 400, 20), $"Page Size: {pageSize.x:F2} x {pageSize.y:F2}");
        GUI.Label(new Rect(10, 110, 400, 20), $"Page Position: {pagePosition.x:F2}, {pagePosition.y:F2}");
        GUI.Label(new Rect(10, 130, 400, 20), $"Preserve Aspect: {preserveAspectRatio}");

        if (Gamepad.all.Count > 0)
        {
            for (int i = 0; i < Mathf.Min(Gamepad.all.Count, 2); i++)
            {
                var gamepad = Gamepad.all[i];
                GUI.Label(new Rect(10, 150 + i * 20, 400, 20), $"Gamepad {i}: {gamepad.displayName}");
            }
        }
        else
        {
            GUI.Label(new Rect(10, 150, 400, 20), "No gamepads detected - using keyboard only");
        }

        // Quick preset buttons for testing
        if (GUI.Button(new Rect(420, 10, 100, 25), "Fullscreen"))
        {
            layoutPreset = PageLayoutPreset.Fullscreen;
            ApplyLayoutPreset(layoutPreset);
        }
        if (GUI.Button(new Rect(420, 40, 100, 25), "Standard"))
        {
            layoutPreset = PageLayoutPreset.Standard;
            ApplyLayoutPreset(layoutPreset);
        }
        if (GUI.Button(new Rect(420, 70, 100, 25), "Widescreen"))
        {
            layoutPreset = PageLayoutPreset.Widescreen;
            ApplyLayoutPreset(layoutPreset);
        }
        if (GUI.Button(new Rect(420, 100, 100, 25), "Portrait"))
        {
            layoutPreset = PageLayoutPreset.Portrait;
            ApplyLayoutPreset(layoutPreset);
        }
    }
}