using System.Collections.Generic;

[System.Serializable]
public class GameData
{
    // --- LOCATION & RESPAWN ---
    public string lastRestedBonfireID = "";
    public string currentSceneName = "TutorialScene";

    // --- PLAYER STATS ---
    public int currentHealth;
    public int maxHealth;
    public int currentCurrency;

    // --- WORLD STATE (Permanent Events) ---
    public List<string> lootedInteractableIDs = new List<string>();
    public List<string> defeatedBossIDs = new List<string>();

    // --- BONFIRES ---
    public List<string> unlockedBonfires = new List<string>();
    public List<string> imbuedBonfireIDs = new List<string>();
    public List<int> imbuedBonfireAlignments = new List<int>();

    // --- CHARMS ---
    public List<string> unlockedCharmIDs = new List<string>();
    public List<string> equippedCharmIDs = new List<string>();

    // --- ABILITIES ---
    public bool hasDash = false;
    public bool hasDoubleJump = false;

    // --- INVENTORY ---
    public List<string> inventoryItemIDs = new List<string>();
    public List<int> inventoryItemAmounts = new List<int>();
    public List<bool> inventoryItemReadStates = new List<bool>();
}
