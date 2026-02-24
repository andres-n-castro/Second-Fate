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

    private void Awake()
    {
        currentHealth = maxHealth;
        rb = GetComponent<Rigidbody2D>();
    }

    public void TakeDamage(int damage, Vector2 knockbackForce)
    {
        if (IsDead) return;

        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0);

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnDamageTaken?.Invoke(damage, knockbackForce);

        // Apply knockback if rigidbody exists (unless handled externally)
        if (!handleKnockbackExternally && rb != null && knockbackForce != Vector2.zero)
        {
            rb.AddForce(knockbackForce, ForceMode2D.Impulse);
            //Debug.Log($"Health AddForce knockback: {knockbackForce}");
        }

        if (currentHealth <= 0)
        {
            Debug.Log($"{gameObject.name} has died");
            OnDeath?.Invoke();
        }
    }

    public void Heal(int amount)
    {
        if (IsDead) return;
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }
}
