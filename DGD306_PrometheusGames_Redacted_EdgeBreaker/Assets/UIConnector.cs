using UnityEngine;
using UnityEngine.UI;

public class UIConnector : MonoBehaviour
{
    void Start()
    {
        // Wait a frame to make sure ScoreManager is ready
        Invoke("ConnectUI", 0.1f);
    }

    void ConnectUI()
    {
        if (ScoreManagerV2.instance == null)
        {
            Debug.LogWarning("ScoreManager not found! Make sure it exists in your first level.");
            return;
        }

        // Find the UI elements in this scene
        Text scoreText = GameObject.Find("ScoreText")?.GetComponent<Text>();
        Text highscoreText = GameObject.Find("HighscoreText")?.GetComponent<Text>();

        // Connect to ScoreManager
        if (scoreText != null && highscoreText != null)
        {
            ScoreManagerV2.instance.SetUIReferences(scoreText, highscoreText);
            Debug.Log("UI connected successfully! Current Score: " + ScoreManagerV2.instance.GetScore());
        }
        else
        {
            Debug.LogWarning("Could not find UI elements! Make sure they are named 'ScoreText' and 'HighscoreText'");

            // The advanced ScoreManager will automatically find them anyway, but this is for manual override
            Debug.Log("ScoreManager will try to find UI elements automatically...");
        }
    }
}