using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MusicClass : MonoBehaviour
{
    [System.Serializable]
    public class SceneMusic
    {
        public string sceneName;
        public AudioClip musicClip;
        [Range(0f, 1f)]
        public float volume = 1f;
    }

    public static MusicClass Instance { get; private set; }

    [Header("Music Settings")]
    [SerializeField] private SceneMusic[] sceneMusicList;
    [SerializeField] private bool fadeTransitions = true;
    [SerializeField] private float fadeTime = 1f;
    [Range(0f, 1f)]
    [SerializeField] private float masterVolume = 1f;

    private AudioSource audioSource;
    private string currentSceneName;
    private bool isFading = false;

    private void Awake()
    {
        // Singleton pattern - prevent duplicates
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeMusic();
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        // Subscribe to scene loaded event
        SceneManager.sceneLoaded += OnSceneLoaded;

        // Play music for current scene
        PlayMusicForCurrentScene();
    }

    private void OnDestroy()
    {
        // Unsubscribe from scene loaded event
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void InitializeMusic()
    {
        // Get or add AudioSource component
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // Configure AudioSource
        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.volume = masterVolume;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        string newSceneName = scene.name;

        // Only change music if we're in a different scene
        if (currentSceneName != newSceneName)
        {
            currentSceneName = newSceneName;
            PlayMusicForScene(newSceneName);
        }
    }

    private void PlayMusicForCurrentScene()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        currentSceneName = sceneName;
        PlayMusicForScene(sceneName);
    }

    private void PlayMusicForScene(string sceneName)
    {
        SceneMusic sceneMusic = GetSceneMusicByName(sceneName);

        if (sceneMusic != null && sceneMusic.musicClip != null)
        {
            PlayTrack(sceneMusic.musicClip, sceneMusic.volume);
        }
        else
        {
            // No music assigned for this scene - stop current music
            StopMusic();
        }
    }

    private SceneMusic GetSceneMusicByName(string sceneName)
    {
        foreach (SceneMusic sceneMusic in sceneMusicList)
        {
            if (sceneMusic.sceneName == sceneName)
            {
                return sceneMusic;
            }
        }
        return null;
    }

    public void PlayTrack(AudioClip clip, float volume = 1f)
    {
        if (clip == null) return;

        // If same clip is already playing, don't restart it
        if (audioSource.clip == clip && audioSource.isPlaying)
        {
            return;
        }

        if (fadeTransitions && audioSource.isPlaying)
        {
            StartCoroutine(FadeToNewTrack(clip, volume * masterVolume));
        }
        else
        {
            audioSource.clip = clip;
            audioSource.volume = volume * masterVolume;
            audioSource.Play();
        }
    }

    public void StopMusic()
    {
        if (fadeTransitions && audioSource.isPlaying)
        {
            StartCoroutine(FadeOut());
        }
        else
        {
            audioSource.Stop();
        }
    }

    public void PauseMusic()
    {
        audioSource.Pause();
    }

    public void ResumeMusic()
    {
        audioSource.UnPause();
    }

    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        audioSource.volume = masterVolume;
    }

    public void SetMusicForScene(string sceneName, AudioClip clip, float volume = 1f)
    {
        // Find existing scene music entry or create new one
        SceneMusic existingEntry = GetSceneMusicByName(sceneName);

        if (existingEntry != null)
        {
            existingEntry.musicClip = clip;
            existingEntry.volume = volume;
        }
        else
        {
            // Add new entry (Note: This won't persist in the inspector)
            System.Array.Resize(ref sceneMusicList, sceneMusicList.Length + 1);
            sceneMusicList[sceneMusicList.Length - 1] = new SceneMusic
            {
                sceneName = sceneName,
                musicClip = clip,
                volume = volume
            };
        }

        // If this is the current scene, play the new music
        if (currentSceneName == sceneName)
        {
            PlayTrack(clip, volume);
        }
    }

    private IEnumerator FadeToNewTrack(AudioClip newClip, float targetVolume)
    {
        if (isFading) yield break;

        isFading = true;
        float startVolume = audioSource.volume;

        // Fade out current track
        yield return StartCoroutine(FadeVolume(startVolume, 0f, fadeTime / 2f));

        // Switch to new track
        audioSource.clip = newClip;
        audioSource.Play();

        // Fade in new track
        yield return StartCoroutine(FadeVolume(0f, targetVolume, fadeTime / 2f));

        isFading = false;
    }

    private IEnumerator FadeOut()
    {
        if (isFading) yield break;

        isFading = true;
        float startVolume = audioSource.volume;

        yield return StartCoroutine(FadeVolume(startVolume, 0f, fadeTime));

        audioSource.Stop();
        isFading = false;
    }

    private IEnumerator FadeVolume(float startVolume, float targetVolume, float duration)
    {
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;
            audioSource.volume = Mathf.Lerp(startVolume, targetVolume, t);
            yield return null;
        }

        audioSource.volume = targetVolume;
    }

    // Public getters for debugging/UI
    public bool IsPlaying => audioSource.isPlaying;
    public bool IsPaused => !audioSource.isPlaying && audioSource.time > 0;
    public AudioClip CurrentClip => audioSource.clip;
    public float CurrentVolume => audioSource.volume;
}