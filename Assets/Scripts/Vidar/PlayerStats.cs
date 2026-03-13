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
        if (playerHealthComponent == null)
        {
            playerHealthComponent = GetComponent<Health>();
        }

        if (playerHealthComponent != null)
        {
            // 2. Clear any old links to avoid double-firing
            playerHealthComponent.OnHealthChanged -= SyncHealthForSaving;
            // 3. Create the fresh link
            playerHealthComponent.OnHealthChanged += SyncHealthForSaving;

            Debug.Log("<color=green>PlayerStats:</color> Successfully linked to Health script in Start.");

            // 4. Set the initial UI state
            SyncHealthForSaving(currentHealth, maxHealth);
        }
    }

    void Update()
    {
        UpdateDisplayCurrencyCount();
        if (Input.GetKeyDown(KeyCode.T))
        {
            SyncHealthForSaving(currentHealth - 1, maxHealth);
        }
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

    public void SyncHealthForSaving(int newCurrentHealth, int newMaxHealth)
    {
        Debug.Log($"<color=cyan>UI UPDATE TRIGGERED:</color> Hearts remaining: {newCurrentHealth}");
        currentHealth = newCurrentHealth;
        maxHealth = newMaxHealth;

        Debug.Log($"UI Sync: Setting {currentHealth} hearts active out of {fullHearts.Length}");

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
            Debug.Log("<color=green>SUCCESS:</color> PlayerStats is now listening to Health.cs");
        }
        else
        {
            // If you see this in the console, you forgot to drag the script into the Inspector slot!
            Debug.LogError("<color=red>FAILURE:</color> playerHealthComponent is NULL on PlayerStats!");
        }
    }

    void OnDisable()
    {
        CurrencyPickup.PickupCurrency -= IncreaseCurrency;

        if (playerHealthComponent != null)
        {
            playerHealthComponent.OnHealthChanged -= SyncHealthForSaving;
        }
    }
}
