using UnityEngine;

/// <summary>
/// Magma Salamander — Fast ground chase enemy that can jump to reach
/// elevated players and launch its body at the player as an attack.
///
/// Behavior:
///   - Patrols back and forth on platforms at moveSpeed.
///   - Detects player on same platform with hysteresis, then chases at chaseSpeed.
///   - Jumps when player is above (jumpHeightThreshold) and grounded, with cooldown.
///   - Launches body at player (dash attack with hitbox) when in attack range.
///   - Gives up at ledges or when player leaves range, with lockout pause.
///   - Damageable via Health. Takes knockback during hitstun.
///
/// Required components: Rigidbody2D, Collider2D, Health.
/// Optional: Animator (uses "MagmaSalamander_Walking" bool,
///           "MagmaSalamander_Attack", "MagmaSalamander_Jump",
///           "MagmaSalamander_Takes_Damage", "MagmaSalamander_Dies" triggers).
///
/// Child objects/transforms needed in inspector:
///   - groundCheck   — positioned at the front-bottom edge for ledge detection
///   - wallCheck     — positioned at the front-center for wall detection
///   - launchHitbox  — AttackHitbox child object for body-launch attack
///
/// EnemyProfile attacks array must contain one entry named "Launch" with the
/// desired windupDuration, activeDuration, recoveryDuration, cooldown, and dashSpeed.
/// </summary>
public class MagmaSalamander : EnemyBase
{
    [Header("Combat")]
    [SerializeField] private AttackHitbox launchHitbox;

    // Outer FSM superstates
    public NonCombatSuperState NonCombatSuper { get; private set; }
    public CombatSuperState CombatSuper { get; private set; }

    // Substates
    public SalamanderPatrolState PatrolState { get; private set; }
    public SalamanderChaseState ChaseState { get; private set; }
    public SalamanderJumpState JumpState { get; private set; }
    public SalamanderLaunchState LaunchState { get; private set; }
    public SalamanderGiveUpState GiveUpState { get; private set; }

    // Outer override states
    public GroundHitstunState HitstunState { get; private set; }
    public FallingDeadState DeadState { get; private set; }

    // Hitbox accessor for states
    public AttackHitbox LaunchHitbox => launchHitbox;

    // Cached attack definition
    public AttackDefinition LaunchAttack { get; private set; }

    // Animation parameter names
    public override string AnimWalking => "MagmaSalamander_Walking";
    public override string AnimAttack => "MagmaSalamander_Attack";
    public override string AnimHitstun => "MagmaSalamander_Takes_Damage";
    public override string AnimDeath => "MagmaSalamander_Dies";
    public virtual string AnimJump => "MagmaSalamander_Jump";

    // Hysteresis timers (accessible by states)
    public float AcquireTargetTimer { get; set; }
    public float LoseTargetTimer { get; set; }

    // Blocked-path re-aggro lockout
    public float BlockedReaggroLockUntil { get; set; }

    // Jump cooldown (world time when jump is allowed again)
    public float JumpCooldownUntil { get; set; }

    // Jump tracking — gravity is managed in FixedUpdate like Vidar
    public bool IsJumping { get; set; }

    protected override void FixedUpdate()
    {
        base.FixedUpdate();

        // Gravity management every frame, matching Vidar's pattern:
        // jump hang gravity is the exception, default gravity is the rule.
        if (IsJumping && Mathf.Abs(Rb.linearVelocity.y) < Profile.jumpHangThreshold)
        {
            SetJumpHangGravity();
        }
        else
        {
            RestoreGravity();
        }
    }

    protected override void InitializeStates()
    {
        // Cache attack definition
        if (Profile.attacks != null)
        {
            foreach (var atk in Profile.attacks)
            {
                if (atk.attackName == "Launch") { LaunchAttack = atk; break; }
            }
        }

        PatrolState = new SalamanderPatrolState(this);
        ChaseState = new SalamanderChaseState(this);
        JumpState = new SalamanderJumpState(this);
        LaunchState = new SalamanderLaunchState(this);
        GiveUpState = new SalamanderGiveUpState(this);
        HitstunState = new GroundHitstunState(this);
        DeadState = new FallingDeadState(this);

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
        if (launchHitbox != null) launchHitbox.Deactivate();
        FSM.ChangeState(DeadState);
    }

    // ---------------------------------------------------------------
    //  Shared Raycast Helpers
    //  Used by Patrol and Chase states for ledge/wall checks with
    //  profile-configurable offsets (beyond what the perception system
    //  provides at the GroundCheck position).
    // ---------------------------------------------------------------

    public bool HasGroundAhead()
    {
        float offset = Profile.ledgeCheckAheadOffset;
        Vector2 origin = GroundCheck != null
            ? (Vector2)GroundCheck.position + new Vector2(FacingDirection * offset, 0f)
            : (Vector2)transform.position + new Vector2(FacingDirection * (0.5f + offset), 0f);
        return Physics2D.Raycast(origin, Vector2.down, Profile.groundCheckDistance, GroundLayer);
    }

    public bool HasGroundBehind()
    {
        float offset = Profile.ledgeCheckBehindOffset;
        if (GroundCheck == null)
        {
            Vector2 fallback = (Vector2)transform.position + new Vector2(-FacingDirection * (0.5f + offset), 0f);
            return Physics2D.Raycast(fallback, Vector2.down, Profile.groundCheckDistance, GroundLayer);
        }
        Vector2 gcWorld = GroundCheck.position;
        Vector2 center = transform.position;
        float mirroredX = center.x - (gcWorld.x - center.x) - FacingDirection * offset;
        return Physics2D.Raycast(new Vector2(mirroredX, gcWorld.y), Vector2.down, Profile.groundCheckDistance, GroundLayer);
    }

    public bool HasWallAhead()
    {
        Vector2 origin = WallCheck != null
            ? (Vector2)WallCheck.position
            : (Vector2)transform.position;
        return Physics2D.Raycast(origin, Vector2.right * FacingDirection,
            Profile.wallCheckDistance, GroundLayer);
    }

    /// <summary>
    /// Shared partial-overhang recovery. When the collider rests on a surface
    /// edge (not grounded by raycast but vertical velocity near zero for several
    /// frames), walks toward the nearest direction with ground.
    /// Returns true if recovery is active (caller should skip normal logic).
    /// </summary>
    public bool TryPartialOverhangRecovery(ref int frameCounter)
    {
        if (!Ctx.isGrounded && Mathf.Abs(Rb.linearVelocity.y) < 0.3f)
            frameCounter++;
        else
            frameCounter = 0;

        if (frameCounter < 3) return false;

        bool groundAhead = HasGroundAhead();
        bool groundBehind = HasGroundBehind();

        if (groundAhead)
            MoveGround(Profile.moveSpeed);
        else if (groundBehind)
        {
            FlipFacing();
            MoveGround(Profile.moveSpeed);
        }
        else
            StopHorizontal();

        SetWalkingAnim(groundAhead || groundBehind);
        return true;
    }

    public void SetWalkingAnim(bool walking)
    {
        if (Anim != null) Anim.SetBool(AnimWalking, walking);
    }

    private void OnDrawGizmosSelected()
    {
        // Ledge check ray — ahead (green)
        float gizmoGroundDist = Profile != null ? Profile.groundCheckDistance : 1f;
        float aheadOffset = Profile != null ? Profile.ledgeCheckAheadOffset : 0.5f;
        Vector2 groundOrigin = GroundCheck != null
            ? (Vector2)GroundCheck.position + new Vector2(FacingDirection * aheadOffset, 0f)
            : (Vector2)transform.position + new Vector2(FacingDirection * (0.5f + aheadOffset), 0f);
        Gizmos.color = Color.green;
        Gizmos.DrawLine(groundOrigin, groundOrigin + Vector2.down * gizmoGroundDist);

        // Ledge check ray — behind (dark green)
        float behindOffset = Profile != null ? Profile.ledgeCheckBehindOffset : 0.5f;
        Vector2 behindOrigin;
        if (GroundCheck != null)
        {
            Vector2 gcWorld = GroundCheck.position;
            Vector2 center = transform.position;
            float mirroredX = center.x - (gcWorld.x - center.x) - FacingDirection * behindOffset;
            behindOrigin = new Vector2(mirroredX, gcWorld.y);
        }
        else
        {
            behindOrigin = (Vector2)transform.position + new Vector2(-FacingDirection * (0.5f + behindOffset), 0f);
        }
        Gizmos.color = new Color(0f, 0.5f, 0f);
        Gizmos.DrawLine(behindOrigin, behindOrigin + Vector2.down * gizmoGroundDist);

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

            // Jump height threshold — white horizontal line
            if (Profile.jumpHeightThreshold > 0f)
            {
                Gizmos.color = Color.white;
                Vector2 ab = transform.position;
                Gizmos.DrawLine(ab + new Vector2(-0.5f, Profile.jumpHeightThreshold),
                                ab + new Vector2( 0.5f, Profile.jumpHeightThreshold));
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
        }
    }

}
