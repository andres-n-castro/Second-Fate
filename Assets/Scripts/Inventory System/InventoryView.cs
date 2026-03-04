using UnityEngine;

public class InventoryView : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    /*
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
    */
}
