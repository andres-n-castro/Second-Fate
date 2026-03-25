using UnityEngine;
using System.Collections.Generic;

// =================================================================
//  PHASE 1 SUPER STATE
//  Hierarchical state that owns a sub-FSM for grounded combat.
//  Sub-states: P1Approach, P1Decision, P1Slash, P1Flurry, P1Thrust, P1GapClose.
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
    public ValkP1GapCloseState GapCloseState { get; private set; }

    public ValkP1Super(ValkyrieBoss valk) : base(valk)
    {
        this.valk = valk;

        ApproachState = new ValkP1ApproachState(valk, this);
        DecisionState = new ValkP1DecisionState(valk, this);
        SlashState = new ValkP1SlashState(valk, this);
        FlurryState = new ValkP1FlurryState(valk, this);
        ThrustState = new ValkP1ThrustState(valk, this);
        GapCloseState = new ValkP1GapCloseState(valk, this);
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
    // The BT checks cooldowns, positional context, and weights, then sets RequestedP2Attack.
    // It does NOT move rigidbodies or toggle hitboxes.
    //
    // Context-aware weight system:
    //   Each attack starts with its base selectionWeight from the profile.
    //   If the current spatial conditions match the attack's ideal scenario,
    //   its weight is multiplied by a contextual boost (e.g. 3-5x).
    //   Attacks that don't match any condition keep their base weight,
    //   so they can still be picked — just less likely.
    //
    //   playerRelativePos = playerPos - bossPos
    //     playerRelativePos.y < 0 → boss is ABOVE player
    //     playerRelativePos.y > 0 → player is ABOVE boss
    private void BuildBehaviorTree()
    {
        attackSelectorBT = new BTAction(ctx =>
        {
            if (RequestedP2Attack != null) return BTStatus.Failure; // already pending

            // Spatial data
            float dx = Mathf.Abs(owner.Ctx.playerRelativePos.x);
            float dy = owner.Ctx.playerRelativePos.y; // negative = boss above player
            float dist = owner.Ctx.playerDistance;
            EnemyProfile p = owner.Profile;

            // Build weighted list of ready attacks with contextual multipliers
            List<(IState state, float weight)> candidates = new List<(IState, float)>();

            // --- Plunge: boss above player, reasonable horizontal alignment ---
            AttackDefinition plungeDef = valk.GetAttackDef("P2Plunge");
            if (plungeDef != null && owner.IsAttackReady("P2Plunge"))
            {
                float w = plungeDef.selectionWeight;
                bool abovePlayer = -dy >= p.valkPlungeMinAbovePlayerY;
                bool horizontalOk = dx <= p.valkPlungeMaxHorizontalOffset;
                if (abovePlayer && horizontalOk)
                    w *= p.valkPlungeWeightMultiplier;
                candidates.Add((PlungeState, w));
            }

            // --- Erratic Slash: close range, roughly same height ---
            AttackDefinition slashDef = valk.GetAttackDef("P2Slash");
            if (slashDef != null && owner.IsAttackReady("P2Slash"))
            {
                float w = slashDef.selectionWeight;
                bool closeRange = dist <= p.valkSlashRange;
                bool sameHeight = Mathf.Abs(dy) <= p.valkSlashMaxVerticalOffset;
                if (closeRange && sameHeight)
                    w *= p.valkSlashWeightMultiplier;
                candidates.Add((ErraticSlashState, w));
            }

            // --- Erratic Flurry: medium range / lateral pressure ---
            AttackDefinition flurryDef = valk.GetAttackDef("P2Flurry");
            if (flurryDef != null && owner.IsAttackReady("P2Flurry"))
            {
                float w = flurryDef.selectionWeight;
                bool inBand = dist >= p.valkFlurryPreferredRangeMin
                           && dist <= p.valkFlurryPreferredRangeMax;
                if (inBand)
                    w *= p.valkFlurryWeightMultiplier;
                candidates.Add((ErraticFlurryState, w));
            }

            if (candidates.Count == 0) return BTStatus.Failure; // nothing ready → stay in hover

            // Weighted random selection among valid candidates
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
        if (owner.Anim != null) owner.Anim.SetBool(owner.AnimWalking, true);
    }

    public override void FixedTick()
    {
        owner.FacePlayer();

        // Ledge/wall avoidance: stop at edges
        if (owner.Ctx.nearLedgeAhead || owner.Ctx.nearWallAhead)
        {
            owner.StopHorizontal();
            return;
        }

        owner.MoveGround(owner.Profile.approachSpeed);

        // Within max engage range and LOS — go to decision
        if (owner.Ctx.playerDistance <= owner.Profile.p1MaxEngageRange
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
public class ValkP1DecisionState : EnemyState
{
    private ValkyrieBoss valk;
    private ValkP1Super p1;
    private float pauseTimer;
    private bool isStalking;

    public ValkP1DecisionState(ValkyrieBoss valk, ValkP1Super p1) : base(valk)
    {
        this.valk = valk;
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

        // While waiting, stalk toward player if not already close
        if (pauseTimer > 0f)
        {
            pauseTimer -= Time.fixedDeltaTime;
            bool shouldStalk = owner.Ctx.playerDistance > owner.Profile.p1CloseRange
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
        if (dist > p.p1MaxEngageRange || !owner.Ctx.hasLineOfSightToPlayer)
        {
            p1.ChangeSubState(p1.ApproachState);
            return;
        }

        // Build candidate list using hitbox-derived reach
        List<(IState state, float weight)> candidates = new List<(IState, float)>();

        AttackDefinition slashDef = valk.GetAttackDef("Slash");
        AttackDefinition flurryDef = valk.GetAttackDef("Flurry");
        AttackDefinition thrustDef = valk.GetAttackDef("Thrust");

        // Slash effective range: hitbox reach + tolerance + micro-lunge travel
        if (slashDef != null && owner.IsAttackReady("Slash"))
        {
            float microLungeTravel = p.p1SlashMicroLungeSpeed * p.p1SlashMicroLungeDuration;
            float slashEffective = valk.SlashReach + 0.3f + microLungeTravel;
            if (dist <= slashEffective)
                candidates.Add((p1.SlashState, slashDef.selectionWeight));
        }

        // Flurry effective range: hitbox reach + tolerance (close range only)
        if (flurryDef != null && owner.IsAttackReady("Flurry"))
        {
            float flurryEffective = valk.FlurryReach + 0.3f;
            if (dist <= flurryEffective)
                candidates.Add((p1.FlurryState, flurryDef.selectionWeight));
        }

        // Thrust effective range: hitbox reach + tolerance + dash travel
        // Only eligible within mid range — beyond that, gap-close mechanic handles it
        // Weight scales down linearly as player gets closer (full weight at midRange, 20% at point-blank)
        if (thrustDef != null && owner.IsAttackReady("Thrust") && dist <= p.p1MidRange)
        {
            float thrustTravel = thrustDef.dashSpeed * thrustDef.activeDuration;
            float thrustEffective = valk.ThrustReach + 0.3f + thrustTravel;
            if (dist <= thrustEffective)
            {
                float closeness = 1f - Mathf.Clamp01(dist / p.p1MidRange);
                float thrustWeight = thrustDef.selectionWeight * Mathf.Lerp(1f, 0.2f, closeness);
                candidates.Add((p1.ThrustState, thrustWeight));
            }
        }

        // If only thrust can reach, gap-close into slash/flurry competes as an option
        bool onlyThrustCanReach = candidates.Count == 1 && candidates[0].state == p1.ThrustState;
        if (onlyThrustCanReach)
        {
            // Gap-close at 60% of thrust weight so thrust is favored (~62/38 split)
            candidates.Add((p1.GapCloseState, candidates[0].weight * 0.6f));
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

        // No attack can reach — walk closer, occasionally gap-close
        if (!TryThrustGapClose(dist, p))
            p1.ChangeSubState(p1.ApproachState);
    }

    /// <summary>
    /// Attempts a thrust gap-close if the player is in the gap-close range band
    /// and the random roll succeeds. Returns true if thrust was initiated.
    /// </summary>
    private bool TryThrustGapClose(float dist, EnemyProfile p)
    {
        if (dist >= p.p1ThrustCloseGapMinRange && dist <= p.p1ThrustCloseGapMaxRange
            && owner.IsAttackReady("Thrust")
            && Random.value < p.p1ThrustCloseGapChance)
        {
            p1.ChangeSubState(p1.GapCloseState);
            return true;
        }
        return false;
    }

    public override void Exit()
    {
        if (isStalking && owner.Anim != null)
            owner.Anim.SetBool(owner.AnimWalking, false);
        owner.StopHorizontal();
    }
}

// ---------------------------------------------------------------
//  P1 Gap Close — Sprint toward player, then launch a pre-chosen attack.
//  Attack is picked on enter so the boss sprints until in range for
//  that specific attack (not just whichever has longest reach).
// ---------------------------------------------------------------
public class ValkP1GapCloseState : EnemyState
{
    private ValkyrieBoss valk;
    private ValkP1Super p1;
    private IState chosenAttack;
    private float targetRange;

    public ValkP1GapCloseState(ValkyrieBoss valk, ValkP1Super p1) : base(valk)
    {
        this.valk = valk;
        this.p1 = p1;
    }

    public override void Enter()
    {
        Debug.Log("Valkyrie: P1 Gap Close");
        if (owner.Anim != null) owner.Anim.SetBool(owner.AnimWalking, true);
        PickAttack();
    }

    public override void FixedTick()
    {
        owner.FacePlayer();

        // Abort if we hit a ledge/wall
        if (owner.Ctx.nearLedgeAhead || owner.Ctx.nearWallAhead)
        {
            owner.StopHorizontal();
            p1.ChangeSubState(p1.DecisionState);
            return;
        }

        // Sprint toward player
        owner.MoveGround(owner.Profile.p1GapCloseRunSpeed);

        // In range for the chosen attack — launch it
        if (owner.Ctx.playerDistance <= targetRange && owner.Ctx.hasLineOfSightToPlayer)
        {
            p1.ChangeSubState(chosenAttack);
            return;
        }

        // Player escaped too far — fall back to approach
        if (owner.Ctx.playerDistance > owner.Profile.p1ThrustCloseGapMaxRange * 1.5f)
        {
            p1.ChangeSubState(p1.ApproachState);
        }
    }

    public override void Exit()
    {
        if (owner.Anim != null) owner.Anim.SetBool(owner.AnimWalking, false);
        owner.StopHorizontal();
    }

    private void PickAttack()
    {
        EnemyProfile p = owner.Profile;
        List<(IState state, float weight, float range)> options = new List<(IState, float, float)>();

        // Gap-close only picks slash or flurry — thrust is handled separately in Decision
        AttackDefinition slashDef = valk.GetAttackDef("Slash");
        if (slashDef != null && owner.IsAttackReady("Slash"))
        {
            float microLungeTravel = p.p1SlashMicroLungeSpeed * p.p1SlashMicroLungeDuration;
            float slashEffective = valk.SlashReach + 0.3f + microLungeTravel;
            options.Add((p1.SlashState, slashDef.selectionWeight, slashEffective));
        }

        AttackDefinition flurryDef = valk.GetAttackDef("Flurry");
        if (flurryDef != null && owner.IsAttackReady("Flurry"))
        {
            float flurryEffective = valk.FlurryReach + 0.3f;
            options.Add((p1.FlurryState, flurryDef.selectionWeight, flurryEffective));
        }

        if (options.Count == 0)
        {
            // Neither slash nor flurry ready — fall back to slash with default range
            chosenAttack = p1.SlashState;
            targetRange = valk.SlashReach + 0.3f;
            return;
        }

        // Weighted random pick
        float totalWeight = 0f;
        for (int i = 0; i < options.Count; i++)
            totalWeight += options[i].weight;

        float roll = Random.value * totalWeight;
        float cumulative = 0f;
        for (int i = 0; i < options.Count; i++)
        {
            cumulative += options[i].weight;
            if (roll <= cumulative)
            {
                chosenAttack = options[i].state;
                targetRange = options[i].range;
                return;
            }
        }

        var last = options[options.Count - 1];
        chosenAttack = last.state;
        targetRange = last.range;
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
    private float microLungeTimer;

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
        float windupDur = atk != null ? atk.windupDuration : 0.4f;
        timer = windupDur;

        // Micro-lunge: slide forward during early windup (capped to windup duration)
        microLungeTimer = Mathf.Min(owner.Profile.p1SlashMicroLungeDuration, windupDur);

        if (owner.Anim != null) owner.Anim.SetTrigger("Valk_Slash");
    }

    public override void FixedTick()
    {
        timer -= Time.fixedDeltaTime;

        switch (phase)
        {
            case Phase.Windup:
                // Micro-lunge: slide forward unless near ledge/wall
                if (microLungeTimer > 0f)
                {
                    microLungeTimer -= Time.fixedDeltaTime;
                    if (!owner.Ctx.nearLedgeAhead && !owner.Ctx.nearWallAhead)
                        owner.MoveGround(owner.Profile.p1SlashMicroLungeSpeed);
                    else
                        owner.StopHorizontal();

                    if (microLungeTimer <= 0f)
                        owner.StopHorizontal();
                }

                if (timer <= 0f)
                {
                    owner.StopHorizontal();
                    phase = Phase.Active;
                    AttackDefinition atk = valk.GetAttackDef("Slash");
                    timer = atk != null ? atk.activeDuration : 0.2f;

                    if (valk.SlashHitbox != null) valk.SlashHitbox.Activate();
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
        if (owner.Ctx.playerDistance <= owner.Profile.p1MaxEngageRange
            && owner.Ctx.hasLineOfSightToPlayer)
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

        if (owner.Anim != null) owner.Anim.SetTrigger("Valk_Flurry");
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
        if (owner.Ctx.playerDistance <= owner.Profile.p1MaxEngageRange
            && owner.Ctx.hasLineOfSightToPlayer)
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

        if (owner.Anim != null) owner.Anim.SetTrigger("Valk_Thrust");
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
        if (owner.Ctx.playerDistance <= owner.Profile.p1MaxEngageRange
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

        if (owner.Anim != null) owner.Anim.SetTrigger("Valk_ErraticSlash");
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

                    if (valk.ErraticSlashHitbox != null) valk.ErraticSlashHitbox.Activate();
                }
                break;

            case Phase.Active:
                if (timer <= 0f)
                {
                    if (valk.ErraticSlashHitbox != null) valk.ErraticSlashHitbox.Deactivate();

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
        if (valk.ErraticSlashHitbox != null) valk.ErraticSlashHitbox.Deactivate();
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

        if (owner.Anim != null) owner.Anim.SetTrigger("Valk_ErraticFlurry");
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

                    if (valk.ErraticFlurryHitbox != null) valk.ErraticFlurryHitbox.Activate();
                }
                break;

            case Phase.Active:
                // Multi-hit: re-activate hitbox at intervals
                hitIntervalTimer -= Time.fixedDeltaTime;
                if (hitIntervalTimer <= 0f && hitsRemaining > 0)
                {
                    if (valk.ErraticFlurryHitbox != null)
                    {
                        valk.ErraticFlurryHitbox.Deactivate();
                        valk.ErraticFlurryHitbox.Activate();
                    }
                    hitsRemaining--;
                    AttackDefinition atk = valk.GetAttackDef("P2Flurry");
                    hitIntervalTimer = atk != null ? atk.hitInterval : 0.15f;
                }

                if (timer <= 0f)
                {
                    if (valk.ErraticFlurryHitbox != null) valk.ErraticFlurryHitbox.Deactivate();

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
        if (valk.ErraticFlurryHitbox != null) valk.ErraticFlurryHitbox.Deactivate();
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

        if (owner.Anim != null) owner.Anim.SetTrigger("Valk_Plunge");
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
    }
}
