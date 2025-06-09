using UnityEngine;
using UnityEngine.SceneManagement;

public class CharacterSelectManager : MonoBehaviour
{
    public static CharacterSelectManager Instance;

    private int readyCount = 0;

    void Awake()
    {
        Instance = this;
    }

    public void PlayerReady()
    {
        readyCount++;
        if (readyCount >= 1) // assuming 2 players
        {
            Invoke(nameof(LoadGame), 1f);
        }
    }

    private void LoadGame()
    {
        SceneManager.LoadScene("SampleScene");
    }
}
