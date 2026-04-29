using System.IO;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }
    private const string DefaultSceneName = "tutorial_hub";
    private const int MaxSaveSlotsToScan = 10;

    [Header("Testing Tools")]
    public bool isSandboxMode = false;

    public GameData currentSaveData = new GameData();
    private int currentSlotIndex = 0;
    private bool applySaveDataOnNextSceneLoad;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else Destroy(this);
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
        if (isSandboxMode || scene.name == "main_menu_scene")
        {
            return;
        }

        bool placePlayerAfterLoad = applySaveDataOnNextSceneLoad;
        applySaveDataOnNextSceneLoad = false;
        StartCoroutine(ApplyLoadedGameAfterSceneLoad(scene, placePlayerAfterLoad));
    }

    private IEnumerator ApplyLoadedGameAfterSceneLoad(Scene scene, bool placePlayerAfterLoad)
    {
        for (int i = 0; i < 30 && GameManager.Instance == null; i++)
        {
            yield return null;
        }

        ApplyCurrentSaveDataToRuntime();

        if (placePlayerAfterLoad && GameManager.Instance != null)
        {
            GameManager.Instance.PlaceLoadedGamePlayer();
        }

        currentSaveData.currentSceneName = scene.name;
        WriteCurrentSaveDataToDisk();
    }

    public static SaveManager EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        GameObject saveManagerObject = new GameObject("SaveManager");
        return saveManagerObject.AddComponent<SaveManager>();
    }

    private static string GetSavePathForSlot(int slotIndex)
    {
        return Path.Combine(Application.persistentDataPath, $"save_slot_{slotIndex}.json");
    }

    private string GetSavePath(int slotIndex)
    {
        return GetSavePathForSlot(slotIndex);
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
        List<string> previousUnlockedCharmIDs = new List<string>(currentSaveData.unlockedCharmIDs);
        List<string> previousEquippedCharmIDs = new List<string>(currentSaveData.equippedCharmIDs);
        List<string> previousCollectedKeyIDs = new List<string>(currentSaveData.collectedKeyIDs);
        bool previousHasDash = currentSaveData.hasDash;
        bool previousHasDoubleJump = currentSaveData.hasDoubleJump;
        currentSaveData = new GameData();
        currentSaveData.lootedInteractableIDs = previousLootedInteractableIDs;
        currentSaveData.defeatedBossIDs = previousDefeatedBossIDs;
        currentSaveData.collectedKeyIDs = TutorialManager.Instance != null
            ? TutorialManager.Instance.GetCollectedKeys()
            : previousCollectedKeyIDs;

        if (PlayerManager.Instance != null && PlayerManager.Instance.playerStats != null)
        {
            PlayerStats playerStats = PlayerManager.Instance.playerStats;
            currentSaveData.currentHealth = playerStats.currentHealth;
            currentSaveData.maxHealth = playerStats.maxHealth;
            currentSaveData.currentCurrency = playerStats.currentCurrency;
            currentSaveData.hasDash = playerStats.canDash;
            currentSaveData.hasDoubleJump = playerStats.hasDoubleJump || playerStats.unlockedDoubleJump;
        }
        else
        {
            currentSaveData.hasDash = previousHasDash;
            currentSaveData.hasDoubleJump = previousHasDoubleJump;
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
        else
        {
            currentSaveData.unlockedCharmIDs = previousUnlockedCharmIDs;
            currentSaveData.equippedCharmIDs = previousEquippedCharmIDs;
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

    public void SaveCurrentSlotForScene(string sceneName)
    {
        SaveGame(currentSlotIndex);

        if (!string.IsNullOrEmpty(sceneName))
        {
            currentSaveData.currentSceneName = sceneName;
            WriteCurrentSaveDataToDisk();
        }
    }

    public void WriteCurrentSaveDataToDisk()
    {
        if (isSandboxMode) return;

        NormalizeCurrentSaveData();
        string json = JsonUtility.ToJson(currentSaveData, true);
        File.WriteAllText(GetSavePath(currentSlotIndex), json);
        Debug.Log($"Current save data written to Slot {currentSlotIndex}");
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

        // Prefer the scene that contains the last rested bonfire so the
        // player spawns at that bonfire, not wherever they happened to quit.
        string sceneName = GetSceneForLastRestedBonfire();
        if (string.IsNullOrEmpty(sceneName))
        {
            sceneName = string.IsNullOrEmpty(currentSaveData.currentSceneName)
                ? DefaultSceneName
                : currentSaveData.currentSceneName;
        }

        applySaveDataOnNextSceneLoad = true;
        SceneManager.LoadScene(sceneName);
        return true;
    }

    private string GetSceneForLastRestedBonfire()
    {
        if (string.IsNullOrEmpty(currentSaveData.lastRestedBonfireID))
        {
            return null;
        }

        // Bonfire save IDs use the format "sceneName:bonfireID"
        int colonIndex = currentSaveData.lastRestedBonfireID.IndexOf(':');
        if (colonIndex > 0)
        {
            return currentSaveData.lastRestedBonfireID.Substring(0, colonIndex);
        }

        return null;
    }

    public void ContinueOrStartDefaultGame()
    {
        if (TryGetMostRecentSaveSlot(out int slotIndex))
        {
            LoadGameAndScene(slotIndex);
            return;
        }

        StartNewGame(0);
    }

    private bool TryGetMostRecentSaveSlot(out int slotIndex)
    {
        slotIndex = -1;
        System.DateTime newestWriteTime = System.DateTime.MinValue;

        for (int i = 0; i < MaxSaveSlotsToScan; i++)
        {
            string path = GetSavePath(i);
            if (!File.Exists(path))
            {
                continue;
            }

            System.DateTime writeTime = File.GetLastWriteTimeUtc(path);
            if (slotIndex == -1 || writeTime > newestWriteTime)
            {
                slotIndex = i;
                newestWriteTime = writeTime;
            }
        }

        return slotIndex >= 0;
    }

    public void StartNewGame(int slotIndex = 0)
    {
        currentSlotIndex = slotIndex;
        currentSaveData = new GameData();
        DeleteSave(slotIndex);
        WriteCurrentSaveDataToDisk();
        applySaveDataOnNextSceneLoad = true;
        SceneManager.LoadScene(DefaultSceneName);
    }

    public bool HasSaveFile(int slotIndex)
    {
        return DoesSaveFileExist(slotIndex);
    }

    public bool DoesSaveExist(int slotIndex)
    {
        return HasSaveFile(slotIndex);
    }

    public static bool DoesSaveFileExist(int slotIndex)
    {
        return File.Exists(GetSavePathForSlot(slotIndex));
    }

    public string GetSaveSlotSummary(int slotIndex)
    {
        return GetSaveSlotSummaryText(slotIndex);
    }

    public static string GetSaveSlotSummaryText(int slotIndex)
    {
        string path = GetSavePathForSlot(slotIndex);
        if (!File.Exists(path))
        {
            return "Empty Slot";
        }

        try
        {
            string json = File.ReadAllText(path);
            GameData saveData = JsonUtility.FromJson<GameData>(json);
            if (saveData == null)
            {
                return "Data Found";
            }

            string sceneName = string.IsNullOrEmpty(saveData.currentSceneName) ? "Unknown Scene" : saveData.currentSceneName;
            string bonfireName = string.IsNullOrEmpty(saveData.lastRestedBonfireID) ? "No Bonfire" : saveData.lastRestedBonfireID;
            return $"{sceneName} - {bonfireName}";
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"Could not read save slot {slotIndex}: {exception.Message}");
            return "Data Found";
        }
    }

    public void MarkInteractableLooted(string persistentID)
    {
        NormalizeCurrentSaveData();

        if (string.IsNullOrEmpty(persistentID) || currentSaveData.lootedInteractableIDs.Contains(persistentID))
        {
            return;
        }

        currentSaveData.lootedInteractableIDs.Add(persistentID);
        WriteCurrentSaveDataToDisk();
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
        WriteCurrentSaveDataToDisk();
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
                string bonfireSaveID = GameManager.GetBonfireSaveID(bonfireData.sceneName, bonfireData.bonfireID);
                if (bonfireData.bonfireID == currentSaveData.lastRestedBonfireID
                    || bonfireSaveID == currentSaveData.lastRestedBonfireID)
                {
                    GameManager.Instance.currentRespawnPoint = bonfireData.spawnPosition;
                    break;
                }
            }

            GameManager.Instance.RefreshSceneBonfires();
        }

        ApplyCharmData();
        TutorialManager.EnsureInstance().SetCollectedKeys(currentSaveData.collectedKeyIDs);
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
        if (currentSaveData.collectedKeyIDs == null) currentSaveData.collectedKeyIDs = new List<string>();
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
