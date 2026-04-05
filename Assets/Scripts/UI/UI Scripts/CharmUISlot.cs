using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CharmUISlot : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler, ISubmitHandler
{
    public Image iconImage;
    public CharmData charmData;
    public bool isEquipSlot;
    public Sprite defaultEmptySprite;

    private CharmUIManager owner;
    private bool isSelected;

    public void Setup(CharmData charm, CharmUIManager manager, bool equipSlot)
    {
        charmData = charm;
        owner = manager;
        isEquipSlot = equipSlot;
        RefreshIcon();
        SetSelected(false);
    }

    public void AssignCharm(CharmData charm)
    {
        charmData = charm;
        RefreshIcon();
    }

    public void SetSelected(bool isSelected)
    {
        this.isSelected = isSelected;
        transform.localScale = isSelected ? new Vector3(1.1f, 1.1f, 1.1f) : Vector3.one;
    }

    public void SetLockedVisual(bool isLocked)
    {
        if (iconImage != null)
        {
            iconImage.color = isLocked ? new Color(0.2f, 0.2f, 0.2f, 1f) : Color.white;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        transform.localScale = new Vector3(1.1f, 1.1f, 1.1f);
        owner?.OnCharmHovered(charmData);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        transform.localScale = Vector3.one;

        if (isSelected)
        {
            transform.localScale = new Vector3(1.1f, 1.1f, 1.1f);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (isEquipSlot)
        {
            owner?.SelectEquipSlot(this);
            return;
        }

        owner?.ToggleCharm(charmData);
    }

    public void OnSelect(BaseEventData eventData)
    {
        transform.localScale = new Vector3(1.1f, 1.1f, 1.1f);
        owner?.OnCharmHovered(charmData);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        transform.localScale = Vector3.one;

        if (isSelected)
        {
            transform.localScale = new Vector3(1.1f, 1.1f, 1.1f);
        }
    }

    public void OnSubmit(BaseEventData eventData)
    {
        OnClick();
    }

    public void OnClick()
    {
        if (isEquipSlot)
        {
            owner?.SelectEquipSlot(this);
            return;
        }

        owner?.ToggleCharm(charmData);
    }

    private void RefreshIcon()
    {
        if (iconImage != null)
        {
            iconImage.sprite = charmData != null ? charmData.charmIcon : defaultEmptySprite;
            iconImage.enabled = iconImage.sprite != null;
        }
    }
}
