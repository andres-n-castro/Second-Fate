using UnityEngine;

/// <summary>
/// Concentrated beam/projectile fired from a sword swing.
/// Travels in a straight line toward the player's position at fire time.
/// Unlike SlashProjectile (floor wave / trigger), this uses collision-based
/// damage and is not ground-constrained.
///
/// Setup (on prefab):
///   - Rigidbody2D with gravityScale = 0.
///   - Collider2D (BoxCollider2D or CapsuleCollider2D), NOT trigger.
///   - Set targetLayers to the Player layer.
///   - Damage and knockback configured in inspector.
///
/// Destroys on any collision (ground, wall, or target).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class SwordBeamProjectile : MonoBehaviour
{
    [Header("Damage")]
    [SerializeField] private int damage = 2;
    [SerializeField] private Vector2 knockbackForce = new Vector2(8f, 4f);
    [SerializeField] private LayerMask targetLayers;

    [Header("Lifetime")]
    [SerializeField] private float maxLifetime = 4f;

    private Rigidbody2D rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    /// <summary>
    /// Set velocity toward target and ignore spawner colliders.
    /// </summary>
    public void Initialize(Vector2 velocity, Collider2D[] spawnersToIgnore,
        int overrideDamage = -1)
    {
        rb.linearVelocity = velocity;

        if (overrideDamage > 0) damage = overrideDamage;

        Collider2D myCol = GetComponent<Collider2D>();
        if (spawnersToIgnore != null && myCol != null)
        {
            for (int i = 0; i < spawnersToIgnore.Length; i++)
            {
                Physics2D.IgnoreCollision(myCol, spawnersToIgnore[i]);
            }
        }

        Destroy(gameObject, maxLifetime);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if ((targetLayers.value & (1 << collision.gameObject.layer)) != 0)
        {
            IDamageable target = collision.collider.GetComponent<IDamageable>();
            if (target == null)
                target = collision.collider.GetComponentInParent<IDamageable>();

            if (target != null)
            {
                Vector2 direction = (collision.transform.position - transform.position).normalized;
                Vector2 kb = new Vector2(direction.x * knockbackForce.x, knockbackForce.y);
                target.TakeDamage(damage, kb);
            }
        }

        Destroy(gameObject);
    }
}
