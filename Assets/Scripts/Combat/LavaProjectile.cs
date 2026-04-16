using UnityEngine;

/// <summary>
/// Physics-based lava projectile. Used by Surtr for lava sweep, eruption, and vomit.
///
/// Setup (on prefab):
///   - Rigidbody2D (gravityScale = 0 for ground-traveling, 1 for arcing).
///   - Collider2D (CircleCollider2D recommended), NOT trigger.
///   - Set targetLayers to the Player layer.
///   - Damage and knockback configured in inspector.
///   - Optionally assign a lavaHazardPrefab to leave a ground puddle on impact.
///
/// The projectile damages the first IDamageable it hits, then destroys itself.
/// Non-target collisions (ground, walls) also destroy the projectile.
/// If lavaHazardPrefab is assigned, spawns a LavaHazard at impact point.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class LavaProjectile : MonoBehaviour
{
    [Header("Damage")]
    [SerializeField] private int damage = 1;
    [SerializeField] private Vector2 knockbackForce = new Vector2(6f, 4f);
    [SerializeField] private LayerMask targetLayers;

    [Header("Lifetime")]
    [SerializeField] private float maxLifetime = 5f;

    [Header("Hazard On Impact")]
    [SerializeField] private GameObject lavaHazardPrefab;

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

        // Spawn hazard at impact point if configured
        if (lavaHazardPrefab != null)
        {
            Vector2 impactPos = collision.contacts.Length > 0
                ? collision.contacts[0].point
                : (Vector2)transform.position;
            Object.Instantiate(lavaHazardPrefab, impactPos, Quaternion.identity);
        }

        Destroy(gameObject);
    }
}
