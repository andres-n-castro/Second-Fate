using Unity.VisualScripting;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private int JumpForce = 45;
    [SerializeField] private int walkspeed = 20;
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

    public void Move(Rigidbody2D rb, float xAxis, Animator anim)
    {
        rb.linearVelocity = new Vector2(walkspeed * xAxis, rb.linearVelocity.y);
        anim.SetBool("Walking", rb.linearVelocity.x != 0 && Grounded());
    }

    public void Jump(Rigidbody2D rb, ref bool isJumping, Animator anim)
    {

        //coyote timer check tied to ground check
        if(Grounded() && rb.linearVelocity.y <= 0.1f)
        {
            coyoteTimeCounter = coyoteTime;
            isJumping = false;
        }
        else
        {
            coyoteTimeCounter -= Time.deltaTime;
        }

        //jump buffer tied to jump input
        if(Input.GetButtonDown("Jump"))
        {
           jumpTimeCounter = jumpTimeBuffer; 
        }
        else
        {
           jumpTimeCounter -= Time.deltaTime; 
        }

        if(!isJumping)
        {
            if(coyoteTimeCounter > 0 && jumpTimeCounter > 0)
            { 
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, JumpForce); 
                isJumping = true;
                jumpTimeCounter = 0;
                coyoteTimeCounter = 0;
            }
        }

        if(isJumping && Mathf.Abs(rb.linearVelocity.y) < jumpHangThreshold)
        {
            rb.gravityScale = defaultGravity * jumpHangGravity;
        }
        else
        {
            rb.gravityScale = defaultGravity;
        }

        if(Input.GetButtonUp("Jump") && rb.linearVelocity.y > 0)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, rb.linearVelocity.y * 0.5f); 
        }

        anim.SetBool("Jumping", isJumping);
        anim.SetFloat("yVelocity", rb.linearVelocity.y);
        anim.SetBool("isGrounded", Grounded());

    }

    public bool Grounded()
    {
        if (Physics2D.Raycast(groundCheck.position, Vector2.down, groundLengthY, whatIsGround)
        || Physics2D.Raycast(groundCheck.position + new Vector3(groundLengthX, 0, 0), Vector2.down, groundLengthY, whatIsGround)
        || Physics2D.Raycast(groundCheck.position + new Vector3(-groundLengthX,0,0), Vector2.down, groundLengthY, whatIsGround))
        {
            return true;
        }

        return false;
    }

    public void MaxFall(Rigidbody2D rb)
    {
        if(rb.linearVelocity.y < 0){
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
        if(xAxis < 0)
        {
            transform.localScale = new Vector3(-1f, transform.localScale.y, 1f);
        }
        else if (xAxis > 0)
        {
            transform.localScale = new Vector3(1f, transform.localScale.y, 1f);
        }

    }
}