using UnityEngine;

/// <summary>
/// Ground spike hazard. Spawned at a position, emerges upward briefly,
/// damages any IDamageable in its trigger collider, then retracts and destroys.
///
/// Setup (on prefab):
///   - Collider2D set to trigger.
///   - Set targetLayers to the Player layer.
///   - Sprite/animation on child for visual emerge/retract.
///   - Damage and knockback configured in inspector.
///
/// Lifecycle: Emerge (scale up) → Active (damage window) → Retract (scale down) → Destroy.
/// </summary>
public class GroundSpike : MonoBehaviour
{
    [Header("Damage")]
    [SerializeField] private int damage = 2;
    [SerializeField] private Vector2 knockbackForce = new Vector2(4f, 6f);
    [SerializeField] private LayerMask targetLayers;

    [Header("Timing")]
    [SerializeField] private float emergeDuration = 0.15f;
    [SerializeField] private float activeDuration = 0.5f;
    [SerializeField] private float retractDuration = 0.2f;

    private enum Phase { Emerge, Active, Retract }
    private Phase phase;
    private float timer;
    private Collider2D col;
    private bool hasHit;
    private Vector3 fullScale;

    /// <summary>
    /// Configure the spike's active duration. Call before or right after Instantiate.
    /// </summary>
    public void Initialize(float overrideActiveDuration = -1f, int overrideDamage = -1)
    {
        if (overrideActiveDuration > 0f) activeDuration = overrideActiveDuration;
        if (overrideDamage > 0) damage = overrideDamage;
    }

    private void Awake()
    {
        col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
        fullScale = transform.localScale;
    }

    private void Start()
    {
        phase = Phase.Emerge;
        timer = emergeDuration;
        transform.localScale = new Vector3(fullScale.x, 0f, fullScale.z);
        if (col != null) col.enabled = false;
    }

    private void Update()
    {
        timer -= Time.deltaTime;

        switch (phase)
        {
            case Phase.Emerge:
                float emergeT = 1f - Mathf.Clamp01(timer / emergeDuration);
                transform.localScale = new Vector3(fullScale.x, fullScale.y * emergeT, fullScale.z);

                if (timer <= 0f)
                {
                    phase = Phase.Active;
                    timer = activeDuration;
                    transform.localScale = fullScale;
                    if (col != null) col.enabled = true;
                }
                break;

            case Phase.Active:
                if (timer <= 0f)
                {
                    phase = Phase.Retract;
                    timer = retractDuration;
                    if (col != null) col.enabled = false;
                }
                break;

            case Phase.Retract:
                float retractT = Mathf.Clamp01(timer / retractDuration);
                transform.localScale = new Vector3(fullScale.x, fullScale.y * retractT, fullScale.z);

                if (timer <= 0f)
                {
                    Destroy(gameObject);
                }
                break;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (phase != Phase.Active) return;
        if (hasHit) return;
        if ((targetLayers.value & (1 << other.gameObject.layer)) == 0) return;

        IDamageable target = other.GetComponent<IDamageable>();
        if (target == null)
            target = other.GetComponentInParent<IDamageable>();

        if (target == null) return;

        hasHit = true;

        Vector2 direction = (other.transform.position - transform.position).normalized;
        Vector2 kb = new Vector2(direction.x * knockbackForce.x, knockbackForce.y);
        target.TakeDamage(damage, kb);
    }
}
