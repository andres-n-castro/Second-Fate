using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(PlayerStats))]
[RequireComponent(typeof(PlayerStates))]

public class PlayerManager : MonoBehaviour, IDamageable
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
    }

    void Start()
    {
        playerController = GetComponent<PlayerController>();
        playerStats = GetComponent<PlayerStats>();
        playerStates = GetComponent<PlayerStates>();

        Health hp = GetComponent<Health>();
        if (hp != null)
        {
            // This tells the Health script: "When you die, run my Die() function"
            hp.OnDeath += Die;
        }
    }

    public void TakeDamage(int damage, Vector2 knockbackForce)
    {
        if (playerStates.isDead || playerStates.isInvincible) return;

        playerStats.currentHealth -= damage;
        Debug.Log($"Player hit! Masks left: {playerStats.currentHealth}");
        Debug.Log($"Hitbox hit: damage={damage}, knockback={knockbackForce}");

        playerStats.SyncHealthForSaving(playerStats.currentHealth, playerStats.maxHealth);

        if (playerStats.currentHealth <= 0)
        {
            //call function to trigger death hit effect
            playerController.KnockBack(knockbackForce, 0.25f);
            Die();
        }

        else
        {
            playerController.KnockBack(knockbackForce, 0.25f);
            StartCoroutine(IFrameSubRoutine(playerStates.invincibilityTimer));
        }
    }

    public void Die()
    {
        // 1. Check if we're already dead to prevent loops
        if (playerStates.isDead) return;
        playerStates.isDead = true;

        Debug.Log("PLAYER MANAGER: Die() confirmed. Resetting...");

        // 2. Reset the hearts in PlayerStats
        playerStats.currentHealth = playerStats.maxHealth;

        // 3. Force the Heart UI objects to turn back on
        playerStats.SyncHealthForSaving(playerStats.maxHealth, playerStats.maxHealth);

        // 4. Reset the Health script internal numbers so IsDead becomes false
        Health hp = GetComponent<Health>();
        if (hp != null)
        {
            hp.InitializeHealth(playerStats.maxHealth, playerStats.maxHealth);
        }

        // 5. Run the Teleport
        PlayerRespawn respawnScript = GetComponent<PlayerRespawn>();
        if (respawnScript != null)
        {
            StartCoroutine(respawnScript.HandleSpikeHit());
        }
    }

    public IEnumerator IFrameSubRoutine(float iFrameTimer)
    {
        playerStates.isInvincible = true;
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

    }

}
