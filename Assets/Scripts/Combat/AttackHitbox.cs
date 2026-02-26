using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Reusable hitbox component. Attach to a child GameObject with a trigger Collider2D.
///
/// Usage:
///   - Call Activate() to enable the hitbox (clears per-activation hit tracking).
///   - Call Deactivate() to disable it.
///   - On trigger enter, damages the first IDamageable found on the target.
///   - Per-activation tracking prevents the same target from being hit twice
///     in a single activation window (no unintended per-frame shredding).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class AttackHitbox : MonoBehaviour
{
    [Header("Damage Settings")]
    [SerializeField] private int damage = 1;
    [SerializeField] private Vector2 knockbackForce = new Vector2(5f, 2f);
    [SerializeField] private LayerMask targetLayers;

    private HashSet<IDamageable> hitTargets = new HashSet<IDamageable>();
    private Collider2D col;

    private void Awake()
    {
        col = GetComponent<Collider2D>();
        col.isTrigger = true;
        Deactivate();
    }

    /// <summary>
    /// Enable the hitbox and clear hit tracking for a new attack window.
    /// </summary>
    public void Activate()
    {
        hitTargets.Clear();
        col.enabled = true;
    }

    /// <summary>
    /// Disable the hitbox.
    /// </summary>
    public void Deactivate()
    {
        col.enabled = false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Layer mask check
        if ((targetLayers.value & (1 << other.gameObject.layer)) == 0) return;

        // Find IDamageable on the collider or its parent
        IDamageable target = other.GetComponent<IDamageable>();
        if (target == null)
            target = other.GetComponentInParent<IDamageable>();

        if (target == null || hitTargets.Contains(target)) return;

        hitTargets.Add(target);

        // Knockback direction: from hitbox toward the target
        Vector2 direction = (other.transform.position - transform.position).normalized;
        Vector2 kb = new Vector2(direction.x * knockbackForce.x, knockbackForce.y);

        target.TakeDamage(damage, kb);
        //Debug.Log($"Hitbox hit: damage={damage}, knockback={kb}");
    }
}
