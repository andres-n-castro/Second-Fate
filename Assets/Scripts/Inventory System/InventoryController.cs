using UnityEngine;

public class InventoryController : MonoBehaviour
{
private InventoryController Instance;
    public GameObject inventoryMenu;
    public ItemPickup itemPickup;
    public InventoryModel inventoryModel;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        Instance = this;

        inventoryModel = GetComponent<InventoryModel>();

    }

    void OpenInventoryMenu()
    {
        //logic for opening Inventory UI
    }

    void OnEnable()
    {
        ItemPickup.PickUpItem += inventoryModel.AddItem;
        UIManager.UIStateChanged += OpenInventoryMenu;
    }

    void OnDisable()
    {
        ItemPickup.PickUpItem -= inventoryModel.AddItem;
    }
}
