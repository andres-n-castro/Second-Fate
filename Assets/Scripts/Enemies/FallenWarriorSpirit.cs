using UnityEngine;
using System.Collections;

/// <summary>
/// Fallen Warrior Spirit — Basic air enemy.
///
/// State machine:
///   Roam → (detect player) → Windup → Dash → Reposition → Windup → ...
///   If player leaves extended range, returns to Roam.
///
/// Roam:   Wander within a bounded area around home position.
/// Windup: Brief telegraph. Stops movement, faces player.
/// Dash:   Fast linear movement toward player. Hitbox active during dash.
/// Reposition: Fly to a new offset around the player before dashing again.
///
/// Takes knockback on hit with brief hitstun; wall checks prevent clipping.
///
/// Required components: Rigidbody2D (gravity 0), Collider2D, Health.
/// Child reference: dashHitbox (AttackHitbox on child GO with trigger collider).
/// Optional: Animator (triggers: "Windup", "Dash", "Die").
/// </summary>
public class FallenWarriorSpirit : MonoBehaviour
{
    private enum State { Roam, Windup, Dash, Reposition, Dead }

    [Header("Detection")]
    [SerializeField] private float detectionRadius = 8f;
    [SerializeField] private float deaggroRadius = 12f;
    [SerializeField] private LayerMask playerLayer;

    [Header("Roaming")]
    [SerializeField] private float roamSpeed = 2f;
    [SerializeField] private float roamRadius = 3f;
    [SerializeField] private float roamChangeInterval = 2f;

    [Header("Dash Attack")]
    [SerializeField] private float windupDuration = 0.5f;
    [SerializeField] private float dashSpeed = 15f;
    [SerializeField] private float dashDuration = 0.3f;
    [SerializeField] private float repositionSpeed = 6f;
    [SerializeField] private float repositionDistance = 4f;
    [SerializeField] private float cooldownAfterReposition = 0.3f;

    [Header("Obstacle Avoidance")]
    [SerializeField] private float wallCheckDistance = 1f;
    [SerializeField] private LayerMask obstacleLayer;

    [Header("Knockback & Hitstun")]
    [SerializeField] private float knockbackForceX = 4f;
    [SerializeField] private float knockbackForceY = 1f;
    [SerializeField] private float hitstunDuration = 0.2f;
    [SerializeField] private float hitstunDrag = 6f;

    [Header("References")]
    [SerializeField] private AttackHitbox dashHitbox;

    private Rigidbody2D rb;
    private Health health;
    private Animator anim;

    private State currentState = State.Roam;
    private Transform playerTransform;
    private Vector2 homePosition;
    private Vector2 roamTarget;
    private float roamTimer;

    // Hitstun state
    private bool isHitstunned;
    private float hitstunTimer;
    private float defaultDrag;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<Health>();
        anim = GetComponent<Animator>();
        homePosition = transform.position;
    }

    private void Start()
    {
        rb.gravityScale = 0f;

        if (health != null)
        {
            health.OnDeath += HandleDeath;
            health.OnDamageTaken += HandleKnockback;
            health.handleKnockbackExternally = true;
        }

        defaultDrag = rb.linearDamping;
        PickNewRoamTarget();
    }

    private void FixedUpdate()
    {
        if (currentState == State.Dead) return;

        // --- Hitstun: pause movement, let knockback play out ---
        if (isHitstunned)
        {
            hitstunTimer -= Time.fixedDeltaTime;

            // Wall safety: stop velocity toward nearby walls
            if (rb.linearVelocity.x != 0f)
            {
                float moveDir = Mathf.Sign(rb.linearVelocity.x);
                RaycastHit2D hit = Physics2D.Raycast(
                    transform.position, Vector2.right * moveDir, wallCheckDistance, obstacleLayer);
                if (hit.collider != null)
                {
                    rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                }
            }

            if (hitstunTimer <= 0f)
            {
                EndHitstun();
            }
            return;
        }

        // Only Roam is driven by FixedUpdate; attack cycle uses coroutines
        if (currentState == State.Roam)
        {
            HandleRoam();
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  KNOCKBACK & HITSTUN
    // ─────────────────────────────────────────────────────────────

    private void HandleKnockback(int damage, Vector2 incomingKnockback)
    {
        if (health.IsDead) return;

        // Direction: use incoming knockback, fallback to away-from-player
        float dir;
        if (incomingKnockback.x != 0f)
        {
            dir = Mathf.Sign(incomingKnockback.x);
        }
        else if (PlayerController.Instance != null)
        {
            dir = Mathf.Sign(transform.position.x - PlayerController.Instance.transform.position.x);
        }
        else
        {
            float facing = transform.localScale.x >= 0 ? 1f : -1f;
            dir = -facing;
        }

        rb.linearVelocity = Vector2.zero;
        rb.AddForce(new Vector2(dir * knockbackForceX, knockbackForceY), ForceMode2D.Impulse);

        isHitstunned = true;
        hitstunTimer = hitstunDuration;

        rb.linearDamping = hitstunDrag;
    }

    private void EndHitstun()
    {
        isHitstunned = false;
        rb.linearDamping = defaultDrag;
    }

    // ─────────────────────────────────────────────────────────────
    //  ROAM
    // ─────────────────────────────────────────────────────────────

    private void HandleRoam()
    {
        Vector2 direction = roamTarget - (Vector2)transform.position;

        if (direction.magnitude < 0.5f)
        {
            PickNewRoamTarget();
            direction = roamTarget - (Vector2)transform.position;
        }

        Vector2 moveDir = AvoidObstacles(direction.normalized);
        rb.linearVelocity = moveDir * roamSpeed;
        FaceDirection(moveDir.x);

        roamTimer -= Time.fixedDeltaTime;
        if (roamTimer <= 0f) PickNewRoamTarget();

        // Check for player
        if (DetectPlayer())
        {
            StartCoroutine(AttackCycle());
        }
    }

    private void PickNewRoamTarget()
    {
        roamTarget = homePosition + Random.insideUnitCircle * roamRadius;
        roamTimer = roamChangeInterval;
    }

    private bool DetectPlayer()
    {
        Collider2D hit = Physics2D.OverlapCircle(transform.position, detectionRadius, playerLayer);
        if (hit != null)
        {
            playerTransform = hit.transform;
            return true;
        }
        return false;
    }

    // ─────────────────────────────────────────────────────────────
    //  ATTACK CYCLE  (Windup → Dash → Reposition → repeat)
    // ─────────────────────────────────────────────────────────────

    private IEnumerator AttackCycle()
    {
        while (currentState != State.Dead && playerTransform != null)
        {
            // De-aggro if player moves far away
            float dist = Vector2.Distance(transform.position, playerTransform.position);
            if (dist > deaggroRadius) break;

            // ── WINDUP ──
            currentState = State.Windup;
            rb.linearVelocity = Vector2.zero;
            FaceDirection(playerTransform.position.x - transform.position.x);

            if (anim != null) anim.SetTrigger("Windup");

            yield return new WaitForSeconds(windupDuration);
            if (currentState == State.Dead) yield break;
            // Wait out any hitstun that started during windup
            while (isHitstunned) yield return null;

            // ── DASH ──
            currentState = State.Dash;

            Vector2 dashDir = ((Vector2)playerTransform.position - (Vector2)transform.position).normalized;

            if (dashHitbox != null) dashHitbox.Activate();
            if (anim != null) anim.SetTrigger("Dash");

            float dashTimer = dashDuration;
            while (dashTimer > 0f)
            {
                if (currentState == State.Dead) yield break;
                // Pause dash during hitstun — don't overwrite knockback velocity
                if (isHitstunned)
                {
                    yield return new WaitForFixedUpdate();
                    continue;
                }
                rb.linearVelocity = dashDir * dashSpeed;
                dashTimer -= Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }

            if (dashHitbox != null) dashHitbox.Deactivate();

            // ── REPOSITION ──
            currentState = State.Reposition;
            rb.linearVelocity = Vector2.zero;

            // Pick a random offset around player, biased upward to stay airborne
            Vector2 offset = Random.insideUnitCircle.normalized * repositionDistance;
            offset.y = Mathf.Abs(offset.y) + 1f;
            Vector2 repositionTarget = (Vector2)playerTransform.position + offset;

            float maxRepoTime = repositionDistance / repositionSpeed + 0.5f;
            float repoTimer = 0f;
            while (repoTimer < maxRepoTime)
            {
                if (currentState == State.Dead) yield break;
                // Pause reposition during hitstun
                if (isHitstunned)
                {
                    yield return new WaitForFixedUpdate();
                    continue;
                }

                Vector2 dir2 = repositionTarget - (Vector2)transform.position;
                if (dir2.magnitude < 0.5f) break;

                Vector2 moveDir = AvoidObstacles(dir2.normalized);
                rb.linearVelocity = moveDir * repositionSpeed;
                FaceDirection(moveDir.x);

                repoTimer += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }

            rb.linearVelocity = Vector2.zero;

            // Brief cooldown before next dash
            yield return new WaitForSeconds(cooldownAfterReposition);
            if (currentState == State.Dead) yield break;
            while (isHitstunned) yield return null;
        }

        // Lost player — return to roaming
        currentState = State.Roam;
        homePosition = transform.position;
        PickNewRoamTarget();
    }

    // ─────────────────────────────────────────────────────────────
    //  UTILITY
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// If a wall is directly ahead, reflect the move direction off the wall normal.
    /// </summary>
    private Vector2 AvoidObstacles(Vector2 desiredDirection)
    {
        RaycastHit2D hit = Physics2D.Raycast(transform.position, desiredDirection, wallCheckDistance, obstacleLayer);
        if (hit.collider != null)
        {
            return Vector2.Reflect(desiredDirection, hit.normal).normalized;
        }
        return desiredDirection;
    }

    private void FaceDirection(float xDirection)
    {
        if (Mathf.Abs(xDirection) < 0.01f) return;
        transform.localScale = new Vector2(xDirection > 0 ? 1 : -1, transform.localScale.y);
    }

    private void HandleDeath()
    {
        currentState = State.Dead;
        StopAllCoroutines();

        // Clean up hitstun state
        isHitstunned = false;
        rb.linearDamping = defaultDrag;

        rb.linearVelocity = Vector2.zero;
        rb.gravityScale = 1f; // Fall to ground on death

        if (dashHitbox != null) dashHitbox.Deactivate();
        if (anim != null) anim.SetTrigger("Die");

        foreach (Collider2D col in GetComponents<Collider2D>())
        {
            col.enabled = false;
        }

        Destroy(gameObject, 2f);
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDeath -= HandleDeath;
            health.OnDamageTaken -= HandleKnockback;
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Detection radius (yellow)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        // De-aggro radius (orange)
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(transform.position, deaggroRadius);

        // Roam area (cyan)
        Gizmos.color = Color.cyan;
        Vector3 home = Application.isPlaying ? (Vector3)homePosition : transform.position;
        Gizmos.DrawWireSphere(home, roamRadius);
    }
}
