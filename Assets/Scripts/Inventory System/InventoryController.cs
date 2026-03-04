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


    void OnEnable()
    {
        ItemPickup.PickUpItem += inventoryModel.AddItem;
    }

    void OnDisable()
    {
        ItemPickup.PickUpItem -= inventoryModel.AddItem;
    }
}
