using UnityEngine;

/// <summary>
/// Thing — Fast ground-based chase enemy derived from Draugr behavior,
/// with all attack and backstep logic removed.
///
/// Behavior:
///   - Patrols back and forth on platforms at moveSpeed.
///   - Detects player on same platform with hysteresis, then chases at chaseSpeed.
///   - Gives up at ledges or when player leaves range, with lockout pause.
///   - Turns around at ledges and walls during patrol.
///   - NO melee attack. NO backstep. Purely chase-pressure.
///   - Damageable via Health. Takes knockback during hitstun.
///
/// Required components: Rigidbody2D, Collider2D, Health.
/// Optional: Animator (uses "Thing_Walking" bool, "Thing_Takes_Damage", "Thing_Dies" triggers).
///
/// Child objects/transforms needed in inspector:
///   - groundCheck  — positioned at the front-bottom edge for ledge detection
///   - wallCheck    — positioned at the front-center for wall detection
///
/// EnemyProfile should have a higher chaseSpeed than Draugr and NO attacks defined.
/// </summary>
public class Thing : EnemyBase
{
    // Outer FSM superstates
    public NonCombatSuperState NonCombatSuper { get; private set; }
    public CombatSuperState CombatSuper { get; private set; }

    // Substates
    public ThingPatrolState PatrolState { get; private set; }
    public ThingChaseState ChaseState { get; private set; }
    public ThingGiveUpState GiveUpState { get; private set; }

    // Outer override states
    public GroundHitstunState HitstunState { get; private set; }
    public GroundDeadState DeadState { get; private set; }

    // Animation parameter names
    public override string AnimWalking => "Thing_Walking";
    public override string AnimAttack => "Thing_Attack";
    public override string AnimHitstun => "Thing_Takes_Damage";
    public override string AnimDeath => "Thing_Dies";

    // Hysteresis timers (accessible by states)
    public float AcquireTargetTimer { get; set; }
    public float LoseTargetTimer { get; set; }

    // Blocked-path re-aggro lockout (world time when aggro is allowed again after obstacle block)
    public float BlockedReaggroLockUntil { get; set; }

    protected override void InitializeStates()
    {
        PatrolState = new ThingPatrolState(this);
        ChaseState = new ThingChaseState(this);
        GiveUpState = new ThingGiveUpState(this);
        HitstunState = new GroundHitstunState(this);
        DeadState = new GroundDeadState(this);

        NonCombatSuper = new NonCombatSuperState(this);
        NonCombatSuper.SetInitialSubState(PatrolState);

        CombatSuper = new CombatSuperState(this);
        CombatSuper.SetInitialSubState(ChaseState);

        FSM.ChangeState(NonCombatSuper);
    }

    protected override void HandleDamageTaken(int damage, Vector2 knockback)
    {
        if (Ctx.isDead) return;

        HitstunState.ReturnState = (Ctx.isPlayerInAggroRange && Ctx.isPlayerOnSamePlatform)
            ? (IState)CombatSuper
            : NonCombatSuper;
        FSM.ChangeState(HitstunState);
        ApplyKnockback(knockback);
    }

    protected override void HandleDeath()
    {
        FSM.ChangeState(DeadState);
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

            // LOS ray (cyan = clear, magenta = blocked)
            if (PlayerController.Instance != null)
            {
                Vector2 eyePos = (Vector2)transform.position + new Vector2(0f, Profile.losEyeOffsetY);
                Vector2 playerEye = (Vector2)PlayerController.Instance.transform.position + new Vector2(0f, Profile.losEyeOffsetY);
                bool hasLOS = Ctx != null && Ctx.hasLineOfSightToPlayer;
                Gizmos.color = hasLOS ? Color.cyan : Color.magenta;
                Gizmos.DrawLine(eyePos, playerEye);
            }

            // Facing deadzone — blue vertical lines
            if (Profile.facingDeadzoneX > 0f)
            {
                Gizmos.color = Color.blue;
                Vector2 dz = transform.position;
                Gizmos.DrawLine(dz + new Vector2(-Profile.facingDeadzoneX, -0.5f),
                                dz + new Vector2(-Profile.facingDeadzoneX,  0.5f));
                Gizmos.DrawLine(dz + new Vector2( Profile.facingDeadzoneX, -0.5f),
                                dz + new Vector2( Profile.facingDeadzoneX,  0.5f));
            }

            // Player-above threshold — white horizontal line
            if (Profile.playerAboveThresholdY > 0f)
            {
                Gizmos.color = Color.white;
                Vector2 ab = transform.position;
                Gizmos.DrawLine(ab + new Vector2(-0.5f, Profile.playerAboveThresholdY),
                                ab + new Vector2( 0.5f, Profile.playerAboveThresholdY));
            }
        }
    }
}
