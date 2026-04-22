using UnityEngine;

public class CurrencyPickup : MonoBehaviour
{
    public int currencyValue = 2;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            if (PlayerManager.Instance != null && PlayerManager.Instance.playerStats != null)
            {
                PlayerManager.Instance.playerStats.currentCurrency += currencyValue;

                if (UIManager.Instance != null)
                {
                    UIManager.Instance.ShowNotification("Item added to inventory");
                }

                Destroy(gameObject);
            }
        }
    }
}
