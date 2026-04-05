using UnityEngine;

[CreateAssetMenu(fileName = "CharmData", menuName = "Scriptable Objects/Charm")]
public class CharmData : ScriptableObject
{
    public string charmID;
    public string charmName;
    public Sprite charmIcon;

    [TextArea(2, 5)]
    public string description;
}
