using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class BonfireUIManager : MonoBehaviour
{
    [Header("Items")]
    public Item treeEssence;
    public Item creatureBlood;

    [Header("Panels")]
    public GameObject noArtifactPanel;
    public GameObject alignmentPanel;
    public GameObject litBonfirePanel;
    public GameObject fastTravelPanel;

    [Header("Default Controller Buttons")]
    public GameObject closeNoArtifactBtn;
    public GameObject goodAlignmentBtn;
    public GameObject restBtn;
    public GameObject closeFastTravelBtn;

    [Header("Alignment Buttons")]
    public Button goodBtnComponent;
    public Button badBtnComponent;

    void OnEnable()
    {
        HideAllPanels();
        GameManager.AlignmentType currentAlignment = GameManager.Instance.GetBonfireAlignment(GameManager.Instance.lastInteractedBonfireID);

        if (currentAlignment != GameManager.AlignmentType.None)
        {
            ShowPanel(litBonfirePanel, restBtn);
        }
        else
        {
            var inventory = InventoryController.Instance.inventoryModel.RetrieveInventoryItems();
            bool hasEssence = false;
            bool hasBlood = false;

            foreach (ItemSlotData slot in inventory)
            {
                if (slot.itemData == treeEssence && slot.amount > 0) hasEssence = true;
                if (slot.itemData == creatureBlood && slot.amount > 0) hasBlood = true;
            }

            if (hasEssence || hasBlood)
            {
                goodBtnComponent.interactable = true;
                badBtnComponent.interactable = true;
                ShowPanel(alignmentPanel, goodAlignmentBtn);
            }
            else
            {
                ShowPanel(noArtifactPanel, closeNoArtifactBtn);
            }
        }
    }

    private void HideAllPanels()
    {
        noArtifactPanel.SetActive(false);
        alignmentPanel.SetActive(false);
        litBonfirePanel.SetActive(false);
        fastTravelPanel.SetActive(false);
    }

    private void ShowPanel(GameObject panel, GameObject defaultButton)
    {
        HideAllPanels();
        panel.SetActive(true);
        if (EventSystem.current != null && defaultButton != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(defaultButton);
        }
    }

    public void ApplyGoodAlignment() => ApplyAlignment(GameManager.AlignmentType.TreeEssence);
    public void ApplyBadAlignment() => ApplyAlignment(GameManager.AlignmentType.CreatureBlood);

    private void ApplyAlignment(GameManager.AlignmentType alignment)
    {
        GameManager.Instance.SetBonfireAlignment(GameManager.Instance.lastInteractedBonfireID, alignment);
        GameManager.Instance.UnlockBonfire(GameManager.Instance.lastInteractedBonfireID);

        ConsumeItem(treeEssence);
        ConsumeItem(creatureBlood);

        Bonfire activeBonfire = Object.FindFirstObjectByType<Bonfire>();
        if (activeBonfire != null) activeBonfire.UpdateVisualState();

        if (SaveManager.Instance != null) SaveManager.Instance.SaveGame(0);

        GameManager.Instance.RestorePreviousState();
    }

    private void ConsumeItem(Item itemToRemove)
    {
        List<ItemSlotData> inventory = InventoryController.Instance.inventoryModel.RetrieveInventoryItems();
        foreach (ItemSlotData slot in inventory)
        {
            if (slot.itemData == itemToRemove && slot.amount > 0)
            {
                slot.amount -= 1;
                if (slot.amount <= 0) InventoryController.Instance.inventoryModel.RemoveItem(slot);
                return;
            }
        }
    }

    public void Rest()
    {
        PlayerManager.Instance.playerStats.currentHealth = PlayerManager.Instance.playerStats.maxHealth;
        PlayerManager.Instance.playerStats.SyncHealthForSaving(PlayerManager.Instance.playerStats.maxHealth, PlayerManager.Instance.playerStats.maxHealth);

        if (PlayerManager.Instance.playerStats.playerHealthComponent != null)
        {
            PlayerManager.Instance.playerStats.playerHealthComponent.InitializeHealth(PlayerManager.Instance.playerStats.maxHealth, PlayerManager.Instance.playerStats.maxHealth);
        }

        PlayerManager.Instance.ResetProtectionCharmCharges();
        Bonfire activeBonfire = Object.FindFirstObjectByType<Bonfire>();
        if (activeBonfire != null)
        {
            GameManager.Instance.currentRespawnPoint = activeBonfire.transform.position;
        }

        GameManager.Instance.TriggerWorldReset();

        if (SaveManager.Instance != null) SaveManager.Instance.SaveGame(0);

        GameManager.Instance.RestorePreviousState();
    }

    public void OpenFastTravelMenu()
    {
        ShowPanel(fastTravelPanel, closeFastTravelBtn);
    }

    public void CloseMenu()
    {
        GameManager.Instance.RestorePreviousState();
    }
}
