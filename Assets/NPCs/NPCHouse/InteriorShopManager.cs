using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages the shop grid panel: item selection, detail display, purchase flow.
/// Uses the real currency from PlayerManager.Instance.playerStats.currentCurrency.
/// </summary>
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
    public Button buyButton;
    public Button backButton;

    [Header("Back Navigation")]
    [Tooltip("Reference to the InteriorMenuController so the Back button can return to the main menu.")]
    public InteriorMenuController menuController;

    private ShopItemButton selectedItem;

    void Start()
    {
        if (confirmPurchasePanel != null) confirmPurchasePanel.SetActive(false);
        if (purchaseMessagePanel != null) purchaseMessagePanel.SetActive(false);

        if (yesButton != null) yesButton.onClick.AddListener(OnYesClicked);
        if (noButton != null) noButton.onClick.AddListener(OnNoClicked);
        if (okButton != null) okButton.onClick.AddListener(OnOkClicked);
        if (buyButton != null) buyButton.onClick.AddListener(OnBuyClicked);
        if (backButton != null) backButton.onClick.AddListener(OnBackClicked);

        ClearDetailPanel();
        UpdateCurrencyUI();
    }

    void OnEnable()
    {
        // Refresh currency display every time the shop panel is shown
        UpdateCurrencyUI();
        ClearDetailPanel();
        selectedItem = null;
    }

    // ------------------------------------------------------------------
    // Item Selection
    // ------------------------------------------------------------------

    public void SelectItem(ShopItemButton item)
    {
        selectedItem = item;
        UpdateDetailPanel(item);
    }

    // ------------------------------------------------------------------
    // Purchase Flow
    // ------------------------------------------------------------------

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

        int playerCurrency = GetPlayerCurrency();

        if (playerCurrency >= selectedItem.price)
        {
            // Deduct from the real currency
            SetPlayerCurrency(playerCurrency - selectedItem.price);
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

    // ------------------------------------------------------------------
    // Back Button — returns to the interaction menu
    // ------------------------------------------------------------------

    void OnBackClicked()
    {
        // Close any open sub-panels first
        if (confirmPurchasePanel != null) confirmPurchasePanel.SetActive(false);
        if (purchaseMessagePanel != null) purchaseMessagePanel.SetActive(false);

        selectedItem = null;
        ClearDetailPanel();

        if (menuController != null)
            menuController.BackToMenu();
    }

    // ------------------------------------------------------------------
    // Currency Helpers — reads/writes PlayerManager.Instance.playerStats
    // ------------------------------------------------------------------

    private int GetPlayerCurrency()
    {
        if (PlayerManager.Instance != null && PlayerManager.Instance.playerStats != null)
            return PlayerManager.Instance.playerStats.currentCurrency;

        return 0;
    }

    private void SetPlayerCurrency(int amount)
    {
        if (PlayerManager.Instance != null && PlayerManager.Instance.playerStats != null)
            PlayerManager.Instance.playerStats.currentCurrency = amount;
    }

    void UpdateCurrencyUI()
    {
        if (currencyText != null)
            currencyText.text = "Coins: " + GetPlayerCurrency();
    }

    // ------------------------------------------------------------------
    // Detail Panel
    // ------------------------------------------------------------------

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