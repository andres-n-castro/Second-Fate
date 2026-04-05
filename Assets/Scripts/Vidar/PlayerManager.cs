using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(PlayerStats))]
[RequireComponent(typeof(PlayerStates))]
[RequireComponent(typeof(CharmManager))]

public class PlayerManager : MonoBehaviour
{
    private const int MaxProtectionHits = 2;

    public static PlayerManager Instance;
    public PlayerController playerController;
    public PlayerMovement playerMovement;
    public PlayerStats playerStats;
    public PlayerStates playerStates;
    public int protectionHitsRemaining = MaxProtectionHits;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }

        if (GetComponent<CharmManager>() == null)
        {
            gameObject.AddComponent<CharmManager>();
        }

        Instance = this;

        playerController = GetComponent<PlayerController>();
        playerMovement = GetComponent<PlayerMovement>();
        playerStats = GetComponent<PlayerStats>();
        playerStates = GetComponent<PlayerStates>();
    }


    public void Die()
    {
        if (playerStates.isDead) return;
        playerStates.isDead = true;
        Debug.Log("Player Died!");

        if (GameManager.Instance != null)
        {
            GameManager.Instance.HandlePlayerDeath();
            return;
        }

        // Fallback for scenes without a GameManager instance.
        StartCoroutine(HandleLocalRespawn());
    }

    private IEnumerator HandleLocalRespawn()
    {
        playerStats.currentHealth = playerStats.maxHealth;
        playerStats.SyncHealthForSaving(playerStats.maxHealth, playerStats.maxHealth);

        if (playerStats.playerHealthComponent != null)
        {
            playerStats.playerHealthComponent.InitializeHealth(playerStats.maxHealth, playerStats.maxHealth);
        }

        PlayerRespawn respawnScript = GetComponent<PlayerRespawn>();
        if (respawnScript != null)
        {
            yield return StartCoroutine(respawnScript.HandleSpikeHit());
        }

        ResetProtectionCharmCharges();
        playerStates.isDead = false;
    }

    public void ResetProtectionCharmCharges()
    {
        protectionHitsRemaining = MaxProtectionHits;
    }

    public IEnumerator IFrameSubRoutine(float iFrameTimer)
    {
        playerStates.isInvincible = true;

        // NEW: Tell Health to block damage
        if (playerStats.playerHealthComponent != null)
            playerStats.playerHealthComponent.isInvulnerable = true;

        float timer = 0f;
        float flashDelay = 0.1f;

        while (timer < iFrameTimer)
        {
            playerController.spriteRenderer.enabled = !playerController.spriteRenderer.enabled;
            yield return new WaitForSeconds(flashDelay);
            timer += flashDelay;
        }

        playerController.spriteRenderer.enabled = true;
        playerStates.isInvincible = false;

        // NEW: Tell Health it can take damage again
        if (playerStats.playerHealthComponent != null)
            playerStats.playerHealthComponent.isInvulnerable = false;


    }


    // This replaces your old TakeDamage function
    private void HandleDamage(int damage, Vector2 knockbackForce)
    {
        if (playerStates.isDead) return;

        if (CharmManager.Instance != null &&
            CharmManager.Instance.IsCharmEquipped("Protection") &&
            protectionHitsRemaining > 0)
        {
            protectionHitsRemaining--;

            int restoredHealth = Mathf.Min(playerStats.currentHealth + damage, playerStats.maxHealth);
            playerStats.SyncHealthForSaving(restoredHealth, playerStats.maxHealth);

            if (playerStats.playerHealthComponent != null)
            {
                playerStats.playerHealthComponent.InitializeHealth(restoredHealth, playerStats.maxHealth);
            }

            StartCoroutine(IFrameSubRoutine(playerStates.invincibilityTimer));
            return;
        }

        // Trigger your specific player reactions
        PlayerController.Instance.TriggerHitStop(0.1f);
        playerController.KnockBack(knockbackForce, 0.25f);
        StartCoroutine(IFrameSubRoutine(playerStates.invincibilityTimer));

        // (Note: Health.cs already checks for death and updates the UI math)
    }


    void OnEnable()
    {
        // Listen for when Health takes damage
        if (playerStats.playerHealthComponent != null)
        {
            playerStats.playerHealthComponent.OnDamageTaken += HandleDamage;
            playerStats.playerHealthComponent.OnDeath += Die;
        }
    }

    void OnDisable()
    {
        if (playerStats.playerHealthComponent != null)
        {
            playerStats.playerHealthComponent.OnDamageTaken -= HandleDamage;
            playerStats.playerHealthComponent.OnDeath -= Die;
        }
    }
}
