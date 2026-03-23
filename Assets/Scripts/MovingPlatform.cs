using UnityEngine;

public class MovingPlatform : MonoBehaviour
{
    public Transform posA, posB;
    public float speed = 3f;
    private Rigidbody2D rb;
    private Vector3 targetPos;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        targetPos = posB.position;
    }

    void FixedUpdate()
    {
        // 1. Calculate the direction to the target
        Vector2 direction = (targetPos - transform.position).normalized;

        // 2. Set the Rigidbody velocity instead of transform.position
        rb.linearVelocity = direction * speed;

        // 3. Check if we are close enough to the target to switch
        if (Vector2.Distance(transform.position, targetPos) < 0.3f)
        {
            targetPos = (targetPos == posA.position) ? posB.position : posA.position;
        }
    }

    private void OnDrawGizmos()
    {
        if (posA != null && posB != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(posA.position, posB.position);
            Gizmos.DrawWireCube(posA.position, transform.localScale);
            Gizmos.DrawWireCube(posB.position, transform.localScale);
        }
    }
}