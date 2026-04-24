using UnityEngine;

/// <summary>
/// Staff projectile that fires toward the player, then continuously
/// tracks the player's live position.
///
/// Setup (on prefab):
///   - Rigidbody2D with gravityScale = 0.
///   - Collider2D (CircleCollider2D recommended), NOT trigger.
///   - Set targetLayers to the Player layer.
///   - Damage, knockback, curveDelay, curveStrength configured in inspector
///     or overridden via Initialize().
///
/// Behavior:
///   1. Flies straight toward the player for curveDelay seconds.
///   2. After the delay, continuously steers toward the player's live
///      position. Can reverse direction if the player moves behind it.
///   3. Destroys on collision with anything, dealing damage to IDamageables.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class OdinProjectile : MonoBehaviour
{
    [Header("Damage")]
    [SerializeField] private int damage = 2;
    [SerializeField] private Vector2 knockbackForce = new Vector2(6f, 4f);
    [SerializeField] private LayerMask targetLayers;

    [Header("Curve Settings")]
    [SerializeField] private float curveDelay = 0.3f;
    [SerializeField] private float curveStrength = 4f;

    [Header("Lifetime")]
    [SerializeField] private float maxLifetime = 5f;

    private Rigidbody2D rb;
    private Vector2 lastKnownTarget;
    private Transform playerTarget;
    private float moveSpeed;
    private float aliveTimer;
    private bool isCurving;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    /// <summary>
    /// Set initial velocity, lock the target position, and ignore spawner colliders.
    /// </summary>
    public void Initialize(Vector2 velocity, Vector2 targetPosition, Collider2D[] spawnersToIgnore,
        int overrideDamage = -1, float overrideCurveDelay = -1f, float overrideCurveStrength = -1f,
        float overrideLifetime = -1f)
    {
        rb.linearVelocity = velocity;
        lastKnownTarget = targetPosition;
        moveSpeed = velocity.magnitude;

        if (overrideDamage > 0) damage = overrideDamage;
        if (overrideCurveDelay >= 0f) curveDelay = overrideCurveDelay;
        if (overrideCurveStrength >= 0f) curveStrength = overrideCurveStrength;
        if (overrideLifetime > 0f) maxLifetime = overrideLifetime;

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

    /// <summary>
    /// Set a live Transform to track. The projectile will continuously
    /// steer toward this target after the curveDelay expires.
    /// </summary>
    public void SetPlayerTarget(Transform target)
    {
        playerTarget = target;
    }

    private void FixedUpdate()
    {
        aliveTimer += Time.fixedDeltaTime;

        if (!isCurving && aliveTimer >= curveDelay)
        {
            isCurving = true;
        }

        if (isCurving)
        {
            // Use live player position if available, otherwise fallback to locked position
            Vector2 targetPos = (playerTarget != null)
                ? (Vector2)playerTarget.position
                : lastKnownTarget;

            Vector2 toTarget = targetPos - (Vector2)transform.position;

            if (toTarget.sqrMagnitude > 0.01f)
            {
                // Set velocity directly toward the player at constant speed.
                // This allows full reversal if the player moves behind the projectile.
                rb.linearVelocity = toTarget.normalized * moveSpeed;
            }
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Ignore collisions with other projectiles (prevents triple-projectile self-destruction)
        if (collision.gameObject.GetComponent<OdinProjectile>() != null) return;

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
