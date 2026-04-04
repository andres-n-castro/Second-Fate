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
    public int maxCharmSlots = 2;

    [Header("Currency System")]
    public TextMeshProUGUI currencyCountText;
    public int currentCurrency;


    void Start()
    {
        if (currentHealth <= 0)
            currentHealth = maxHealth;

        UpdateCharmCapacity();

        if (playerHealthComponent != null)
        {
            playerHealthComponent.InitializeHealth(currentHealth, maxHealth);
        }
    }

    void Update()
    {
        UpdateDisplayCurrencyCount();
    }

    void AddCurrency(int amount)
    {
        currentCurrency += amount;
        Debug.Log("Current player currency count:" + currentCurrency);
    }

    void DecreaseCurrency(int amount)
    {
        currentCurrency -= amount;
    }

    void UpdateDisplayCurrencyCount()
    {
        if (currencyCountText != null)
            currencyCountText.text = currentCurrency.ToString();
    }

    public void UpdateCharmCapacity()
    {
        if (GameManager.Instance != null && GameManager.Instance.globalGoodMultiplier > 1.0f)
        {
            maxCharmSlots = 3;
        }
        else
        {
            maxCharmSlots = 2;
        }
    }

    public void UpdateHeartsDisplay()
    {
        for (int i = 0; i < fullHearts.Length; i++)
        {
            if (fullHearts[i] != null)
            {
                fullHearts[i].SetActive(i < currentHealth);
            }
        }
    }

    public void SyncHealthForSaving(int newCurrentHealth, int newMaxHealth)
    {
        currentHealth = newCurrentHealth;
        maxHealth = newMaxHealth;
        UpdateHeartsDisplay();
    }

    void OnEnable()
    {
        CurrencyPickup.OnCurrencyPickedUp += AddCurrency;

        if (playerHealthComponent != null)
        {
            playerHealthComponent.OnHealthChanged += SyncHealthForSaving;
        }
    }

    void OnDisable()
    {
        CurrencyPickup.OnCurrencyPickedUp -= AddCurrency;

        if (playerHealthComponent != null)
        {
            playerHealthComponent.OnHealthChanged -= SyncHealthForSaving;
        }
    }
}
