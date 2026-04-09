using UnityEngine;

public class ShopItemClickRelay : MonoBehaviour
{
    public InteriorShopManager shopManager;
    public ShopItemButton itemData;

    public void ClickItem()
    {
        if (shopManager != null && itemData != null)
        {
            shopManager.SelectItem(itemData);
        }
    }
}