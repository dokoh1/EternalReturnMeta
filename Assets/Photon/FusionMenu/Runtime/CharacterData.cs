using UnityEngine;

[CreateAssetMenu(fileName = "CharacterData", menuName = "Game/CharacterData")]
public class CharacterData : ScriptableObject
{
    public string CharacterName;
    public Sprite fullImage;
    public Sprite vsImage;
}