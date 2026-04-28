using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }
    private const string DefaultSceneName = "tutorial_hub";

    [Header("Testing Tools")]
    public bool isSandboxMode = false;

    public GameData currentSaveData = new GameData();
    private int currentSlotIndex = 0;
    private bool applySaveDataOnNextSceneLoad;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!isSandboxMode && applySaveDataOnNextSceneLoad)
        {
            ApplyCurrentSaveDataToRuntime();
            applySaveDataOnNextSceneLoad = false;
        }
    }

    private string GetSavePath(int slotIndex)
    {
        return Path.Combine(Application.persistentDataPath, $"save_slot_{slotIndex}.json");
    }

    public void SaveGame(int slotIndex)
    {
        if (isSandboxMode) return;

        NormalizeCurrentSaveData();
        currentSlotIndex = slotIndex;
        List<string> previousInventoryItemIDs = new List<string>(currentSaveData.inventoryItemIDs);
        List<int> previousInventoryItemAmounts = new List<int>(currentSaveData.inventoryItemAmounts);
        List<bool> previousInventoryItemReadStates = new List<bool>(currentSaveData.inventoryItemReadStates);
        List<string> previousLootedInteractableIDs = new List<string>(currentSaveData.lootedInteractableIDs);
        List<string> previousDefeatedBossIDs = new List<string>(currentSaveData.defeatedBossIDs);
        currentSaveData = new GameData();
        currentSaveData.lootedInteractableIDs = previousLootedInteractableIDs;
        currentSaveData.defeatedBossIDs = previousDefeatedBossIDs;

        if (PlayerManager.Instance != null && PlayerManager.Instance.playerStats != null)
        {
            PlayerStats playerStats = PlayerManager.Instance.playerStats;
            currentSaveData.currentHealth = playerStats.currentHealth;
            currentSaveData.maxHealth = playerStats.maxHealth;
            currentSaveData.currentCurrency = playerStats.currentCurrency;
            currentSaveData.hasDash = playerStats.canDash;
            currentSaveData.hasDoubleJump = playerStats.hasDoubleJump || playerStats.unlockedDoubleJump;
        }

        if (GameManager.Instance != null)
        {
            currentSaveData.lastRestedBonfireID = GameManager.Instance.lastRestedBonfireID;
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
        else
        {
            currentSaveData.inventoryItemIDs = previousInventoryItemIDs;
            currentSaveData.inventoryItemAmounts = previousInventoryItemAmounts;
            currentSaveData.inventoryItemReadStates = previousInventoryItemReadStates;
        }

        string json = JsonUtility.ToJson(currentSaveData, true);
        File.WriteAllText(GetSavePath(slotIndex), json);
        Debug.Log($"Game Saved successfully to Slot {slotIndex}");
    }

    public void SaveCurrentSlot()
    {
        SaveGame(currentSlotIndex);
    }

    public bool LoadGame(int slotIndex)
    {
        if (isSandboxMode)
        {
            Debug.LogWarning("Sandbox Mode Active: Loading a blank slate.");
            currentSaveData = new GameData();
            return true;
        }

        string path = GetSavePath(slotIndex);
        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);
            currentSaveData = JsonUtility.FromJson<GameData>(json);
            NormalizeCurrentSaveData();
            currentSlotIndex = slotIndex;
            Debug.Log($"Game Loaded successfully from Slot {slotIndex}");

            ApplyCurrentSaveDataToRuntime();

            return true;
        }

        Debug.LogWarning($"No save file found in Slot {slotIndex}");
        return false;
    }

    public bool LoadGameAndScene(int slotIndex)
    {
        if (!LoadGame(slotIndex))
        {
            return false;
        }

        string sceneName = string.IsNullOrEmpty(currentSaveData.currentSceneName)
            ? DefaultSceneName
            : currentSaveData.currentSceneName;

        applySaveDataOnNextSceneLoad = true;
        GameManager.Instance?.QueueLoadedGamePlacement();
        SceneManager.LoadScene(sceneName);
        return true;
    }

    public void ContinueOrStartDefaultGame()
    {
        if (HasSaveFile(0))
        {
            LoadGameAndScene(0);
            return;
        }

        StartNewGame(0);
    }

    public void StartNewGame(int slotIndex = 0)
    {
        currentSlotIndex = slotIndex;
        currentSaveData = new GameData();
        DeleteSave(slotIndex);
        applySaveDataOnNextSceneLoad = true;
        GameManager.Instance?.QueueLoadedGamePlacement();
        SceneManager.LoadScene(DefaultSceneName);
    }

    public bool HasSaveFile(int slotIndex)
    {
        return File.Exists(GetSavePath(slotIndex));
    }

    public void MarkInteractableLooted(string persistentID)
    {
        NormalizeCurrentSaveData();

        if (string.IsNullOrEmpty(persistentID) || currentSaveData.lootedInteractableIDs.Contains(persistentID))
        {
            return;
        }

        currentSaveData.lootedInteractableIDs.Add(persistentID);
    }

    public bool IsInteractableLooted(string persistentID)
    {
        NormalizeCurrentSaveData();
        return !string.IsNullOrEmpty(persistentID) && currentSaveData.lootedInteractableIDs.Contains(persistentID);
    }

    public void MarkBossDefeated(string bossID)
    {
        NormalizeCurrentSaveData();

        if (string.IsNullOrEmpty(bossID) || currentSaveData.defeatedBossIDs.Contains(bossID))
        {
            return;
        }

        currentSaveData.defeatedBossIDs.Add(bossID);
    }

    public bool IsBossDefeated(string bossID)
    {
        NormalizeCurrentSaveData();
        return !string.IsNullOrEmpty(bossID) && currentSaveData.defeatedBossIDs.Contains(bossID);
    }

    public string BuildSceneObjectID(GameObject sceneObject)
    {
        if (sceneObject == null)
        {
            return string.Empty;
        }

        return $"{sceneObject.scene.name}:{GetTransformPath(sceneObject.transform)}";
    }

    private string GetTransformPath(Transform target)
    {
        if (target == null)
        {
            return string.Empty;
        }

        string path = target.name;
        Transform parent = target.parent;
        while (parent != null)
        {
            path = $"{parent.name}/{path}";
            parent = parent.parent;
        }

        return path;
    }

    private void ApplyCurrentSaveDataToRuntime()
    {
        NormalizeCurrentSaveData();

        if (PlayerManager.Instance != null && PlayerManager.Instance.playerStats != null)
        {
            PlayerStats playerStats = PlayerManager.Instance.playerStats;
            playerStats.currentCurrency = currentSaveData.currentCurrency;
            playerStats.hasDoubleJump = currentSaveData.hasDoubleJump;
            playerStats.unlockedDoubleJump = currentSaveData.hasDoubleJump;
            playerStats.canDash = currentSaveData.hasDash;
            playerStats.hasDash = currentSaveData.hasDash;

            int loadedMaxHealth = currentSaveData.maxHealth > 0 ? currentSaveData.maxHealth : playerStats.maxHealth;
            int loadedCurrentHealth = currentSaveData.currentHealth > 0 ? currentSaveData.currentHealth : loadedMaxHealth;
            playerStats.SyncHealthForSaving(loadedCurrentHealth, loadedMaxHealth);

            if (playerStats.playerHealthComponent != null)
            {
                playerStats.playerHealthComponent.InitializeHealth(loadedCurrentHealth, loadedMaxHealth);
            }
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.lastRestedBonfireID = currentSaveData.lastRestedBonfireID;
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

        ApplyCharmData();
        ApplyInventoryData();
    }

    private void ApplyCharmData()
    {
        if (CharmManager.Instance == null || GameDatabase.Instance == null)
        {
            return;
        }

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

        CharmManager.Instance.EnforceEquippedCharmLimit();
    }

    private void ApplyInventoryData()
    {
        if (InventoryController.Instance == null ||
            InventoryController.Instance.inventoryModel == null ||
            GameDatabase.Instance == null)
        {
            return;
        }

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

    private void NormalizeCurrentSaveData()
    {
        if (currentSaveData == null)
        {
            currentSaveData = new GameData();
        }

        if (currentSaveData.lootedInteractableIDs == null) currentSaveData.lootedInteractableIDs = new List<string>();
        if (currentSaveData.defeatedBossIDs == null) currentSaveData.defeatedBossIDs = new List<string>();
        if (currentSaveData.unlockedBonfires == null) currentSaveData.unlockedBonfires = new List<string>();
        if (currentSaveData.imbuedBonfireIDs == null) currentSaveData.imbuedBonfireIDs = new List<string>();
        if (currentSaveData.imbuedBonfireAlignments == null) currentSaveData.imbuedBonfireAlignments = new List<int>();
        if (currentSaveData.unlockedCharmIDs == null) currentSaveData.unlockedCharmIDs = new List<string>();
        if (currentSaveData.equippedCharmIDs == null) currentSaveData.equippedCharmIDs = new List<string>();
        if (currentSaveData.inventoryItemIDs == null) currentSaveData.inventoryItemIDs = new List<string>();
        if (currentSaveData.inventoryItemAmounts == null) currentSaveData.inventoryItemAmounts = new List<int>();
        if (currentSaveData.inventoryItemReadStates == null) currentSaveData.inventoryItemReadStates = new List<bool>();
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
