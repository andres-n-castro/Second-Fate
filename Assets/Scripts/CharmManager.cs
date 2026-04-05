using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PlayerManager))]
public class CharmManager : MonoBehaviour
{
    public static CharmManager Instance;

    public List<CharmData> unlockedCharms = new List<CharmData>();
    public List<CharmData> equippedCharms = new List<CharmData>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void UnlockCharm(CharmData newCharm)
    {
        if (newCharm == null || unlockedCharms.Contains(newCharm))
        {
            return;
        }

        unlockedCharms.Add(newCharm);
    }

    public bool EquipCharm(CharmData charm)
    {
        if (charm == null || !unlockedCharms.Contains(charm))
        {
            return false;
        }

        if (equippedCharms.Contains(charm))
        {
            return true;
        }

        if (PlayerManager.Instance == null || PlayerManager.Instance.playerStats == null)
        {
            return false;
        }

        if (equippedCharms.Count >= PlayerManager.Instance.playerStats.maxCharmSlots)
        {
            return false;
        }

        equippedCharms.Add(charm);
        return true;
    }

    public bool UnequipCharm(CharmData charm)
    {
        if (charm == null)
        {
            return false;
        }

        return equippedCharms.Remove(charm);
    }

    public bool IsCharmEquipped(string charmID)
    {
        foreach (CharmData charm in equippedCharms)
        {
            if (charm != null && charm.charmID == charmID)
            {
                return true;
            }
        }

        return false;
    }
}
