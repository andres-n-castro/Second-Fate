using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(PlayerStats))]
[RequireComponent(typeof(PlayerStates))]

public class PlayerManager : MonoBehaviour
{

    public static PlayerManager Instance;
    public PlayerController playerController;
    public PlayerStats playerStats;
    public PlayerStates playerStates;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }

        Instance = this;

        playerController = GetComponent<PlayerController>();
        playerStats = GetComponent<PlayerStats>();
        playerStates = GetComponent<PlayerStates>();
    }


    public void Die()
    {
        if (playerStates.isDead) return;
        playerStates.isDead = true;
        Debug.Log("Player Died!");

        // 1. Reset Health in Stats (Hearts)
        playerStats.currentHealth = playerStats.maxHealth;
        playerStats.SyncHealthForSaving(playerStats.maxHealth, playerStats.maxHealth);

        // 2. Reset Health in the Health Component (Logic)
        if (playerStats.playerHealthComponent != null)
        {
            playerStats.playerHealthComponent.InitializeHealth(playerStats.maxHealth, playerStats.maxHealth);
        }

        // 3. Teleport
        PlayerRespawn respawnScript = GetComponent<PlayerRespawn>();
        if (respawnScript != null)
        {
            StartCoroutine(respawnScript.HandleSpikeHit());
        }
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
        Debug.Log("flag 1");
        if (playerStates.isDead) return;

        Debug.Log("flag 2");
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
