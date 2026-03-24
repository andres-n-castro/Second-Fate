using UnityEngine;
using System.Collections.Generic;

// =================================================================
//  PHASE 1 SUPER STATE
//  Hierarchical state that owns a sub-FSM for grounded spear combat.
//  Sub-states: P1Approach, P1Decision, P1SpearThrust, P1SpearFlurry.
// =================================================================
public class TyrP1Super : HierarchicalState
{
    private TyrBoss tyr;

    // Phase 1 substates
    public TyrP1ApproachState ApproachState { get; private set; }
    public TyrP1DecisionState DecisionState { get; private set; }
    public TyrP1SpearThrustState SpearThrustState { get; private set; }
    public TyrP1SpearFlurryState SpearFlurryState { get; private set; }

    public TyrP1Super(TyrBoss tyr) : base(tyr)
    {
        this.tyr = tyr;

        ApproachState = new TyrP1ApproachState(tyr, this);
        DecisionState = new TyrP1DecisionState(tyr, this);
        SpearThrustState = new TyrP1SpearThrustState(tyr, this);
        SpearFlurryState = new TyrP1SpearFlurryState(tyr, this);
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
//  Hierarchical state that owns a Phase 2 sub-FSM for shield combat.
//  A Behavior Tree selects the next action, but ONLY when the
//  sub-FSM is in P2PressureOrIdle. The BT sets an intent; Idle consumes it.
// =================================================================
public class TyrP2Super : HierarchicalState
{
    private TyrBoss tyr;
    private BTNode attackSelectorBT;

    // Phase 2 substates
    public TyrP2PressureOrIdleState IdleState { get; private set; }
    public TyrP2ShieldBlockState ShieldBlockState { get; private set; }
    public TyrP2ShieldSlamState ShieldSlamState { get; private set; }
    public TyrP2ShieldFlurryState ShieldFlurryState { get; private set; }

    // BT intent — set by BT, consumed by P2PressureOrIdle
    public IState RequestedP2Action { get; set; }

    public TyrP2Super(TyrBoss tyr) : base(tyr)
    {
        this.tyr = tyr;

        IdleState = new TyrP2PressureOrIdleState(tyr, this);
        ShieldBlockState = new TyrP2ShieldBlockState(tyr, this);
        ShieldSlamState = new TyrP2ShieldSlamState(tyr, this);
        ShieldFlurryState = new TyrP2ShieldFlurryState(tyr, this);

        BuildBehaviorTree();
    }

    public override void Enter()
    {
        Debug.Log("Tyr: Entering Phase 2");
        RequestedP2Action = null;
        subMachine.ChangeState(IdleState);
    }

    public override void Tick()
    {
        // BT ticks every frame but can only request actions when in Idle
        if (subMachine.CurrentState == IdleState)
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
    // The BT checks cooldowns and player-attack recency, then sets RequestedP2Action.
    // It does NOT move rigidbodies or toggle hitboxes.
    //
    // Reactive weight system:
    //   - If the player attacked recently (timeSincePlayerAttacked < threshold),
    //     Shield Block weight is boosted heavily.
    //   - If the player has NOT attacked recently, offensive actions
    //     (Shield Slam, Shield Flurry) get boosted.
    private void BuildBehaviorTree()
    {
        attackSelectorBT = new BTAction(ctx =>
        {
            if (RequestedP2Action != null) return BTStatus.Failure; // already pending

            float dist = owner.Ctx.playerDistance;
            float timeSinceAtk = owner.Ctx.timeSincePlayerAttacked;
            EnemyProfile p = owner.Profile;

            // Threshold: player attacked within this many seconds = "recently"
            float recentAttackThreshold = p.tyrReactiveBlockWindow;
            bool playerAttackedRecently = timeSinceAtk < recentAttackThreshold;

            List<(IState state, float weight)> candidates = new List<(IState, float)>();

            // --- Shield Block: reactive defense ---
            AttackDefinition blockDef = tyr.GetAttackDef("ShieldBlock");
            if (blockDef != null && owner.IsAttackReady("ShieldBlock"))
            {
                float w = blockDef.selectionWeight;
                if (playerAttackedRecently)
                    w *= p.tyrBlockWeightMultiplier;
                candidates.Add((ShieldBlockState, w));
            }

            // --- Shield Slam: aggressive when player is passive ---
            AttackDefinition slamDef = tyr.GetAttackDef("ShieldSlam");
            if (slamDef != null && owner.IsAttackReady("ShieldSlam"))
            {
                float w = slamDef.selectionWeight;
                if (!playerAttackedRecently && dist <= p.tyrSlamRange)
                    w *= p.tyrSlamWeightMultiplier;
                candidates.Add((ShieldSlamState, w));
            }

            // --- Shield Flurry: aggressive when player is passive ---
            AttackDefinition flurryDef = tyr.GetAttackDef("ShieldFlurry");
            if (flurryDef != null && owner.IsAttackReady("ShieldFlurry"))
            {
                float w = flurryDef.selectionWeight;
                if (!playerAttackedRecently && dist <= p.tyrFlurryRange)
                    w *= p.tyrFlurryWeightMultiplier;
                candidates.Add((ShieldFlurryState, w));
            }

            if (candidates.Count == 0) return BTStatus.Failure;

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
                    RequestedP2Action = candidates[i].state;
                    return BTStatus.Success;
                }
            }

            // Fallback: pick last
            RequestedP2Action = candidates[candidates.Count - 1].state;
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
public class TyrP1ApproachState : EnemyState
{
    private TyrBoss tyr;
    private TyrP1Super p1;

    public TyrP1ApproachState(TyrBoss tyr, TyrP1Super p1) : base(tyr)
    {
        this.tyr = tyr;
        this.p1 = p1;
    }

    public override void Enter()
    {
        if (owner.Anim != null) owner.Anim.SetBool(owner.AnimWalking, true);
    }

    public override void FixedTick()
    {
        owner.FacePlayer();

        if (owner.Ctx.nearLedgeAhead || owner.Ctx.nearWallAhead)
        {
            owner.StopHorizontal();
            return;
        }

        owner.MoveGround(owner.Profile.approachSpeed);

        if (owner.Ctx.playerDistance <= owner.Profile.tyrMaxEngageRange
            && owner.Ctx.hasLineOfSightToPlayer)
        {
            p1.ChangeSubState(p1.DecisionState);
        }
    }

    public override void Exit()
    {
        if (owner.Anim != null) owner.Anim.SetBool(owner.AnimWalking, false);
        owner.StopHorizontal();
    }
}

// ---------------------------------------------------------------
//  P1 Decision — Brief pause, then pick a weighted attack.
// ---------------------------------------------------------------
public class TyrP1DecisionState : EnemyState
{
    private TyrBoss tyr;
    private TyrP1Super p1;
    private float pauseTimer;
    private bool isStalking;

    public TyrP1DecisionState(TyrBoss tyr, TyrP1Super p1) : base(tyr)
    {
        this.tyr = tyr;
        this.p1 = p1;
    }

    public override void Enter()
    {
        owner.FacePlayer();
        isStalking = false;
        pauseTimer = Random.Range(owner.Profile.minAttackCooldown, owner.Profile.maxAttackCooldown);
    }

    public override void FixedTick()
    {
        owner.FacePlayer();

        // Stalk toward player while waiting
        if (pauseTimer > 0f)
        {
            pauseTimer -= Time.fixedDeltaTime;
            bool shouldStalk = owner.Ctx.playerDistance > owner.Profile.tyrCloseRange
                && !owner.Ctx.nearLedgeAhead && !owner.Ctx.nearWallAhead;

            if (shouldStalk)
            {
                if (!isStalking)
                {
                    isStalking = true;
                    if (owner.Anim != null) owner.Anim.SetBool(owner.AnimWalking, true);
                }
                owner.MoveGround(owner.Profile.approachSpeed);
            }
            else
            {
                if (isStalking)
                {
                    isStalking = false;
                    if (owner.Anim != null) owner.Anim.SetBool(owner.AnimWalking, false);
                }
                owner.StopHorizontal();
            }
            return;
        }

        if (isStalking)
        {
            isStalking = false;
            if (owner.Anim != null) owner.Anim.SetBool(owner.AnimWalking, false);
        }
        owner.StopHorizontal();

        float dist = owner.Ctx.playerDistance;
        EnemyProfile p = owner.Profile;

        // Player moved beyond max engage range — walk closer
        if (dist > p.tyrMaxEngageRange || !owner.Ctx.hasLineOfSightToPlayer)
        {
            p1.ChangeSubState(p1.ApproachState);
            return;
        }

        // Build candidate list
        List<(IState state, float weight)> candidates = new List<(IState, float)>();

        AttackDefinition thrustDef = tyr.GetAttackDef("SpearThrust");
        AttackDefinition flurryDef = tyr.GetAttackDef("SpearFlurry");

        // Spear Thrust: effective at close-to-mid range, has a dash component
        if (thrustDef != null && owner.IsAttackReady("SpearThrust"))
        {
            float thrustTravel = thrustDef.dashSpeed * thrustDef.activeDuration;
            float thrustEffective = tyr.SpearThrustReach + 0.3f + thrustTravel;
            if (dist <= thrustEffective)
                candidates.Add((p1.SpearThrustState, thrustDef.selectionWeight));
        }

        // Spear Flurry: close range only
        if (flurryDef != null && owner.IsAttackReady("SpearFlurry"))
        {
            float flurryEffective = tyr.SpearFlurryReach + 0.3f;
            if (dist <= flurryEffective)
                candidates.Add((p1.SpearFlurryState, flurryDef.selectionWeight));
        }

        if (candidates.Count > 0)
        {
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
            p1.ChangeSubState(candidates[candidates.Count - 1].state);
            return;
        }

        // Nothing in range — walk closer
        p1.ChangeSubState(p1.ApproachState);
    }

    public override void Exit()
    {
        if (isStalking && owner.Anim != null)
            owner.Anim.SetBool(owner.AnimWalking, false);
        owner.StopHorizontal();
    }
}

// ---------------------------------------------------------------
//  P1 Spear Thrust — Forward thrust. Windup → Active (dash) → Recovery.
// ---------------------------------------------------------------
public class TyrP1SpearThrustState : EnemyState
{
    private enum Phase { Windup, Active, Recovery }

    private TyrBoss tyr;
    private TyrP1Super p1;
    private Phase phase;
    private float timer;

    public TyrP1SpearThrustState(TyrBoss tyr, TyrP1Super p1) : base(tyr)
    {
        this.tyr = tyr;
        this.p1 = p1;
    }

    public override void Enter()
    {
        Debug.Log("Tyr: P1 Spear Thrust");
        phase = Phase.Windup;
        owner.StopHorizontal();
        owner.FacePlayer();

        AttackDefinition atk = tyr.GetAttackDef("SpearThrust");
        timer = atk != null ? atk.windupDuration : 0.4f;

        if (owner.Anim != null) owner.Anim.SetTrigger("Tyr_SpearThrust");
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
                    AttackDefinition atk = tyr.GetAttackDef("SpearThrust");
                    timer = atk != null ? atk.activeDuration : 0.3f;

                    // Thrust lunges forward
                    float thrustSpeed = atk != null ? atk.dashSpeed : 8f;
                    owner.MoveGround(thrustSpeed);

                    if (tyr.SpearThrustHitbox != null) tyr.SpearThrustHitbox.Activate();
                }
                break;

            case Phase.Active:
                if (timer <= 0f)
                {
                    owner.StopHorizontal();
                    if (tyr.SpearThrustHitbox != null) tyr.SpearThrustHitbox.Deactivate();

                    phase = Phase.Recovery;
                    AttackDefinition atk = tyr.GetAttackDef("SpearThrust");
                    timer = atk != null ? atk.recoveryDuration : 0.5f;

                    owner.StartCooldown("SpearThrust");
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
        if (tyr.SpearThrustHitbox != null) tyr.SpearThrustHitbox.Deactivate();
        owner.StopHorizontal();
    }

    private void ReturnToDecisionOrApproach()
    {
        if (owner.Ctx.playerDistance <= owner.Profile.tyrMaxEngageRange
            && owner.Ctx.hasLineOfSightToPlayer)
            p1.ChangeSubState(p1.DecisionState);
        else
            p1.ChangeSubState(p1.ApproachState);
    }
}

// ---------------------------------------------------------------
//  P1 Spear Flurry — Multi-hit spear flurry. Windup → Active → Recovery.
// ---------------------------------------------------------------
public class TyrP1SpearFlurryState : EnemyState
{
    private enum Phase { Windup, Active, Recovery }

    private TyrBoss tyr;
    private TyrP1Super p1;
    private Phase phase;
    private float timer;
    private int hitsRemaining;
    private float hitIntervalTimer;

    public TyrP1SpearFlurryState(TyrBoss tyr, TyrP1Super p1) : base(tyr)
    {
        this.tyr = tyr;
        this.p1 = p1;
    }

    public override void Enter()
    {
        Debug.Log("Tyr: P1 Spear Flurry");
        phase = Phase.Windup;
        owner.StopHorizontal();
        owner.FacePlayer();

        AttackDefinition atk = tyr.GetAttackDef("SpearFlurry");
        timer = atk != null ? atk.windupDuration : 0.3f;

        if (owner.Anim != null) owner.Anim.SetTrigger("Tyr_SpearFlurry");
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
                    AttackDefinition atk = tyr.GetAttackDef("SpearFlurry");
                    timer = atk != null ? atk.activeDuration : 0.6f;
                    hitsRemaining = atk != null ? atk.hitCount : 3;
                    hitIntervalTimer = 0f;

                    if (tyr.SpearFlurryHitbox != null) tyr.SpearFlurryHitbox.Activate();
                }
                break;

            case Phase.Active:
                // Multi-hit: re-activate hitbox at intervals to clear hit tracking
                hitIntervalTimer -= Time.fixedDeltaTime;
                if (hitIntervalTimer <= 0f && hitsRemaining > 0)
                {
                    if (tyr.SpearFlurryHitbox != null)
                    {
                        tyr.SpearFlurryHitbox.Deactivate();
                        tyr.SpearFlurryHitbox.Activate();
                    }
                    hitsRemaining--;
                    AttackDefinition atk = tyr.GetAttackDef("SpearFlurry");
                    hitIntervalTimer = atk != null ? atk.hitInterval : 0.15f;
                }

                if (timer <= 0f)
                {
                    if (tyr.SpearFlurryHitbox != null) tyr.SpearFlurryHitbox.Deactivate();

                    phase = Phase.Recovery;
                    AttackDefinition atk = tyr.GetAttackDef("SpearFlurry");
                    timer = atk != null ? atk.recoveryDuration : 0.5f;

                    owner.StartCooldown("SpearFlurry");
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
        if (tyr.SpearFlurryHitbox != null) tyr.SpearFlurryHitbox.Deactivate();
    }

    private void ReturnToDecisionOrApproach()
    {
        if (owner.Ctx.playerDistance <= owner.Profile.tyrMaxEngageRange
            && owner.Ctx.hasLineOfSightToPlayer)
            p1.ChangeSubState(p1.DecisionState);
        else
            p1.ChangeSubState(p1.ApproachState);
    }
}

// =================================================================
//  PHASE 2 SUBSTATES
// =================================================================

// ---------------------------------------------------------------
//  P2 Pressure / Idle — Grounded stalking while waiting for BT intent.
//  Consumes BT intent (RequestedP2Action) to transition to actions.
// ---------------------------------------------------------------
public class TyrP2PressureOrIdleState : EnemyState
{
    private TyrBoss tyr;
    private TyrP2Super p2;
    private float decisionDelay;

    public TyrP2PressureOrIdleState(TyrBoss tyr, TyrP2Super p2) : base(tyr)
    {
        this.tyr = tyr;
        this.p2 = p2;
    }

    public override void Enter()
    {
        p2.RequestedP2Action = null;
        // Brief delay before accepting BT requests
        decisionDelay = Random.Range(owner.Profile.minAttackCooldown, owner.Profile.maxAttackCooldown);
    }

    public override void FixedTick()
    {
        float dt = Time.fixedDeltaTime;
        decisionDelay -= dt;

        // Consume BT intent if ready
        if (decisionDelay <= 0f && p2.RequestedP2Action != null)
        {
            IState action = p2.RequestedP2Action;
            p2.RequestedP2Action = null;
            p2.ChangeSubState(action);
            return;
        }

        // Stalk toward player while waiting
        owner.FacePlayer();
        float dist = owner.Ctx.playerDistance;

        if (dist > owner.Profile.tyrCloseRange
            && !owner.Ctx.nearLedgeAhead && !owner.Ctx.nearWallAhead)
        {
            if (owner.Anim != null) owner.Anim.SetBool(owner.AnimWalking, true);
            owner.MoveGround(owner.Profile.approachSpeed);
        }
        else
        {
            if (owner.Anim != null) owner.Anim.SetBool(owner.AnimWalking, false);
            owner.StopHorizontal();
        }
    }

    public override void Exit()
    {
        if (owner.Anim != null) owner.Anim.SetBool(owner.AnimWalking, false);
        owner.StopHorizontal();
    }
}

// ---------------------------------------------------------------
//  P2 Shield Block — Defensive stance. Windup → Active (invulnerable) → Recovery.
//  Tyr is invulnerable for the duration of the Active phase.
// ---------------------------------------------------------------
public class TyrP2ShieldBlockState : EnemyState
{
    private enum Phase { Windup, Active, Recovery }

    private TyrBoss tyr;
    private TyrP2Super p2;
    private Phase phase;
    private float timer;

    public TyrP2ShieldBlockState(TyrBoss tyr, TyrP2Super p2) : base(tyr)
    {
        this.tyr = tyr;
        this.p2 = p2;
    }

    public override void Enter()
    {
        Debug.Log("Tyr: P2 Shield Block");
        phase = Phase.Windup;
        owner.StopHorizontal();
        owner.FacePlayer();

        AttackDefinition atk = tyr.GetAttackDef("ShieldBlock");
        timer = atk != null ? atk.windupDuration : 0.2f;

        if (owner.Anim != null) owner.Anim.SetTrigger("Tyr_ShieldBlock");
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
                    AttackDefinition atk = tyr.GetAttackDef("ShieldBlock");
                    timer = atk != null ? atk.activeDuration : 1.0f;

                    // Enable invulnerability for the block duration
                    if (owner.Health != null) owner.Health.isInvulnerable = true;
                }
                break;

            case Phase.Active:
                if (timer <= 0f)
                {
                    // End invulnerability
                    if (owner.Health != null) owner.Health.isInvulnerable = false;

                    phase = Phase.Recovery;
                    AttackDefinition atk = tyr.GetAttackDef("ShieldBlock");
                    timer = atk != null ? atk.recoveryDuration : 0.3f;

                    owner.StartCooldown("ShieldBlock");
                }
                break;

            case Phase.Recovery:
                if (timer <= 0f)
                {
                    p2.ChangeSubState(p2.IdleState);
                }
                break;
        }
    }

    public override void Exit()
    {
        // Safety net: always restore vulnerability on exit
        if (owner.Health != null) owner.Health.isInvulnerable = false;
    }
}

// ---------------------------------------------------------------
//  P2 Shield Slam — Heavy slam. Windup → Active → Recovery.
// ---------------------------------------------------------------
public class TyrP2ShieldSlamState : EnemyState
{
    private enum Phase { Windup, Active, Recovery }

    private TyrBoss tyr;
    private TyrP2Super p2;
    private Phase phase;
    private float timer;

    public TyrP2ShieldSlamState(TyrBoss tyr, TyrP2Super p2) : base(tyr)
    {
        this.tyr = tyr;
        this.p2 = p2;
    }

    public override void Enter()
    {
        Debug.Log("Tyr: P2 Shield Slam");
        phase = Phase.Windup;
        owner.StopHorizontal();
        owner.FacePlayer();

        AttackDefinition atk = tyr.GetAttackDef("ShieldSlam");
        timer = atk != null ? atk.windupDuration : 0.5f;

        if (owner.Anim != null) owner.Anim.SetTrigger("Tyr_ShieldSlam");
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
                    AttackDefinition atk = tyr.GetAttackDef("ShieldSlam");
                    timer = atk != null ? atk.activeDuration : 0.3f;

                    // Short forward step during slam
                    float slamSpeed = atk != null ? atk.dashSpeed : 4f;
                    if (slamSpeed > 0f)
                        owner.MoveGround(slamSpeed);

                    if (tyr.ShieldSlamHitbox != null) tyr.ShieldSlamHitbox.Activate();
                }
                break;

            case Phase.Active:
                if (timer <= 0f)
                {
                    owner.StopHorizontal();
                    if (tyr.ShieldSlamHitbox != null) tyr.ShieldSlamHitbox.Deactivate();

                    phase = Phase.Recovery;
                    AttackDefinition atk = tyr.GetAttackDef("ShieldSlam");
                    timer = atk != null ? atk.recoveryDuration : 0.6f;

                    owner.StartCooldown("ShieldSlam");
                }
                break;

            case Phase.Recovery:
                if (timer <= 0f)
                {
                    p2.ChangeSubState(p2.IdleState);
                }
                break;
        }
    }

    public override void Exit()
    {
        if (tyr.ShieldSlamHitbox != null) tyr.ShieldSlamHitbox.Deactivate();
        owner.StopHorizontal();
    }
}

// ---------------------------------------------------------------
//  P2 Shield Flurry — Multi-hit shield bash flurry.
//  Windup → Active (multi-hit) → Recovery.
// ---------------------------------------------------------------
public class TyrP2ShieldFlurryState : EnemyState
{
    private enum Phase { Windup, Active, Recovery }

    private TyrBoss tyr;
    private TyrP2Super p2;
    private Phase phase;
    private float timer;
    private int hitsRemaining;
    private float hitIntervalTimer;

    public TyrP2ShieldFlurryState(TyrBoss tyr, TyrP2Super p2) : base(tyr)
    {
        this.tyr = tyr;
        this.p2 = p2;
    }

    public override void Enter()
    {
        Debug.Log("Tyr: P2 Shield Flurry");
        phase = Phase.Windup;
        owner.StopHorizontal();
        owner.FacePlayer();

        AttackDefinition atk = tyr.GetAttackDef("ShieldFlurry");
        timer = atk != null ? atk.windupDuration : 0.3f;

        if (owner.Anim != null) owner.Anim.SetTrigger("Tyr_ShieldFlurry");
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
                    AttackDefinition atk = tyr.GetAttackDef("ShieldFlurry");
                    timer = atk != null ? atk.activeDuration : 0.6f;
                    hitsRemaining = atk != null ? atk.hitCount : 3;
                    hitIntervalTimer = 0f;

                    if (tyr.ShieldFlurryHitbox != null) tyr.ShieldFlurryHitbox.Activate();
                }
                break;

            case Phase.Active:
                hitIntervalTimer -= Time.fixedDeltaTime;
                if (hitIntervalTimer <= 0f && hitsRemaining > 0)
                {
                    if (tyr.ShieldFlurryHitbox != null)
                    {
                        tyr.ShieldFlurryHitbox.Deactivate();
                        tyr.ShieldFlurryHitbox.Activate();
                    }
                    hitsRemaining--;
                    AttackDefinition atk = tyr.GetAttackDef("ShieldFlurry");
                    hitIntervalTimer = atk != null ? atk.hitInterval : 0.15f;
                }

                if (timer <= 0f)
                {
                    if (tyr.ShieldFlurryHitbox != null) tyr.ShieldFlurryHitbox.Deactivate();

                    phase = Phase.Recovery;
                    AttackDefinition atk = tyr.GetAttackDef("ShieldFlurry");
                    timer = atk != null ? atk.recoveryDuration : 0.5f;

                    owner.StartCooldown("ShieldFlurry");
                }
                break;

            case Phase.Recovery:
                if (timer <= 0f)
                {
                    p2.ChangeSubState(p2.IdleState);
                }
                break;
        }
    }

    public override void Exit()
    {
        if (tyr.ShieldFlurryHitbox != null) tyr.ShieldFlurryHitbox.Deactivate();
    }
}
