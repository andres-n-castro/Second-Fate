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

    private float groundedRecallTimer;

    public void Move(Rigidbody2D rb, float xAxis, Animator anim)
    {
        Vector2 pVelocity = Vector2.zero;

        // We use GetComponent here to make sure we are grabbing the rider on THIS player
        if (TryGetComponent<PlatformRider>(out var rider))
        {
            pVelocity = rider.GetPlatformVelocity();
        }

        // This is the critical math: 
        // (Walk Input) + (Platform Speed)
        float targetX = (walkspeed * xAxis) + pVelocity.x;

        // Apply the combined velocity to the Rigidbody
        rb.linearVelocity = new Vector2(targetX, rb.linearVelocity.y);

        anim.SetBool("Walking", xAxis != 0 && Grounded());
    }

    public void Jump(Rigidbody2D rb, ref bool isJumping, Animator anim)
    {

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

        if (Input.GetKey(KeyCode.S)) return;

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

    }

    public void Flip(float xAxis)
    {
        if (xAxis < 0)
        {
            transform.localScale = new Vector3(-1f, transform.localScale.y, 1f);
        }
        else if (xAxis > 0)
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