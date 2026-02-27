using UnityEngine;
using System;

public abstract class EnemyBase : MonoBehaviour
{
    [Header("Profile")]
    [SerializeField] private EnemyProfile profile;

    [Header("Environment Checks")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private Transform wallCheck;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private LayerMask obstacleLayer;

    // Cached components
    public Rigidbody2D Rb { get; private set; }
    public Animator Anim { get; private set; }
    public Health Health { get; private set; }
    public EnemyProfile Profile => profile;
    public EnemyContext Ctx { get; private set; }
    public StateMachine FSM { get; private set; }
    public EnemyPerception2D Perception { get; private set; }

    // Accessors for perception
    public Transform GroundCheck => groundCheck;
    public Transform WallCheck => wallCheck;
    public LayerMask GroundLayer => groundLayer;
    public LayerMask ObstacleLayer => obstacleLayer;

    public int FacingDirection { get; private set; } = 1;

    // Attack cooldowns
    private float[] attackCooldownTimers;
    private float defaultDrag;

    // Contact damage
    private float _lastContactDamageTime = -999f;

    protected virtual void Awake()
    {
        Rb = GetComponent<Rigidbody2D>();
        Anim = GetComponent<Animator>();
        Health = GetComponent<Health>();
    }

    protected virtual void Start()
    {
        Ctx = new EnemyContext();
        FSM = new StateMachine();
        Perception = new EnemyPerception2D(this, Ctx);

        defaultDrag = Rb.linearDamping;

        if (profile.attacks != null)
        {
            attackCooldownTimers = new float[profile.attacks.Length];
        }

        if (Health != null)
        {
            Health.OnDeath += OnDeath;
            Health.OnDamageTaken += OnDamageTaken;
            Health.handleKnockbackExternally = true;
        }

        InitializeStates();
    }

    protected abstract void InitializeStates();

    protected virtual void Update()
    {
        FSM.Tick();

        // Tick attack cooldowns
        if (attackCooldownTimers != null)
        {
            for (int i = 0; i < attackCooldownTimers.Length; i++)
            {
                if (attackCooldownTimers[i] > 0f)
                    attackCooldownTimers[i] -= Time.deltaTime;
            }
        }
    }

    protected virtual void FixedUpdate()
    {
        Perception.Update();
        FSM.FixedTick();
    }

    protected virtual void OnDestroy()
    {
        Perception?.Cleanup();

        if (Health != null)
        {
            Health.OnDeath -= OnDeath;
            Health.OnDamageTaken -= OnDamageTaken;
        }
    }

    // ---------------------------------------------------------------
    //  Movement Helpers
    // ---------------------------------------------------------------

    public void MoveGround(float speed)
    {
        Rb.linearVelocity = new Vector2(FacingDirection * speed, Rb.linearVelocity.y);
    }

    public void MoveDirection(Vector2 direction, float speed)
    {
        Rb.linearVelocity = direction.normalized * speed;
    }

    public void MoveToward(Vector2 target, float speed)
    {
        Vector2 dir = (target - (Vector2)transform.position).normalized;
        Rb.linearVelocity = dir * speed;
    }

    public void StopHorizontal()
    {
        Rb.linearVelocity = new Vector2(0f, Rb.linearVelocity.y);
    }

    public void StopAll()
    {
        Rb.linearVelocity = Vector2.zero;
    }

    public void FaceDirection(int dir)
    {
        if (dir == 0) return;
        FacingDirection = dir > 0 ? 1 : -1;
        transform.localScale = new Vector2(FacingDirection, transform.localScale.y);
    }

    public void FlipFacing()
    {
        FaceDirection(-FacingDirection);
    }

    public void FacePlayer()
    {
        if (Ctx.playerTransform == null) return;
        float dirX = Ctx.playerTransform.position.x - transform.position.x;
        if (Mathf.Abs(dirX) < 0.01f) return;
        FaceDirection(dirX > 0 ? 1 : -1);
    }

    public Vector2 AvoidObstacles(Vector2 desiredDirection)
    {
        LayerMask mask = obstacleLayer != 0 ? obstacleLayer : groundLayer;
        RaycastHit2D hit = Physics2D.Raycast(
            transform.position, desiredDirection, profile.wallCheckDistance, mask);
        if (hit.collider != null)
        {
            return Vector2.Reflect(desiredDirection, hit.normal).normalized;
        }
        return desiredDirection;
    }

    // ---------------------------------------------------------------
    //  Knockback
    // ---------------------------------------------------------------

    public void ApplyKnockback(Vector2 incomingKnockback)
    {
        float dir;
        if (incomingKnockback.x != 0f)
        {
            dir = Mathf.Sign(incomingKnockback.x);
        }
        else if (PlayerController.Instance != null)
        {
            dir = Mathf.Sign(transform.position.x - PlayerController.Instance.transform.position.x);
        }
        else
        {
            dir = -FacingDirection;
        }

        Rb.linearVelocity = new Vector2(0f, Rb.linearVelocity.y);
        Rb.AddForce(new Vector2(dir * profile.knockbackForceX, profile.knockbackForceY), ForceMode2D.Impulse);
        Rb.linearDamping = profile.hitstunDrag;
    }

    public void RestoreDrag()
    {
        Rb.linearDamping = defaultDrag;
    }

    // ---------------------------------------------------------------
    //  Attack Cooldowns
    // ---------------------------------------------------------------

    public bool IsAttackReady(int attackIndex)
    {
        if (attackCooldownTimers == null || attackIndex < 0 || attackIndex >= attackCooldownTimers.Length)
            return false;
        return attackCooldownTimers[attackIndex] <= 0f;
    }

    public bool IsAttackReady(string attackName)
    {
        if (profile.attacks == null) return false;
        for (int i = 0; i < profile.attacks.Length; i++)
        {
            if (profile.attacks[i].attackName == attackName)
                return IsAttackReady(i);
        }
        return false;
    }

    public void StartCooldown(int attackIndex)
    {
        if (attackCooldownTimers == null || attackIndex < 0 || attackIndex >= attackCooldownTimers.Length)
            return;
        attackCooldownTimers[attackIndex] = profile.attacks[attackIndex].cooldown;
    }

    public void StartCooldown(string attackName)
    {
        if (profile.attacks == null) return;
        for (int i = 0; i < profile.attacks.Length; i++)
        {
            if (profile.attacks[i].attackName == attackName)
            {
                StartCooldown(i);
                return;
            }
        }
    }

    // ---------------------------------------------------------------
    //  Contact Damage
    // ---------------------------------------------------------------

    protected virtual void OnCollisionEnter2D(Collision2D collision)
    {
        TryApplyContactDamage(collision.collider);
    }

    protected virtual void OnCollisionStay2D(Collision2D collision)
    {
        TryApplyContactDamage(collision.collider);
    }

    private void TryApplyContactDamage(Collider2D other)
    {
        if (!profile.enableContactDamage) return;
        if (profile.contactRequiresEnemyAlive && Ctx.isDead) return;
        if (Time.time - _lastContactDamageTime < profile.contactCooldownSeconds) return;

        // 1. Look for the IDamageable interface instead of "Health.cs"
        IDamageable target = other.GetComponent<IDamageable>();
        if (target == null)
            target = other.GetComponentInParent<IDamageable>();

        if (target == null) return;

        // 2. Calculate the knockback direction
        float dirX = other.transform.position.x - transform.position.x;
        float knockDir = Mathf.Abs(dirX) > 0.01f ? Mathf.Sign(dirX) : 1f;
        Vector2 knockback = new Vector2(knockDir * profile.contactKnockbackX, profile.contactKnockbackY);

        // 3. Send the damage and let the target's script (PlayerManager) handle the rest!
        target.TakeDamage(profile.contactDamage, knockback);

        _lastContactDamageTime = Time.time;
    }

    // ---------------------------------------------------------------
    //  Damage / Death Callbacks
    // ---------------------------------------------------------------

    private void OnDeath()
    {
        Ctx.isDead = true;
        HandleDeath();
    }

    private void OnDamageTaken(int damage, Vector2 knockback)
    {
        HandleDamageTaken(damage, knockback);
    }

    protected virtual void HandleDeath()
    {
        StopAll();

        if (Anim != null) Anim.SetTrigger("Die");

        foreach (Collider2D col in GetComponents<Collider2D>())
        {
            col.enabled = false;
        }

        Destroy(gameObject, 2f);
    }

    protected virtual void HandleDamageTaken(int damage, Vector2 knockback)
    {
        // Override in subclasses to enter hitstun
    }

}
