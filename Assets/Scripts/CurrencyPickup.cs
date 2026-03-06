using System;
using UnityEngine;

public class CurrencyPickup : MonoBehaviour
{
    public static event Action PickupCurrency;

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            PickupCurrency?.Invoke();
            Destroy(gameObject);
            Debug.Log("player picked up currency!");
        }
    }
}
