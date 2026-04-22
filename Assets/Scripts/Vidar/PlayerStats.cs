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

    [Header("Unlockable Abilities")]
    public bool canDash = false;
    public bool unlockedDoubleJump = true; // TODO: Tie to Boss Defeat later

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

    public bool SpendCurrency(int amount)
    {
        if (amount < 0)
        {
            return false;
        }

        if (currentCurrency >= amount)
        {
            currentCurrency -= amount;
            return true;
        }

        return false;
    }

    void UpdateDisplayCurrencyCount()
    {
        if (currencyCountText != null)
            currencyCountText.text = currentCurrency.ToString();
    }

    public void UpdateCharmCapacity()
    {
        maxCharmSlots = 2;
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
        if (playerHealthComponent != null)
        {
            playerHealthComponent.OnHealthChanged += SyncHealthForSaving;
        }
    }

    void OnDisable()
    {
        if (playerHealthComponent != null)
        {
            playerHealthComponent.OnHealthChanged -= SyncHealthForSaving;
        }
    }
}
