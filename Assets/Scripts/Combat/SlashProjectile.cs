using UnityEngine;

/// <summary>
/// Horizontal slash wave projectile. Travels forward at a fixed height
/// so the player must jump over it to dodge.
///
/// Setup (on prefab):
///   - Rigidbody2D with gravityScale = 0, constraints freeze Y position.
///   - Collider2D set as trigger (tall enough to require a jump, but not full screen).
///   - Set targetLayers to the Player layer.
///   - Damage and knockback configured in inspector.
///
/// Destroys on hitting a wall (groundLayers) or after maxLifetime.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class SlashProjectile : MonoBehaviour
{
    [Header("Damage")]
    [SerializeField] private int damage = 2;
    [SerializeField] private Vector2 knockbackForce = new Vector2(8f, 4f);
    [SerializeField] private LayerMask targetLayers;

    [Header("Environment")]
    [SerializeField] private LayerMask groundLayers;

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

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Destroy on wall/ground
        if ((groundLayers.value & (1 << other.gameObject.layer)) != 0)
        {
            Destroy(gameObject);
            return;
        }

        // Damage target
        if (hasHit) return;
        if ((targetLayers.value & (1 << other.gameObject.layer)) == 0) return;

        IDamageable target = other.GetComponent<IDamageable>();
        if (target == null)
            target = other.GetComponentInParent<IDamageable>();

        if (target == null) return;

        hasHit = true;

        Vector2 direction = (other.transform.position - transform.position).normalized;
        Vector2 kb = new Vector2(Mathf.Sign(rb.linearVelocity.x) * knockbackForce.x, knockbackForce.y);
        target.TakeDamage(damage, kb);

        Destroy(gameObject);
    }
}
