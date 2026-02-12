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

        // Apply knockback if rigidbody exists
        if (rb != null && knockbackForce != Vector2.zero)
        {
            rb.AddForce(knockbackForce, ForceMode2D.Impulse);
        }

        if (currentHealth <= 0)
        {
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
