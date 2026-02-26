using UnityEngine;

/// <summary>
/// Interface for any object that can take damage.
/// Implement on player, enemies, destructibles, etc.
/// </summary>
public interface IDamageable
{
    void TakeDamage(int damage, Vector2 knockbackForce);
}
