using UnityEngine;

public class PlayerFreeze : MonoBehaviour
{
    public bool isFrozen;

    Rigidbody2D rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        if (isFrozen)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }
}