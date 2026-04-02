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

    private const float IdleDuration = 0.2f;
    private const float StuckThreshold = 0.3f;

    public SalamanderPatrolState(MagmaSalamander salamander) : base(salamander)
    {
        this.salamander = salamander;
    }

    public override void Enter()
    {
        stuckTimer = 0f;
        isIdle = false;
        salamander.AcquireTargetTimer = 0f;

        // Face toward the player's last known position so patrol walks that way
        float dirToLastSeen = owner.Ctx.lastSeenPlayerPos.x - owner.transform.position.x;
        if (Mathf.Abs(dirToLastSeen) > 0.1f)
            owner.FaceDirection(dirToLastSeen > 0 ? 1 : -1);
    }

    public override void FixedTick()
    {
        // Check for player detection with hysteresis
        bool aggroConditions = owner.Ctx.isPlayerInAggroRange && owner.Ctx.isPlayerOnSamePlatform;

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
    }

    public override void Exit()
    {
        if (owner.Anim != null) owner.Anim.SetBool(owner.AnimWalking, false);
    }

    private void StartIdle()
    {
        isIdle = true;
        idleTimer = IdleDuration;
        owner.FlipFacing();
        owner.StopHorizontal();
        if (owner.Anim != null) owner.Anim.SetBool(owner.AnimWalking, false);
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

    public SalamanderChaseState(MagmaSalamander salamander) : base(salamander)
    {
        this.salamander = salamander;
    }

    public override void Enter()
    {
        salamander.LoseTargetTimer = 0f;
        stuckTimer = 0f;
        lastX = owner.transform.position.x;
    }

    public override void FixedTick()
    {
        // Player dead or null → NonCombat
        if (owner.Ctx.playerTransform == null)
        {
            owner.FSM.ChangeState(salamander.NonCombatSuper); // cross: Combat → NonCombat
            return;
        }

        // Check lose conditions with hysteresis
        bool shouldLose = !owner.Ctx.isPlayerInDeaggroRange
            || !owner.Ctx.hasLineOfSightToPlayer;

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

        // Stuck detection — if no meaningful horizontal progress, accumulate timer
        float currentX = owner.transform.position.x;
        if (Mathf.Abs(currentX - lastX) < owner.Profile.minProgressThreshold)
        {
            stuckTimer += Time.fixedDeltaTime;
            if (stuckTimer >= owner.Profile.stuckTimeout)
            {
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

        // Ledge/wall avoidance — stop at edges, don't walk off
        if (owner.Ctx.nearLedgeAhead || owner.Ctx.nearWallAhead)
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

    public SalamanderJumpState(MagmaSalamander salamander) : base(salamander)
    {
        this.salamander = salamander;
    }

    public override void Enter()
    {
        hasLeftGround = false;
        owner.FacePlayer();

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

        // Landed — return to chase
        if (owner.Ctx.isGrounded)
        {
            salamander.JumpCooldownUntil = Time.time + owner.Profile.jumpCooldown;
            salamander.CombatSuper.ForceSubState(salamander.ChaseState); // within Combat
        }
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
