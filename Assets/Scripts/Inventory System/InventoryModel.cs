using System;
using System.Collections.Generic;
using UnityEngine;

public class InventoryModel : MonoBehaviour
{
    List<ItemSlotData> inventoryItems = new List<ItemSlotData>();
    public event Action<ItemSlotData> OnItemAdded;
    public event Action<ItemSlotData> OnItemRemoved;

    public void AddItem(Item item)
    {
        ItemSlotData newItemData = new()
        {
            itemData = item,
            isRead = false
        };
        inventoryItems.Add(newItemData);

        OnItemAdded?.Invoke(newItemData);
    }

    public void RemoveItem(ItemSlotData itemData)
    {
        if(inventoryItems.Remove(itemData))
            OnItemRemoved?.Invoke(itemData);
    }

    public List<ItemSlotData> RetrieveInventoryItems()
    {
        return inventoryItems;
    }

}

public class ItemSlotData
{
    public Item itemData;
    public bool isRead;
}
