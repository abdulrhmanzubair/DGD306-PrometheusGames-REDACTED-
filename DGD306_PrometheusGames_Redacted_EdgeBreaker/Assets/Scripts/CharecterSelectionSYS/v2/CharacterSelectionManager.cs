using UnityEngine;

public class CharacterSelectionManager : MonoBehaviour
{
    public static CharacterSelectionManager Instance { get; private set; }
    
    public enum CharacterType { Melee, Gunner }
    public CharacterType[] PlayerSelections { get; } = new CharacterType[2];
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void SetPlayerSelection(int playerIndex, CharacterType character)
    {
        if(playerIndex >= 0 && playerIndex < 2)
            PlayerSelections[playerIndex] = character;
    }
}