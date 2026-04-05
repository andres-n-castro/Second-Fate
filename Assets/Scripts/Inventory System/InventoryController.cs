using System;
using UnityEngine;

public class InventoryController : MonoBehaviour
{
    public static InventoryController Instance;

    [HideInInspector]
    public InventoryModel inventoryModel;
    public InventoryView inventoryView;

    void Awake()
    {
        // 1. If another instance exists, destroy this one and STOP executing.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // 2. Safely grab components
        inventoryModel = GetComponent<InventoryModel>();
        if (inventoryView == null) inventoryView = GetComponent<InventoryView>();
    }

    void OnEnable()
    {
        // 3. Bulletproof subscriptions
        if (inventoryModel != null)
        {
            ItemPickup.PickUpItem += inventoryModel.AddItem;
        }

        if (inventoryView != null)
        {
            UIManager.OnInventoryToggled += inventoryView.DrawInventory;
        }
    }

    void OnDisable()
    {
        // 4. Bulletproof un-subscriptions
        if (inventoryModel != null)
        {
            ItemPickup.PickUpItem -= inventoryModel.AddItem;
        }

        if (inventoryView != null)
        {
            UIManager.OnInventoryToggled -= inventoryView.DrawInventory;
        }
    }
}
