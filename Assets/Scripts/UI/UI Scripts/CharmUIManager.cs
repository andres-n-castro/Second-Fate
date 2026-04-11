using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class CharmUIManager : MonoBehaviour
{
    public Transform topSlotsPanel;
    public Transform bottomInventoryPanel;
    public CharmUISlot slotPrefab;

    [Header("Right Info Panel")]
    public TextMeshProUGUI charmNameText;
    public TextMeshProUGUI charmDescriptionText;
    public Image bigIconImage;

    [Header("Charm Database")]
    public List<CharmData> allGameCharms = new List<CharmData>();

    private readonly List<CharmUISlot> equipSlots = new List<CharmUISlot>();
    private CharmUISlot selectedEquipSlot;
    private MenuTabManager menuTabManager;

    void Awake()
    {
        menuTabManager = FindFirstObjectByType<MenuTabManager>();
    }

    void OnEnable()
    {
        RefreshUI();
    }

    public void RefreshUI()
    {
        if (CharmManager.Instance == null || slotPrefab == null)
        {
            return;
        }

        CharmManager.Instance.ValidateEquippedCharms();
        selectedEquipSlot = null;
        ClearChildren(topSlotsPanel);
        ClearChildren(bottomInventoryPanel);
        equipSlots.Clear();
        List<Selectable> newTopSelectables = new List<Selectable>();
        List<Selectable> newBottomSelectables = new List<Selectable>();

        if (topSlotsPanel != null)
        {
            int maxSlots = CharmManager.Instance.GetMaxCharmSlots();

            for (int i = 0; i < maxSlots; i++)
            {
                CharmUISlot slot = Instantiate(slotPrefab, topSlotsPanel);
                slot.transform.localScale = Vector3.one;
                slot.isEquipSlot = true;

                if (i < CharmManager.Instance.equippedCharms.Count)
                {
                    slot.Setup(CharmManager.Instance.equippedCharms[i], this, true);
                }
                else
                {
                    slot.Setup(null, this, true);
                }

                equipSlots.Add(slot);
                Selectable selectable = slot.GetComponent<Selectable>();
                if (selectable != null)
                {
                    newTopSelectables.Add(selectable);
                }
            }
        }

        if (newTopSelectables.Count > 0)
        {
            if (menuTabManager == null)
            {
                menuTabManager = FindFirstObjectByType<MenuTabManager>();
            }

            if (menuTabManager != null)
            {
                menuTabManager.SetFirstCharmSlot(newTopSelectables[0].gameObject);
            }
        }

        if (bottomInventoryPanel != null)
        {
            foreach (CharmData charm in CharmManager.Instance.unlockedCharms)
            {
                CharmUISlot slot = Instantiate(slotPrefab, bottomInventoryPanel);
                slot.transform.localScale = Vector3.one;
                slot.isEquipSlot = false;
                slot.Setup(charm, this, false);
                Selectable selectable = slot.GetComponent<Selectable>();
                if (selectable != null)
                {
                    newBottomSelectables.Add(selectable);
                }
            }
        }

        if (CharmManager.Instance.unlockedCharms.Count > 0)
        {
            OnCharmHovered(CharmManager.Instance.unlockedCharms[0]);
        }
        else
        {
            ClearInfoPanel();
        }

    }

    public void SelectEquipSlot(CharmUISlot slot)
    {
        if (slot == null || !slot.isEquipSlot)
        {
            return;
        }

        if (selectedEquipSlot != null)
        {
            selectedEquipSlot.SetSelected(false);
        }

        selectedEquipSlot = slot;
        selectedEquipSlot.SetSelected(true);
    }

    public void OnCharmHovered(CharmData data)
    {
        if (data == null)
        {
            ClearInfoPanel();
            return;
        }

        if (charmNameText != null)
        {
            charmNameText.text = data.charmName;
        }

        if (charmDescriptionText != null)
        {
            charmDescriptionText.text = data.description;
        }

        if (bigIconImage != null)
        {
            bigIconImage.sprite = data.charmIcon;
            bigIconImage.enabled = data.charmIcon != null;
        }
    }

    public void ToggleCharm(CharmData clickedCharm)
    {
        if (selectedEquipSlot == null || clickedCharm == null || CharmManager.Instance == null)
        {
            return;
        }

        if (!CharmManager.Instance.unlockedCharms.Contains(clickedCharm))
        {
            return;
        }

        if (selectedEquipSlot.charmData != null)
        {
            CharmManager.Instance.UnequipCharm(selectedEquipSlot.charmData);
        }

        if (!CharmManager.Instance.EquipCharm(clickedCharm))
        {
            return;
        }

        selectedEquipSlot.SetSelected(false);
        selectedEquipSlot = null;
        RefreshUI();

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);

            if (equipSlots.Count > 0)
            {
                EventSystem.current.SetSelectedGameObject(equipSlots[0].gameObject);
            }
        }
    }

    private void ClearInfoPanel()
    {
        if (charmNameText != null)
        {
            charmNameText.text = string.Empty;
        }

        if (charmDescriptionText != null)
        {
            charmDescriptionText.text = string.Empty;
        }

        if (bigIconImage != null)
        {
            bigIconImage.sprite = null;
            bigIconImage.enabled = false;
        }
    }

    private void ClearChildren(Transform parent)
    {
        if (parent == null)
        {
            return;
        }

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
    }
}
