using UnityEngine;

// ---------------------------------------------------------------
//  FWSHoverPatrolState
//  Wander within roamRadius of home position. Obstacle-aware target
//  sampling, smoothed velocity, stuck detection with recovery.
// ---------------------------------------------------------------
public class FWSHoverPatrolState : EnemyState
{
    private FallenWarriorSpirit fws;
    private Vector2 roamTarget;
    private float roamTimer;

    // Stuck detection
    private float stuckTimer;
    private float lastDistToTarget;

    // Facing hysteresis
    private float facingHoldTimer;
    private const float FacingHoldDuration = 0.15f;

    public FWSHoverPatrolState(FallenWarriorSpirit fws) : base(fws)
    {
        this.fws = fws;
    }

    public override void Enter()
    {
        PickNewRoamTarget();
        stuckTimer = 0f;
        lastDistToTarget = float.MaxValue;
        facingHoldTimer = 0f;
    }

    public override void FixedTick()
    {
        // Check for player detection
        if (owner.Ctx.isPlayerInAggroRange)
        {
            owner.FSM.ChangeState(fws.EngageDecisionState);
            return;
        }

        float dt = Time.fixedDeltaTime;
        Vector2 pos = owner.transform.position;
        Vector2 toTarget = roamTarget - pos;
        float distToTarget = toTarget.magnitude;

        // Arrived at target — pick a new one
        if (distToTarget < owner.Profile.patrolArriveThreshold)
        {
            PickNewRoamTarget();
            toTarget = roamTarget - pos;
            distToTarget = toTarget.magnitude;
        }

        // Stuck detection: not making progress toward target
        float progress = lastDistToTarget - distToTarget;
        lastDistToTarget = distToTarget;

        if (progress < owner.Profile.stuckMinProgress * dt)
        {
            stuckTimer += dt;
            if (stuckTimer >= owner.Profile.stuckSeconds)
            {
                PickNewRoamTarget();
                stuckTimer = 0f;
                lastDistToTarget = float.MaxValue;
                return;
            }
        }
        else
        {
            stuckTimer = 0f;
        }

        // Timer-based target refresh
        roamTimer -= dt;
        if (roamTimer <= 0f) PickNewRoamTarget();

        // Smoothed movement — lerp velocity instead of hard-setting
        Vector2 desiredVel = distToTarget > 0.01f
            ? (toTarget / distToTarget) * owner.Profile.roamSpeed
            : Vector2.zero;
        owner.Rb.linearVelocity = Vector2.Lerp(
            owner.Rb.linearVelocity, desiredVel, owner.Profile.patrolSmoothing * dt);

        // Facing with hysteresis — prevent rapid flipping
        facingHoldTimer -= dt;
        if (facingHoldTimer <= 0f)
        {
            float velX = owner.Rb.linearVelocity.x;
            if (Mathf.Abs(velX) > 0.2f)
            {
                int newFacing = velX > 0 ? 1 : -1;
                if (newFacing != owner.FacingDirection)
                {
                    owner.FaceDirection(newFacing);
                    facingHoldTimer = FacingHoldDuration;
                }
            }
        }
    }

    public override void Exit()
    {
        owner.StopAll();
    }

    private void PickNewRoamTarget()
    {
        roamTimer = owner.Profile.roamChangeInterval;
        Vector2 pos = owner.transform.position;
        LayerMask blockMask = owner.GroundLayer | owner.ObstacleLayer;
        float radius = owner.Profile.roamRadius;
        float clearance = owner.Profile.patrolTargetClearanceRadius;
        int samples = owner.Profile.patrolTargetSampleCount;

        for (int i = 0; i < samples; i++)
        {
            Vector2 candidate = fws.HomePosition + Random.insideUnitCircle * radius;

            // Reject if candidate overlaps an obstacle
            if (Physics2D.OverlapCircle(candidate, clearance, blockMask) != null)
                continue;

            // Reject if path to candidate is blocked
            Vector2 toCandidate = candidate - pos;
            float dist = toCandidate.magnitude;
            if (dist > 0.1f)
            {
                RaycastHit2D hit = Physics2D.CircleCast(
                    pos, clearance, toCandidate.normalized, dist, blockMask);
                if (hit.collider != null)
                    continue;
            }

            roamTarget = candidate;
            stuckTimer = 0f;
            lastDistToTarget = float.MaxValue;
            return;
        }

        // All samples blocked — hover slightly above current position as safe fallback
        roamTarget = pos + new Vector2(0f, 0.5f);
        stuckTimer = 0f;
        lastDistToTarget = float.MaxValue;
    }
}

// ---------------------------------------------------------------
//  FWSEngageDecisionState
//  Chooses between dash attack and reposition based on adaptive
//  weighting from player dash tracking. Deaggros if player leaves range.
// ---------------------------------------------------------------
public class FWSEngageDecisionState : EnemyState
{
    private FallenWarriorSpirit fws;

    public FWSEngageDecisionState(FallenWarriorSpirit fws) : base(fws)
    {
        this.fws = fws;
    }

    public override void Enter()
    {
        owner.StopAll();
        owner.FacePlayer();
    }

    public override void FixedTick()
    {
        // Deaggro check
        if (!owner.Ctx.isPlayerInDeaggroRange)
        {
            fws.HomePosition = owner.transform.position;
            owner.FSM.ChangeState(fws.PatrolState);
            return;
        }

        // LOS lost — disengage, return to patrol
        if (!owner.Ctx.hasLineOfSightToPlayer)
        {
            fws.HomePosition = owner.transform.position;
            owner.FSM.ChangeState(fws.PatrolState);
            return;
        }

        // Compute adaptive dash weight
        float dashWeight = owner.Profile.baseDashWeight
            + owner.Ctx.playerDashCountRecent * owner.Profile.dashWeightBoostPerDash;
        float repositionWeight = 1f;
        float totalWeight = dashWeight + repositionWeight;

        float roll = Random.value * totalWeight;

        if (roll < dashWeight && owner.IsAttackReady("Dash"))
        {
            owner.FSM.ChangeState(fws.DashAttackState);
        }
        else
        {
            owner.FSM.ChangeState(fws.RepositionState);
        }
    }
}

// ---------------------------------------------------------------
//  FWSDashAttackState
//  Timer-based attack: Windup → Active (dash) → Recovery.
//  Hitbox activates during Active phase. Interruptible by hitstun/death.
// ---------------------------------------------------------------
public class FWSDashAttackState : EnemyState
{
    private enum Phase { Windup, Active, Recovery }

    private FallenWarriorSpirit fws;
    private Phase phase;
    private float timer;
    private Vector2 dashDirection;

    public FWSDashAttackState(FallenWarriorSpirit fws) : base(fws)
    {
        this.fws = fws;
    }

    public override void Enter()
    {
        phase = Phase.Windup;
        owner.StopAll();
        owner.FacePlayer();

        // Get attack definition for timing
        AttackDefinition atk = GetDashAttack();
        timer = atk != null ? atk.windupDuration : 0.5f;

        if (owner.Anim != null) owner.Anim.SetTrigger("Windup");
    }

    public override void FixedTick()
    {
        timer -= Time.fixedDeltaTime;

        switch (phase)
        {
            case Phase.Windup:
                // Cancel dash if LOS lost during windup — don't dash into walls
                if (!owner.Ctx.hasLineOfSightToPlayer)
                {
                    fws.HomePosition = owner.transform.position;
                    owner.FSM.ChangeState(fws.PatrolState);
                    return;
                }

                if (timer <= 0f)
                {
                    // Lock dash direction at end of windup
                    if (owner.Ctx.playerTransform != null)
                    {
                        dashDirection = ((Vector2)owner.Ctx.playerTransform.position
                            - (Vector2)owner.transform.position).normalized;
                    }
                    else
                    {
                        dashDirection = Vector2.right * owner.FacingDirection;
                    }

                    phase = Phase.Active;
                    AttackDefinition atk = GetDashAttack();
                    timer = atk != null ? atk.dashDuration : 0.3f;

                    if (fws.DashHitbox != null) fws.DashHitbox.Activate();
                    if (owner.Anim != null) owner.Anim.SetTrigger("Dash");
                }
                break;

            case Phase.Active:
                owner.MoveDirection(dashDirection, GetDashAttack()?.dashSpeed ?? 15f);

                if (timer <= 0f)
                {
                    if (fws.DashHitbox != null) fws.DashHitbox.Deactivate();
                    owner.StopAll();

                    phase = Phase.Recovery;
                    AttackDefinition atk = GetDashAttack();
                    timer = atk != null ? atk.recoveryDuration : 0.3f;

                    owner.StartCooldown("Dash");
                }
                break;

            case Phase.Recovery:
                if (timer <= 0f)
                {
                    owner.FSM.ChangeState(fws.EngageDecisionState);
                }
                break;
        }
    }

    public override void Exit()
    {
        if (fws.DashHitbox != null) fws.DashHitbox.Deactivate();
        owner.StopAll();
    }

    private AttackDefinition GetDashAttack()
    {
        if (owner.Profile.attacks == null) return null;
        for (int i = 0; i < owner.Profile.attacks.Length; i++)
        {
            if (owner.Profile.attacks[i].attackName == "Dash")
                return owner.Profile.attacks[i];
        }
        return null;
    }
}

// ---------------------------------------------------------------
//  FWSRepositionState
//  Fly to a random offset around the player (biased upward).
//  Then transitions back to EngageDecision.
// ---------------------------------------------------------------
public class FWSRepositionState : EnemyState
{
    private FallenWarriorSpirit fws;
    private Vector2 repositionTarget;
    private float timer;
    private float maxTime;

    public FWSRepositionState(FallenWarriorSpirit fws) : base(fws)
    {
        this.fws = fws;
    }

    public override void Enter()
    {
        float repositionDistance = 4f;
        float repositionSpeed = owner.Profile.flySpeed;

        // Pick a random offset around player, biased upward
        if (owner.Ctx.playerTransform != null)
        {
            Vector2 offset = Random.insideUnitCircle.normalized * repositionDistance;
            offset.y = Mathf.Abs(offset.y) + 1f;
            repositionTarget = (Vector2)owner.Ctx.playerTransform.position + offset;
        }
        else
        {
            repositionTarget = (Vector2)owner.transform.position + Random.insideUnitCircle * repositionDistance;
        }

        maxTime = repositionDistance / repositionSpeed + 0.5f;
        timer = 0f;
    }

    public override void FixedTick()
    {
        timer += Time.fixedDeltaTime;

        // Bail out if player is gone or no longer reachable
        if (!owner.Ctx.isPlayerInDeaggroRange || !owner.Ctx.hasLineOfSightToPlayer)
        {
            owner.StopAll();
            fws.HomePosition = owner.transform.position;
            owner.FSM.ChangeState(fws.PatrolState);
            return;
        }

        Vector2 dir = repositionTarget - (Vector2)owner.transform.position;
        if (dir.magnitude < 0.5f || timer >= maxTime)
        {
            owner.StopAll();
            owner.FSM.ChangeState(fws.EngageDecisionState);
            return;
        }

        Vector2 moveDir = owner.AvoidObstacles(dir.normalized);
        owner.MoveDirection(moveDir, owner.Profile.flySpeed);

        if (Mathf.Abs(moveDir.x) > 0.01f)
            owner.FaceDirection(moveDir.x > 0 ? 1 : -1);
    }

    public override void Exit()
    {
        owner.StopAll();
    }
}
