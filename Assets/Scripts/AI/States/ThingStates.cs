using UnityEngine;

// ---------------------------------------------------------------
//  ThingPatrolState
//  Walk at moveSpeed. Ledge/wall avoidance ON. Stuck detection.
//  Idle pause on turn. Increments acquireTargetTimer if player
//  detected on same platform. Transitions to Chase when timer ready.
// ---------------------------------------------------------------
public class ThingPatrolState : EnemyState
{
    private Thing thing;
    private float stuckTimer;
    private bool isIdle;
    private float idleTimer;

    private const float IdleDuration = 0.2f;
    private const float StuckThreshold = 0.3f;

    public ThingPatrolState(Thing thing) : base(thing)
    {
        this.thing = thing;
    }

    public override void Enter()
    {
        stuckTimer = 0f;
        isIdle = false;
        thing.AcquireTargetTimer = 0f;

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
        if (aggroConditions && thing.BlockedReaggroLockUntil > 0f)
        {
            if (Time.time < thing.BlockedReaggroLockUntil)
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
                    thing.BlockedReaggroLockUntil = 0f;
                }
            }
        }

        if (aggroConditions)
        {
            thing.AcquireTargetTimer += Time.fixedDeltaTime;
            if (thing.AcquireTargetTimer >= owner.Profile.acquireTargetDelay)
            {
                owner.FSM.ChangeState(thing.CombatSuper); // cross: NonCombat → Combat
                return;
            }
        }
        else
        {
            thing.AcquireTargetTimer = 0f;
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
//  ThingChaseState
//  Walk toward player at chaseSpeed. Ledge/wall avoidance ON.
//  FacePlayer each tick. Transitions to GiveUp if player leaves
//  platform or deaggro range (with hysteresis timer).
//  No melee attack or backstep — chase-only pressure enemy.
// ---------------------------------------------------------------
public class ThingChaseState : EnemyState
{
    private Thing thing;
    private float stuckTimer;
    private float lastX;

    public ThingChaseState(Thing thing) : base(thing)
    {
        this.thing = thing;
    }

    public override void Enter()
    {
        thing.LoseTargetTimer = 0f;
        stuckTimer = 0f;
        lastX = owner.transform.position.x;
    }

    public override void FixedTick()
    {
        // Player dead or null → NonCombat
        if (owner.Ctx.playerTransform == null)
        {
            owner.FSM.ChangeState(thing.NonCombatSuper); // cross: Combat → NonCombat
            return;
        }

        // Check lose conditions with hysteresis
        // isPlayerOnSamePlatform intentionally excluded — lose target on LOS/range only,
        // not because the player stepped on an obstacle. Wall check handles movement naturally.
        bool shouldLose = !owner.Ctx.isPlayerInDeaggroRange
            || !owner.Ctx.hasLineOfSightToPlayer;

        if (shouldLose)
        {
            thing.LoseTargetTimer += Time.fixedDeltaTime;
            if (thing.LoseTargetTimer >= owner.Profile.loseTargetDelay)
            {
                thing.CombatSuper.ForceSubState(thing.GiveUpState); // within Combat
                return;
            }
        }
        else
        {
            thing.LoseTargetTimer = 0f;
        }

        // Stuck detection — if no meaningful horizontal progress, accumulate timer
        float currentX = owner.transform.position.x;
        if (Mathf.Abs(currentX - lastX) < owner.Profile.minProgressThreshold)
        {
            stuckTimer += Time.fixedDeltaTime;
            if (stuckTimer >= owner.Profile.stuckTimeout)
            {
                thing.BlockedReaggroLockUntil = Time.time + owner.Profile.blockedReaggroCooldown;
                thing.CombatSuper.ForceSubState(thing.GiveUpState);
                return;
            }
        }
        else
        {
            stuckTimer = 0f;
            lastX = currentX;
        }

        // Player directly overhead on the same platform — hold position, don't thrash horizontally
        // All three required: same platform + within horizontal deadzone + above Y threshold
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
//  ThingGiveUpState
//  Stop movement. Face last seen player direction. Pause for
//  giveUpPauseDuration. Does NOT re-enter Chase during lockout.
//  When timer expires → Patrol.
// ---------------------------------------------------------------
public class ThingGiveUpState : EnemyState
{
    private Thing thing;
    private float timer;

    public ThingGiveUpState(Thing thing) : base(thing)
    {
        this.thing = thing;
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
            owner.FSM.ChangeState(thing.NonCombatSuper); // cross: Combat → NonCombat
        }
    }
}
