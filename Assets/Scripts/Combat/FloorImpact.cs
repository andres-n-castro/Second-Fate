using UnityEngine;

/// <summary>
/// Wide floor-level impact trigger spawned by plunge/slam attacks.
/// Damages grounded players who are standing on the floor.
/// Airborne/jumping players fly above the trigger and avoid damage.
///
/// Setup (on prefab):
///   - Collider2D set as trigger. Should be wide (X) and short (Y) so only
///     floor-level targets are hit. Position at ground level.
///   - Set targetLayers to the Player layer.
///   - Damage and knockback configured in inspector.
///
/// Lifecycle: instantly active on spawn → active for duration → destroy.
/// Generic enough for any boss floor-slam attack.
/// </summary>
public class FloorImpact : MonoBehaviour
{
    [Header("Damage")]
    [SerializeField] private int damage = 2;
    [SerializeField] private Vector2 knockbackForce = new Vector2(6f, 8f);
    [SerializeField] private LayerMask targetLayers;

    [Header("Timing")]
    [SerializeField] private float activeDuration = 0.4f;

    private Collider2D col;
    private float timer;
    private bool hasHit;

    /// <summary>
    /// Configure the impact radius and duration. Call right after Instantiate.
    /// </summary>
    public void Initialize(float radius, float duration, int overrideDamage = -1)
    {
        activeDuration = duration;
        if (overrideDamage > 0) damage = overrideDamage;

        // Scale the collider to match desired radius
        var circle = GetComponent<CircleCollider2D>();
        if (circle != null)
        {
            circle.radius = radius;
        }
        else
        {
            var box = GetComponent<BoxCollider2D>();
            if (box != null)
            {
                box.size = new Vector2(radius * 2f, box.size.y);
            }
        }
    }

    private void Awake()
    {
        col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    private void Start()
    {
        timer = activeDuration;
    }

    private void Update()
    {
        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasHit) return;
        if ((targetLayers.value & (1 << other.gameObject.layer)) == 0) return;

        IDamageable target = other.GetComponent<IDamageable>();
        if (target == null)
            target = other.GetComponentInParent<IDamageable>();

        if (target == null) return;

        hasHit = true;

        float dirX = other.transform.position.x - transform.position.x;
        float knockDir = Mathf.Abs(dirX) > 0.01f ? Mathf.Sign(dirX) : 1f;
        Vector2 kb = new Vector2(knockDir * knockbackForce.x, knockbackForce.y);
        target.TakeDamage(damage, kb);
    }
}
