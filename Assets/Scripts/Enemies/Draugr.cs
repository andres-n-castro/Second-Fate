using UnityEngine;

/// <summary>
/// Draugr — Basic ground enemy using the AI framework.
///
/// Behavior:
///   - Patrols back and forth on platforms at a slow speed.
///   - Detects player on same platform with hysteresis, then chases.
///   - Gives up at ledges or when player leaves range, with lockout pause.
///   - Turns around at ledges and walls during patrol.
///   - Attacks player with a melee strike when in range.
///   - Damageable via the Health component. Takes knockback during hitstun.
///   - Can be knocked off platforms during hitstun (ledge checks disabled).
///
/// Required components: Rigidbody2D, Collider2D, Health.
/// Optional: Animator (uses "Walking" bool, "Die", "MeleeWindup", "MeleeAttack" triggers).
///
/// Child objects/transforms needed in inspector:
///   - groundCheck  — positioned at the front-bottom edge for ledge detection
///   - wallCheck    — positioned at the front-center for wall detection
///   - meleeHitbox  — AttackHitbox child object; configure damage/knockback/targetLayers there
///
/// EnemyProfile attacks array must contain one entry named "Melee" with the
/// desired windupDuration, activeDuration, recoveryDuration, and cooldown.
/// </summary>
public class Draugr : EnemyBase
{
    [Header("Combat")]
    [SerializeField] private AttackHitbox meleeHitbox;

    // State references (public for state cross-references)
    public DraugrPatrolState PatrolState { get; private set; }
    public DraugrChaseState ChaseState { get; private set; }
    public DraugrGiveUpState GiveUpState { get; private set; }
    public DraugrMeleeAttackState MeleeAttackState { get; private set; }
    public GroundHitstunState HitstunState { get; private set; }
    public GroundDeadState DeadState { get; private set; }

    // Hitbox accessor for states
    public AttackHitbox MeleeHitbox => meleeHitbox;

    // Hysteresis timers (accessible by states)
    public float AcquireTargetTimer { get; set; }
    public float LoseTargetTimer { get; set; }

    protected override void InitializeStates()
    {
        PatrolState = new DraugrPatrolState(this);
        ChaseState = new DraugrChaseState(this);
        GiveUpState = new DraugrGiveUpState(this);
        MeleeAttackState = new DraugrMeleeAttackState(this);
        HitstunState = new GroundHitstunState(this);
        DeadState = new GroundDeadState(this);

        FSM.ChangeState(PatrolState);
    }

    protected override void HandleDamageTaken(int damage, Vector2 knockback)
    {
        if (Ctx.isDead) return;

        ApplyKnockback(knockback);

        // Don't return to the attack state after hitstun — go back to chase instead
        HitstunState.ReturnState = FSM.CurrentState == MeleeAttackState
            ? (IState)ChaseState
            : FSM.CurrentState;
        FSM.ChangeState(HitstunState);
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

            // LOS ray (green = clear, red = blocked)
            if (PlayerController.Instance != null)
            {
                Vector2 eyePos = (Vector2)transform.position + new Vector2(0f, Profile.losEyeOffsetY);
                Vector2 playerEye = (Vector2)PlayerController.Instance.transform.position + new Vector2(0f, Profile.losEyeOffsetY);
                bool hasLOS = Ctx != null && Ctx.hasLineOfSightToPlayer;
                Gizmos.color = hasLOS ? Color.cyan : Color.magenta;
                Gizmos.DrawLine(eyePos, playerEye);
            }

            // Facing deadzone — blue vertical lines either side of center
            if (Profile.draugrFacingDeadzoneX > 0f)
            {
                Gizmos.color = Color.blue;
                Vector2 dz = transform.position;
                Gizmos.DrawLine(dz + new Vector2(-Profile.draugrFacingDeadzoneX, -0.5f),
                                dz + new Vector2(-Profile.draugrFacingDeadzoneX,  0.5f));
                Gizmos.DrawLine(dz + new Vector2( Profile.draugrFacingDeadzoneX, -0.5f),
                                dz + new Vector2( Profile.draugrFacingDeadzoneX,  0.5f));
            }

            // Player-above threshold — white horizontal line above Draugr
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
