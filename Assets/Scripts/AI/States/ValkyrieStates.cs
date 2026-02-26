using UnityEngine;
using System.Collections.Generic;

// =================================================================
//  PHASE 1 SUPER STATE
//  Hierarchical state that owns a sub-FSM for grounded combat.
//  Sub-states: P1Approach, P1Decision, P1Slash, P1Flurry, P1Thrust.
// =================================================================
public class ValkP1Super : HierarchicalState
{
    private ValkyrieBoss valk;

    // Phase 1 substates
    public ValkP1ApproachState ApproachState { get; private set; }
    public ValkP1DecisionState DecisionState { get; private set; }
    public ValkP1SlashState SlashState { get; private set; }
    public ValkP1FlurryState FlurryState { get; private set; }
    public ValkP1ThrustState ThrustState { get; private set; }

    public ValkP1Super(ValkyrieBoss valk) : base(valk)
    {
        this.valk = valk;

        ApproachState = new ValkP1ApproachState(valk, this);
        DecisionState = new ValkP1DecisionState(valk, this);
        SlashState = new ValkP1SlashState(valk, this);
        FlurryState = new ValkP1FlurryState(valk, this);
        ThrustState = new ValkP1ThrustState(valk, this);
    }

    public override void Enter()
    {
        subMachine.ChangeState(ApproachState);
    }

    public void ChangeSubState(IState state)
    {
        subMachine.ChangeState(state);
    }
}

// =================================================================
//  PHASE 2 SUPER STATE
//  Hierarchical state that owns a Phase 2 sub-FSM for flying combat.
//  A Behavior Tree selects the next attack, but ONLY when the
//  sub-FSM is in P2Hover. The BT sets an intent; P2Hover consumes it.
// =================================================================
public class ValkP2Super : HierarchicalState
{
    private ValkyrieBoss valk;
    private BTNode attackSelectorBT;

    // Phase 2 substates
    public ValkP2HoverState HoverState { get; private set; }
    public ValkP2ErraticSlashState ErraticSlashState { get; private set; }
    public ValkP2ErraticFlurryState ErraticFlurryState { get; private set; }
    public ValkP2PlungeState PlungeState { get; private set; }

    // BT intent — set by BT, consumed by P2Hover
    public IState RequestedP2Attack { get; set; }

    public ValkP2Super(ValkyrieBoss valk) : base(valk)
    {
        this.valk = valk;

        HoverState = new ValkP2HoverState(valk, this);
        ErraticSlashState = new ValkP2ErraticSlashState(valk, this);
        ErraticFlurryState = new ValkP2ErraticFlurryState(valk, this);
        PlungeState = new ValkP2PlungeState(valk, this);

        BuildBehaviorTree();
    }

    public override void Enter()
    {
        Debug.Log("Valkyrie: Entering Phase 2");
        // Boss becomes airborne in Phase 2
        owner.Rb.gravityScale = 0f;
        RequestedP2Attack = null;
        subMachine.ChangeState(HoverState);
    }

    public override void Tick()
    {
        // BT ticks every frame but can only request attacks when in Hover
        if (subMachine.CurrentState == HoverState)
        {
            attackSelectorBT.Tick(owner.Ctx);
        }

        base.Tick();
    }

    public void ChangeSubState(IState state)
    {
        subMachine.ChangeState(state);
    }

    // ----- Behavior Tree Construction -----
    // The BT checks cooldowns and weights, then sets RequestedP2Attack.
    // It does NOT move rigidbodies or toggle hitboxes.
    private void BuildBehaviorTree()
    {
        // Weighted random selector: pick from ready attacks using selectionWeight
        attackSelectorBT = new BTAction(ctx =>
        {
            if (RequestedP2Attack != null) return BTStatus.Failure; // already pending

            // Build weighted list of ready attacks
            List<(IState state, float weight)> candidates = new List<(IState, float)>();

            AttackDefinition slashDef = valk.GetAttackDef("P2Slash");
            AttackDefinition flurryDef = valk.GetAttackDef("P2Flurry");
            AttackDefinition plungeDef = valk.GetAttackDef("P2Plunge");

            if (slashDef != null && owner.IsAttackReady("P2Slash"))
                candidates.Add((ErraticSlashState, slashDef.selectionWeight));
            if (flurryDef != null && owner.IsAttackReady("P2Flurry"))
                candidates.Add((ErraticFlurryState, flurryDef.selectionWeight));
            if (plungeDef != null && owner.IsAttackReady("P2Plunge"))
                candidates.Add((PlungeState, plungeDef.selectionWeight));

            if (candidates.Count == 0) return BTStatus.Failure; // nothing ready

            // Weighted random selection
            float totalWeight = 0f;
            for (int i = 0; i < candidates.Count; i++)
                totalWeight += candidates[i].weight;

            float roll = Random.value * totalWeight;
            float cumulative = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                cumulative += candidates[i].weight;
                if (roll <= cumulative)
                {
                    RequestedP2Attack = candidates[i].state;
                    return BTStatus.Success;
                }
            }

            // Fallback: pick last
            RequestedP2Attack = candidates[candidates.Count - 1].state;
            return BTStatus.Success;
        });
    }
}

// =================================================================
//  PHASE 1 SUBSTATES
// =================================================================

// ---------------------------------------------------------------
//  P1 Approach — Walk toward player until in attack range.
// ---------------------------------------------------------------
public class ValkP1ApproachState : EnemyState
{
    private ValkyrieBoss valk;
    private ValkP1Super p1;

    public ValkP1ApproachState(ValkyrieBoss valk, ValkP1Super p1) : base(valk)
    {
        this.valk = valk;
        this.p1 = p1;
    }

    public override void Enter()
    {
        //Debug.Log("Valkyrie P1 Approach entered");
        if (owner.Anim != null) owner.Anim.SetBool("Walking", true);
    }

    public override void FixedTick()
    {
        //Debug.Log($"Approach: playerTransform={owner.Ctx.playerTransform}, nearLedge={owner.Ctx.nearLedgeAhead}, nearWall={owner.Ctx.nearWallAhead}");
        owner.FacePlayer();

        // Ledge/wall avoidance: stop at edges
        if (owner.Ctx.nearLedgeAhead || owner.Ctx.nearWallAhead)
        {
            owner.StopHorizontal();
            return;
        }

        owner.MoveGround(owner.Profile.approachSpeed);

        // In attack range — go to decision
        if (owner.Ctx.isPlayerInAttackRange && owner.Ctx.hasLineOfSightToPlayer)
        {
            p1.ChangeSubState(p1.DecisionState);
        }
    }

    public override void Exit()
    {
        if (owner.Anim != null) owner.Anim.SetBool("Walking", false);
        owner.StopHorizontal();
    }
}

// ---------------------------------------------------------------
//  P1 Decision — Brief pause, then pick a weighted attack.
// ---------------------------------------------------------------
public class ValkP1DecisionState : EnemyState
{
    private ValkyrieBoss valk;
    private ValkP1Super p1;
    private float pauseTimer;

    public ValkP1DecisionState(ValkyrieBoss valk, ValkP1Super p1) : base(valk)
    {
        this.valk = valk;
        this.p1 = p1;
    }

    public override void Enter()
    {
        owner.StopHorizontal();
        owner.FacePlayer();
        pauseTimer = Random.Range(owner.Profile.minAttackCooldown, owner.Profile.maxAttackCooldown);
    }

    public override void FixedTick()
    {
        pauseTimer -= Time.fixedDeltaTime;
        if (pauseTimer > 0f) return;

        // Player moved out of range — re-approach
        if (!owner.Ctx.isPlayerInAttackRange)
        {
            p1.ChangeSubState(p1.ApproachState);
            return;
        }

        // Weighted attack selection from ready attacks
        List<(IState state, float weight)> candidates = new List<(IState, float)>();

        AttackDefinition slashDef = valk.GetAttackDef("Slash");
        AttackDefinition flurryDef = valk.GetAttackDef("Flurry");
        AttackDefinition thrustDef = valk.GetAttackDef("Thrust");

        if (slashDef != null && owner.IsAttackReady("Slash"))
            candidates.Add((p1.SlashState, slashDef.selectionWeight));
        if (flurryDef != null && owner.IsAttackReady("Flurry"))
            candidates.Add((p1.FlurryState, flurryDef.selectionWeight));
        if (thrustDef != null && owner.IsAttackReady("Thrust"))
            candidates.Add((p1.ThrustState, thrustDef.selectionWeight));

        if (candidates.Count == 0)
        {
            // Nothing ready — wait a bit longer
            pauseTimer = 0.3f;
            return;
        }

        // Weighted random pick
        float totalWeight = 0f;
        for (int i = 0; i < candidates.Count; i++)
            totalWeight += candidates[i].weight;

        float roll = Random.value * totalWeight;
        float cumulative = 0f;
        for (int i = 0; i < candidates.Count; i++)
        {
            cumulative += candidates[i].weight;
            if (roll <= cumulative)
            {
                p1.ChangeSubState(candidates[i].state);
                return;
            }
        }

        // Fallback
        p1.ChangeSubState(candidates[candidates.Count - 1].state);
    }
}

// ---------------------------------------------------------------
//  P1 Slash — Sword slash. Windup → Active → Recovery.
// ---------------------------------------------------------------
public class ValkP1SlashState : EnemyState
{
    private enum Phase { Windup, Active, Recovery }

    private ValkyrieBoss valk;
    private ValkP1Super p1;
    private Phase phase;
    private float timer;

    public ValkP1SlashState(ValkyrieBoss valk, ValkP1Super p1) : base(valk)
    {
        this.valk = valk;
        this.p1 = p1;
    }

    public override void Enter()
    {
        Debug.Log("Valkyrie: P1 Slash");
        phase = Phase.Windup;
        owner.StopHorizontal();
        owner.FacePlayer();

        AttackDefinition atk = valk.GetAttackDef("Slash");
        timer = atk != null ? atk.windupDuration : 0.4f;

        if (owner.Anim != null) owner.Anim.SetTrigger("SlashWindup");
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
                    AttackDefinition atk = valk.GetAttackDef("Slash");
                    timer = atk != null ? atk.activeDuration : 0.2f;

                    if (valk.SlashHitbox != null) valk.SlashHitbox.Activate();
                    if (owner.Anim != null) owner.Anim.SetTrigger("SlashAttack");
                }
                break;

            case Phase.Active:
                if (timer <= 0f)
                {
                    if (valk.SlashHitbox != null) valk.SlashHitbox.Deactivate();

                    phase = Phase.Recovery;
                    AttackDefinition atk = valk.GetAttackDef("Slash");
                    timer = atk != null ? atk.recoveryDuration : 0.5f;

                    owner.StartCooldown("Slash");
                }
                break;

            case Phase.Recovery:
                if (timer <= 0f)
                {
                    ReturnToDecisionOrApproach();
                }
                break;
        }
    }

    public override void Exit()
    {
        if (valk.SlashHitbox != null) valk.SlashHitbox.Deactivate();
    }

    private void ReturnToDecisionOrApproach()
    {
        if (owner.Ctx.isPlayerInAttackRange)
            p1.ChangeSubState(p1.DecisionState);
        else
            p1.ChangeSubState(p1.ApproachState);
    }
}

// ---------------------------------------------------------------
//  P1 Flurry — Multi-hit sword flurry. Windup → Active (multi-hit) → Recovery.
// ---------------------------------------------------------------
public class ValkP1FlurryState : EnemyState
{
    private enum Phase { Windup, Active, Recovery }

    private ValkyrieBoss valk;
    private ValkP1Super p1;
    private Phase phase;
    private float timer;
    private int hitsRemaining;
    private float hitIntervalTimer;

    public ValkP1FlurryState(ValkyrieBoss valk, ValkP1Super p1) : base(valk)
    {
        this.valk = valk;
        this.p1 = p1;
    }

    public override void Enter()
    {
        Debug.Log("Valkyrie: P1 Flurry");
        phase = Phase.Windup;
        owner.StopHorizontal();
        owner.FacePlayer();

        AttackDefinition atk = valk.GetAttackDef("Flurry");
        timer = atk != null ? atk.windupDuration : 0.3f;

        if (owner.Anim != null) owner.Anim.SetTrigger("FlurryWindup");
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
                    AttackDefinition atk = valk.GetAttackDef("Flurry");
                    timer = atk != null ? atk.activeDuration : 0.6f;
                    hitsRemaining = atk != null ? atk.hitCount : 3;
                    hitIntervalTimer = 0f;

                    if (valk.FlurryHitbox != null) valk.FlurryHitbox.Activate();
                    if (owner.Anim != null) owner.Anim.SetTrigger("FlurryAttack");
                }
                break;

            case Phase.Active:
                // Multi-hit: re-activate hitbox at intervals to clear hit tracking
                hitIntervalTimer -= Time.fixedDeltaTime;
                if (hitIntervalTimer <= 0f && hitsRemaining > 0)
                {
                    if (valk.FlurryHitbox != null)
                    {
                        valk.FlurryHitbox.Deactivate();
                        valk.FlurryHitbox.Activate();
                    }
                    hitsRemaining--;
                    AttackDefinition atk = valk.GetAttackDef("Flurry");
                    hitIntervalTimer = atk != null ? atk.hitInterval : 0.15f;
                }

                if (timer <= 0f)
                {
                    if (valk.FlurryHitbox != null) valk.FlurryHitbox.Deactivate();

                    phase = Phase.Recovery;
                    AttackDefinition atk = valk.GetAttackDef("Flurry");
                    timer = atk != null ? atk.recoveryDuration : 0.5f;

                    owner.StartCooldown("Flurry");
                }
                break;

            case Phase.Recovery:
                if (timer <= 0f)
                {
                    ReturnToDecisionOrApproach();
                }
                break;
        }
    }

    public override void Exit()
    {
        if (valk.FlurryHitbox != null) valk.FlurryHitbox.Deactivate();
    }

    private void ReturnToDecisionOrApproach()
    {
        if (owner.Ctx.isPlayerInAttackRange)
            p1.ChangeSubState(p1.DecisionState);
        else
            p1.ChangeSubState(p1.ApproachState);
    }
}

// ---------------------------------------------------------------
//  P1 Thrust — Helmet thrust. Windup → Active → Recovery.
// ---------------------------------------------------------------
public class ValkP1ThrustState : EnemyState
{
    private enum Phase { Windup, Active, Recovery }

    private ValkyrieBoss valk;
    private ValkP1Super p1;
    private Phase phase;
    private float timer;

    public ValkP1ThrustState(ValkyrieBoss valk, ValkP1Super p1) : base(valk)
    {
        this.valk = valk;
        this.p1 = p1;
    }

    public override void Enter()
    {
        Debug.Log("Valkyrie: P1 Thrust");
        phase = Phase.Windup;
        owner.StopHorizontal();
        owner.FacePlayer();

        AttackDefinition atk = valk.GetAttackDef("Thrust");
        timer = atk != null ? atk.windupDuration : 0.5f;

        if (owner.Anim != null) owner.Anim.SetTrigger("ThrustWindup");
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
                    AttackDefinition atk = valk.GetAttackDef("Thrust");
                    timer = atk != null ? atk.activeDuration : 0.3f;

                    // Thrust lunges forward
                    float thrustSpeed = atk != null ? atk.dashSpeed : 8f;
                    owner.MoveGround(thrustSpeed);

                    if (valk.ThrustHitbox != null) valk.ThrustHitbox.Activate();
                    if (owner.Anim != null) owner.Anim.SetTrigger("ThrustAttack");
                }
                break;

            case Phase.Active:
                if (timer <= 0f)
                {
                    owner.StopHorizontal();
                    if (valk.ThrustHitbox != null) valk.ThrustHitbox.Deactivate();

                    phase = Phase.Recovery;
                    AttackDefinition atk = valk.GetAttackDef("Thrust");
                    timer = atk != null ? atk.recoveryDuration : 0.6f;

                    owner.StartCooldown("Thrust");
                }
                break;

            case Phase.Recovery:
                if (timer <= 0f)
                {
                    ReturnToDecisionOrApproach();
                }
                break;
        }
    }

    public override void Exit()
    {
        if (valk.ThrustHitbox != null) valk.ThrustHitbox.Deactivate();
        owner.StopHorizontal();
    }

    private void ReturnToDecisionOrApproach()
    {
        if (owner.Ctx.isPlayerInAttackRange)
            p1.ChangeSubState(p1.DecisionState);
        else
            p1.ChangeSubState(p1.ApproachState);
    }
}

// =================================================================
//  PHASE 2 SUBSTATES
// =================================================================

// ---------------------------------------------------------------
//  P2 Hover — Erratic repositioning while airborne.
//  Consumes BT intent (RequestedP2Attack) to transition to attacks.
// ---------------------------------------------------------------
public class ValkP2HoverState : EnemyState
{
    private ValkyrieBoss valk;
    private ValkP2Super p2;
    private Vector2 hoverTarget;
    private float retargetTimer;
    private float decisionDelay;

    // Facing hysteresis
    private float facingHoldTimer;
    private const float FacingHoldDuration = 0.15f;

    // Erratic offset
    private Vector2 erraticOffset;
    private float erraticTimer;

    public ValkP2HoverState(ValkyrieBoss valk, ValkP2Super p2) : base(valk)
    {
        this.valk = valk;
        this.p2 = p2;
    }

    public override void Enter()
    {
        p2.RequestedP2Attack = null;
        PickHoverTarget();
        facingHoldTimer = 0f;
        erraticTimer = 0f;
        // Brief delay before accepting BT requests — prevents instant attack after phase enter
        decisionDelay = Random.Range(owner.Profile.minAttackCooldown, owner.Profile.maxAttackCooldown);
    }

    public override void FixedTick()
    {
        float dt = Time.fixedDeltaTime;
        decisionDelay -= dt;

        // Consume BT intent if ready
        if (decisionDelay <= 0f && p2.RequestedP2Attack != null)
        {
            IState attack = p2.RequestedP2Attack;
            p2.RequestedP2Attack = null;
            p2.ChangeSubState(attack);
            return;
        }

        // Erratic offset — small random perturbations at intervals
        erraticTimer -= dt;
        if (erraticTimer <= 0f)
        {
            erraticOffset = Random.insideUnitCircle * owner.Profile.erraticIntensity;
            erraticTimer = Random.Range(0.3f, 0.7f);
        }

        // Retarget periodically to follow player
        retargetTimer -= dt;
        if (retargetTimer <= 0f)
        {
            PickHoverTarget();
        }

        // Move toward hover target + erratic offset
        Vector2 pos = (Vector2)owner.transform.position;
        Vector2 destination = hoverTarget + erraticOffset;
        Vector2 toTarget = destination - pos;
        float dist = toTarget.magnitude;

        if (dist > 0.3f)
        {
            Vector2 desiredVel = (toTarget / dist) * owner.Profile.flySpeed;
            desiredVel = owner.AvoidObstacles(desiredVel);
            owner.Rb.linearVelocity = Vector2.Lerp(
                owner.Rb.linearVelocity, desiredVel, 5f * dt);
        }
        else
        {
            owner.Rb.linearVelocity = Vector2.Lerp(
                owner.Rb.linearVelocity, Vector2.zero, 5f * dt);
        }

        // Facing with hysteresis
        facingHoldTimer -= dt;
        if (facingHoldTimer <= 0f)
        {
            if (owner.Ctx.playerTransform != null)
            {
                float dirX = owner.Ctx.playerTransform.position.x - owner.transform.position.x;
                if (Mathf.Abs(dirX) > 0.3f)
                {
                    int newFacing = dirX > 0 ? 1 : -1;
                    if (newFacing != owner.FacingDirection)
                    {
                        owner.FaceDirection(newFacing);
                        facingHoldTimer = FacingHoldDuration;
                    }
                }
            }
        }
    }

    public override void Exit()
    {
        owner.StopAll();
    }

    private void PickHoverTarget()
    {
        retargetTimer = Random.Range(1.5f, 2.5f);

        if (owner.Ctx.playerTransform != null)
        {
            // Hover above and to one side of the player
            Vector2 playerPos = owner.Ctx.playerTransform.position;
            float sideOffset = (Random.value > 0.5f ? 1f : -1f) * Random.Range(2f, 4f);
            hoverTarget = playerPos + new Vector2(sideOffset, owner.Profile.hoverHeight);
        }
        else
        {
            hoverTarget = (Vector2)owner.transform.position + Vector2.up * owner.Profile.hoverHeight;
        }
    }
}

// ---------------------------------------------------------------
//  P2 Erratic Slash — Erratic approach + slash while airborne.
//  Windup (fly toward player erratically) → Active → Recovery.
// ---------------------------------------------------------------
public class ValkP2ErraticSlashState : EnemyState
{
    private enum Phase { Windup, Active, Recovery }

    private ValkyrieBoss valk;
    private ValkP2Super p2;
    private Phase phase;
    private float timer;
    private Vector2 erraticOffset;

    public ValkP2ErraticSlashState(ValkyrieBoss valk, ValkP2Super p2) : base(valk)
    {
        this.valk = valk;
        this.p2 = p2;
    }

    public override void Enter()
    {
        Debug.Log("Valkyrie: P2 Erratic Slash");
        phase = Phase.Windup;
        owner.FacePlayer();
        erraticOffset = Random.insideUnitCircle * owner.Profile.erraticIntensity;

        AttackDefinition atk = valk.GetAttackDef("P2Slash");
        timer = atk != null ? atk.windupDuration : 0.4f;

        if (owner.Anim != null) owner.Anim.SetTrigger("SlashWindup");
    }

    public override void FixedTick()
    {
        timer -= Time.fixedDeltaTime;

        switch (phase)
        {
            case Phase.Windup:
                // Erratic approach toward player during windup
                if (owner.Ctx.playerTransform != null)
                {
                    Vector2 target = (Vector2)owner.Ctx.playerTransform.position + erraticOffset;
                    owner.MoveToward(target, owner.Profile.flySpeed * 1.2f);
                }
                owner.FacePlayer();

                if (timer <= 0f)
                {
                    owner.StopAll();
                    phase = Phase.Active;
                    AttackDefinition atk = valk.GetAttackDef("P2Slash");
                    timer = atk != null ? atk.activeDuration : 0.2f;

                    if (valk.SlashHitbox != null) valk.SlashHitbox.Activate();
                    if (owner.Anim != null) owner.Anim.SetTrigger("SlashAttack");
                }
                break;

            case Phase.Active:
                if (timer <= 0f)
                {
                    if (valk.SlashHitbox != null) valk.SlashHitbox.Deactivate();

                    phase = Phase.Recovery;
                    AttackDefinition atk = valk.GetAttackDef("P2Slash");
                    timer = atk != null ? atk.recoveryDuration : 0.5f;

                    owner.StartCooldown("P2Slash");
                }
                break;

            case Phase.Recovery:
                // Drift to a stop during recovery
                owner.Rb.linearVelocity = Vector2.Lerp(
                    owner.Rb.linearVelocity, Vector2.zero, 5f * Time.fixedDeltaTime);

                if (timer <= 0f)
                {
                    p2.ChangeSubState(p2.HoverState);
                }
                break;
        }
    }

    public override void Exit()
    {
        if (valk.SlashHitbox != null) valk.SlashHitbox.Deactivate();
        owner.StopAll();
    }
}

// ---------------------------------------------------------------
//  P2 Erratic Flurry — Erratic approach + multi-hit flurry while airborne.
//  Windup (fly toward player erratically) → Active (multi-hit) → Recovery.
// ---------------------------------------------------------------
public class ValkP2ErraticFlurryState : EnemyState
{
    private enum Phase { Windup, Active, Recovery }

    private ValkyrieBoss valk;
    private ValkP2Super p2;
    private Phase phase;
    private float timer;
    private int hitsRemaining;
    private float hitIntervalTimer;
    private Vector2 erraticOffset;

    public ValkP2ErraticFlurryState(ValkyrieBoss valk, ValkP2Super p2) : base(valk)
    {
        this.valk = valk;
        this.p2 = p2;
    }

    public override void Enter()
    {
        Debug.Log("Valkyrie: P2 Erratic Flurry");
        phase = Phase.Windup;
        owner.FacePlayer();
        erraticOffset = Random.insideUnitCircle * owner.Profile.erraticIntensity;

        AttackDefinition atk = valk.GetAttackDef("P2Flurry");
        timer = atk != null ? atk.windupDuration : 0.3f;

        if (owner.Anim != null) owner.Anim.SetTrigger("FlurryWindup");
    }

    public override void FixedTick()
    {
        timer -= Time.fixedDeltaTime;

        switch (phase)
        {
            case Phase.Windup:
                // Erratic approach during windup
                if (owner.Ctx.playerTransform != null)
                {
                    Vector2 target = (Vector2)owner.Ctx.playerTransform.position + erraticOffset;
                    owner.MoveToward(target, owner.Profile.flySpeed * 1.2f);
                }
                owner.FacePlayer();

                if (timer <= 0f)
                {
                    owner.StopAll();
                    phase = Phase.Active;
                    AttackDefinition atk = valk.GetAttackDef("P2Flurry");
                    timer = atk != null ? atk.activeDuration : 0.6f;
                    hitsRemaining = atk != null ? atk.hitCount : 3;
                    hitIntervalTimer = 0f;

                    if (valk.FlurryHitbox != null) valk.FlurryHitbox.Activate();
                    if (owner.Anim != null) owner.Anim.SetTrigger("FlurryAttack");
                }
                break;

            case Phase.Active:
                // Multi-hit: re-activate hitbox at intervals
                hitIntervalTimer -= Time.fixedDeltaTime;
                if (hitIntervalTimer <= 0f && hitsRemaining > 0)
                {
                    if (valk.FlurryHitbox != null)
                    {
                        valk.FlurryHitbox.Deactivate();
                        valk.FlurryHitbox.Activate();
                    }
                    hitsRemaining--;
                    AttackDefinition atk = valk.GetAttackDef("P2Flurry");
                    hitIntervalTimer = atk != null ? atk.hitInterval : 0.15f;
                }

                if (timer <= 0f)
                {
                    if (valk.FlurryHitbox != null) valk.FlurryHitbox.Deactivate();

                    phase = Phase.Recovery;
                    AttackDefinition atk = valk.GetAttackDef("P2Flurry");
                    timer = atk != null ? atk.recoveryDuration : 0.5f;

                    owner.StartCooldown("P2Flurry");
                }
                break;

            case Phase.Recovery:
                owner.Rb.linearVelocity = Vector2.Lerp(
                    owner.Rb.linearVelocity, Vector2.zero, 5f * Time.fixedDeltaTime);

                if (timer <= 0f)
                {
                    p2.ChangeSubState(p2.HoverState);
                }
                break;
        }
    }

    public override void Exit()
    {
        if (valk.FlurryHitbox != null) valk.FlurryHitbox.Deactivate();
        owner.StopAll();
    }
}

// ---------------------------------------------------------------
//  P2 Plunge — Rise high, then dive at the player.
//  Rise phase → Dive (active hitbox) → Recovery on ground impact.
// ---------------------------------------------------------------
public class ValkP2PlungeState : EnemyState
{
    private enum Phase { Rise, Dive, Recovery }

    private ValkyrieBoss valk;
    private ValkP2Super p2;
    private Phase phase;
    private float timer;
    private float riseHeight;
    private float riseStartY;
    private Vector2 plungeTarget;

    public ValkP2PlungeState(ValkyrieBoss valk, ValkP2Super p2) : base(valk)
    {
        this.valk = valk;
        this.p2 = p2;
    }

    public override void Enter()
    {
        Debug.Log("Valkyrie: P2 Plunge");
        phase = Phase.Rise;
        owner.StopAll();
        owner.FacePlayer();

        riseStartY = owner.transform.position.y;
        riseHeight = owner.Profile.hoverHeight + 2f;

        // Rise duration — use windup as rise time
        AttackDefinition atk = valk.GetAttackDef("P2Plunge");
        timer = atk != null ? atk.windupDuration : 0.6f;

        if (owner.Anim != null) owner.Anim.SetTrigger("PlungeRise");
    }

    public override void FixedTick()
    {
        timer -= Time.fixedDeltaTime;

        switch (phase)
        {
            case Phase.Rise:
                // Fly upward
                owner.Rb.linearVelocity = Vector2.up * owner.Profile.flySpeed * 1.5f;

                if (timer <= 0f || owner.transform.position.y >= riseStartY + riseHeight)
                {
                    // Lock plunge target to player's current X position
                    if (owner.Ctx.playerTransform != null)
                        plungeTarget = new Vector2(owner.Ctx.playerTransform.position.x, owner.Ctx.playerTransform.position.y);
                    else
                        plungeTarget = new Vector2(owner.transform.position.x, riseStartY);

                    phase = Phase.Dive;
                    AttackDefinition atk = valk.GetAttackDef("P2Plunge");
                    // Generous dive timer — ends on ground contact or timeout
                    timer = atk != null ? atk.activeDuration + 1f : 2f;

                    if (valk.PlungeHitbox != null) valk.PlungeHitbox.Activate();
                    if (owner.Anim != null) owner.Anim.SetTrigger("PlungeDive");
                }
                break;

            case Phase.Dive:
                // Dive toward the locked target position
                Vector2 diveDir = (plungeTarget - (Vector2)owner.transform.position).normalized;
                // Bias heavily downward
                diveDir = new Vector2(diveDir.x * 0.4f, -1f).normalized;

                AttackDefinition plungeAtk = valk.GetAttackDef("P2Plunge");
                float diveSpeed = plungeAtk != null ? plungeAtk.dashSpeed : 18f;
                if (diveSpeed <= 0f) diveSpeed = 18f;
                owner.MoveDirection(diveDir, diveSpeed);

                // Check for ground contact — use a short raycast down
                RaycastHit2D groundHit = Physics2D.Raycast(
                    owner.transform.position, Vector2.down, 0.5f, owner.GroundLayer);

                if (groundHit.collider != null || timer <= 0f)
                {
                    EndPlunge();
                }
                break;

            case Phase.Recovery:
                if (timer <= 0f)
                {
                    // Return to hover (lift off)
                    p2.ChangeSubState(p2.HoverState);
                }
                break;
        }
    }

    public override void Exit()
    {
        if (valk.PlungeHitbox != null) valk.PlungeHitbox.Deactivate();
        owner.StopAll();
    }

    private void EndPlunge()
    {
        if (valk.PlungeHitbox != null) valk.PlungeHitbox.Deactivate();
        owner.StopAll();

        phase = Phase.Recovery;
        AttackDefinition atk = valk.GetAttackDef("P2Plunge");
        timer = atk != null ? atk.recoveryDuration : 0.8f;

        owner.StartCooldown("P2Plunge");

        if (owner.Anim != null) owner.Anim.SetTrigger("PlungeLand");
    }
}
