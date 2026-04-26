using UnityEngine;

/// <summary>
/// Horizontal slash wave projectile. Travels forward at a fixed height
/// so the player must jump over it to dodge.
///
/// Setup (on prefab):
///   - Rigidbody2D with gravityScale = 0, constraints freeze Y position.
///   - Collider2D (NOT trigger) tall enough to require a jump.
///   - Set targetLayers to the Player layer.
///   - Damage and knockback configured in inspector.
///
/// Destroys on collision with anything (like OdinProjectile).
/// Deals damage + knockback to entities on targetLayers.
/// Spawner colliders are ignored via Physics2D.IgnoreCollision.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class SlashProjectile : MonoBehaviour
{
    [Header("Damage")]
    [SerializeField] private int damage = 2;
    [SerializeField] private Vector2 knockbackForce = new Vector2(8f, 4f);
    [SerializeField] private LayerMask targetLayers;

    [Header("Lifetime")]
    [SerializeField] private float maxLifetime = 4f;

    private Rigidbody2D rb;
    private bool hasHit;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    /// <summary>
    /// Set horizontal velocity and ignore spawner colliders.
    /// </summary>
    public void Initialize(Vector2 velocity, Collider2D[] spawnersToIgnore,
        int overrideDamage = -1)
    {
        rb.linearVelocity = velocity;

        // Flip sprite to face movement direction
        if (velocity.x < 0f)
            transform.localScale = new Vector3(-Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);

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

    public void SetTargetLayers(LayerMask layers) { targetLayers = layers; }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Ignore collisions with other slash projectiles
        if (collision.gameObject.GetComponent<SlashProjectile>() != null) return;

        // Damage target on matching layer
        if (!hasHit && (targetLayers.value & (1 << collision.gameObject.layer)) != 0)
        {
            IDamageable target = collision.collider.GetComponent<IDamageable>();
            if (target == null)
                target = collision.collider.GetComponentInParent<IDamageable>();

            if (target != null)
            {
                hasHit = true;
                Vector2 kb = new Vector2(Mathf.Sign(rb.linearVelocity.x) * knockbackForce.x, knockbackForce.y);
                target.TakeDamage(damage, kb);
            }
        }

        // Always destroy on collision (spawner collisions already ignored)
        Destroy(gameObject);
    }
}
