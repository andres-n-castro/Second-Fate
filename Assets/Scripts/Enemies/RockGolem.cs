using UnityEngine;

/// <summary>
/// Rock Golem — Slow ground enemy that throws rock projectiles.
///
/// Behavior:
///   - Patrols back and forth on platforms at moveSpeed.
///   - Detects player in aggroRange (no same-platform requirement — ranged).
///   - Faces player and waits for attack cooldown, then throws a rock.
///   - Rock follows a gravity arc toward the player's position at throw time.
///   - Gives up when player leaves deaggro range or LOS is lost.
///   - Damageable via Health. Takes knockback during hitstun.
///
/// Required components: Rigidbody2D, Collider2D, Health.
/// Optional: Animator (uses "RockGolem_Walking" bool, "RockGolem_Attack",
///           "RockGolem_Takes_Damage", "RockGolem_Dies" triggers).
///
/// Child objects/transforms needed in inspector:
///   - groundCheck    — positioned at the front-bottom edge for ledge detection
///   - wallCheck      — positioned at the front-center for wall detection
///   - rockSpawnPoint — position where rocks spawn (e.g. hand)
///
/// Prefab reference:
///   - rockPrefab — RockProjectile prefab with Rigidbody2D, Collider2D, RockProjectile script
///
/// EnemyProfile attacks array must contain one entry named "RockThrow" with the
/// desired windupDuration, recoveryDuration, and cooldown.
/// </summary>
public class RockGolem : EnemyBase
{
    [Header("Projectile")]
    [SerializeField] private GameObject rockPrefab;
    [SerializeField] private Transform rockSpawnPoint;

    // Outer FSM superstates
    public NonCombatSuperState NonCombatSuper { get; private set; }
    public CombatSuperState CombatSuper { get; private set; }

    // Substates
    public RockGolemPatrolState PatrolState { get; private set; }
    public RockGolemCombatIdleState CombatIdleState { get; private set; }
    public RockGolemThrowState ThrowState { get; private set; }
    public RockGolemGiveUpState GiveUpState { get; private set; }

    // Outer override states
    public GroundHitstunState HitstunState { get; private set; }
    public GroundDeadState DeadState { get; private set; }

    // Prefab/spawn accessors for states
    public GameObject RockPrefab => rockPrefab;
    public Transform RockSpawnPoint => rockSpawnPoint;

    // Animation parameter names
    public override string AnimWalking => "RockGolem_Walking";
    public override string AnimAttack => "RockGolem_Attack";
    public override string AnimHitstun => "RockGolem_Takes_Damage";
    public override string AnimDeath => "RockGolem_Dies";

    protected override void InitializeStates()
    {
        PatrolState = new RockGolemPatrolState(this);
        CombatIdleState = new RockGolemCombatIdleState(this);
        ThrowState = new RockGolemThrowState(this);
        GiveUpState = new RockGolemGiveUpState(this);
        HitstunState = new GroundHitstunState(this);
        DeadState = new GroundDeadState(this);

        NonCombatSuper = new NonCombatSuperState(this);
        NonCombatSuper.SetInitialSubState(PatrolState);

        CombatSuper = new CombatSuperState(this);
        CombatSuper.SetInitialSubState(CombatIdleState);

        FSM.ChangeState(NonCombatSuper);
    }

    protected override void HandleDamageTaken(int damage, Vector2 knockback)
    {
        if (Ctx.isDead) return;

        // No same-platform check — ranged enemy re-engages on range alone
        HitstunState.ReturnState = Ctx.isPlayerInAggroRange
            ? (IState)CombatSuper
            : NonCombatSuper;
        FSM.ChangeState(HitstunState);
        ApplyKnockback(knockback);
    }

    protected override void HandleDeath()
    {
        FSM.ChangeState(DeadState);
    }

    /// <summary>
    /// Calculate a gravity-compensated throw velocity to arc toward the target.
    /// </summary>
    public Vector2 CalculateThrowVelocity(Vector2 targetPos)
    {
        Vector2 spawnPos = rockSpawnPoint != null
            ? (Vector2)rockSpawnPoint.position
            : (Vector2)transform.position;
        Vector2 toTarget = targetPos - spawnPos;

        // Flight time based on horizontal distance; clamped to avoid extreme velocities at close range
        float horizontalDist = Mathf.Abs(toTarget.x);
        float flightTime = Mathf.Max(horizontalDist / Profile.projectileSpeed, 0.3f);

        float gravity = Mathf.Abs(Physics2D.gravity.y);
        float vx = toTarget.x / flightTime;
        float vy = (toTarget.y + 0.5f * gravity * flightTime * flightTime) / flightTime;

        return new Vector2(vx, vy);
    }

    private void OnDrawGizmosSelected()
    {
        // Ledge check ray (green)
        Vector2 groundOrigin = GroundCheck != null
            ? (Vector2)GroundCheck.position
            : (Vector2)transform.position + new Vector2(FacingDirection * 0.5f, 0f);
        Gizmos.color = Color.green;
        Gizmos.DrawLine(groundOrigin, groundOrigin + Vector2.down * (Profile != null ? Profile.groundCheckDistance : 1f));

        // Wall check ray (red)
        Vector2 wallOrigin = WallCheck != null
            ? (Vector2)WallCheck.position
            : (Vector2)transform.position;
        Gizmos.color = Color.red;
        Gizmos.DrawLine(wallOrigin, wallOrigin + Vector2.right * FacingDirection * (Profile != null ? Profile.wallCheckDistance : 0.5f));

        if (Profile != null)
        {
            // Aggro range (yellow)
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, Profile.aggroRange);

            // Deaggro range (orange)
            Gizmos.color = new Color(1f, 0.5f, 0f);
            Gizmos.DrawWireSphere(transform.position, Profile.deaggroRange);

            // Attack range (red)
            Gizmos.color = new Color(1f, 0f, 0f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, Profile.attackRange);

            // LOS ray (cyan = clear, magenta = blocked)
            if (PlayerController.Instance != null)
            {
                Vector2 eyePos = (Vector2)transform.position + new Vector2(0f, Profile.losEyeOffsetY);
                Vector2 playerEye = (Vector2)PlayerController.Instance.transform.position + new Vector2(0f, Profile.losEyeOffsetY);
                bool hasLOS = Ctx != null && Ctx.hasLineOfSightToPlayer;
                Gizmos.color = hasLOS ? Color.cyan : Color.magenta;
                Gizmos.DrawLine(eyePos, playerEye);
            }
        }
    }
}
