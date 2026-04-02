using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BonfireUIManager : MonoBehaviour
{
    public Button goodAlignmentBtn;
    public Button badAlignmentBtn;
    public Button leaveBtn;
    public Item treeEssence;
    public Item creatureBlood;

    void OnEnable()
    {
        var inventory = InventoryController.Instance.inventoryModel.RetrieveInventoryItems();

        bool hasEssence = false;
        bool hasBlood = false;

        foreach (ItemSlotData slot in inventory)
        {
            if (slot.itemData == treeEssence && slot.amount > 0)
            {
                hasEssence = true;
            }

            if (slot.itemData == creatureBlood && slot.amount > 0)
            {
                hasBlood = true;
            }
        }

        goodAlignmentBtn.interactable = hasEssence;
        badAlignmentBtn.interactable = hasBlood;
    }

    public void ApplyGoodAlignment()
    {
        GameManager.Instance.SetBonfireAlignment(GameManager.Instance.lastInteractedBonfireID, GameManager.AlignmentType.TreeEssence);
        ConsumeItem(treeEssence);
        GameManager.Instance.RestorePreviousState();
    }

    public void ApplyBadAlignment()
    {
        GameManager.Instance.SetBonfireAlignment(GameManager.Instance.lastInteractedBonfireID, GameManager.AlignmentType.CreatureBlood);
        ConsumeItem(creatureBlood);
        GameManager.Instance.RestorePreviousState();
    }

    public void CloseMenu()
    {
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

                if (slot.amount <= 0)
                {
                    InventoryController.Instance.inventoryModel.RemoveItem(slot);
                }

                return;
            }
        }
    }
}
