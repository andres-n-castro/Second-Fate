using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class MenuTabManager : MonoBehaviour
{
    public GameObject inventoryPanel;
    public GameObject charmsPanel;

    public Button inventoryTabButton;
    public Button charmsTabButton;

    void OnEnable()
    {
        OpenInventoryTab();

        if (EventSystem.current != null && inventoryTabButton != null)
        {
            EventSystem.current.SetSelectedGameObject(inventoryTabButton.gameObject);
        }
    }

    public void OpenInventoryTab()
    {
        if (inventoryPanel != null) inventoryPanel.SetActive(true);
        if (charmsPanel != null) charmsPanel.SetActive(false);
    }

    public void OpenCharmsTab()
    {
        if (charmsPanel != null) charmsPanel.SetActive(true);
        if (inventoryPanel != null) inventoryPanel.SetActive(false);
    }

    public void SetFirstCharmSlot(GameObject slot)
    {
        if (charmsTabButton != null && slot != null)
        {
            Navigation nav = charmsTabButton.navigation;
            nav.mode = Navigation.Mode.Explicit;
            nav.selectOnDown = slot.GetComponent<Selectable>();
            nav.selectOnLeft = inventoryTabButton;
            charmsTabButton.navigation = nav;
        }
    }

    public void SetFirstInventorySlot(GameObject slot)
    {
        if (inventoryTabButton != null && slot != null)
        {
            Navigation nav = inventoryTabButton.navigation;
            nav.mode = Navigation.Mode.Explicit;
            nav.selectOnDown = slot.GetComponent<Selectable>();
            nav.selectOnRight = charmsTabButton;
            inventoryTabButton.navigation = nav;
        }
    }
}
