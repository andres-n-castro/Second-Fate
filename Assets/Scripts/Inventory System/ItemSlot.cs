using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System;

public class ItemSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, ISelectHandler, IDeselectHandler, ISubmitHandler
{
    public Image iconImage;
    public TextMeshProUGUI amountText;
    public GameObject highlightFrame;

    private Item currentItem;
    private Action<Item> onClickCallback;
    private Vector3 originalScale;

    public void Setup(ItemSlotData data, Action<Item> onClick)
    {
        currentItem = data.itemData;
        onClickCallback = onClick;

        iconImage.sprite = data.itemData.itemSprite;
        amountText.text = data.amount > 1 ? data.amount.ToString() : "";
        originalScale = transform.localScale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        transform.localScale = originalScale * 1.1f;
        if (highlightFrame != null) highlightFrame.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        transform.localScale = originalScale;
        if (highlightFrame != null) highlightFrame.SetActive(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        onClickCallback?.Invoke(currentItem);
    }

    public void OnSelect(BaseEventData eventData)
    {
        transform.localScale = originalScale * 1.1f;
        if (highlightFrame != null) highlightFrame.SetActive(true);
        onClickCallback?.Invoke(currentItem);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        transform.localScale = originalScale;
        if (highlightFrame != null) highlightFrame.SetActive(false);
    }

    public void OnSubmit(BaseEventData eventData)
    {
        onClickCallback?.Invoke(currentItem);
    }
}
