using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ShopItemButton : MonoBehaviour
{
    [Header("Item Data")]
    public string itemName;
    [TextArea(2, 5)] public string description;
    public int price;
    public Sprite itemSprite;

    [Header("Optional")]
    public Button button;
}