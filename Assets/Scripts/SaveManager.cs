using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    public GameData currentSaveData = new GameData();
    private int currentSlotIndex = 0;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private string GetSavePath(int slotIndex)
    {
        return Path.Combine(Application.persistentDataPath, $"save_slot_{slotIndex}.json");
    }

    public void SaveGame(int slotIndex)
    {
        currentSlotIndex = slotIndex;
        currentSaveData = new GameData();

        if (PlayerManager.Instance != null && PlayerManager.Instance.playerStats != null)
        {
            PlayerStats playerStats = PlayerManager.Instance.playerStats;
            currentSaveData.currentHealth = playerStats.currentHealth;
            currentSaveData.maxHealth = playerStats.maxHealth;
            currentSaveData.currentCurrency = playerStats.currentCurrency;
            currentSaveData.hasDash = playerStats.canDash;
            currentSaveData.hasDoubleJump = playerStats.hasDoubleJump;
        }

        if (GameManager.Instance != null)
        {
            currentSaveData.lastRestedBonfireID = GameManager.Instance.lastInteractedBonfireID;
            currentSaveData.currentSceneName = SceneManager.GetActiveScene().name;
            currentSaveData.unlockedBonfires = new List<string>(GameManager.Instance.unlockedBonfires);
            currentSaveData.imbuedBonfireIDs = GameManager.Instance.GetImbuedBonfireIDs();
            currentSaveData.imbuedBonfireAlignments = GameManager.Instance.GetImbuedBonfireAlignments();
        }

        if (CharmManager.Instance != null)
        {
            foreach (CharmData charm in CharmManager.Instance.unlockedCharms)
            {
                if (charm != null)
                {
                    currentSaveData.unlockedCharmIDs.Add(charm.name);
                }
            }

            foreach (CharmData charm in CharmManager.Instance.equippedCharms)
            {
                if (charm != null)
                {
                    currentSaveData.equippedCharmIDs.Add(charm.name);
                }
            }
        }

        if (InventoryController.Instance != null && InventoryController.Instance.inventoryModel != null)
        {
            List<ItemSlotData> inventory = InventoryController.Instance.inventoryModel.RetrieveInventoryItems();
            foreach (ItemSlotData slot in inventory)
            {
                if (slot.itemData == null)
                {
                    continue;
                }

                currentSaveData.inventoryItemIDs.Add(slot.itemData.name);
                currentSaveData.inventoryItemAmounts.Add(slot.amount);
                currentSaveData.inventoryItemReadStates.Add(slot.isRead);
            }
        }

        string json = JsonUtility.ToJson(currentSaveData, true);
        File.WriteAllText(GetSavePath(slotIndex), json);
        Debug.Log($"Game Saved successfully to Slot {slotIndex}");
    }

    public bool LoadGame(int slotIndex)
    {
        string path = GetSavePath(slotIndex);
        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);
            currentSaveData = JsonUtility.FromJson<GameData>(json);
            currentSlotIndex = slotIndex;
            Debug.Log($"Game Loaded successfully from Slot {slotIndex}");

            if (PlayerManager.Instance != null && PlayerManager.Instance.playerStats != null)
            {
                PlayerStats playerStats = PlayerManager.Instance.playerStats;
                playerStats.currentCurrency = currentSaveData.currentCurrency;
                playerStats.hasDoubleJump = currentSaveData.hasDoubleJump;
                playerStats.canDash = currentSaveData.hasDash;
                playerStats.hasDash = currentSaveData.hasDash;
                playerStats.SyncHealthForSaving(currentSaveData.currentHealth, currentSaveData.maxHealth);

                if (playerStats.playerHealthComponent != null)
                {
                    playerStats.playerHealthComponent.InitializeHealth(currentSaveData.currentHealth, currentSaveData.maxHealth);
                }
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.lastInteractedBonfireID = currentSaveData.lastRestedBonfireID;
                GameManager.Instance.ClearBonfireSaveState();

                foreach (string bonfireID in currentSaveData.unlockedBonfires)
                {
                    GameManager.Instance.UnlockBonfire(bonfireID);
                }

                int imbuedCount = Mathf.Min(currentSaveData.imbuedBonfireIDs.Count, currentSaveData.imbuedBonfireAlignments.Count);
                for (int i = 0; i < imbuedCount; i++)
                {
                    GameManager.Instance.SetBonfireAlignment(
                        currentSaveData.imbuedBonfireIDs[i],
                        (GameManager.AlignmentType)currentSaveData.imbuedBonfireAlignments[i]);
                }

                foreach (BonfireTravelData bonfireData in GameManager.Instance.masterBonfireRegistry)
                {
                    if (bonfireData.bonfireID == currentSaveData.lastRestedBonfireID)
                    {
                        GameManager.Instance.currentRespawnPoint = bonfireData.spawnPosition;
                        break;
                    }
                }
            }

            if (CharmManager.Instance != null && GameDatabase.Instance != null)
            {
                CharmManager.Instance.unlockedCharms.Clear();
                CharmManager.Instance.equippedCharms.Clear();

                foreach (string charmID in currentSaveData.unlockedCharmIDs)
                {
                    CharmData charm = GameDatabase.Instance.GetCharmByID(charmID);
                    if (charm != null)
                    {
                        CharmManager.Instance.UnlockCharm(charm);
                    }
                }

                foreach (string charmID in currentSaveData.equippedCharmIDs)
                {
                    CharmData charm = GameDatabase.Instance.GetCharmByID(charmID);
                    if (charm != null)
                    {
                        CharmManager.Instance.EquipCharm(charm);
                    }
                }
            }

            if (InventoryController.Instance != null &&
                InventoryController.Instance.inventoryModel != null &&
                GameDatabase.Instance != null)
            {
                List<ItemSlotData> inventory = InventoryController.Instance.inventoryModel.RetrieveInventoryItems();
                inventory.Clear();

                for (int i = 0; i < currentSaveData.inventoryItemIDs.Count; i++)
                {
                    Item item = GameDatabase.Instance.GetItemByID(currentSaveData.inventoryItemIDs[i]);
                    if (item == null)
                    {
                        continue;
                    }

                    int amount = i < currentSaveData.inventoryItemAmounts.Count ? currentSaveData.inventoryItemAmounts[i] : 1;
                    bool isRead = i < currentSaveData.inventoryItemReadStates.Count && currentSaveData.inventoryItemReadStates[i];

                    inventory.Add(new ItemSlotData
                    {
                        itemData = item,
                        amount = amount,
                        isRead = isRead
                    });
                }

                if (InventoryController.Instance.inventoryView != null)
                {
                    InventoryController.Instance.inventoryView.DrawInventory();
                }
            }

            return true;
        }

        Debug.LogWarning($"No save file found in Slot {slotIndex}");
        return false;
    }

    public void DeleteSave(int slotIndex)
    {
        string path = GetSavePath(slotIndex);
        if (File.Exists(path))
        {
            File.Delete(path);
            Debug.Log($"Deleted save in Slot {slotIndex}");
        }
    }
}
