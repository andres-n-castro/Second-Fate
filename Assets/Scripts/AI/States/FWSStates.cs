using UnityEngine;

// ---------------------------------------------------------------
//  FWSPatrolState
//  Wander within roamRadius of home position. Obstacle-aware target
//  sampling, smoothed velocity, stuck detection with recovery.
// ---------------------------------------------------------------
public class FWSPatrolState : EnemyState
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

    public FWSPatrolState(FallenWarriorSpirit fws) : base(fws)
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
        fws.TryPickValidTarget(fws.HomePosition, owner.Profile.roamRadius, out roamTarget);
        stuckTimer = 0f;
        lastDistToTarget = float.MaxValue;
    }
}

// ---------------------------------------------------------------
//  FWSEngageDecisionState
//  Brief pause before choosing dash attack or reposition.
//  Prevents rapid state-cycling by holding for a cooldown period.
// ---------------------------------------------------------------
public class FWSEngageDecisionState : EnemyState
{
    private FallenWarriorSpirit fws;
    private float cooldownTimer;

    public FWSEngageDecisionState(FallenWarriorSpirit fws) : base(fws)
    {
        this.fws = fws;
    }

    public override void Enter()
    {
        owner.FacePlayer();
        cooldownTimer = owner.Profile.repositionDecisionCooldown;
    }

    public override void FixedTick()
    {
        float dt = Time.fixedDeltaTime;

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

        // Smoothly decelerate during decision pause instead of hard stop
        owner.Rb.linearVelocity = Vector2.Lerp(
            owner.Rb.linearVelocity, Vector2.zero, owner.Profile.patrolSmoothing * dt);

        // Wait out cooldown before making a decision
        cooldownTimer -= dt;
        if (cooldownTimer > 0f) return;

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
//  Dashes to an intercept point that stops short of the player so
//  walls behind the player don't block the path check.
//  Mid-dash wall detection ends the dash gracefully on collision.
// ---------------------------------------------------------------
public class FWSDashAttackState : EnemyState
{
    private enum Phase { Windup, Active, Recovery }

    private FallenWarriorSpirit fws;
    private Phase phase;
    private float timer;
    private Vector2 dashDirection;
    private float dashSpeed;

    public FWSDashAttackState(FallenWarriorSpirit fws) : base(fws)
    {
        this.fws = fws;
    }

    public override void Enter()
    {
        phase = Phase.Windup;
        owner.StopAll();
        owner.FacePlayer();

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
                // Cancel dash if LOS lost during windup
                if (!owner.Ctx.hasLineOfSightToPlayer)
                {
                    fws.HomePosition = owner.transform.position;
                    owner.FSM.ChangeState(fws.PatrolState);
                    return;
                }

                if (timer <= 0f)
                {
                    // Lock dash direction at end of windup
                    Vector2 enemyPos = owner.transform.position;
                    if (owner.Ctx.playerTransform != null)
                    {
                        dashDirection = ((Vector2)owner.Ctx.playerTransform.position
                            - enemyPos).normalized;
                    }
                    else
                    {
                        dashDirection = Vector2.right * owner.FacingDirection;
                    }

                    AttackDefinition atk = GetDashAttack();
                    dashSpeed = atk != null ? atk.dashSpeed : 15f;
                    float fullDashDist = dashSpeed * (atk != null ? atk.dashDuration : 0.3f);
                    float playerDist = owner.Ctx.playerDistance;
                    float stopShort = owner.Profile.dashStopShortDistance;
                    float castRadius = owner.Profile.dashPathCastRadius;

                    // Intercept distance: stop short of the player, clamped to max dash range
                    float interceptDist = Mathf.Min(fullDashDist, Mathf.Max(0f, playerDist - stopShort));

                    // Too close to dash meaningfully — reposition instead
                    if (interceptDist < 0.5f)
                    {
                        owner.FSM.ChangeState(fws.RepositionState);
                        return;
                    }

                    // Path check to intercept point (not through the player into the wall behind)
                    if (!fws.IsPathClear(dashDirection, interceptDist, castRadius))
                    {
                        owner.FSM.ChangeState(fws.RepositionState);
                        return;
                    }

                    phase = Phase.Active;
                    // Scale dash duration to intercept distance
                    timer = interceptDist / dashSpeed;

                    if (fws.DashHitbox != null) fws.DashHitbox.Activate();
                    if (owner.Anim != null) owner.Anim.SetTrigger("Dash");
                }
                break;

            case Phase.Active:
                // Mid-dash wall check — lookahead 2 frames for early termination
                float lookAhead = dashSpeed * Time.fixedDeltaTime * 2f;
                if (!fws.IsPathClear(dashDirection, lookAhead, owner.Profile.dashPathCastRadius))
                {
                    EndDash();
                    break;
                }

                owner.MoveDirection(dashDirection, dashSpeed);

                if (timer <= 0f)
                {
                    EndDash();
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

    private void EndDash()
    {
        if (fws.DashHitbox != null) fws.DashHitbox.Deactivate();
        owner.StopAll();

        phase = Phase.Recovery;
        AttackDefinition atk = GetDashAttack();
        timer = atk != null ? atk.recoveryDuration : 0.3f;

        owner.StartCooldown("Dash");
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
//  Fly to an obstacle-aware offset around the player (biased upward).
//  Smoothed velocity, stuck detection with recovery, facing hysteresis.
// ---------------------------------------------------------------
public class FWSRepositionState : EnemyState
{
    private FallenWarriorSpirit fws;
    private Vector2 repositionTarget;
    private float maxTime;
    private float timer;

    // Stuck detection
    private float stuckTimer;
    private float lastDistToTarget;

    // Facing hysteresis
    private float facingHoldTimer;
    private const float FacingHoldDuration = 0.15f;

    public FWSRepositionState(FallenWarriorSpirit fws) : base(fws)
    {
        this.fws = fws;
    }

    public override void Enter()
    {
        PickRepositionTarget();
        stuckTimer = 0f;
        lastDistToTarget = float.MaxValue;
        facingHoldTimer = 0f;
        timer = 0f;

        float dist = owner.Profile.repositionDistance;
        maxTime = dist / owner.Profile.flySpeed + 0.5f;
    }

    public override void FixedTick()
    {
        float dt = Time.fixedDeltaTime;
        timer += dt;

        // Bail out if player is gone or LOS lost
        if (!owner.Ctx.isPlayerInDeaggroRange || !owner.Ctx.hasLineOfSightToPlayer)
        {
            fws.HomePosition = owner.transform.position;
            owner.FSM.ChangeState(fws.PatrolState);
            return;
        }

        Vector2 pos = owner.transform.position;
        Vector2 toTarget = repositionTarget - pos;
        float distToTarget = toTarget.magnitude;

        // Arrived or timed out
        if (distToTarget < owner.Profile.patrolArriveThreshold || timer >= maxTime)
        {
            owner.FSM.ChangeState(fws.EngageDecisionState);
            return;
        }

        // Stuck detection: not making progress toward target
        float progress = lastDistToTarget - distToTarget;
        lastDistToTarget = distToTarget;

        if (progress < owner.Profile.stuckMinProgress * dt)
        {
            stuckTimer += dt;
            if (stuckTimer >= owner.Profile.stuckSeconds)
            {
                // Re-sample a new target instead of buzzing in place
                PickRepositionTarget();
                stuckTimer = 0f;
                lastDistToTarget = float.MaxValue;
                return;
            }
        }
        else
        {
            stuckTimer = 0f;
        }

        // Smoothed movement — lerp velocity instead of hard-setting
        Vector2 desiredVel = (toTarget / distToTarget) * owner.Profile.flySpeed;
        owner.Rb.linearVelocity = Vector2.Lerp(
            owner.Rb.linearVelocity, desiredVel, owner.Profile.patrolSmoothing * dt);

        // Facing with hysteresis
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

    private void PickRepositionTarget()
    {
        float dist = owner.Profile.repositionDistance;
        Vector2 center = owner.Ctx.playerTransform != null
            ? (Vector2)owner.Ctx.playerTransform.position + new Vector2(0f, 1f)
            : (Vector2)owner.transform.position;

        if (!fws.TryPickValidTarget(center, dist, out repositionTarget))
        {
            // Fallback: stay near current position, slightly above
            repositionTarget = (Vector2)owner.transform.position + new Vector2(0f, 1f);
        }

        stuckTimer = 0f;
        lastDistToTarget = float.MaxValue;
    }
}
