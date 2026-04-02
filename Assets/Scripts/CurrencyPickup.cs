using System;
using UnityEngine;

public class CurrencyPickup : MonoBehaviour
{
    public int currencyAmount = 1;
    public static event Action<int> OnCurrencyPickedUp;

    private void OnTriggerEnter2D(Collider2D other)
    {
        CheckPickup(other.gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        CheckPickup(collision.gameObject);
    }

    private void CheckPickup(GameObject otherObject)
    {
        if (otherObject.CompareTag("Player"))
        {
            OnCurrencyPickedUp?.Invoke(currencyAmount);
            Destroy(gameObject);
        }
    }
}
