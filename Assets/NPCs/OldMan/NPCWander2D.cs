using UnityEngine;

public class NPCWander2D : MonoBehaviour
{
    [SerializeField] Rigidbody2D rb;
    [SerializeField] NPC npc;

    [Header("Wander")]
    [SerializeField] float speed = 0.75f;
    [SerializeField] float wanderRadius = 3.0f;
    [SerializeField] float moveTimeMin = 1.0f;
    [SerializeField] float moveTimeMax = 3.1f;
    [SerializeField] float waitTimeMin = 2.0f;
    [SerializeField] float waitTimeMax = 7.0f;

    Vector2 home;
    Vector2 dir;
    float timer;
    bool moving;

    void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (npc == null) npc = GetComponent<NPC>();

        if (rb != null) home = rb.position;

        PickNewMove();
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        if (npc != null && npc.BlocksMovement())
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        timer -= Time.fixedDeltaTime;

        if (moving)
        {
            Vector2 next = rb.position + dir * speed * Time.fixedDeltaTime;

            if (Mathf.Abs(next.x - home.x) > wanderRadius)
            {
                dir = new Vector2((home.x - rb.position.x) >= 0f ? 1f : -1f, 0f);
                next = rb.position + dir * speed * Time.fixedDeltaTime;
            }

            rb.MovePosition(new Vector2(next.x, rb.position.y));

            if (timer <= 0f)
            {
                moving = false;
                timer = Random.Range(waitTimeMin, waitTimeMax);
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            }
        }
        else
        {
            if (timer <= 0f) PickNewMove();
        }
    }

    void PickNewMove()
    {
        float sign = Random.value < 0.5f ? -1f : 1f;
        dir = new Vector2(sign, 0f);
        moving = true;
        timer = Random.Range(moveTimeMin, moveTimeMax);
    }
}