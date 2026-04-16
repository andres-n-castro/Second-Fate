using UnityEngine;

// ---------------------------------------------------------------
//  SalamanderPatrolState
//  Walk at moveSpeed. Ledge/wall avoidance ON. Stuck detection.
//  Idle pause on turn. Increments acquireTargetTimer if player
//  detected on same platform. Transitions to Chase when timer ready.
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
        // Proximity-based sensing: bypass LOS gating (salamander is a sensing
        // elemental, not a vision hunter). Still requires the player to be on
        // the same platform — jumping over obstacles only happens once already
        // aggro'd and chasing, never as the initial aggro trigger.
        bool aggroConditions =
            owner.Ctx.playerDistance <= owner.Profile.aggroRange
            && owner.Ctx.isPlayerOnSamePlatform;

        // Blocked-path lockout: suppress re-aggro during lockout timer.
        // After lockout expires, also check that the path toward the player
        // is not still blocked by the same obstacle (wall/ledge in chase direction).
        if (aggroConditions && salamander.BlockedReaggroLockUntil > 0f)
        {
            if (Time.time < salamander.BlockedReaggroLockUntil)
            {
                // Lockout still active — suppress
                aggroConditions = false;
            }
            else
            {
                // Lockout expired — check if path toward player is still blocked
                float dirToPlayer = owner.Ctx.playerRelativePos.x;
                bool playerAhead = Mathf.Abs(dirToPlayer) > 0.1f
                    && Mathf.Sign(dirToPlayer) == owner.FacingDirection;

                if (playerAhead && (owner.Ctx.nearWallAhead || owner.Ctx.nearLedgeAhead))
                {
                    // Still blocked — keep suppressing
                    aggroConditions = false;
                }
                else
                {
                    // Path is clear — clear the lockout permanently
                    salamander.BlockedReaggroLockUntil = 0f;
                }
            }
        }

        if (aggroConditions)
        {
            salamander.AcquireTargetTimer += Time.fixedDeltaTime;
            if (salamander.AcquireTargetTimer >= owner.Profile.acquireTargetDelay)
            {
                owner.FSM.ChangeState(salamander.CombatSuper); // cross: NonCombat → Combat
                return;
            }
        }
        else
        {
            salamander.AcquireTargetTimer = 0f;
        }

        // ── Partial-overhang recovery ──
        // If not grounded by raycast but vertical velocity has settled near
        // zero for several consecutive frames, the collider is physically
        // resting on a surface edge. Walk toward ground to regain full footing
        // before normal patrol logic (which would flip endlessly at ledges).
        if (!owner.Ctx.isGrounded && Mathf.Abs(owner.Rb.linearVelocity.y) < 0.3f)
            partialSupportFrames++;
        else
            partialSupportFrames = 0;

        if (partialSupportFrames >= 3)
        {
            bool groundAhead = HasGroundAhead();
            bool groundBehind = HasGroundBehind();

            if (groundAhead)
            {
                owner.MoveGround(owner.Profile.moveSpeed);
            }
            else if (groundBehind)
            {
                owner.FlipFacing();
                owner.MoveGround(owner.Profile.moveSpeed);
            }
            else
            {
                owner.StopHorizontal();
            }
            if (owner.Anim != null) owner.Anim.SetBool(owner.AnimWalking, groundAhead || groundBehind);
            return;
        }

        // Idle pause after turning
        if (isIdle)
        {
            owner.StopHorizontal();
            idleTimer -= Time.fixedDeltaTime;
            if (idleTimer <= 0f)
            {
                isIdle = false;
            }
            return;
        }

        // Turn around at ledge or wall
        if (owner.Ctx.nearLedgeAhead || owner.Ctx.nearWallAhead)
        {
            StartIdle();
            return;
        }

        // Stuck detection
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

        // Walk forward
        owner.MoveGround(owner.Profile.moveSpeed);
        if (owner.Anim != null) owner.Anim.SetBool(owner.AnimWalking, true);

        // Reset stall counter when making real progress
        if (Mathf.Abs(owner.transform.position.x - lastProgressX) > 0.3f)
        {
            stallCount = 0;
            lastProgressX = owner.transform.position.x;
        }
    }

    public override void Exit()
    {
        if (owner.Anim != null) owner.Anim.SetBool(owner.AnimWalking, false);
    }

    private void StartIdle()
    {
        isIdle = true;
        idleTimer = IdleDuration;
        stallCount++;

        // After repeated stalls, jump to free the collider from the ledge edge
        // instead of flipping endlessly. Only jump if actually grounded.
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

        owner.FlipFacing();
        owner.StopHorizontal();
        if (owner.Anim != null) owner.Anim.SetBool(owner.AnimWalking, false);
    }

    private bool HasGroundAhead()
    {
        float extraOffset = 0.5f;
        Vector2 origin = owner.GroundCheck != null
            ? (Vector2)owner.GroundCheck.position + new Vector2(owner.FacingDirection * extraOffset, 0f)
            : (Vector2)owner.transform.position + new Vector2(owner.FacingDirection * (0.5f + extraOffset), 0f);
        return Physics2D.Raycast(origin, Vector2.down, owner.Profile.groundCheckDistance, owner.GroundLayer);
    }

    private bool HasGroundBehind()
    {
        float extraOffset = 0.5f;
        if (owner.GroundCheck == null)
        {
            Vector2 fallback = (Vector2)owner.transform.position + new Vector2(-owner.FacingDirection * (0.5f + extraOffset), 0f);
            return Physics2D.Raycast(fallback, Vector2.down, owner.Profile.groundCheckDistance, owner.GroundLayer);
        }
        Vector2 gcWorld = owner.GroundCheck.position;
        Vector2 center = owner.transform.position;
        float mirroredX = center.x - (gcWorld.x - center.x) - owner.FacingDirection * extraOffset;
        return Physics2D.Raycast(new Vector2(mirroredX, gcWorld.y), Vector2.down, owner.Profile.groundCheckDistance, owner.GroundLayer);
    }
}

// ---------------------------------------------------------------
//  SalamanderChaseState
//  Walk toward player at chaseSpeed. Ledge/wall avoidance ON.
//  FacePlayer each tick. Jump trigger when player is above.
//  Launch trigger when in attack range. Transitions to GiveUp if
//  player leaves deaggro range or LOS lost (with hysteresis timer).
// ---------------------------------------------------------------
public class SalamanderChaseState : EnemyState
{
    private MagmaSalamander salamander;
    private float stuckTimer;
    private float lastX;
    private int partialSupportFrames;

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
    }

    public override void FixedTick()
    {
        // Player dead or null → NonCombat
        if (owner.Ctx.playerTransform == null)
        {
            owner.FSM.ChangeState(salamander.NonCombatSuper); // cross: Combat → NonCombat
            return;
        }

        // Check lose conditions with hysteresis.
        // Salamander is a proximity sensor — don't drop target just because
        // an obstacle blocks LOS. Only lose on raw deaggro distance.
        bool shouldLose = !owner.Ctx.isPlayerInDeaggroRange;

        if (shouldLose)
        {
            salamander.LoseTargetTimer += Time.fixedDeltaTime;
            if (salamander.LoseTargetTimer >= owner.Profile.loseTargetDelay)
            {
                salamander.CombatSuper.ForceSubState(salamander.GiveUpState); // within Combat
                return;
            }
        }
        else
        {
            salamander.LoseTargetTimer = 0f;
        }

        // Launch attack trigger — in attack range, grounded, attack ready
        if (owner.Ctx.hasLineOfSightToPlayer
            && owner.Ctx.isGrounded
            && owner.Ctx.isPlayerInAttackRange
            && owner.IsAttackReady("Launch"))
        {
            salamander.CombatSuper.ForceSubState(salamander.LaunchState); // within Combat
            return;
        }

        // Jump trigger — player is above, grounded, jump off cooldown
        if (owner.Ctx.isGrounded
            && owner.Ctx.playerRelativePos.y > owner.Profile.jumpHeightThreshold
            && Time.time >= salamander.JumpCooldownUntil)
        {
            salamander.CombatSuper.ForceSubState(salamander.JumpState); // within Combat
            return;
        }

        // ── Partial-overhang recovery ──
        // If not grounded by raycast but vertical velocity has settled near
        // zero for several consecutive frames, the collider is physically
        // resting on a surface edge (partial support). Walk toward the
        // nearest direction with ground to regain full footing.
        if (!owner.Ctx.isGrounded && Mathf.Abs(owner.Rb.linearVelocity.y) < 0.3f)
            partialSupportFrames++;
        else
            partialSupportFrames = 0;

        if (partialSupportFrames >= 3)
        {
            bool groundAhead = HasGroundAhead();
            bool groundBehind = HasGroundBehind();

            if (groundAhead)
            {
                owner.MoveGround(owner.Profile.moveSpeed);
            }
            else if (groundBehind)
            {
                owner.FlipFacing();
                owner.MoveGround(owner.Profile.moveSpeed);
            }
            else
            {
                owner.StopHorizontal();
            }
            if (owner.Anim != null) owner.Anim.SetBool(owner.AnimWalking, groundAhead || groundBehind);
            return;
        }

        // ── Ledge/wall avoidance ──
        // Must run BEFORE stuck detection so the salamander waits for jump
        // cooldown on the ledge instead of the stuck timer giving up early.
        if (owner.Ctx.nearLedgeAhead || owner.Ctx.nearWallAhead)
        {
            // Player is below — chasing horizontally won't help.
            // Don't call FacePlayer here: flipping the facing direction
            // moves GroundCheck to the other side, which makes
            // nearLedgeAhead oscillate every frame and causes stutter.
            if (owner.Ctx.playerRelativePos.y < -0.5f)
            {
                if (owner.Ctx.isGrounded && Time.time >= salamander.JumpCooldownUntil)
                {
                    owner.FacePlayer();
                    salamander.CombatSuper.ForceSubState(salamander.JumpState);
                    return;
                }
                owner.StopHorizontal();
                if (owner.Anim != null) owner.Anim.SetBool(owner.AnimWalking, false);
                return;
            }

            owner.FacePlayer();

            // Wall blocks the path toward the player — give up
            if (HasWallAhead())
            {
                salamander.BlockedReaggroLockUntil = Time.time + owner.Profile.blockedReaggroCooldown;
                salamander.CombatSuper.ForceSubState(salamander.GiveUpState);
                return;
            }

            // If ground exists ahead (FacePlayer turned us away from a ledge),
            // fall through to normal chase.
            if (HasGroundAhead())
            {
                // Ground exists, no wall — chase normally below
            }
            else if (owner.Ctx.isGrounded && Time.time >= salamander.JumpCooldownUntil)
            {
                // Grounded with jump ready — jump toward player over the ledge
                salamander.CombatSuper.ForceSubState(salamander.JumpState);
                return;
            }
            else
            {
                // Wait for jump cooldown or grounded state
                owner.StopHorizontal();
                if (owner.Anim != null) owner.Anim.SetBool(owner.AnimWalking, false);
                return;
            }
        }

        // Stuck detection — if no meaningful horizontal progress, accumulate timer.
        // When stuck, try jumping toward the player to get unstuck (e.g. caught on
        // a ledge edge). Only give up if the jump is on cooldown or not grounded.
        float currentX = owner.transform.position.x;
        if (Mathf.Abs(currentX - lastX) < owner.Profile.minProgressThreshold)
        {
            stuckTimer += Time.fixedDeltaTime;
            if (stuckTimer >= owner.Profile.stuckTimeout)
            {
                stuckTimer = 0f;

                salamander.BlockedReaggroLockUntil = Time.time + owner.Profile.blockedReaggroCooldown;
                salamander.CombatSuper.ForceSubState(salamander.GiveUpState);
                return;
            }
        }
        else
        {
            stuckTimer = 0f;
            lastX = currentX;
        }

        // Player directly overhead on the same platform — hold position, don't thrash horizontally
        if (owner.Ctx.isPlayerOnSamePlatform
            && owner.Ctx.playerRelativePos.y > owner.Profile.playerAboveThresholdY
            && Mathf.Abs(owner.Ctx.playerRelativePos.x) < owner.Profile.facingDeadzoneX)
        {
            owner.StopHorizontal();
            if (owner.Anim != null) owner.Anim.SetBool(owner.AnimWalking, false);
            return;
        }

        // If LOS lost but still in lose delay, chase toward last seen position
        if (!owner.Ctx.hasLineOfSightToPlayer)
        {
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
        if (owner.Anim != null) owner.Anim.SetBool(owner.AnimWalking, true);
    }

    public override void Exit()
    {
        if (owner.Anim != null) owner.Anim.SetBool(owner.AnimWalking, false);
    }

    /// <summary>
    /// Fresh ground check in the current facing direction using GroundCheck position.
    /// Called after FacePlayer may have flipped the facing, so GroundCheck.position
    /// reflects the new front edge immediately.
    /// </summary>
    private bool HasGroundAhead()
    {
        float extraOffset = 0.5f;
        Vector2 origin = owner.GroundCheck != null
            ? (Vector2)owner.GroundCheck.position + new Vector2(owner.FacingDirection * extraOffset, 0f)
            : (Vector2)owner.transform.position + new Vector2(owner.FacingDirection * (0.5f + extraOffset), 0f);
        return Physics2D.Raycast(origin, Vector2.down, owner.Profile.groundCheckDistance, owner.GroundLayer);
    }

    /// <summary>
    /// Fresh ground check behind the enemy (mirrored GroundCheck position).
    /// </summary>
    private bool HasGroundBehind()
    {
        float extraOffset = 0.5f;
        if (owner.GroundCheck == null)
        {
            Vector2 fallback = (Vector2)owner.transform.position + new Vector2(-owner.FacingDirection * (0.5f + extraOffset), 0f);
            return Physics2D.Raycast(fallback, Vector2.down, owner.Profile.groundCheckDistance, owner.GroundLayer);
        }
        Vector2 gcWorld = owner.GroundCheck.position;
        Vector2 center = owner.transform.position;
        float mirroredX = center.x - (gcWorld.x - center.x) - owner.FacingDirection * extraOffset;
        return Physics2D.Raycast(new Vector2(mirroredX, gcWorld.y), Vector2.down, owner.Profile.groundCheckDistance, owner.GroundLayer);
    }

    /// <summary>
    /// Fresh wall check in the current facing direction using WallCheck position.
    /// Called after FacePlayer so it reflects the direction the salamander will jump.
    /// </summary>
    private bool HasWallAhead()
    {
        Vector2 origin = owner.WallCheck != null
            ? (Vector2)owner.WallCheck.position
            : (Vector2)owner.transform.position;
        return Physics2D.Raycast(origin, Vector2.right * owner.FacingDirection,
            owner.Profile.wallCheckDistance, owner.GroundLayer);
    }
}

// ---------------------------------------------------------------
//  SalamanderJumpState
//  Apply upward impulse toward player with forward force. Uses
//  hasLeftGround flag to avoid false landing detection on jump frame.
//  When grounded again → returns to Chase.
// ---------------------------------------------------------------
public class SalamanderJumpState : EnemyState
{
    private MagmaSalamander salamander;
    private bool hasLeftGround;
    private bool hasPeaked;
    private float distanceToPlayerAtLaunch;
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
        distanceToPlayerAtLaunch = owner.Ctx.playerDistance;

        // Apply jump impulse — upward + forward toward player
        float forwardForce = owner.FacingDirection * owner.Profile.jumpForwardForce;
        owner.Rb.linearVelocity = new Vector2(forwardForce, owner.Profile.jumpForce);

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
        // near zero for a sustained period, the collider is physically resting
        // on a surface edge even though the center-point raycast missed.
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

            // If the jump didn't get us closer to the player, give up
            if (owner.Ctx.playerDistance >= distanceToPlayerAtLaunch)
            {
                owner.FSM.ChangeState(salamander.NonCombatSuper);
                return;
            }

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
//  Timer-based body-dash attack: Windup → Active (dash with hitbox)
//  → Recovery. Direction locked at end of windup. Wall/ledge checks
//  end the dash early. After recovery → Chase or GiveUp.
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

        AttackDefinition atk = GetLaunchAttack();
        timer = atk != null ? atk.windupDuration : 0.3f;

        if (owner.Anim != null) owner.Anim.SetTrigger(owner.AnimAttack);
    }

    public override void FixedTick()
    {
        timer -= Time.fixedDeltaTime;

        switch (phase)
        {
            case Phase.Windup:
                owner.StopHorizontal(); // Hold position during windup
                if (timer <= 0f)
                {
                    // Lock direction at end of windup
                    owner.FacePlayer();

                    AttackDefinition atk = GetLaunchAttack();
                    dashSpeed = atk != null ? atk.dashSpeed : 10f;
                    timer = atk != null ? atk.activeDuration : 0.3f;

                    phase = Phase.Active;
                    if (salamander.LaunchHitbox != null) salamander.LaunchHitbox.Activate();
                }
                break;

            case Phase.Active:
                // Wall/ledge check — end dash early
                if (owner.Ctx.nearWallAhead || owner.Ctx.nearLedgeAhead)
                {
                    EndDash();
                    break;
                }

                owner.MoveGround(dashSpeed);

                if (timer <= 0f)
                {
                    EndDash();
                }
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
        // Safety net: deactivate hitbox if interrupted (e.g. hitstun)
        if (salamander.LaunchHitbox != null) salamander.LaunchHitbox.Deactivate();
        owner.StopHorizontal();
    }

    private void EndDash()
    {
        if (salamander.LaunchHitbox != null) salamander.LaunchHitbox.Deactivate();
        owner.StopHorizontal();

        phase = Phase.Recovery;
        AttackDefinition atk = GetLaunchAttack();
        timer = atk != null ? atk.recoveryDuration : 0.5f;

        owner.StartCooldown("Launch");
    }

    private void TransitionPostAttack()
    {
        if (owner.Ctx.isPlayerInAggroRange && owner.Ctx.isPlayerOnSamePlatform)
            salamander.CombatSuper.ForceSubState(salamander.ChaseState);   // within Combat
        else if (owner.Ctx.isPlayerInDeaggroRange)
            salamander.CombatSuper.ForceSubState(salamander.GiveUpState);  // within Combat
        else
            owner.FSM.ChangeState(salamander.NonCombatSuper);              // cross: Combat → NonCombat
    }

    private AttackDefinition GetLaunchAttack()
    {
        if (owner.Profile.attacks == null) return null;
        for (int i = 0; i < owner.Profile.attacks.Length; i++)
        {
            if (owner.Profile.attacks[i].attackName == "Launch")
                return owner.Profile.attacks[i];
        }
        return null;
    }
}

// ---------------------------------------------------------------
//  SalamanderGiveUpState
//  Stop movement. Face last seen player direction. Pause for
//  giveUpPauseDuration. When timer expires → Patrol.
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
        {
            owner.FaceDirection(lastDir.x > 0 ? 1 : -1);
        }

        if (owner.Anim != null) owner.Anim.SetBool(owner.AnimWalking, false);
    }

    public override void FixedTick()
    {
        timer -= Time.fixedDeltaTime;
        if (timer <= 0f)
        {
            owner.FSM.ChangeState(salamander.NonCombatSuper); // cross: Combat → NonCombat
        }
    }
}
