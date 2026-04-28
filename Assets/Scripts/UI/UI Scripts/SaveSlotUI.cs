using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SaveSlotUI : MonoBehaviour
{
    public int slotIndex;
    public TextMeshProUGUI slotTitleText;
    public TextMeshProUGUI slotDataText;
    public Button slotButton;

    private SaveLoadMenuController menuController;
    public bool hasData { get; private set; }

    public void Initialize(int index, SaveLoadMenuController controller)
    {
        slotIndex = index;
        menuController = controller;
        CacheReferences();

        if (slotTitleText != null && slotTitleText != slotDataText)
        {
            slotTitleText.text = "Save Slot " + (index + 1);
        }

        if (slotButton != null)
        {
            slotButton.onClick.RemoveListener(OnSlotClicked);
            slotButton.onClick.AddListener(OnSlotClicked);
        }

        RefreshSlotData();
    }

    public void RefreshSlotData()
    {
        hasData = SaveManager.DoesSaveFileExist(slotIndex);

        if (slotDataText == null)
        {
            return;
        }

        slotDataText.text = hasData
            ? "Slot " + (slotIndex + 1)
            : "Empty Slot";
    }

    private void OnSlotClicked()
    {
        if (menuController != null)
        {
            menuController.HandleSlotClicked(this);
        }
    }

    private void CacheReferences()
    {
        if (slotButton == null)
        {
            slotButton = GetComponentInChildren<Button>(true);
        }

        if (slotTitleText != null && slotDataText != null)
        {
            return;
        }

        TextMeshProUGUI[] texts = GetComponentsInChildren<TextMeshProUGUI>(true);
        if (texts.Length == 1)
        {
            if (slotDataText == null)
            {
                slotDataText = texts[0];
            }

            return;
        }

        if (texts.Length > 0 && slotTitleText == null)
        {
            slotTitleText = texts[0];
        }

        if (texts.Length > 1 && slotDataText == null)
        {
            slotDataText = texts[1];
        }
    }
}
