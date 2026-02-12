using UnityEngine;

/// <summary>
/// Draugr — Basic ground enemy.
///
/// Behavior:
///   - Patrols back and forth on platforms at a slow speed.
///   - Turns around at ledges (no ground ahead) and walls.
///   - Does NOT attack the player — contact damage only if you add a separate trigger.
///   - Damageable via the Health component (responds to player sword attacks).
///
/// Required components: Rigidbody2D, Collider2D, Health.
/// Optional: Animator (uses "Walking" bool, "Die" trigger).
///
/// Child Transforms needed in inspector:
///   - groundCheck  — positioned at the front-bottom edge for ledge detection
///   - wallCheck    — positioned at the front-center for wall detection
/// </summary>
public class Draugr : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private Transform wallCheck;
    [SerializeField] private float groundCheckDistance = 1f;
    [SerializeField] private float wallCheckDistance = 0.5f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Idle Pause")]
    [SerializeField] private float idleDuration = 0.2f;

    private Rigidbody2D rb;
    private Animator anim;
    private Health health;

    private int facingDirection = 1; // 1 = right, -1 = left
    private bool isIdle;
    private float idleTimer;
    private float stuckTimer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        health = GetComponent<Health>();
    }

    private void Start()
    {
        if (health != null)
        {
            health.OnDeath += HandleDeath;
        }
    }

    private void FixedUpdate()
    {
        if (health != null && health.IsDead) return;

        if (isIdle)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            idleTimer -= Time.fixedDeltaTime;
            if (idleTimer <= 0f)
            {
                isIdle = false;
            }
            return;
        }

        // Turn around at ledge or wall
        if (!CheckGroundAhead() || CheckWallAhead())
        {
            StartIdle();
            return;
        }

        // Stuck detection — if trying to move but barely moving, treat as wall
        if (Mathf.Abs(rb.linearVelocity.x) < 0.1f)
        {
            stuckTimer += Time.fixedDeltaTime;
            if (stuckTimer > 0.3f)
            {
                stuckTimer = 0f;
                StartIdle();
                return;
            }
        }
        else
        {
            stuckTimer = 0f;
        }

        // Walk forward
        rb.linearVelocity = new Vector2(facingDirection * moveSpeed, rb.linearVelocity.y);

        if (anim != null) anim.SetBool("Walking", true);
    }

    /// <summary>
    /// Returns true if there is ground ahead (prevents walking off ledges).
    /// </summary>
    private bool CheckGroundAhead()
    {
        Vector2 origin = groundCheck != null
            ? (Vector2)groundCheck.position
            : (Vector2)transform.position + new Vector2(facingDirection * 0.5f, 0f);

        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, groundCheckDistance, groundLayer);
        return hit.collider != null;
    }

    /// <summary>
    /// Returns true if a wall is directly ahead.
    /// </summary>
    private bool CheckWallAhead()
    {
        Vector2 origin = wallCheck != null
            ? (Vector2)wallCheck.position
            : (Vector2)transform.position;

        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.right * facingDirection, wallCheckDistance, groundLayer);
        return hit.collider != null;
    }

    private void StartIdle()
    {
        isIdle = true;
        idleTimer = idleDuration;

        facingDirection *= -1;

        // Flip sprite (matches PlayerController convention)
        transform.localScale = new Vector2(facingDirection, transform.localScale.y);

        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

        if (anim != null) anim.SetBool("Walking", false);
    }

    private void HandleDeath()
    {
        rb.linearVelocity = Vector2.zero;

        if (anim != null) anim.SetTrigger("Die");

        // Disable colliders so player can walk through
        foreach (Collider2D col in GetComponents<Collider2D>())
        {
            col.enabled = false;
        }

        // Destroy after delay to allow death animation
        Destroy(gameObject, 1.5f);
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDeath -= HandleDeath;
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Ledge check ray (green)
        Vector2 groundOrigin = groundCheck != null
            ? (Vector2)groundCheck.position
            : (Vector2)transform.position + new Vector2(facingDirection * 0.5f, 0f);
        Gizmos.color = Color.green;
        Gizmos.DrawLine(groundOrigin, groundOrigin + Vector2.down * groundCheckDistance);

        // Wall check ray (red)
        Vector2 wallOrigin = wallCheck != null
            ? (Vector2)wallCheck.position
            : (Vector2)transform.position;
        Gizmos.color = Color.red;
        Gizmos.DrawLine(wallOrigin, wallOrigin + Vector2.right * facingDirection * wallCheckDistance);
    }
}
