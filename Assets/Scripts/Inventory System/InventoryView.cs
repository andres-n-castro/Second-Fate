using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventoryView : MonoBehaviour
{
    [Header("Grid Panel (Middle)")]
    public Transform gridContainer;
    public GameObject itemSlotPrefab;

    [Header("Info Panel (Right)")]
    public GameObject infoPanel;
    public TextMeshProUGUI itemNameText;
    public TextMeshProUGUI itemDescriptionText;
    public Image itemBigIcon;

    public void DrawInventory()
    {
        for (int i = gridContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(gridContainer.GetChild(i).gameObject);
        }

        infoPanel.SetActive(false);

        List<ItemSlotData> inventory = InventoryController.Instance.inventoryModel.RetrieveInventoryItems();

        foreach (ItemSlotData slotData in inventory)
        {
            GameObject newSlot = Instantiate(itemSlotPrefab, gridContainer);
            ItemSlot itemSlot = newSlot.GetComponent<ItemSlot>();
            itemSlot.Setup(slotData, UpdateInfoPanel);
        }
    }

    private void UpdateInfoPanel(Item item)
    {
        infoPanel.SetActive(true);
        itemNameText.text = item.itemName;
        itemDescriptionText.text = item.itemInfo;
        itemBigIcon.sprite = item.itemSprite;
    }
}
