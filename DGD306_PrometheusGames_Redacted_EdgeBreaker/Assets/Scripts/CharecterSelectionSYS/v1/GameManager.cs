using UnityEngine;

public enum CharacterType { Melee, Gunner }

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    public CharacterType player1Selection = CharacterType.Melee;
    public CharacterType player2Selection = CharacterType.Melee;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
