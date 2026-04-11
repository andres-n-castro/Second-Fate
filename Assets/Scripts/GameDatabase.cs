using UnityEngine;
using System.Collections.Generic;

public class GameDatabase : MonoBehaviour
{
    public static GameDatabase Instance { get; private set; }

    [Header("Scriptable Object Databases")]
    public List<Item> allItems = new List<Item>();
    public List<CharmData> allCharms = new List<CharmData>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public Item GetItemByID(string itemID)
    {
        return allItems.Find(item => item.name == itemID);
    }

    public CharmData GetCharmByID(string charmID)
    {
        return allCharms.Find(charm => charm.name == charmID);
    }
}
