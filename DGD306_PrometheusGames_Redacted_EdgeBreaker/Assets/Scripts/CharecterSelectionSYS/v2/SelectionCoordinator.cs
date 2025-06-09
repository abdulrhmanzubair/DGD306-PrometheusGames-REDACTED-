using UnityEngine;
using UnityEngine.SceneManagement;

public class SelectionCoordinator : MonoBehaviour
{
    public static SelectionCoordinator Instance;
    
    private int readyCount = 0;
    private const int requiredPlayers = 2;

    void Awake()
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
        Debug.Log($"Player ready! Total: {readyCount}/{requiredPlayers}");
        
        if (readyCount >= requiredPlayers)
        {
            Debug.Log("All players ready! Loading game...");
            
        }
    }
}