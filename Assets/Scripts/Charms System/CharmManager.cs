using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PlayerManager))]
public class CharmManager : MonoBehaviour
{
    public static CharmManager Instance;

    [Header("Capacity")]
    public int defaultMaxCharms = 1;

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

        if (equippedCharms.Count >= GetMaxCharmSlots())
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

    public int GetMaxCharmSlots()
    {
        if (GameManager.Instance != null &&
            GameManager.Instance.GetActiveAlignment() == GameManager.AlignmentType.CreatureBlood)
        {
            return defaultMaxCharms + 1;
        }

        return defaultMaxCharms;
    }

    public void ValidateEquippedCharms()
    {
        int max = GetMaxCharmSlots();
        while (equippedCharms.Count > max)
        {
            CharmData charmToDrop = equippedCharms[equippedCharms.Count - 1];
            equippedCharms.RemoveAt(equippedCharms.Count - 1);
            Debug.Log($"Unequipped {charmToDrop.name} due to lost slot.");
        }
    }

    public void EnforceEquippedCharmLimit()
    {
        ValidateEquippedCharms();
    }
}
