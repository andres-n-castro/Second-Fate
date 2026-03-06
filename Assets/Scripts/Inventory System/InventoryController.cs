using System;
using UnityEditor.Timeline;
using UnityEngine;

public class InventoryController : MonoBehaviour
{
    public static InventoryController Instance;
    public GameObject inventoryCanvas;
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

    }

    void OpenInventoryUI(UIManager.UIStates currentState)
    {
        if(currentState == UIManager.UIStates.inventoryUI)
        {
            Debug.Log("Succesfully entered inventory menu state!");
            inventoryCanvas.SetActive(true);
            Debug.Log("Succesfully opened menu!");
        }
        else
        {
            Debug.Log("Succesfully entered turn off inventory menu state section!");
            inventoryCanvas.SetActive(false);
            Debug.Log("Succesfully turned off inventory menu!");
        }
    }

    void OnEnable()
    {
        //ItemPickup.PickUpItem += inventoryModel.AddItem;
        UIManager.UIStateChanged += OpenInventoryUI;
    }

    void OnDisable()
    {
        //ItemPickup.PickUpItem -= inventoryModel.AddItem;
        UIManager.UIStateChanged -= OpenInventoryUI;
    }
}
