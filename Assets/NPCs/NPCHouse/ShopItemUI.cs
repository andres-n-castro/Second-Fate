using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ShopItemUI : MonoBehaviour
{
    public CharmData charmToSell;
    public Button buyButton;
    public TMP_Text priceText;

    private void Start()
    {
        if (buyButton == null)
        {
            buyButton = GetComponent<Button>();
        }

        if (buyButton != null)
        {
            buyButton.onClick.AddListener(AttemptPurchase);
        }

        RefreshUI();
    }

    private void OnEnable()
    {
        RefreshUI();
    }

    private void RefreshUI()
    {
        if (priceText != null && charmToSell != null)
        {
            priceText.text = charmToSell.charmCost.ToString();
        }

        if (charmToSell != null &&
            CharmManager.Instance != null &&
            CharmManager.Instance.unlockedCharms.Contains(charmToSell))
        {
            gameObject.SetActive(false);
        }
    }

    private void AttemptPurchase()
    {
        if (charmToSell == null)
        {
            return;
        }

        if (PlayerManager.Instance != null &&
            PlayerManager.Instance.playerStats != null &&
            PlayerManager.Instance.playerStats.SpendCurrency(charmToSell.charmCost))
        {
            if (CharmManager.Instance != null && !CharmManager.Instance.unlockedCharms.Contains(charmToSell))
            {
                CharmManager.Instance.unlockedCharms.Add(charmToSell);
            }

            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowNotification("Purchased: " + charmToSell.charmName);
            }

            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.SaveGame(0);
            }

            gameObject.SetActive(false);
        }
        else if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowNotification("Not enough currency!");
        }
    }
}
