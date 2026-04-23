using UnityEngine;

// ---------------------------------------------------------------
//  SalamanderPatrolState
//  Walk at moveSpeed. Ledge/wall → idle pause → flip. Stuck detection.
//  Proximity-based aggro with hysteresis and blocked-path lockout.
//  Transitions to CombatSuper when acquire timer fills.
// ---------------------------------------------------------------
public class SalamanderPatrolState : EnemyState
{
    private MagmaSalamander salamander;
    private float stuckTimer;
    private bool isIdle;
    private float idleTimer;
    private int stallCount;
    private float lastProgressX;
    private int partialSupportFrames;

    private const float IdleDuration = 0.2f;
    private const float StuckThreshold = 0.3f;
    private const int MaxStallsBeforeLock = 2;

    public SalamanderPatrolState(MagmaSalamander salamander) : base(salamander)
    {
        this.salamander = salamander;
    }

    public override void Enter()
    {
        stuckTimer = 0f;
        isIdle = false;
        stallCount = 0;
        lastProgressX = owner.transform.position.x;
        partialSupportFrames = 0;
        salamander.AcquireTargetTimer = 0f;

        // Face toward the player's last known position so patrol walks that way
        float dirToLastSeen = owner.Ctx.lastSeenPlayerPos.x - owner.transform.position.x;
        if (Mathf.Abs(dirToLastSeen) > 0.1f)
            owner.FaceDirection(dirToLastSeen > 0 ? 1 : -1);
    }

    public override void FixedTick()
    {
        // --- Aggro detection ---
        if (TryAggro()) return;

        // --- Partial-overhang recovery ---
        if (salamander.TryPartialOverhangRecovery(ref partialSupportFrames)) return;

        // --- Idle pause before turning ---
        if (isIdle)
        {
            owner.StopHorizontal();
            idleTimer -= Time.fixedDeltaTime;
            if (idleTimer <= 0f)
            {
                isIdle = false;
                owner.FlipFacing();
            }
            return;
        }

        // --- Ledge/wall → idle pause ---
        if (owner.Ctx.nearLedgeAhead || owner.Ctx.nearWallAhead)
        {
            StartIdle();
            return;
        }

        // --- Stuck detection ---
        if (Mathf.Abs(owner.Rb.linearVelocity.x) < 0.1f)
        {
            stuckTimer += Time.fixedDeltaTime;
            if (stuckTimer > StuckThreshold)
            {
                stuckTimer = 0f;
                StartIdle();
                return;
            }
        }
        else
        {
            stuckTimer = 0f;
        }

        // --- Walk forward ---
        owner.MoveGround(owner.Profile.moveSpeed);
        salamander.SetWalkingAnim(true);

        // Reset stall counter when making real progress
        if (Mathf.Abs(owner.transform.position.x - lastProgressX) > 0.3f)
        {
            stallCount = 0;
            lastProgressX = owner.transform.position.x;
        }
    }

    public override void Exit()
    {
        salamander.SetWalkingAnim(false);
    }

    /// <summary>
    /// Proximity-based aggro with hysteresis timer and blocked-path lockout.
    /// Returns true if state transitioned (caller should return).
    /// </summary>
    private bool TryAggro()
    {
        bool aggroConditions =
            owner.Ctx.playerDistance <= owner.Profile.aggroRange
            && owner.Ctx.isPlayerOnSamePlatform;

        // Blocked-path lockout: suppress re-aggro during lockout timer
        if (aggroConditions && salamander.BlockedReaggroLockUntil > 0f)
        {
            if (Time.time < salamander.BlockedReaggroLockUntil)
            {
                aggroConditions = false;
            }
            else
            {
                // Lockout expired — check if path toward player is still blocked
                float dirToPlayer = owner.Ctx.playerRelativePos.x;
                bool playerAhead = Mathf.Abs(dirToPlayer) > 0.1f
                    && Mathf.Sign(dirToPlayer) == owner.FacingDirection;

                if (playerAhead && (owner.Ctx.nearWallAhead || owner.Ctx.nearLedgeAhead))
                    aggroConditions = false;
                else
                    salamander.BlockedReaggroLockUntil = 0f;
            }
        }

        if (aggroConditions)
        {
            salamander.AcquireTargetTimer += Time.fixedDeltaTime;
            if (salamander.AcquireTargetTimer >= owner.Profile.acquireTargetDelay)
            {
                owner.FSM.ChangeState(salamander.CombatSuper);
                return true;
            }
        }
        else
        {
            salamander.AcquireTargetTimer = 0f;
        }

        return false;
    }

    private void StartIdle()
    {
        isIdle = true;
        idleTimer = IdleDuration;
        stallCount++;

        // After repeated stalls, hop to free the collider from a ledge edge.
        // This is a terrain recovery, not a combat jump — raw impulse is intentional
        // (no IsJumping flag, no JumpState transition).
        if (stallCount >= MaxStallsBeforeLock)
        {
            stallCount = 0;
            if (owner.Ctx.isGrounded)
            {
                owner.FacePlayer();
                owner.Rb.linearVelocity = new Vector2(
                    owner.FacingDirection * owner.Profile.jumpForwardForce,
                    owner.Profile.jumpForce);
            }
            return;
        }

        owner.StopHorizontal();
        salamander.SetWalkingAnim(false);
    }
}

// ---------------------------------------------------------------
//  SalamanderChaseState
//  Chase player at chaseSpeed. Decision priority:
//    1. Player null → NonCombat
//    2. Deaggro distance (hysteresis) → GiveUp
//    3. Attack range + LOS + grounded → Launch
//    4. Jump lunge (blocked / player above / player far) → Jump
//    5. Partial-overhang recovery
//    6. Obstacle avoidance (force-walk / ledge / wall)
//    7. Stuck detection → GiveUp
//    8. Player overhead deadzone → hold
//    9. Chase movement (LOS-aware)
// ---------------------------------------------------------------
public class SalamanderChaseState : EnemyState
{
    private MagmaSalamander salamander;
    private float stuckTimer;
    private float lastX;
    private int partialSupportFrames;

    // Force-walk recovery for false-positive ledge reads on tiny terrain lips
    private float ledgeStuckTimer;
    private float forceWalkTimer;

    private const float LedgeStuckThreshold = 0.20f;
    private const float ForceWalkDuration   = 0.25f;
    private const float LedgeStuckVelThreshold = 0.1f;

    public SalamanderChaseState(MagmaSalamander salamander) : base(salamander)
    {
        this.salamander = salamander;
    }

    public override void Enter()
    {
        salamander.LoseTargetTimer = 0f;
        stuckTimer = 0f;
        lastX = owner.transform.position.x;
        partialSupportFrames = 0;
        ledgeStuckTimer = 0f;
        forceWalkTimer = 0f;
    }

    public override void FixedTick()
    {
        // --- 1. Exit: player gone ---
        if (owner.Ctx.playerTransform == null)
        {
            owner.FSM.ChangeState(salamander.NonCombatSuper);
            return;
        }

        // --- 2. Deaggro with hysteresis ---
        if (CheckDeaggro()) return;

        // --- 3. Attack trigger ---
        if (owner.Ctx.hasLineOfSightToPlayer
            && owner.Ctx.isGrounded
            && owner.Ctx.isPlayerInAttackRange
            && owner.IsAttackReady("Launch"))
        {
            salamander.CombatSuper.ForceSubState(salamander.LaunchState);
            return;
        }

        // --- 4. Jump lunge (requires reason: blocked, player above, or player far) ---
        if (owner.Ctx.isGrounded && Time.time >= salamander.JumpCooldownUntil)
        {
            bool blocked = owner.Ctx.nearLedgeAhead || owner.Ctx.nearWallAhead;
            bool playerAbove = owner.Ctx.playerRelativePos.y > owner.Profile.jumpHeightThreshold;
            bool playerFar = owner.Ctx.playerDistance > owner.Profile.attackRange * 1.5f;

            if (blocked || playerAbove || playerFar)
            {
                salamander.CombatSuper.ForceSubState(salamander.JumpState);
                return;
            }
        }

        // --- 5. Partial-overhang recovery ---
        if (salamander.TryPartialOverhangRecovery(ref partialSupportFrames)) return;

        // --- 6. Obstacle avoidance ---
        if (HandleObstacles()) return;

        // --- 7. Stuck detection ---
        if (CheckStuck()) return;

        // --- 8. Player directly overhead: hold position ---
        if (owner.Ctx.isPlayerOnSamePlatform
            && owner.Ctx.playerRelativePos.y > owner.Profile.playerAboveThresholdY
            && Mathf.Abs(owner.Ctx.playerRelativePos.x) < owner.Profile.facingDeadzoneX)
        {
            owner.StopHorizontal();
            salamander.SetWalkingAnim(false);
            return;
        }

        // --- 9. Chase movement ---
        ChaseMovement();
    }

    public override void Exit()
    {
        salamander.SetWalkingAnim(false);
    }

    // ---------------------------------------------------------------
    //  Decision Helpers
    // ---------------------------------------------------------------

    private bool CheckDeaggro()
    {
        bool shouldLose = !owner.Ctx.isPlayerInDeaggroRange;

        if (shouldLose)
        {
            salamander.LoseTargetTimer += Time.fixedDeltaTime;
            if (salamander.LoseTargetTimer >= owner.Profile.loseTargetDelay)
            {
                salamander.CombatSuper.ForceSubState(salamander.GiveUpState);
                return true;
            }
        }
        else
        {
            salamander.LoseTargetTimer = 0f;
        }

        return false;
    }

    /// <summary>
    /// Handle force-walk recovery and ledge/wall blocking.
    /// Returns true if the tick was consumed (caller should return).
    /// </summary>
    private bool HandleObstacles()
    {
        bool ledgeBlocked = owner.Ctx.nearLedgeAhead || owner.Ctx.nearWallAhead;

        // Active force-walk: ignore ledge reads, only respect real walls
        if (forceWalkTimer > 0f)
        {
            forceWalkTimer -= Time.fixedDeltaTime;
            owner.FacePlayer();

            if (salamander.HasWallAhead())
            {
                forceWalkTimer = 0f;
                GiveUpBlocked();
                return true;
            }

            owner.MoveGround(owner.Profile.chaseSpeed);
            salamander.SetWalkingAnim(true);

            // Cleared the lip — end recovery early
            if (!ledgeBlocked)
                forceWalkTimer = 0f;

            return true;
        }

        if (!ledgeBlocked)
        {
            ledgeStuckTimer = 0f;
            return false;
        }

        owner.FacePlayer();

        // Wall blocks the path → give up
        if (salamander.HasWallAhead())
        {
            ledgeStuckTimer = 0f;
            GiveUpBlocked();
            return true;
        }

        // Ground ahead after FacePlayer (turned away from ledge) → chase normally
        if (salamander.HasGroundAhead())
        {
            ledgeStuckTimer = 0f;
            return false;
        }

        // Jump available → jump over the obstacle
        if (owner.Ctx.isGrounded && Time.time >= salamander.JumpCooldownUntil)
        {
            ledgeStuckTimer = 0f;
            salamander.CombatSuper.ForceSubState(salamander.JumpState);
            return true;
        }

        // Stuck on ledge lip with jump on cooldown → accumulate for force-walk
        if (owner.Ctx.isGrounded
            && Mathf.Abs(owner.Rb.linearVelocity.x) < LedgeStuckVelThreshold)
        {
            ledgeStuckTimer += Time.fixedDeltaTime;
            if (ledgeStuckTimer >= LedgeStuckThreshold)
            {
                ledgeStuckTimer = 0f;
                forceWalkTimer = ForceWalkDuration;
                return true;
            }
        }

        // Wait for jump cooldown or grounded state
        owner.StopHorizontal();
        salamander.SetWalkingAnim(false);
        return true;
    }

    private bool CheckStuck()
    {
        float currentX = owner.transform.position.x;
        if (Mathf.Abs(currentX - lastX) < owner.Profile.minProgressThreshold)
        {
            stuckTimer += Time.fixedDeltaTime;
            if (stuckTimer >= owner.Profile.stuckTimeout)
            {
                stuckTimer = 0f;
                GiveUpBlocked();
                return true;
            }
        }
        else
        {
            stuckTimer = 0f;
            lastX = currentX;
        }
        return false;
    }

    private void GiveUpBlocked()
    {
        salamander.BlockedReaggroLockUntil = Time.time + owner.Profile.blockedReaggroCooldown;
        salamander.CombatSuper.ForceSubState(salamander.GiveUpState);
    }

    // ---------------------------------------------------------------
    //  Movement
    // ---------------------------------------------------------------

    private void ChaseMovement()
    {
        if (!owner.Ctx.hasLineOfSightToPlayer)
        {
            // LOS lost but still in lose delay — chase toward last seen position
            float lastSeenDir = owner.Ctx.lastSeenPlayerPos.x - owner.transform.position.x;
            if (Mathf.Abs(lastSeenDir) > 0.5f)
            {
                owner.FaceDirection(lastSeenDir > 0 ? 1 : -1);
                owner.MoveGround(owner.Profile.chaseSpeed);
            }
            else
            {
                owner.StopHorizontal();
            }
        }
        else
        {
            // Deadzone-aware facing — only flip when player is clearly to one side
            float relX = owner.Ctx.playerRelativePos.x;
            if (Mathf.Abs(relX) >= owner.Profile.facingDeadzoneX)
                owner.FaceDirection(relX > 0 ? 1 : -1);

            owner.MoveGround(owner.Profile.chaseSpeed);
        }
        salamander.SetWalkingAnim(true);
    }
}

// ---------------------------------------------------------------
//  SalamanderJumpState
//  Apply upward impulse toward player with distance-scaled forward
//  force. Higher jump when blocked or player is above. Uses
//  hasLeftGround flag to avoid false landing detection on jump frame.
//  Always returns to Chase on landing.
// ---------------------------------------------------------------
public class SalamanderJumpState : EnemyState
{
    private MagmaSalamander salamander;
    private bool hasLeftGround;
    private bool hasPeaked;
    private float airTimer;
    private float settledTimer;

    private const float SettledVelocityThreshold = 0.5f;
    private const float SettledDuration = 0.15f;

    public SalamanderJumpState(MagmaSalamander salamander) : base(salamander)
    {
        this.salamander = salamander;
    }

    public override void Enter()
    {
        hasLeftGround = false;
        hasPeaked = false;
        airTimer = 0f;
        settledTimer = 0f;
        salamander.IsJumping = true;
        owner.FacePlayer();

        // Jump impulse — lunge toward the player.
        // Forward force scales with horizontal distance to cover the gap.
        float horizDist = Mathf.Abs(owner.Ctx.playerRelativePos.x);
        float forwardSpeed = Mathf.Max(owner.Profile.jumpForwardForce, horizDist * 2f);
        float forwardForce = owner.FacingDirection * forwardSpeed;

        // Jump higher when blocked by an obstacle or player is above
        float jumpForce = owner.Profile.jumpForce;
        bool blocked = owner.Ctx.nearWallAhead || owner.Ctx.nearLedgeAhead;
        bool playerAbove = owner.Ctx.playerRelativePos.y > owner.Profile.jumpHeightThreshold;
        if (blocked || playerAbove)
            jumpForce *= 1.5f;

        float vertBoost = Mathf.Max(0f, owner.Ctx.playerRelativePos.y * 0.5f);
        owner.Rb.linearVelocity = new Vector2(forwardForce, jumpForce + vertBoost);

        if (owner.Anim != null) owner.Anim.SetTrigger(salamander.AnimJump);
    }

    public override void FixedTick()
    {
        // Wait until we've actually left the ground before checking for landing
        if (!hasLeftGround)
        {
            if (!owner.Ctx.isGrounded)
                hasLeftGround = true;
            return;
        }

        airTimer += Time.fixedDeltaTime;

        // Track peak — once velocity turns downward, we've peaked
        if (!hasPeaked && owner.Rb.linearVelocity.y < 0f)
            hasPeaked = true;

        // Primary landing: raycast-based grounded check
        bool landed = owner.Ctx.isGrounded;

        // Fallback landing: after peaking, if vertical velocity has settled
        // near zero for a sustained period, the collider is resting on a
        // surface edge even though the center-point raycast missed.
        if (!landed && hasPeaked)
        {
            if (Mathf.Abs(owner.Rb.linearVelocity.y) < SettledVelocityThreshold)
            {
                settledTimer += Time.fixedDeltaTime;
                if (settledTimer >= SettledDuration)
                    landed = true;
            }
            else
            {
                settledTimer = 0f;
            }
        }

        if (landed)
        {
            salamander.IsJumping = false;
            salamander.JumpCooldownUntil = Time.time + owner.Profile.jumpCooldown;
            salamander.CombatSuper.ForceSubState(salamander.ChaseState);
        }
    }

    public override void Exit()
    {
        salamander.IsJumping = false;
    }
}

// ---------------------------------------------------------------
//  SalamanderLaunchState
//  Timer-based body-dash attack: Windup → Active (dash + hitbox)
//  → Recovery. Direction locked at end of windup. Wall/ledge checks
//  end the dash early. Uses cached LaunchAttack from MagmaSalamander.
// ---------------------------------------------------------------
public class SalamanderLaunchState : EnemyState
{
    private enum Phase { Windup, Active, Recovery }

    private MagmaSalamander salamander;
    private Phase phase;
    private float timer;
    private float dashSpeed;

    public SalamanderLaunchState(MagmaSalamander salamander) : base(salamander)
    {
        this.salamander = salamander;
    }

    public override void Enter()
    {
        phase = Phase.Windup;
        owner.StopHorizontal();
        owner.FacePlayer();

        var atk = salamander.LaunchAttack;
        timer = atk != null ? atk.windupDuration : 0.3f;

        if (owner.Anim != null) owner.Anim.SetTrigger(owner.AnimAttack);
    }

    public override void FixedTick()
    {
        timer -= Time.fixedDeltaTime;

        switch (phase)
        {
            case Phase.Windup:
                owner.StopHorizontal();
                if (timer <= 0f)
                {
                    // Lock direction at end of windup
                    owner.FacePlayer();

                    var atk = salamander.LaunchAttack;
                    dashSpeed = atk != null ? atk.dashSpeed : 10f;
                    timer = atk != null ? atk.activeDuration : 0.3f;

                    phase = Phase.Active;
                    if (salamander.LaunchHitbox != null) salamander.LaunchHitbox.Activate();
                }
                break;

            case Phase.Active:
                if (owner.Ctx.nearWallAhead || owner.Ctx.nearLedgeAhead)
                {
                    EndDash();
                    break;
                }

                owner.MoveGround(dashSpeed);

                if (timer <= 0f)
                    EndDash();
                break;

            case Phase.Recovery:
                owner.StopHorizontal();
                if (timer <= 0f)
                    TransitionPostAttack();
                break;
        }
    }

    public override void Exit()
    {
        if (salamander.LaunchHitbox != null) salamander.LaunchHitbox.Deactivate();
        owner.StopHorizontal();
    }

    private void EndDash()
    {
        if (salamander.LaunchHitbox != null) salamander.LaunchHitbox.Deactivate();
        owner.StopHorizontal();

        phase = Phase.Recovery;
        var atk = salamander.LaunchAttack;
        timer = atk != null ? atk.recoveryDuration : 0.5f;

        owner.StartCooldown("Launch");
    }

    private void TransitionPostAttack()
    {
        if (owner.Ctx.isPlayerInAggroRange && owner.Ctx.isPlayerOnSamePlatform)
            salamander.CombatSuper.ForceSubState(salamander.ChaseState);
        else if (owner.Ctx.isPlayerInDeaggroRange)
            salamander.CombatSuper.ForceSubState(salamander.GiveUpState);
        else
            owner.FSM.ChangeState(salamander.NonCombatSuper);
    }
}

// ---------------------------------------------------------------
//  SalamanderGiveUpState
//  Stop movement. Face last seen player direction. Pause for
//  giveUpPauseDuration. When timer expires → NonCombat (Patrol).
// ---------------------------------------------------------------
public class SalamanderGiveUpState : EnemyState
{
    private MagmaSalamander salamander;
    private float timer;

    public SalamanderGiveUpState(MagmaSalamander salamander) : base(salamander)
    {
        this.salamander = salamander;
    }

    public override void Enter()
    {
        timer = owner.Profile.giveUpPauseDuration;
        owner.StopHorizontal();

        // Face direction player was last seen
        Vector2 lastDir = owner.Ctx.lastSeenPlayerPos - (Vector2)owner.transform.position;
        if (Mathf.Abs(lastDir.x) > 0.01f)
            owner.FaceDirection(lastDir.x > 0 ? 1 : -1);

        salamander.SetWalkingAnim(false);
    }

    public override void FixedTick()
    {
        timer -= Time.fixedDeltaTime;
        if (timer <= 0f)
        {
            owner.FSM.ChangeState(salamander.NonCombatSuper);
        }
    }
}
