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

    void OnEnable()
    {
        DrawInventory();
    }

    public void DrawInventory()
    {
        for (int i = gridContainer.childCount - 1; i >= 0; i--)
        {
            Transform child = gridContainer.GetChild(i);
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }

        infoPanel.SetActive(false);

        Debug.Log("DrawInventory called! Fetching items...");
        if (InventoryController.Instance == null || InventoryController.Instance.inventoryModel == null)
        {
            Debug.LogWarning("InventoryController or Model is not initialized yet. Skipping DrawInventory.");
            return;
        }

        List<ItemSlotData> inventory = InventoryController.Instance.inventoryModel.RetrieveInventoryItems();
        Debug.Log($"Items found in model: {inventory.Count}");
        GameObject firstNewSlot = null;

        for (int i = 0; i < inventory.Count; i++)
        {
            ItemSlotData slotData = inventory[i];
            GameObject newSlot = Instantiate(itemSlotPrefab, gridContainer);
            Debug.Log($"Spawning UI slot for: {slotData.itemData.itemName}");
            newSlot.transform.localScale = Vector3.one;
            ItemSlot itemSlot = newSlot.GetComponent<ItemSlot>();
            itemSlot.Setup(slotData, UpdateInfoPanel);

            if (i == 0)
            {
                firstNewSlot = newSlot;
            }
        }

        if (firstNewSlot != null)
        {
            MenuTabManager menuTabManager = FindFirstObjectByType<MenuTabManager>();
            if (menuTabManager != null)
            {
                menuTabManager.SetFirstInventorySlot(firstNewSlot);
            }
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
