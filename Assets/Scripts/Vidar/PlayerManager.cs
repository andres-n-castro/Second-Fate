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
        if(Instance != null && Instance != this)
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
    }

    public void TakeDamage(int damage, Vector2 knockbackForce)
    {
        if (playerStates.isDead || playerStates.isInvincible) return;

        playerStats.currentHealth -= damage;
        Debug.Log($"Player hit! Masks left: {playerStats.currentHealth}");
        Debug.Log($"Hitbox hit: damage={damage}, knockback={knockbackForce}");

        if(playerStats.currentHealth <= 0)
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
        playerStates.isDead = true;
        Debug.Log("Player Died!");
        //shout to the game manager to reset the game
    }

    public IEnumerator IFrameSubRoutine(float iFrameTimer)
    {
        playerStates.isInvincible = true;
        float timer = 0f;
        float flashDelay = 0.1f;

        while(timer < iFrameTimer)
        {
            playerController.spriteRenderer.enabled = !playerController.spriteRenderer.enabled;
            yield return new WaitForSeconds(flashDelay);
            timer += flashDelay;
        }
        playerController.spriteRenderer.enabled = true;
        playerStates.isInvincible = false;

    }
    
}
