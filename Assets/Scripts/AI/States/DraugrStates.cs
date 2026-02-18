using UnityEngine;

// ---------------------------------------------------------------
//  DraugrPatrolState
//  Walk at moveSpeed. Ledge/wall avoidance ON. Stuck detection.
//  Idle pause on turn. Increments acquireTargetTimer if player
//  detected on same platform. Transitions to Chase when timer ready.
// ---------------------------------------------------------------
public class DraugrPatrolState : EnemyState
{
    private Draugr draugr;
    private float stuckTimer;
    private bool isIdle;
    private float idleTimer;

    private const float IdleDuration = 0.2f;
    private const float StuckThreshold = 0.3f;

    public DraugrPatrolState(Draugr draugr) : base(draugr)
    {
        this.draugr = draugr;
    }

    public override void Enter()
    {
        stuckTimer = 0f;
        isIdle = false;
        draugr.AcquireTargetTimer = 0f;
    }

    public override void FixedTick()
    {
        // Check for player detection with hysteresis
        if (owner.Ctx.isPlayerInAggroRange && owner.Ctx.isPlayerOnSamePlatform)
        {
            draugr.AcquireTargetTimer += Time.fixedDeltaTime;
            if (draugr.AcquireTargetTimer >= owner.Profile.acquireTargetDelay)
            {
                owner.FSM.ChangeState(draugr.ChaseState);
                return;
            }
        }
        else
        {
            draugr.AcquireTargetTimer = 0f;
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
        if (owner.Anim != null) owner.Anim.SetBool("Walking", true);
    }

    public override void Exit()
    {
        if (owner.Anim != null) owner.Anim.SetBool("Walking", false);
    }

    private void StartIdle()
    {
        isIdle = true;
        idleTimer = IdleDuration;
        owner.FlipFacing();
        owner.StopHorizontal();
        if (owner.Anim != null) owner.Anim.SetBool("Walking", false);
    }
}

// ---------------------------------------------------------------
//  DraugrChaseState
//  Walk toward player at chaseSpeed. Ledge/wall avoidance ON.
//  FacePlayer each tick. Transitions to GiveUp if player leaves
//  platform or deaggro range (with hysteresis timer).
// ---------------------------------------------------------------
public class DraugrChaseState : EnemyState
{
    private Draugr draugr;

    public DraugrChaseState(Draugr draugr) : base(draugr)
    {
        this.draugr = draugr;
    }

    public override void Enter()
    {
        draugr.LoseTargetTimer = 0f;
    }

    public override void FixedTick()
    {
        // Player dead or null → Patrol
        if (owner.Ctx.playerTransform == null)
        {
            owner.FSM.ChangeState(draugr.PatrolState);
            return;
        }

        // Check lose conditions with hysteresis
        bool shouldLose = !owner.Ctx.isPlayerOnSamePlatform
            || !owner.Ctx.isPlayerInDeaggroRange
            || !owner.Ctx.hasLineOfSightToPlayer;

        if (shouldLose)
        {
            draugr.LoseTargetTimer += Time.fixedDeltaTime;
            if (draugr.LoseTargetTimer >= owner.Profile.loseTargetDelay)
            {
                owner.FSM.ChangeState(draugr.GiveUpState);
                return;
            }
        }
        else
        {
            draugr.LoseTargetTimer = 0f;
        }

        // Ledge/wall avoidance — stop at edges, don't walk off
        if (owner.Ctx.nearLedgeAhead || owner.Ctx.nearWallAhead)
        {
            owner.StopHorizontal();
            if (owner.Anim != null) owner.Anim.SetBool("Walking", false);
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
            // Chase the player directly
            owner.FacePlayer();
            owner.MoveGround(owner.Profile.chaseSpeed);
        }
        if (owner.Anim != null) owner.Anim.SetBool("Walking", true);
    }

    public override void Exit()
    {
        if (owner.Anim != null) owner.Anim.SetBool("Walking", false);
    }
}

// ---------------------------------------------------------------
//  DraugrGiveUpState
//  Stop movement. Face last seen player direction. Pause for
//  giveUpPauseDuration. Does NOT re-enter Chase during lockout.
//  When timer expires → Patrol.
// ---------------------------------------------------------------
public class DraugrGiveUpState : EnemyState
{
    private Draugr draugr;
    private float timer;

    public DraugrGiveUpState(Draugr draugr) : base(draugr)
    {
        this.draugr = draugr;
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

        if (owner.Anim != null) owner.Anim.SetBool("Walking", false);
    }

    public override void FixedTick()
    {
        timer -= Time.fixedDeltaTime;
        if (timer <= 0f)
        {
            owner.FSM.ChangeState(draugr.PatrolState);
        }
    }
}
