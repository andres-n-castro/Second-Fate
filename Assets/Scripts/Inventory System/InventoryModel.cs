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
        ItemSlotData newItem = new()
        {
            itemData = item,
            isRead = false
        };
        inventoryItems.Add(newItem);

        OnItemAdded?.Invoke(newItem);
    }

    public void RemoveItem(ItemSlotData item)
    {
        if(inventoryItems.Remove(item))
            OnItemRemoved?.Invoke(item);
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
