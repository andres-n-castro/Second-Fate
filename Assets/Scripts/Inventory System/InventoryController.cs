using System;
#if UNITY_EDITOR
using UnityEditor.Timeline;
#endif
using UnityEngine;

public class InventoryController : MonoBehaviour
{
    public static InventoryController Instance;
    public ItemPickup itemPickup;

    [HideInInspector]
    public InventoryModel inventoryModel;
    public InventoryView inventoryView;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        Instance = this;

        inventoryModel = GetComponent<InventoryModel>();
        if (inventoryView == null) inventoryView = GetComponent<InventoryView>();
    }

    void OnEnable()
    {
        ItemPickup.PickUpItem += inventoryModel.AddItem;
        if (inventoryView != null) UIManager.OnInventoryToggled += inventoryView.DrawInventory;
    }

    void OnDisable()
    {
        ItemPickup.PickUpItem -= inventoryModel.AddItem;
        if (inventoryView != null) UIManager.OnInventoryToggled -= inventoryView.DrawInventory;
    }
}
