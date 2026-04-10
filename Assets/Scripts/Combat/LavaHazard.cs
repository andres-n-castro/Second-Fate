using UnityEngine;

/// <summary>
/// Lightweight persistent ground hazard. Damages IDamageables that enter/stay in the zone.
///
/// Setup (on prefab):
///   - Collider2D set to isTrigger = true.
///   - Set targetLayers to the Player layer.
///   - Configure damage, knockback, tick interval, and lifetime in inspector.
///
/// Self-destructs after lifetime expires.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class LavaHazard : MonoBehaviour
{
    [Header("Damage")]
    [SerializeField] private int damage = 1;
    [SerializeField] private Vector2 knockbackForce = new Vector2(3f, 4f);
    [SerializeField] private LayerMask targetLayers;

    [Header("Timing")]
    [SerializeField] private float damageCooldown = 0.8f;
    [SerializeField] private float lifetime = 4f;

    private float lastDamageTime = -999f;

    private void Start()
    {
        // Ensure trigger collider
        Collider2D col = GetComponent<Collider2D>();
        col.isTrigger = true;

        Destroy(gameObject, lifetime);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if ((targetLayers.value & (1 << other.gameObject.layer)) == 0) return;
        if (Time.time - lastDamageTime < damageCooldown) return;

        IDamageable target = other.GetComponent<IDamageable>();
        if (target == null)
            target = other.GetComponentInParent<IDamageable>();

        if (target == null) return;

        Vector2 direction = (other.transform.position - transform.position).normalized;
        Vector2 kb = new Vector2(direction.x * knockbackForce.x, knockbackForce.y);
        target.TakeDamage(damage, kb);

        lastDamageTime = Time.time;
    }
}
