using UnityEditor.Timeline;
using UnityEngine;

public class InventoryController : MonoBehaviour
{
private InventoryController Instance;
    public GameObject inventoryCanvas;
    public ItemPickup itemPickup;
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
            
        }
    }

    void OnEnable()
    {
        ItemPickup.PickUpItem += inventoryModel.AddItem;
        UIManager.UIStateChanged += OpenInventoryUI;
    }

    void OnDisable()
    {
        ItemPickup.PickUpItem -= inventoryModel.AddItem;
    }
}
