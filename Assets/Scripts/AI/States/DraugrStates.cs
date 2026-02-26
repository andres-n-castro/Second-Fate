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
                owner.FSM.ChangeState(draugr.CombatSuper); // cross: NonCombat → Combat
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
    private float stuckTimer;
    private float lastX;

    public DraugrChaseState(Draugr draugr) : base(draugr)
    {
        this.draugr = draugr;
    }

    public override void Enter()
    {
        draugr.LoseTargetTimer = 0f;
        stuckTimer = 0f;
        lastX = owner.transform.position.x;
    }

    public override void FixedTick()
    {
        // Player dead or null → NonCombat
        if (owner.Ctx.playerTransform == null)
        {
            owner.FSM.ChangeState(draugr.NonCombatSuper); // cross: Combat → NonCombat
            return;
        }

        // Check lose conditions with hysteresis
        // isPlayerOnSamePlatform intentionally excluded — lose target on LOS/range only,
        // not because the player stepped on an obstacle. Wall check handles movement naturally.
        bool shouldLose = !owner.Ctx.isPlayerInDeaggroRange
            || !owner.Ctx.hasLineOfSightToPlayer;

        if (shouldLose)
        {
            draugr.LoseTargetTimer += Time.fixedDeltaTime;
            if (draugr.LoseTargetTimer >= owner.Profile.loseTargetDelay)
            {
                draugr.CombatSuper.ForceSubState(draugr.GiveUpState); // within Combat
                return;
            }
        }
        else
        {
            draugr.LoseTargetTimer = 0f;
        }

        // Melee attack trigger — all conditions must be met
        if (owner.Ctx.hasLineOfSightToPlayer
            && owner.Ctx.isPlayerOnSamePlatform
            && owner.Ctx.isGrounded
            && owner.Ctx.isPlayerInAttackRange
            && owner.IsAttackReady("Melee"))
        {
            draugr.CombatSuper.ForceSubState(draugr.MeleeAttackState); // within Combat
            return;
        }

        // Stuck detection — if no meaningful horizontal progress, accumulate timer
        float currentX = owner.transform.position.x;
        if (Mathf.Abs(currentX - lastX) < owner.Profile.draugrMinProgressThreshold)
        {
            stuckTimer += Time.fixedDeltaTime;
            if (stuckTimer >= owner.Profile.draugrStuckTimeout)
            {
                draugr.CombatSuper.ForceSubState(draugr.GiveUpState);
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
            && Mathf.Abs(owner.Ctx.playerRelativePos.x) < owner.Profile.draugrFacingDeadzoneX)
        {
            owner.StopHorizontal();
            if (owner.Anim != null) owner.Anim.SetBool("Walking", false);
            return;
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
            // Deadzone-aware facing — only flip when player is clearly to one side
            float relX = owner.Ctx.playerRelativePos.x;
            if (Mathf.Abs(relX) >= owner.Profile.draugrFacingDeadzoneX)
                owner.FaceDirection(relX > 0 ? 1 : -1);

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
            owner.FSM.ChangeState(draugr.NonCombatSuper); // cross: Combat → NonCombat
        }
    }
}

// ---------------------------------------------------------------
//  DraugrMeleeAttackState
//  Timer-based melee: Windup → Active (hitbox on) → Recovery.
//  Enemy stops and faces player for the full duration.
//  After recovery, returns to Chase if still aggro, GiveUp if
//  player left platform/range, Patrol otherwise.
// ---------------------------------------------------------------
public class DraugrMeleeAttackState : EnemyState
{
    private enum Phase { Windup, Active, Recovery }

    private Draugr draugr;
    private Phase phase;
    private float timer;

    public DraugrMeleeAttackState(Draugr draugr) : base(draugr)
    {
        this.draugr = draugr;
    }

    public override void Enter()
    {
        phase = Phase.Windup;
        owner.StopHorizontal();
        owner.FacePlayer();

        AttackDefinition atk = GetMeleeAttack();
        timer = atk != null ? atk.windupDuration : 0.3f;

        if (owner.Anim != null) owner.Anim.SetTrigger("MeleeAttack");
    }

    public override void FixedTick()
    {
        timer -= Time.fixedDeltaTime;

        switch (phase)
        {
            case Phase.Windup:
                if (timer <= 0f)
                {
                    phase = Phase.Active;
                    AttackDefinition atk = GetMeleeAttack();
                    timer = atk != null ? atk.activeDuration : 0.2f;

                    if (draugr.MeleeHitbox != null) draugr.MeleeHitbox.Activate();
                }
                break;

            case Phase.Active:
                if (timer <= 0f)
                {
                    if (draugr.MeleeHitbox != null) draugr.MeleeHitbox.Deactivate();
                    owner.StartCooldown("Melee");

                    phase = Phase.Recovery;
                    AttackDefinition atk = GetMeleeAttack();
                    timer = atk != null ? atk.recoveryDuration : 0.4f;
                }
                break;

            case Phase.Recovery:
                if (timer <= 0f)
                    TransitionPostAttack();
                break;
        }
    }

    public override void Exit()
    {
        // Safety net: deactivate hitbox if interrupted mid-active (e.g. hitstun)
        if (draugr.MeleeHitbox != null) draugr.MeleeHitbox.Deactivate();
        owner.StopHorizontal();
    }

    private void TransitionPostAttack()
    {
        if (owner.Ctx.isPlayerInAggroRange && owner.Ctx.isPlayerOnSamePlatform)
            draugr.CombatSuper.ForceSubState(draugr.ChaseState);   // within Combat
        else if (owner.Ctx.isPlayerInDeaggroRange)
            draugr.CombatSuper.ForceSubState(draugr.GiveUpState);  // within Combat
        else
            owner.FSM.ChangeState(draugr.NonCombatSuper);          // cross: Combat → NonCombat
    }

    private AttackDefinition GetMeleeAttack()
    {
        if (owner.Profile.attacks == null) return null;
        for (int i = 0; i < owner.Profile.attacks.Length; i++)
        {
            if (owner.Profile.attacks[i].attackName == "Melee")
                return owner.Profile.attacks[i];
        }
        return null;
    }
}
