using UnityEngine;
using UnityEngine.SceneManagement;

public class GameSceneManager : MonoBehaviour
{
    public static GameSceneManager Instance { get; private set; }

    [Header("Scene Names")]
    [SerializeField] private string mainMenuScene = "MainMenu";
    [SerializeField] private string characterSelectScene = "CharacterSelect";
    [SerializeField] private string firstLevelScene = "Level1";
    
    [Header("Loading Screen")]
    [SerializeField] private GameObject loadingScreenPrefab;
    [SerializeField] private float minLoadTime = 1.5f;
    
    private GameObject currentLoadingScreen;
    private AsyncOperation currentLoadOperation;

    void Awake()
    {
        InitializeSingleton();
    }

    private void InitializeSingleton()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // ================== PUBLIC SCENE LOADING METHODS ==================
    
    public void LoadMainMenu()
    {
        DestroyCharacterSelectionManager();
        LoadScene(mainMenuScene);
    }

    public void LoadCharacterSelection()
    {
        EnsureCharacterSelectionManager();
        LoadScene(characterSelectScene);
    }

    public void StartGame()
    {
        if (!CharacterSelectionManager.Instance)
        {
            Debug.LogWarning("No character selections found! Creating default selections.");
            CreateDefaultSelections();
        }
        
        LoadScene(firstLevelScene);
    }

    public void RestartCurrentLevel()
    {
        LoadScene(SceneManager.GetActiveScene().name);
    }

    public void LoadNextLevel()
    {
        // Get the current level number
        string currentScene = SceneManager.GetActiveScene().name;
        if (currentScene.StartsWith("Level"))
        {
            if (int.TryParse(currentScene.Substring(5), out int levelNum))
            {
                LoadScene($"Level{levelNum + 1}");
                return;
            }
        }
        
        // Fallback to first level if we can't determine next
        Debug.LogWarning("Couldn't determine next level. Loading first level.");
        LoadScene(firstLevelScene);
    }

    public void QuitGame()
    {
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }

    // ================== LOADING IMPLEMENTATION ==================
    
    private void LoadScene(string sceneName)
    {
        // Create loading screen
        ShowLoadingScreen();
        
        // Start async loading
        currentLoadOperation = SceneManager.LoadSceneAsync(sceneName);
        currentLoadOperation.allowSceneActivation = false;
        
        // Wait for minimum load time
        StartCoroutine(CompleteSceneLoad(sceneName));
    }

    private System.Collections.IEnumerator CompleteSceneLoad(string sceneName)
    {
        float loadStartTime = Time.time;
        float loadProgress = 0f;
        
        // Track loading progress
        while (!currentLoadOperation.isDone || (Time.time - loadStartTime) < minLoadTime)
        {
            loadProgress = Mathf.Clamp01(currentLoadOperation.progress / 0.9f);
            UpdateLoadingScreen(loadProgress);
            
            if (loadProgress >= 0.9f && (Time.time - loadStartTime) >= minLoadTime)
            {
                currentLoadOperation.allowSceneActivation = true;
            }
            
            yield return null;
        }
    }

    // ================== LOADING SCREEN UI ==================
    
    private void ShowLoadingScreen()
    {
        if (loadingScreenPrefab && !currentLoadingScreen)
        {
            currentLoadingScreen = Instantiate(loadingScreenPrefab);
            DontDestroyOnLoad(currentLoadingScreen);
        }
    }

    private void UpdateLoadingScreen(float progress)
    {
        if (!currentLoadingScreen) return;
        
        // Update loading bar if available
        var progressBar = currentLoadingScreen.GetComponentInChildren<UnityEngine.UI.Slider>();
        if (progressBar) progressBar.value = progress;
        
        // Update progress text
        var progressText = currentLoadingScreen.GetComponentInChildren<TMPro.TextMeshProUGUI>();
        if (progressText) progressText.text = $"LOADING... {(int)(progress * 100)}%";
    }

    private void HideLoadingScreen()
    {
        if (currentLoadingScreen)
        {
            Destroy(currentLoadingScreen);
            currentLoadingScreen = null;
        }
    }

    // ================== CHARACTER SELECTION MANAGEMENT ==================
    
    private void EnsureCharacterSelectionManager()
    {
        if (!CharacterSelectionManager.Instance)
        {
            GameObject managerObj = new GameObject("CharacterSelectionManager");
            managerObj.AddComponent<CharacterSelectionManager>();
        }
    }

    private void DestroyCharacterSelectionManager()
    {
        if (CharacterSelectionManager.Instance)
        {
            Destroy(CharacterSelectionManager.Instance.gameObject);
        }
    }

    private void CreateDefaultSelections()
    {
        GameObject managerObj = new GameObject("CharacterSelectionManager");
        var manager = managerObj.AddComponent<CharacterSelectionManager>();
        manager.SetPlayerSelection(0, CharacterSelectionManager.CharacterType.Melee);
        manager.SetPlayerSelection(1, CharacterSelectionManager.CharacterType.Gunner);
    }

    // ================== EVENT HANDLERS ==================
    
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"Scene loaded: {scene.name}");
        HideLoadingScreen();
        
        // Special initialization for specific scenes
        if (scene.name == characterSelectScene)
        {
            InitializeCharacterSelectScene();
        }
        else if (scene.name == firstLevelScene)
        {
            InitializeGameScene();
        }
    }

    private void InitializeCharacterSelectScene()
    {
        
    }

    private void InitializeGameScene()
    {
        // Ensure players are spawned
        var spawner = FindObjectOfType<PlayerSpawner>();
        if (spawner) spawner.SpawnPlayers();
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }
}