using UnityEngine;
using System.Collections.Generic;

// =================================================================
//  PHASE 1 SUPER STATE
//  Hierarchical state that owns a sub-FSM for greatsword combat.
//  Sub-states: P1Approach, P1Decision, P1ShockwaveSlash,
//              P1SwordTornado, P1SwordBeam.
// =================================================================
public class HeimdallP1Super : HierarchicalState
{
    private HeimdallBoss heimdall;

    // Phase 1 substates
    public HeimdallP1ApproachState ApproachState { get; private set; }
    public HeimdallP1DecisionState DecisionState { get; private set; }
    public HeimdallP1ShockwaveSlashState ShockwaveSlashState { get; private set; }
    public HeimdallP1SwordTornadoState SwordTornadoState { get; private set; }
    public HeimdallP1SwordBeamState SwordBeamState { get; private set; }

    public HeimdallP1Super(HeimdallBoss heimdall) : base(heimdall)
    {
        this.heimdall = heimdall;

        ApproachState = new HeimdallP1ApproachState(heimdall, this);
        DecisionState = new HeimdallP1DecisionState(heimdall, this);
        ShockwaveSlashState = new HeimdallP1ShockwaveSlashState(heimdall, this);
        SwordTornadoState = new HeimdallP1SwordTornadoState(heimdall, this);
        SwordBeamState = new HeimdallP1SwordBeamState(heimdall, this);
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
//  Hierarchical state that owns a Phase 2 sub-FSM for radiating
//  greatsword combat. A Behavior Tree selects the next attack,
//  but ONLY when the sub-FSM is in P2Idle. The BT sets an intent;
//  P2Idle consumes it.
// =================================================================
public class HeimdallP2Super : HierarchicalState
{
    private HeimdallBoss heimdall;
    private BTNode attackSelectorBT;

    // Phase 2 substates
    public HeimdallP2IdleState IdleState { get; private set; }
    public HeimdallP2ProjectileSwordsState ProjectileSwordsState { get; private set; }
    public HeimdallP2SwordPlungeState SwordPlungeState { get; private set; }
    public HeimdallP2GiantSlashState GiantSlashState { get; private set; }

    // BT intent — set by BT, consumed by P2Idle
    public IState RequestedP2Attack { get; set; }

    public HeimdallP2Super(HeimdallBoss heimdall) : base(heimdall)
    {
        this.heimdall = heimdall;

        IdleState = new HeimdallP2IdleState(heimdall, this);
        ProjectileSwordsState = new HeimdallP2ProjectileSwordsState(heimdall, this);
        SwordPlungeState = new HeimdallP2SwordPlungeState(heimdall, this);
        GiantSlashState = new HeimdallP2GiantSlashState(heimdall, this);

        BuildBehaviorTree();
    }

    public override void Enter()
    {
        Debug.Log("Heimdall: Entering Phase 2");
        RequestedP2Attack = null;
        subMachine.ChangeState(IdleState);
    }

    public override void Tick()
    {
        // BT ticks every frame but can only request attacks when in Idle
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
    // The BT checks cooldowns, positional context, and weights, then sets RequestedP2Attack.
    // It does NOT move rigidbodies or toggle hitboxes.
    //
    // Attack selection:
    //   ProjectileSwords: favored at range or when player is moving evasively.
    //   SwordPlunge: favored when player is grounded / on same platform.
    //   GiantSlash: committed high-pressure punish.
    private void BuildBehaviorTree()
    {
        attackSelectorBT = new BTAction(ctx =>
        {
            if (RequestedP2Attack != null) return BTStatus.Failure; // already pending

            float dist = owner.Ctx.playerDistance;
            EnemyProfile p = owner.Profile;

            List<(IState state, float weight)> candidates = new List<(IState, float)>();

            // --- Projectile Swords: ranged pressure ---
            AttackDefinition projSwords = heimdall.GetAttackDef("ProjectileSwords");
            if (projSwords != null && owner.IsAttackReady("ProjectileSwords"))
            {
                float w = projSwords.selectionWeight;
                if (dist >= p.heimdallProjectileSwordsMinRange && dist <= p.heimdallProjectileSwordsMaxRange)
                    w *= p.heimdallProjectileSwordsWeightMultiplier;
                candidates.Add((ProjectileSwordsState, w));
            }

            // --- Sword Plunge: favored when player is grounded ---
            AttackDefinition plunge = heimdall.GetAttackDef("SwordPlunge");
            if (plunge != null && owner.IsAttackReady("SwordPlunge"))
            {
                float w = plunge.selectionWeight;
                if (dist <= p.heimdallSwordPlungeMaxRange && owner.Ctx.isPlayerOnSamePlatform)
                    w *= p.heimdallSwordPlungeWeightMultiplier;
                candidates.Add((SwordPlungeState, w));
            }

            // --- Giant Slash: committed high-damage pressure ---
            AttackDefinition giantSlash = heimdall.GetAttackDef("GiantSlash");
            if (giantSlash != null && owner.IsAttackReady("GiantSlash"))
            {
                float w = giantSlash.selectionWeight;
                if (dist <= p.heimdallGiantSlashMaxRange)
                    w *= p.heimdallGiantSlashWeightMultiplier;
                candidates.Add((GiantSlashState, w));
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
                    RequestedP2Attack = candidates[i].state;
                    return BTStatus.Success;
                }
            }

            RequestedP2Attack = candidates[candidates.Count - 1].state;
            return BTStatus.Success;
        });
    }
}

// =================================================================
//  PHASE 1 SUBSTATES
// =================================================================

// ---------------------------------------------------------------
//  P1 Approach — Aggressive walk toward player until in engage range.
//  Heimdall is a pressure boss, not a spacing mage.
// ---------------------------------------------------------------
public class HeimdallP1ApproachState : EnemyState
{
    private HeimdallBoss heimdall;
    private HeimdallP1Super p1;

    public HeimdallP1ApproachState(HeimdallBoss heimdall, HeimdallP1Super p1) : base(heimdall)
    {
        this.heimdall = heimdall;
        this.p1 = p1;
    }

    public override void Enter()
    {
        if (owner.Anim != null) owner.Anim.SetBool(owner.AnimWalking, true);
    }

    public override void FixedTick()
    {
        owner.FacePlayer();

        // Ledge/wall avoidance
        if (owner.Ctx.nearLedgeAhead || owner.Ctx.nearWallAhead)
        {
            owner.StopHorizontal();

            // If in range, go to decision even if blocked
            if (owner.Ctx.playerDistance <= owner.Profile.heimdallMaxEngageRange
                && owner.Ctx.hasLineOfSightToPlayer)
                p1.ChangeSubState(p1.DecisionState);
            return;
        }

        owner.MoveGround(owner.Profile.approachSpeed);

        // Within max engage range and LOS — go to decision
        if (owner.Ctx.playerDistance <= owner.Profile.heimdallMaxEngageRange
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
//  P1 Decision — Brief pause while stalking, then pick a weighted
//  attack. Heimdall stalks aggressively toward the player.
// ---------------------------------------------------------------
public class HeimdallP1DecisionState : EnemyState
{
    private HeimdallBoss heimdall;
    private HeimdallP1Super p1;
    private float pauseTimer;
    private bool isStalking;

    private const float StalkStopBuffer = 0.3f;

    public HeimdallP1DecisionState(HeimdallBoss heimdall, HeimdallP1Super p1) : base(heimdall)
    {
        this.heimdall = heimdall;
        this.p1 = p1;
    }

    public override void Enter()
    {
        owner.FacePlayer();
        pauseTimer = Random.Range(owner.Profile.minAttackCooldown, owner.Profile.maxAttackCooldown);

        // Start stalking immediately if player is beyond close range
        isStalking = owner.Ctx.playerDistance > owner.Profile.heimdallCloseRange
            && !owner.Ctx.nearLedgeAhead && !owner.Ctx.nearWallAhead;
        if (isStalking && owner.Anim != null)
            owner.Anim.SetBool(owner.AnimWalking, true);
    }

    public override void FixedTick()
    {
        owner.FacePlayer();

        // Stalk while waiting
        if (pauseTimer > 0f)
        {
            pauseTimer -= Time.fixedDeltaTime;

            float threshold = isStalking
                ? owner.Profile.heimdallCloseRange - StalkStopBuffer
                : owner.Profile.heimdallCloseRange;
            bool shouldStalk = owner.Ctx.playerDistance > threshold
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
        if (dist > p.heimdallMaxEngageRange || !owner.Ctx.hasLineOfSightToPlayer)
        {
            p1.ChangeSubState(p1.ApproachState);
            return;
        }

        // Build candidate list
        List<(IState state, float weight)> candidates = new List<(IState, float)>();

        // --- Shockwave Slash: mid range, jumpable wave ---
        AttackDefinition shockDef = heimdall.GetAttackDef("ShockwaveSlash");
        if (shockDef != null && owner.IsAttackReady("ShockwaveSlash"))
        {
            float w = shockDef.selectionWeight;
            if (dist <= p.heimdallShockwaveSlashMaxRange)
                w *= p.heimdallShockwaveSlashWeightMultiplier;
            candidates.Add((p1.ShockwaveSlashState, w));
        }

        // --- Sword Tornado: close range, sustained danger ---
        AttackDefinition tornadoDef = heimdall.GetAttackDef("SwordTornado");
        if (tornadoDef != null && owner.IsAttackReady("SwordTornado"))
        {
            float w = tornadoDef.selectionWeight;
            if (dist <= p.heimdallSwordTornadoMaxRange)
                w *= p.heimdallSwordTornadoWeightMultiplier;
            candidates.Add((p1.SwordTornadoState, w));
        }

        // --- Sword Beam: ranged pressure ---
        AttackDefinition beamDef = heimdall.GetAttackDef("SwordBeam");
        if (beamDef != null && owner.IsAttackReady("SwordBeam"))
        {
            float w = beamDef.selectionWeight;
            if (dist >= p.heimdallSwordBeamMinRange && dist <= p.heimdallSwordBeamMaxRange)
                w *= p.heimdallSwordBeamWeightMultiplier;
            candidates.Add((p1.SwordBeamState, w));
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

        // No attack can reach — walk closer
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
//  P1 Shockwave Slash — Melee slash + horizontal shockwave.
//  Windup → Active (melee hitbox + spawn shockwave) → Recovery.
//  Reuses SlashProjectile for the shockwave wave.
// ---------------------------------------------------------------
public class HeimdallP1ShockwaveSlashState : EnemyState
{
    private enum Phase { Windup, Active, Recovery }

    private HeimdallBoss heimdall;
    private HeimdallP1Super p1;
    private Phase phase;
    private float timer;
    private bool shockwaveSpawned;

    public HeimdallP1ShockwaveSlashState(HeimdallBoss heimdall, HeimdallP1Super p1) : base(heimdall)
    {
        this.heimdall = heimdall;
        this.p1 = p1;
    }

    public override void Enter()
    {
        Debug.Log("Heimdall: P1 Shockwave Slash");
        phase = Phase.Windup;
        owner.StopHorizontal();
        owner.FacePlayer();
        shockwaveSpawned = false;

        AttackDefinition atk = heimdall.GetAttackDef("ShockwaveSlash");
        timer = atk != null ? atk.windupDuration : 0.25f;

        if (owner.Anim != null) owner.Anim.SetTrigger("Heimdall_ShockwaveSlash");
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
                    AttackDefinition atk = heimdall.GetAttackDef("ShockwaveSlash");
                    timer = atk != null ? atk.activeDuration : 0.25f;

                    if (heimdall.ShockwaveSlashHitbox != null) heimdall.ShockwaveSlashHitbox.Activate();

                    // Spawn jumpable shockwave
                    if (!shockwaveSpawned)
                    {
                        shockwaveSpawned = true;
                        SpawnShockwave();
                    }
                }
                break;

            case Phase.Active:
                if (timer <= 0f)
                {
                    if (heimdall.ShockwaveSlashHitbox != null) heimdall.ShockwaveSlashHitbox.Deactivate();

                    phase = Phase.Recovery;
                    AttackDefinition atk = heimdall.GetAttackDef("ShockwaveSlash");
                    timer = atk != null ? atk.recoveryDuration : 0.125f;

                    owner.StartCooldown("ShockwaveSlash");
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
        if (heimdall.ShockwaveSlashHitbox != null) heimdall.ShockwaveSlashHitbox.Deactivate();
    }

    private void SpawnShockwave()
    {
        EnemyProfile p = owner.Profile;
        AttackDefinition atk = heimdall.GetAttackDef("ShockwaveSlash");
        int facing = owner.FacingDirection;
        Vector2 spawnPos = (Vector2)owner.transform.position
            + new Vector2(facing * 1f, p.heimdallShockwaveHeight);
        Vector2 velocity = new Vector2(facing * p.heimdallShockwaveSpeed, 0f);

        heimdall.SpawnShockwave(spawnPos, velocity, atk != null ? atk.damage : 1);
    }

    private void ReturnToDecisionOrApproach()
    {
        if (owner.Ctx.playerDistance <= owner.Profile.heimdallMaxEngageRange
            && owner.Ctx.hasLineOfSightToPlayer)
            p1.ChangeSubState(p1.DecisionState);
        else
            p1.ChangeSubState(p1.ApproachState);
    }
}

// ---------------------------------------------------------------
//  P1 Sword Tornado — Spinning multi-hit close-range attack.
//  Windup → Active (sustained hitbox with multi-hit cycling) → Recovery.
// ---------------------------------------------------------------
public class HeimdallP1SwordTornadoState : EnemyState
{
    private enum Phase { Windup, Active, Recovery }

    private HeimdallBoss heimdall;
    private HeimdallP1Super p1;
    private Phase phase;
    private float timer;
    private int hitsRemaining;
    private float hitIntervalTimer;

    public HeimdallP1SwordTornadoState(HeimdallBoss heimdall, HeimdallP1Super p1) : base(heimdall)
    {
        this.heimdall = heimdall;
        this.p1 = p1;
    }

    public override void Enter()
    {
        Debug.Log("Heimdall: P1 Sword Tornado");
        phase = Phase.Windup;
        owner.StopHorizontal();
        owner.FacePlayer();

        AttackDefinition atk = heimdall.GetAttackDef("SwordTornado");
        timer = atk != null ? atk.windupDuration : 0.083f;

        if (owner.Anim != null) owner.Anim.SetTrigger("Heimdall_SwordTornado");
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
                    AttackDefinition atk = heimdall.GetAttackDef("SwordTornado");
                    timer = atk != null ? atk.activeDuration : 0.417f;
                    hitsRemaining = atk != null ? atk.hitCount : 4;
                    hitIntervalTimer = 0f;

                    if (heimdall.SwordTornadoHitbox != null) heimdall.SwordTornadoHitbox.Activate();
                }
                break;

            case Phase.Active:
                // Multi-hit: re-activate hitbox at intervals to clear hit tracking
                hitIntervalTimer -= Time.fixedDeltaTime;
                if (hitIntervalTimer <= 0f && hitsRemaining > 0)
                {
                    if (heimdall.SwordTornadoHitbox != null)
                    {
                        heimdall.SwordTornadoHitbox.Deactivate();
                        heimdall.SwordTornadoHitbox.Activate();
                    }
                    hitsRemaining--;
                    AttackDefinition atk = heimdall.GetAttackDef("SwordTornado");
                    hitIntervalTimer = atk != null ? atk.hitInterval : 0.2f;
                }

                if (timer <= 0f)
                {
                    if (heimdall.SwordTornadoHitbox != null) heimdall.SwordTornadoHitbox.Deactivate();

                    phase = Phase.Recovery;
                    AttackDefinition atk = heimdall.GetAttackDef("SwordTornado");
                    timer = atk != null ? atk.recoveryDuration : 0.083f;

                    owner.StartCooldown("SwordTornado");
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
        if (heimdall.SwordTornadoHitbox != null) heimdall.SwordTornadoHitbox.Deactivate();
    }

    private void ReturnToDecisionOrApproach()
    {
        if (owner.Ctx.playerDistance <= owner.Profile.heimdallMaxEngageRange
            && owner.Ctx.hasLineOfSightToPlayer)
            p1.ChangeSubState(p1.DecisionState);
        else
            p1.ChangeSubState(p1.ApproachState);
    }
}

// ---------------------------------------------------------------
//  P1 Sword Beam — Concentrated beam attack using hitbox.
//  Windup → Active (hitbox active) → Recovery.
//  The animation resizes the SwordBeamHitbox collider to match the beam visual.
// ---------------------------------------------------------------
public class HeimdallP1SwordBeamState : EnemyState
{
    private enum Phase { Windup, Active, Recovery }

    private HeimdallBoss heimdall;
    private HeimdallP1Super p1;
    private Phase phase;
    private float timer;

    public HeimdallP1SwordBeamState(HeimdallBoss heimdall, HeimdallP1Super p1) : base(heimdall)
    {
        this.heimdall = heimdall;
        this.p1 = p1;
    }

    public override void Enter()
    {
        Debug.Log("Heimdall: P1 Sword Beam");
        phase = Phase.Windup;
        owner.StopHorizontal();
        owner.FacePlayer();

        AttackDefinition atk = heimdall.GetAttackDef("SwordBeam");
        timer = atk != null ? atk.windupDuration : 0.1f;

        if (owner.Anim != null) owner.Anim.SetTrigger("Heimdall_SwordBeam");
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
                    AttackDefinition atk = heimdall.GetAttackDef("SwordBeam");
                    timer = atk != null ? atk.activeDuration : 0.3f;

                    if (heimdall.SwordBeamHitbox != null) heimdall.SwordBeamHitbox.Activate();
                }
                break;

            case Phase.Active:
                if (timer <= 0f)
                {
                    if (heimdall.SwordBeamHitbox != null) heimdall.SwordBeamHitbox.Deactivate();

                    phase = Phase.Recovery;
                    AttackDefinition atk = heimdall.GetAttackDef("SwordBeam");
                    timer = atk != null ? atk.recoveryDuration : 0.1f;

                    owner.StartCooldown("SwordBeam");
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
        if (heimdall.SwordBeamHitbox != null) heimdall.SwordBeamHitbox.Deactivate();
    }

    private void ReturnToDecisionOrApproach()
    {
        if (owner.Ctx.playerDistance <= owner.Profile.heimdallMaxEngageRange
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
//  P2 Idle — Aggressive pressure/spacing between attacks.
//  Stalks toward player, consumes BT intent to transition to attacks.
//  Shorter delay than P1 for aggression.
// ---------------------------------------------------------------
public class HeimdallP2IdleState : EnemyState
{
    private HeimdallBoss heimdall;
    private HeimdallP2Super p2;
    private float decisionDelay;
    private bool isWalking;

    private const float StalkStopBuffer = 0.3f;

    public HeimdallP2IdleState(HeimdallBoss heimdall, HeimdallP2Super p2) : base(heimdall)
    {
        this.heimdall = heimdall;
        this.p2 = p2;
    }

    public override void Enter()
    {
        p2.RequestedP2Attack = null;
        decisionDelay = Random.Range(
            owner.Profile.heimdallP2MinAttackCooldown,
            owner.Profile.heimdallP2MaxAttackCooldown);

        owner.FacePlayer();
        isWalking = owner.Ctx.playerDistance > owner.Profile.heimdallCloseRange
            && !owner.Ctx.nearLedgeAhead && !owner.Ctx.nearWallAhead;
        if (isWalking && owner.Anim != null)
            owner.Anim.SetBool(owner.AnimWalking, true);
    }

    public override void FixedTick()
    {
        float dt = Time.fixedDeltaTime;
        decisionDelay -= dt;
        owner.FacePlayer();

        // Consume BT intent if ready
        if (decisionDelay <= 0f && p2.RequestedP2Attack != null)
        {
            IState attack = p2.RequestedP2Attack;
            p2.RequestedP2Attack = null;
            SetWalking(false);
            owner.StopHorizontal();
            p2.ChangeSubState(attack);
            return;
        }

        // Aggressive stalking toward player
        float threshold = isWalking
            ? owner.Profile.heimdallCloseRange - StalkStopBuffer
            : owner.Profile.heimdallCloseRange;
        bool shouldWalk = owner.Ctx.playerDistance > threshold
            && !owner.Ctx.nearLedgeAhead && !owner.Ctx.nearWallAhead;

        if (shouldWalk)
        {
            SetWalking(true);
            owner.MoveGround(owner.Profile.approachSpeed);
        }
        else
        {
            SetWalking(false);
            owner.StopHorizontal();
        }
    }

    public override void Exit()
    {
        SetWalking(false);
        owner.StopHorizontal();
    }

    private void SetWalking(bool walking)
    {
        if (walking == isWalking) return;
        isWalking = walking;
        if (owner.Anim != null) owner.Anim.SetBool(owner.AnimWalking, walking);
    }
}

// ---------------------------------------------------------------
//  P2 Projectile Swords — Fires multiple projectile swords that
//  curve toward the player's last-known position.
//  Reuses OdinProjectile for the curve-to-last-known behavior.
//  Windup → Active (spawn projectile swords) → Recovery.
// ---------------------------------------------------------------
public class HeimdallP2ProjectileSwordsState : EnemyState
{
    private enum Phase { Windup, Active, Recovery }

    private HeimdallBoss heimdall;
    private HeimdallP2Super p2;
    private Phase phase;
    private float timer;
    private bool projectilesSpawned;

    public HeimdallP2ProjectileSwordsState(HeimdallBoss heimdall, HeimdallP2Super p2) : base(heimdall)
    {
        this.heimdall = heimdall;
        this.p2 = p2;
    }

    public override void Enter()
    {
        Debug.Log("Heimdall: P2 Projectile Swords");
        phase = Phase.Windup;
        owner.StopHorizontal();
        owner.FacePlayer();
        projectilesSpawned = false;

        AttackDefinition atk = heimdall.GetAttackDef("ProjectileSwords");
        timer = atk != null ? atk.windupDuration : 0.2f;

        if (owner.Anim != null) owner.Anim.SetTrigger("Heimdall_ProjectileSwords");
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
                    AttackDefinition atk = heimdall.GetAttackDef("ProjectileSwords");
                    timer = atk != null ? atk.activeDuration : 0.2f;

                    if (!projectilesSpawned)
                    {
                        projectilesSpawned = true;
                        SpawnProjectileSwords();
                    }
                }
                break;

            case Phase.Active:
                if (timer <= 0f)
                {
                    phase = Phase.Recovery;
                    AttackDefinition atk = heimdall.GetAttackDef("ProjectileSwords");
                    timer = atk != null ? atk.recoveryDuration : 0.2f;

                    owner.StartCooldown("ProjectileSwords");
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

    private void SpawnProjectileSwords()
    {
        EnemyProfile p = owner.Profile;
        int count = p.heimdallProjectileSwordCount;
        float spreadAngle = p.heimdallProjectileSwordSpreadAngle;
        float speed = p.heimdallProjectileSwordSpeed;
        int facing = owner.FacingDirection;

        // Capture player's last-known position at fire time
        Vector2 targetPos = owner.Ctx.playerTransform != null
            ? (Vector2)owner.Ctx.playerTransform.position
            : owner.Ctx.lastSeenPlayerPos;

        float centerAngle = facing > 0 ? 0f : 180f;
        float startAngle = centerAngle - spreadAngle * 0.5f;

        for (int i = 0; i < count; i++)
        {
            float angle;
            if (count > 1)
                angle = startAngle + spreadAngle * ((float)i / (count - 1));
            else
                angle = centerAngle;

            float rad = angle * Mathf.Deg2Rad;
            Vector2 fireDir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)).normalized;

            // Each projectile targets slightly offset positions for spread
            Vector2 offsetTarget = targetPos + Random.insideUnitCircle * 0.5f;

            AttackDefinition projAtk = heimdall.GetAttackDef("ProjectileSwords");
            heimdall.SpawnProjectileSword(
                fireDir * speed,
                offsetTarget,
                projAtk != null ? projAtk.damage : 1,
                p.heimdallProjectileSwordCurveDelay,
                p.heimdallProjectileSwordCurveStrength,
                p.heimdallProjectileSwordLifetime);
        }
    }
}

// ---------------------------------------------------------------
//  P2 Sword Plunge — Leap into the air and plunge the greatsword
//  into the ground, spawning a floor impact that punishes grounded
//  players.
//  Windup → Leap (upward) → Fall (downward) → Impact (floor hit) → Recovery.
// ---------------------------------------------------------------
public class HeimdallP2SwordPlungeState : EnemyState
{
    private enum Phase { Windup, Leap, Fall, Impact, Recovery }

    private HeimdallBoss heimdall;
    private HeimdallP2Super p2;
    private Phase phase;
    private float timer;
    private bool hasPeaked;

    public HeimdallP2SwordPlungeState(HeimdallBoss heimdall, HeimdallP2Super p2) : base(heimdall)
    {
        this.heimdall = heimdall;
        this.p2 = p2;
    }

    public override void Enter()
    {
        Debug.Log("Heimdall: P2 Sword Plunge");
        phase = Phase.Windup;
        owner.StopHorizontal();
        owner.FacePlayer();
        hasPeaked = false;

        AttackDefinition atk = heimdall.GetAttackDef("SwordPlunge");
        timer = atk != null ? atk.windupDuration : 0.1f;

        if (owner.Anim != null) owner.Anim.SetTrigger("Heimdall_SwordPlunge");
    }

    public override void FixedTick()
    {
        timer -= Time.fixedDeltaTime;

        switch (phase)
        {
            case Phase.Windup:
                if (timer <= 0f)
                {
                    phase = Phase.Leap;
                    EnemyProfile p = owner.Profile;

                    // Launch upward and slightly toward player
                    owner.FacePlayer();
                    owner.Rb.linearVelocity = new Vector2(
                        owner.FacingDirection * p.heimdallPlungeForwardForce,
                        p.heimdallPlungeLeapForceY);
                }
                break;

            case Phase.Leap:
                // Wait until we peak (velocity.y goes negative)
                if (owner.Rb.linearVelocity.y <= 0f)
                {
                    hasPeaked = true;
                    phase = Phase.Fall;

                    // Accelerate downward for dramatic plunge
                    owner.Rb.linearVelocity = new Vector2(0f, -owner.Profile.heimdallPlungeFallSpeed);
                }
                break;

            case Phase.Fall:
                // Force fast fall speed
                if (owner.Rb.linearVelocity.y > -owner.Profile.heimdallPlungeFallSpeed)
                    owner.Rb.linearVelocity = new Vector2(0f, -owner.Profile.heimdallPlungeFallSpeed);

                // Check if we've landed
                if (owner.Ctx.isGrounded && hasPeaked)
                {
                    phase = Phase.Impact;
                    owner.StopAll();

                    AttackDefinition atk = heimdall.GetAttackDef("SwordPlunge");
                    timer = atk != null ? atk.activeDuration : 0.2f;

                    // Activate plunge hitbox at landing
                    if (heimdall.SwordPlungeHitbox != null) heimdall.SwordPlungeHitbox.Activate();
                }
                break;

            case Phase.Impact:
                if (timer <= 0f)
                {
                    if (heimdall.SwordPlungeHitbox != null) heimdall.SwordPlungeHitbox.Deactivate();

                    phase = Phase.Recovery;
                    AttackDefinition atk = heimdall.GetAttackDef("SwordPlunge");
                    timer = atk != null ? atk.recoveryDuration : 0.1f;

                    owner.StartCooldown("SwordPlunge");
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
        if (heimdall.SwordPlungeHitbox != null) heimdall.SwordPlungeHitbox.Deactivate();
        owner.StopHorizontal();
    }
}

// ---------------------------------------------------------------
//  P2 Giant Slash — Massive slash using hitbox.
//  Stronger, broader version of Phase 1 shockwave slash.
//  Windup → Active (hitbox active) → Recovery.
// ---------------------------------------------------------------
public class HeimdallP2GiantSlashState : EnemyState
{
    private enum Phase { Windup, Active, Recovery }

    private HeimdallBoss heimdall;
    private HeimdallP2Super p2;
    private Phase phase;
    private float timer;

    public HeimdallP2GiantSlashState(HeimdallBoss heimdall, HeimdallP2Super p2) : base(heimdall)
    {
        this.heimdall = heimdall;
        this.p2 = p2;
    }

    public override void Enter()
    {
        Debug.Log("Heimdall: P2 Giant Slash");
        phase = Phase.Windup;
        owner.StopHorizontal();
        owner.FacePlayer();

        AttackDefinition atk = heimdall.GetAttackDef("GiantSlash");
        timer = atk != null ? atk.windupDuration : 0.083f;

        if (owner.Anim != null) owner.Anim.SetTrigger("Heimdall_GiantSlash");
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
                    AttackDefinition atk = heimdall.GetAttackDef("GiantSlash");
                    timer = atk != null ? atk.activeDuration : 0.167f;

                    if (heimdall.GiantSlashHitbox != null) heimdall.GiantSlashHitbox.Activate();
                }
                break;

            case Phase.Active:
                if (timer <= 0f)
                {
                    if (heimdall.GiantSlashHitbox != null) heimdall.GiantSlashHitbox.Deactivate();

                    phase = Phase.Recovery;
                    AttackDefinition atk = heimdall.GetAttackDef("GiantSlash");
                    timer = atk != null ? atk.recoveryDuration : 0.083f;

                    owner.StartCooldown("GiantSlash");
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
        if (heimdall.GiantSlashHitbox != null) heimdall.GiantSlashHitbox.Deactivate();
    }
}
