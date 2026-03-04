using System.Collections.Generic;
using UnityEngine;

public class PlayerStats : MonoBehaviour
{

    [Header("Health System")]
    public int maxHealth = 4;
    public int currentHealth;

    [Header("Powers System")]
    public bool hasDoubleJump = false;
    public bool hasDash = false;

    [Header("Charms System")]
    public List<bool> Charms;

    [Header("Currency System")]

    public int currentCurrency;

    void Start()
    {
        currentHealth = maxHealth;
    }

    void Update()
    {
        
    }

    void IncreaseCurrency()
    {
        currentCurrency += 1;
        Debug.Log("Current player currency count:" + currentCurrency);
    }

    void DecreaseCurrency(int amount)
    {
        currentCurrency -= amount;
    }

    void OnEnable()
    {
        CurrencyPickup.PickupCurrency += IncreaseCurrency;
    }

    void OnDisable()
    {
        CurrencyPickup.PickupCurrency -= IncreaseCurrency;
    }
}
