using UnityEngine;

public enum CharmEffect
{
    None,
    Haste,
    Agility,
    Protection,
    CritChance
}

[CreateAssetMenu(fileName = "CharmData", menuName = "Scriptable Objects/Charm")]
public class CharmData : ScriptableObject
{
    public string charmID;
    public string charmName;
    public Sprite charmIcon;
    public CharmEffect charmEffect;

    [TextArea(2, 5)]
    public string description;
}
