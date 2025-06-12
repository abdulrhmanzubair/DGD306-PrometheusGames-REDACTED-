
using UnityEngine;

public enum CharacterType
{
    Gunner,
    Melee
}

[System.Serializable]
public class CharacterData
{
    public CharacterType type;
    public string name;
    public Sprite portrait;
    public GameObject prefab;
    [TextArea(2, 4)]
    public string description;
}