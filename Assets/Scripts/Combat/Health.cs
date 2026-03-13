using UnityEngine;
using System;

/// <summary>
/// Manages health for any entity (player, enemy, boss).
/// Implements IDamageable so AttackHitbox can deal damage generically.
/// Subscribe to OnHealthChanged / OnDeath for UI or behavior hooks.
/// </summary>
public class Health : MonoBehaviour, IDamageable
{
    [Header("Health Settings")]
    [SerializeField] private int maxHealth = 5;

    private int currentHealth;
    private Rigidbody2D rb;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsDead => currentHealth <= 0;
    public float HealthPercent => (float)currentHealth / maxHealth;

    /// <summary> Fires (currentHealth, maxHealth) whenever HP changes. </summary>
    public event Action<int, int> OnHealthChanged;

    /// <summary> Fires once when health reaches zero. </summary>
    public event Action OnDeath;

    /// <summary> Fires (damage, knockbackForce) on every hit. Lets entities handle knockback themselves. </summary>
    public event Action<int, Vector2> OnDamageTaken;

    /// <summary>
    /// When true, TakeDamage skips built-in knockback.
    /// Set by entities that handle knockback in their OnDamageTaken handler.
    /// </summary>
    [HideInInspector] public bool handleKnockbackExternally;
    [HideInInspector] public bool isInvulnerable;

    private void Awake()
    {
        currentHealth = maxHealth;
        rb = GetComponent<Rigidbody2D>();
    }

    public void TakeDamage(int damage, Vector2 knockbackForce)
    {
        if (IsDead || isInvulnerable) return;

        currentHealth -= damage;
        Debug.Log($"{gameObject.name} took {damage} damage! HP left: {currentHealth}");
        Debug.Log($"<color=orange>DAMAGE DETECTED:</color> {gameObject.name} was hit. HP is now {currentHealth}. My Instance ID is: {this.GetInstanceID()}");
        currentHealth = Mathf.Max(currentHealth, 0);

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnDamageTaken?.Invoke(damage, knockbackForce);

        // Apply knockback if rigidbody exists (unless handled externally)
        if (!handleKnockbackExternally && rb != null && knockbackForce != Vector2.zero)
        {
            rb.AddForce(knockbackForce, ForceMode2D.Impulse);
        }

        if (currentHealth <= 0)
        {
            Debug.Log(gameObject.name + " has reached 0 HP in Health.cs");

            // 1. Try the Instance first (The most reliable way)
            if (PlayerManager.Instance != null)
            {
                Debug.Log("Found PlayerManager via Instance! Calling Die()...");
                PlayerManager.Instance.Die();
            }
            // 2. Fallback: Search the object hierarchy
            else if (GetComponentInParent<PlayerManager>() != null)
            {
                GetComponentInParent<PlayerManager>().Die();
            }
            else
            {
                Debug.LogWarning("CRITICAL: Could not find PlayerManager anywhere!");
                OnDeath?.Invoke();
            }
        }
    }

    public void Heal(int amount)
    {
        if (IsDead) return;
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void InitializeHealth(int savedCurrentHealth, int savedMaxHealth)
    {
        maxHealth = savedMaxHealth;
        currentHealth = savedCurrentHealth;

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }
}
