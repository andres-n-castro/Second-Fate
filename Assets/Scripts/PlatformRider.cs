using UnityEngine;

public class PlatformRider : MonoBehaviour
{
    private Rigidbody2D rb;
    private Vector2 platformVelocity;
    private Rigidbody2D activePlatformRb;

    void Start() => rb = GetComponent<Rigidbody2D>();

    // This gets the velocity of the platform the player is standing on
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("MovingPlatform"))
        {
            activePlatformRb = collision.gameObject.GetComponent<Rigidbody2D>();
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("MovingPlatform"))
        {
            activePlatformRb = null;
            platformVelocity = Vector2.zero;
        }
    }

    void FixedUpdate()
    {
        if (activePlatformRb != null)
        {
            // We use the platform's velocity (ensure the platform's RB is Kinematic or has its own movement script)
            platformVelocity = activePlatformRb.linearVelocity;
        }
    }

    public Vector2 GetPlatformVelocity() => platformVelocity;
}