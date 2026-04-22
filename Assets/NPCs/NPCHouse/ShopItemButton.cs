using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ShopItemButton : MonoBehaviour
{
    [Header("Item Data")]
    public CharmData charmData;
    public string itemName;
    [TextArea(2, 5)] public string description;
    public int price;
    public Sprite itemSprite;

    [Header("Optional")]
    public Button button;
    public TMP_Text priceText;

    public string DisplayName => charmData != null && !string.IsNullOrWhiteSpace(charmData.charmName) ? charmData.charmName : itemName;
    public string DisplayDescription => charmData != null && !string.IsNullOrWhiteSpace(charmData.description) ? charmData.description : description;
    public int DisplayPrice => charmData != null ? charmData.charmCost : price;
    public Sprite DisplaySprite => charmData != null && charmData.charmIcon != null ? charmData.charmIcon : itemSprite;

    private void Awake()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }
    }

    private void OnEnable()
    {
        RefreshVisualState();
    }

    public bool IsOwned()
    {
        return charmData != null &&
               CharmManager.Instance != null &&
               CharmManager.Instance.unlockedCharms.Contains(charmData);
    }

    public void RefreshVisualState()
    {
        if (priceText != null)
        {
            priceText.text = DisplayPrice.ToString();
        }

        if (button != null)
        {
            button.interactable = !IsOwned();
        }
    }
}