using UnityEngine;

public class CharacterSelectionData : MonoBehaviour
{
    public static CharacterSelectionData Instance { get; private set; }

    public enum CharacterType { Melee, Gunner }
    public CharacterType[] selections = new CharacterType[2];

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else Destroy(gameObject);
    }

    public void SetSelection(int playerIndex, CharacterType character)
    {
        if (playerIndex >= 0 && playerIndex < selections.Length)
            selections[playerIndex] = character;
    }

    public CharacterType GetSelection(int index) => selections[index];
}
