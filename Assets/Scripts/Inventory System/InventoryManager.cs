using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    private InventoryManager Instance;
    public GameObject inventoryMenu;
    private bool inventoryMenuActivated;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        Instance = this;

    }

    void Start()
    {
        
    }

    
    void Update()
    {

        //later will change this logic to instead invoke the event in the UI manager to set the inventory UI to active
        //but for right now we will set the inventory UI active logic in this script
        if(Input.GetButtonDown("Open Inventory") && !inventoryMenuActivated)
        {
            Time.timeScale = 0f;
            inventoryMenu.SetActive(true);
            inventoryMenuActivated = true;
        }
        else if(Input.GetButtonDown("Open Inventory") && inventoryMenuActivated)
        {
            Time.timeScale = 1f;
            inventoryMenu.SetActive(false);
            inventoryMenuActivated = false;
        }
    }
}
