using UnityEngine;
using UnityEngine.SceneManagement;

public class SelectionSystem : MonoBehaviour
{
    public static SelectionSystem Instance;
    
    private int readyCount = 0;
    private const int maxPlayers = 2;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void PlayerReady()
    {
        readyCount++;
        CheckAllReady();
    }

    private void CheckAllReady()
    {
        if (readyCount >= maxPlayers)
        {
            LoadGameScene();
        }
    }

   
    private void LoadGameScene()
    {
        SceneManager.LoadScene("Level1");
    }
}