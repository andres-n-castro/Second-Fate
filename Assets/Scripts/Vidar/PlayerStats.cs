using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class PlayerStats : MonoBehaviour
{

    [Header("Health System")]
    public int maxHealth = 4;
    public int currentHealth;
    public Health playerHealthComponent;
    public GameObject[] fullHearts;

    [Header("Powers System")]
    public bool hasDoubleJump = false;
    public bool hasDash = false;

    [Header("Charms System")]
    public List<bool> Charms;

    [Header("Currency System")]
    public TextMeshProUGUI currencyCountText;
    public int currentCurrency;


    void Start()
    {
        if (playerHealthComponent != null)
        {
            playerHealthComponent.InitializeHealth(currentHealth, maxHealth);
        }
    }

    void Update()
    {
        UpdateDisplayCurrencyCount();
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

    void UpdateDisplayCurrencyCount()
    {
        currencyCountText.text = currentCurrency.ToString();
    }

    void SyncHealthForSaving(int newCurrentHealth, int newMaxHealth)
    {
        currentHealth = newCurrentHealth;
        maxHealth = newMaxHealth;

        for (int i = 0; i < fullHearts.Length; i++)
        {
            if (fullHearts[i] != null)
            {
                fullHearts[i].SetActive(i < currentHealth);
            }
        }
    }

    void OnEnable()
    {
        CurrencyPickup.PickupCurrency += IncreaseCurrency;

        if (playerHealthComponent != null)
        {
            playerHealthComponent.OnHealthChanged += SyncHealthForSaving;
        }
    }

    void OnDisable()
    {
        CurrencyPickup.PickupCurrency -= IncreaseCurrency;

        if(playerHealthComponent != null)
        {
            playerHealthComponent.OnHealthChanged -= SyncHealthForSaving;
        }
    }
}
