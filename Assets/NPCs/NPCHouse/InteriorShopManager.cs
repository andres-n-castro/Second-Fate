using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InteriorShopManager : MonoBehaviour
{
    [Header("Panels")]
    public GameObject shopPanel;
    public GameObject confirmPurchasePanel;
    public GameObject purchaseMessagePanel;
    public GameObject detailPanel;

    [Header("Text")]
    public TMP_Text confirmText;
    public TMP_Text purchaseMessageText;
    public TMP_Text currencyText;

    [Header("Detail Panel UI")]
    public TMP_Text detailNameText;
    public TMP_Text detailDescriptionText;
    public TMP_Text detailPriceText;
    public Image detailImage;

    [Header("Buttons")]
    public Button yesButton;
    public Button noButton;
    public Button okButton;
    public Button exitButton;
    public Button buyButton;

    [Header("Placeholder Currency")]
    public int currentCurrency = 200;

    private ShopItemButton selectedItem;

    void Start()
    {
        if (confirmPurchasePanel != null) confirmPurchasePanel.SetActive(false);
        if (purchaseMessagePanel != null) purchaseMessagePanel.SetActive(false);

        if (yesButton != null) yesButton.onClick.AddListener(OnYesClicked);
        if (noButton != null) noButton.onClick.AddListener(OnNoClicked);
        if (okButton != null) okButton.onClick.AddListener(OnOkClicked);
        if (exitButton != null) exitButton.onClick.AddListener(CloseShop);
        if (buyButton != null) buyButton.onClick.AddListener(OnBuyClicked);

        ClearDetailPanel();
        UpdateCurrencyUI();
    }

    public void SelectItem(ShopItemButton item)
    {
        selectedItem = item;
        UpdateDetailPanel(item);
    }

    void OnBuyClicked()
    {
        if (selectedItem == null)
            return;

        if (confirmPurchasePanel != null)
            confirmPurchasePanel.SetActive(true);

        if (confirmText != null)
            confirmText.text = "Purchase " + selectedItem.itemName + " for " + selectedItem.price + " coins?";
    }

    void OnYesClicked()
    {
        if (selectedItem == null)
            return;

        if (confirmPurchasePanel != null)
            confirmPurchasePanel.SetActive(false);

        if (currentCurrency >= selectedItem.price)
        {
            currentCurrency -= selectedItem.price;
            UpdateCurrencyUI();

            if (purchaseMessageText != null)
                purchaseMessageText.text = "Purchase successful!";

            if (purchaseMessagePanel != null)
                purchaseMessagePanel.SetActive(true);

            selectedItem.gameObject.SetActive(false);
            selectedItem = null;
            ClearDetailPanel();
        }
        else
        {
            if (purchaseMessageText != null)
                purchaseMessageText.text = "You do not have enough currency.";

            if (purchaseMessagePanel != null)
                purchaseMessagePanel.SetActive(true);
        }
    }

    void OnNoClicked()
    {
        if (confirmPurchasePanel != null)
            confirmPurchasePanel.SetActive(false);
    }

    void OnOkClicked()
    {
        if (purchaseMessagePanel != null)
            purchaseMessagePanel.SetActive(false);
    }

    void CloseShop()
    {
        if (confirmPurchasePanel != null) confirmPurchasePanel.SetActive(false);
        if (purchaseMessagePanel != null) purchaseMessagePanel.SetActive(false);
        if (shopPanel != null) shopPanel.SetActive(false);

        selectedItem = null;
        ClearDetailPanel();
    }

    void UpdateCurrencyUI()
    {
        if (currencyText != null)
            currencyText.text = "Coins: " + currentCurrency;
    }

    void UpdateDetailPanel(ShopItemButton item)
    {
        if (detailPanel != null)
            detailPanel.SetActive(true);

        if (detailNameText != null)
            detailNameText.text = item.itemName;

        if (detailDescriptionText != null)
            detailDescriptionText.text = item.description;

        if (detailPriceText != null)
            detailPriceText.text = "Price: " + item.price;

        if (detailImage != null)
        {
            detailImage.sprite = item.itemSprite;
            detailImage.enabled = item.itemSprite != null;
        }
    }

    void ClearDetailPanel()
    {
        if (detailNameText != null)
            detailNameText.text = "Select a Charm";

        if (detailDescriptionText != null)
            detailDescriptionText.text = "Choose a charm on the left to see its details.";

        if (detailPriceText != null)
            detailPriceText.text = "Price:";

        if (detailImage != null)
        {
            detailImage.sprite = null;
            detailImage.enabled = false;
        }
    }
}