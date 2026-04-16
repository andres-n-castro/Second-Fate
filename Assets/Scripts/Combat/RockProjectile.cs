using UnityEngine;

/// <summary>
/// Physics-based rock projectile. Spawned by RockGolem during RockGolemThrowState.
///
/// Setup (on prefab):
///   - Rigidbody2D with gravityScale = 1 (natural arc).
///   - Collider2D (CircleCollider2D recommended), NOT trigger.
///   - Set targetLayers to the Player layer.
///   - Damage and knockback configured in inspector.
///
/// The projectile damages the first IDamageable it hits, then destroys itself.
/// Non-target collisions (ground, walls) also destroy the projectile.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class RockProjectile : MonoBehaviour, IDamageable
{
    [Header("Damage")]
    [SerializeField] private int damage = 1;
    [SerializeField] private Vector2 knockbackForce = new Vector2(6f, 4f);
    [SerializeField] private LayerMask targetLayers;

    [Header("Lifetime")]
    [SerializeField] private float maxLifetime = 5f;

    [Header("Gravity")]
    [SerializeField] private float maxFallVelocity = 28f;

    private Rigidbody2D rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    /// <summary>
    /// Set initial velocity and ignore collision with the spawner's colliders.
    /// </summary>
    public void Initialize(Vector2 velocity, Collider2D[] spawnersToIgnore)
    {
        rb.linearVelocity = velocity;

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

    private void FixedUpdate()
    {
        if (rb.linearVelocity.y < 0f)
        {
            rb.linearVelocity = new Vector2(
                rb.linearVelocity.x,
                Mathf.Max(rb.linearVelocity.y, -maxFallVelocity));
        }
    }

    public void TakeDamage(int damage, Vector2 knockbackForce)
    {
        Destroy(gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Check if we hit a valid target
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

        // Destroy on any collision (ground, wall, or target)
        Destroy(gameObject);
    }
}
