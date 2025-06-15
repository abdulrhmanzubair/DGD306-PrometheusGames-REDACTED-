using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Manages respawn UI including countdown timer and player status
/// </summary>
public class RespawnUIManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject respawnPanel;
    public Text respawnCountdownText;
    public Text respawnMessageText;
    public Image respawnProgressBar;
    public Text livesRemainingText;

    [Header("Co-op UI")]
    public GameObject waitingForRespawnPanel;
    public Text waitingMessageText;
    public Text teammateStatusText;

    [Header("Visual Effects")]
    public Image fadeOverlay;
    public Color respawnFadeColor = new Color(1, 0, 0, 0.3f);
    public AnimationCurve fadeAnimation = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Audio")]
    public AudioClip countdownBeepSound;
    public AudioClip finalBeepSound;
    [Range(0f, 1f)] public float beepVolume = 0.4f;

    private AudioSource audioSource;
    private Coroutine currentRespawnCoroutine;
    private GameLifeManager gameLifeManager;

    void Start()
    {
        // Get references
        gameLifeManager = GameLifeManager.Instance;

        // Set up audio
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        audioSource.volume = beepVolume;

        // Subscribe to events
        if (gameLifeManager != null)
        {
            gameLifeManager.OnPlayerLifeChanged += OnPlayerLifeChanged;
            gameLifeManager.OnPlayerRespawn += OnPlayerRespawned;
            gameLifeManager.OnGameOver += OnGameOver;
        }

        // Initially hide all respawn UI
        HideAllRespawnUI();
    }

    void OnDestroy()
    {
        // Unsubscribe from events
        if (gameLifeManager != null)
        {
            gameLifeManager.OnPlayerLifeChanged -= OnPlayerLifeChanged;
            gameLifeManager.OnPlayerRespawn -= OnPlayerRespawned;
            gameLifeManager.OnGameOver -= OnGameOver;
        }
    }

    void OnPlayerLifeChanged(int playerIndex, int livesRemaining)
    {
        // Update lives display
        UpdateLivesDisplay(playerIndex, livesRemaining);

        // Check if this player died (but still has lives)
        if (livesRemaining > 0)
        {
            StartRespawnSequence(playerIndex);
        }
    }

    void OnPlayerRespawned(int playerIndex)
    {
        // Hide respawn UI for this player
        HideRespawnUI(playerIndex);
    }

    void OnGameOver()
    {
        // Stop any respawn sequences
        if (currentRespawnCoroutine != null)
        {
            StopCoroutine(currentRespawnCoroutine);
        }

        HideAllRespawnUI();
    }

    void StartRespawnSequence(int playerIndex)
    {
        // Stop any existing respawn sequence
        if (currentRespawnCoroutine != null)
        {
            StopCoroutine(currentRespawnCoroutine);
        }

        // Start new respawn sequence
        currentRespawnCoroutine = StartCoroutine(RespawnCountdownSequence(playerIndex));
    }

    IEnumerator RespawnCountdownSequence(int playerIndex)
    {
        float respawnTime = gameLifeManager != null ? gameLifeManager.respawnDelay : 3f;
        bool isSoloMode = gameLifeManager != null && gameLifeManager.IsSoloMode; // Fixed: Use public property

        // Show appropriate UI
        if (isSoloMode)
        {
            ShowSoloRespawnUI(playerIndex);
        }
        else
        {
            ShowCoopRespawnUI(playerIndex);
        }

        // Countdown loop
        float timeRemaining = respawnTime;
        while (timeRemaining > 0)
        {
            // Update countdown text
            UpdateCountdownDisplay(timeRemaining);

            // Update progress bar
            if (respawnProgressBar != null)
            {
                respawnProgressBar.fillAmount = 1f - (timeRemaining / respawnTime);
            }

            // Play beep sounds
            if (timeRemaining <= 3f && timeRemaining > 0.1f)
            {
                float fractionalPart = timeRemaining % 1f;
                if (fractionalPart > 0.9f) // Play beep at each second
                {
                    PlayBeepSound(timeRemaining <= 1f);
                }
            }

            // Update fade overlay
            UpdateFadeOverlay(timeRemaining / respawnTime);

            yield return Time.deltaTime;
            timeRemaining -= Time.deltaTime;
        }

        // Final countdown reached
        UpdateCountdownDisplay(0f);
        if (respawnProgressBar != null)
        {
            respawnProgressBar.fillAmount = 1f;
        }

        // Hide UI after a brief moment
        yield return new WaitForSeconds(0.5f);
        HideRespawnUI(playerIndex);
    }

    void ShowSoloRespawnUI(int playerIndex)
    {
        if (respawnPanel != null)
        {
            respawnPanel.SetActive(true);
        }

        if (respawnMessageText != null)
        {
            respawnMessageText.text = "Respawning at checkpoint...";
        }

        if (waitingForRespawnPanel != null)
        {
            waitingForRespawnPanel.SetActive(false);
        }
    }

    void ShowCoopRespawnUI(int playerIndex)
    {
        if (respawnPanel != null)
        {
            respawnPanel.SetActive(true);
        }

        if (respawnMessageText != null)
        {
            respawnMessageText.text = $"Player {playerIndex + 1} respawning near teammate...";
        }

        if (waitingForRespawnPanel != null)
        {
            waitingForRespawnPanel.SetActive(true);
        }

        if (waitingMessageText != null)
        {
            waitingMessageText.text = $"Player {playerIndex + 1} is down!";
        }

        UpdateTeammateStatus(playerIndex);
    }

    void UpdateTeammateStatus(int deadPlayerIndex)
    {
        if (teammateStatusText == null || gameLifeManager == null) return;

        string statusText = "";

        // Find living teammates - Fixed: Use public property
        var allPlayers = gameLifeManager.AllPlayers;
        foreach (var player in allPlayers)
        {
            if (player.PlayerIndex != deadPlayerIndex)
            {
                PlayerHealthSystem health = player.GetComponent<PlayerHealthSystem>();
                if (health != null && health.IsAlive)
                {
                    statusText += $"Player {player.PlayerIndex + 1}: Alive\n";
                }
                else
                {
                    statusText += $"Player {player.PlayerIndex + 1}: Down\n";
                }
            }
        }

        teammateStatusText.text = statusText.Trim();
    }

    void UpdateCountdownDisplay(float timeRemaining)
    {
        if (respawnCountdownText != null)
        {
            if (timeRemaining <= 0)
            {
                respawnCountdownText.text = "RESPAWNING!";
                respawnCountdownText.color = Color.green;
            }
            else
            {
                respawnCountdownText.text = $"Respawn in: {timeRemaining:F1}";
                respawnCountdownText.color = timeRemaining <= 1f ? Color.red : Color.white;
            }
        }
    }

    void UpdateLivesDisplay(int playerIndex, int livesRemaining)
    {
        if (livesRemainingText != null)
        {
            livesRemainingText.text = $"Lives Remaining: {livesRemaining}";
            livesRemainingText.color = livesRemaining <= 1 ? Color.red : Color.white;
        }
    }

    void UpdateFadeOverlay(float normalizedTime)
    {
        if (fadeOverlay == null) return;

        // Create pulsing fade effect during respawn
        float pulseIntensity = fadeAnimation.Evaluate(normalizedTime);
        Color fadeColor = respawnFadeColor;
        fadeColor.a = pulseIntensity * respawnFadeColor.a;
        fadeOverlay.color = fadeColor;
        fadeOverlay.gameObject.SetActive(fadeColor.a > 0.01f);
    }

    void PlayBeepSound(bool isFinalBeep)
    {
        if (audioSource == null) return;

        AudioClip clipToPlay = isFinalBeep && finalBeepSound != null ? finalBeepSound : countdownBeepSound;

        if (clipToPlay != null)
        {
            audioSource.PlayOneShot(clipToPlay, beepVolume);
        }
    }

    void HideRespawnUI(int playerIndex)
    {
        if (respawnPanel != null)
        {
            respawnPanel.SetActive(false);
        }

        if (waitingForRespawnPanel != null)
        {
            waitingForRespawnPanel.SetActive(false);
        }

        if (fadeOverlay != null)
        {
            fadeOverlay.gameObject.SetActive(false);
        }
    }

    void HideAllRespawnUI()
    {
        if (respawnPanel != null)
        {
            respawnPanel.SetActive(false);
        }

        if (waitingForRespawnPanel != null)
        {
            waitingForRespawnPanel.SetActive(false);
        }

        if (fadeOverlay != null)
        {
            fadeOverlay.gameObject.SetActive(false);
        }
    }

    // Public methods for external control
    public void ShowManualRespawnUI(int playerIndex, string message)
    {
        if (respawnPanel != null)
        {
            respawnPanel.SetActive(true);
        }

        if (respawnMessageText != null)
        {
            respawnMessageText.text = message;
        }
    }

    public void HideManualRespawnUI()
    {
        HideAllRespawnUI();
    }
}