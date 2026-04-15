using UnityEngine;

public class PlatformRider : MonoBehaviour
{
    private Rigidbody2D rb;
    private Vector2 platformVelocity;
    private Rigidbody2D activePlatformRb;

    void Start() => rb = GetComponent<Rigidbody2D>();

    private void OnCollisionEnter2D(Collision2D collision)
    {
        Rigidbody2D otherRb = collision.collider.attachedRigidbody;
        if (otherRb != null && otherRb.gameObject.CompareTag("MovingPlatform"))
        {
            activePlatformRb = otherRb;
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        Rigidbody2D otherRb = collision.collider.attachedRigidbody;
        if (otherRb != null && otherRb.gameObject.CompareTag("MovingPlatform"))
        {
            activePlatformRb = null;
            platformVelocity = Vector2.zero;
        }
    }

    void FixedUpdate()
    {
        if (activePlatformRb != null)
        {
            platformVelocity = activePlatformRb.linearVelocity;
        }
    }

    public Vector2 GetPlatformVelocity() => platformVelocity;
}
