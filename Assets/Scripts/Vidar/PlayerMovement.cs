using Unity.VisualScripting;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{   
    [SerializeField] private int JumpForce = 45;
    [SerializeField] private int walkspeed = 10;
    [SerializeField] private LayerMask whatIsGround;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundLengthY = 0.1f;
    [SerializeField] private float groundLengthX = 0.3f;

    [SerializeField] private float maxFallVelocity = 28;
    private float jumpTimeCounter;
    [SerializeField] private float jumpTimeBuffer = 0.2f;
    [SerializeField] private float coyoteTime = 0.1f;
    private float coyoteTimeCounter;

    [SerializeField] private float jumpHangThreshold;
    [SerializeField] private float jumpHangGravity;
    public float defaultGravity;

    private PlayerFreeze playerFreeze;

    private float groundedRecallTimer;

    [Header("Enemy Collision Settings")]
    [SerializeField] private LayerMask whatIsEnemy;
    [SerializeField] private float enemyCheckDistance = 0.6f;

    public void Move(Rigidbody2D rb, float xAxis, Animator anim)
    {
        if (playerFreeze == null)
            playerFreeze = GetComponent<PlayerFreeze>();

        if (playerFreeze != null && playerFreeze.isFrozen)
        {
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            anim.SetBool("Walking", false);
            return;
        }

        float adjustedXAxis = xAxis;

        // If the player is trying to move left or right...
        if (xAxis != 0)
        {
            Vector2 checkDirection = xAxis > 0 ? Vector2.right : Vector2.left;

            // Cast a short invisible ray forward from the center of the player
            RaycastHit2D hit = Physics2D.Raycast(transform.position, checkDirection, enemyCheckDistance, whatIsEnemy);

            // If the ray hits an enemy, cancel the horizontal movement speed so we don't push them!
            if (hit.collider != null)
            {
                adjustedXAxis = 0;
            }
        }

        // so player does not fall off moving platform when standing still
        Vector2 platformVelocity = Vector2.zero;
        if (TryGetComponent<PlatformRider>(out var rider))
        {
            platformVelocity = rider.GetPlatformVelocity();
        }

        float targetX = (walkspeed * adjustedXAxis) + platformVelocity.x;

        rb.linearVelocity = new Vector2(targetX, rb.linearVelocity.y);

        anim.SetBool("Walking", (walkspeed * adjustedXAxis) != 0 && Grounded());
    }

    public void Jump(Rigidbody2D rb, ref bool isJumping, Animator anim)
    {
        if (playerFreeze == null)
            playerFreeze = GetComponent<PlayerFreeze>();

        if (playerFreeze != null && playerFreeze.isFrozen)
        {
            anim.SetBool("Jumping", false);
            return;
        }

        //coyote timer check tied to ground check
        if (Grounded() && (rb.linearVelocity.y <= 0.5f || TryGetComponent<PlatformRider>(out var r) && r.GetPlatformVelocity().y > 0))
        {
            coyoteTimeCounter = coyoteTime;
            isJumping = false;
        }
        else
        {
            coyoteTimeCounter -= Time.deltaTime;
        }

        //jump buffer tied to jump input
        if (Input.GetButtonDown("Jump"))
        {
            jumpTimeCounter = jumpTimeBuffer;
        }
        else
        {
            jumpTimeCounter -= Time.deltaTime;
        }

        if (!isJumping)
        {
            if (coyoteTimeCounter > 0 && jumpTimeCounter > 0)
            {
                float extraYVelocity = 0;
                if (TryGetComponent<PlatformRider>(out var rider))
                {
                    extraYVelocity = Mathf.Max(0, rider.GetPlatformVelocity().y);
                }
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, JumpForce + extraYVelocity);
                isJumping = true;
                jumpTimeCounter = 0;
                coyoteTimeCounter = 0;
                anim.SetTrigger("JumpTrigger");
            }
        }

        if (isJumping && Mathf.Abs(rb.linearVelocity.y) < jumpHangThreshold)
        {
            rb.gravityScale = defaultGravity * jumpHangGravity;
        }
        else
        {
            rb.gravityScale = defaultGravity;
        }

        if (Input.GetButtonUp("Jump") && rb.linearVelocity.y > 0)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, rb.linearVelocity.y * 0.5f);
        }

        if (Grounded())
        {
            groundedRecallTimer = 0.1f;
        }
        else
        {
            groundedRecallTimer -= Time.deltaTime;
        }

        anim.SetBool("isGrounded", groundedRecallTimer > 0);
        anim.SetBool("Jumping", isJumping && groundedRecallTimer <= 0);
        // anim.SetBool("Jumping", isJumping);
        // anim.SetFloat("yVelocity", rb.linearVelocity.y);
        // anim.SetBool("isGrounded", Grounded());

    }

    public bool Grounded()
    {
        if (Physics2D.Raycast(groundCheck.position, Vector2.down, groundLengthY, whatIsGround)
        || Physics2D.Raycast(groundCheck.position + new Vector3(groundLengthX, 0, 0), Vector2.down, groundLengthY, whatIsGround)
        || Physics2D.Raycast(groundCheck.position + new Vector3(-groundLengthX, 0, 0), Vector2.down, groundLengthY, whatIsGround))
        {
            return true;
        }

        return false;
    }

    public void MaxFall(Rigidbody2D rb)
    {
        if (rb.linearVelocity.y < 0)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, Mathf.Max(rb.linearVelocity.y, -maxFallVelocity));
        }
    }

    public void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;

        Gizmos.color = Color.green;

        // Direction of the ray (downwards)
        Vector3 downDir = Vector3.down * groundLengthY;

        // 1. Center Ray
        Gizmos.DrawLine(groundCheck.position, groundCheck.position + downDir);

        // 2. Right Ray (+groundLengthX)
        Vector3 rightPos = groundCheck.position + new Vector3(groundLengthX, 0, 0);
        Gizmos.DrawLine(rightPos, rightPos + downDir);

        // 3. Left Ray (-groundLengthX)
        Vector3 leftPos = groundCheck.position + new Vector3(-groundLengthX, 0, 0);
        Gizmos.DrawLine(leftPos, leftPos + downDir);

        Gizmos.color = Color.yellow;
        Vector2 checkDirection = transform.localScale.x > 0 ? Vector2.right : Vector2.left;
        Gizmos.DrawLine(transform.position, (Vector2)transform.position + (checkDirection * enemyCheckDistance));

    }

    public void Flip(float xAxis)
    {
        float flipThreshold = 0.4f;

        if (xAxis < -flipThreshold)
        {
            transform.localScale = new Vector3(-1f, transform.localScale.y, 1f);
        }
        else if (xAxis > flipThreshold)
        {
            transform.localScale = new Vector3(1f, transform.localScale.y, 1f);
        }

    }

    public void Dash()
    {

    }

    public void DoubleJump()
    {

    }

    public void WallJump()
    {

    }
}