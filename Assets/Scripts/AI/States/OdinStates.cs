using UnityEngine;
using System.Collections.Generic;

// =================================================================
//  PHASE 1 SUPER STATE
//  Hierarchical state that owns a sub-FSM for staff mage combat.
//  Sub-states: P1Approach, P1Decision, P1StaffProjectile,
//              P1GroundSpikes, P1StaffMelee.
// =================================================================
public class OdinP1Super : HierarchicalState
{
    private OdinBoss odin;

    // Phase 1 substates
    public OdinP1ApproachState ApproachState { get; private set; }
    public OdinP1DecisionState DecisionState { get; private set; }
    public OdinP1StaffProjectileState StaffProjectileState { get; private set; }
    public OdinP1GroundSpikesState GroundSpikesState { get; private set; }
    public OdinP1StaffMeleeState StaffMeleeState { get; private set; }

    public OdinP1Super(OdinBoss odin) : base(odin)
    {
        this.odin = odin;

        ApproachState = new OdinP1ApproachState(odin, this);
        DecisionState = new OdinP1DecisionState(odin, this);
        StaffProjectileState = new OdinP1StaffProjectileState(odin, this);
        GroundSpikesState = new OdinP1GroundSpikesState(odin, this);
        StaffMeleeState = new OdinP1StaffMeleeState(odin, this);
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
//  Hierarchical state that owns a Phase 2 sub-FSM for intensified magic.
//  A Behavior Tree selects the next attack, but ONLY when the
//  sub-FSM is in P2Idle. The BT sets an intent; P2Idle consumes it.
// =================================================================
public class OdinP2Super : HierarchicalState
{
    private OdinBoss odin;
    private BTNode attackSelectorBT;

    // Phase 2 substates
    public OdinP2IdleState IdleState { get; private set; }
    public OdinP2TripleProjectileState TripleProjectileState { get; private set; }
    public OdinP2ConsecutiveSpikesState ConsecutiveSpikesState { get; private set; }
    public OdinP2LargeSlashState LargeSlashState { get; private set; }

    // BT intent — set by BT, consumed by P2Idle
    public IState RequestedP2Attack { get; set; }

    public OdinP2Super(OdinBoss odin) : base(odin)
    {
        this.odin = odin;

        IdleState = new OdinP2IdleState(odin, this);
        TripleProjectileState = new OdinP2TripleProjectileState(odin, this);
        ConsecutiveSpikesState = new OdinP2ConsecutiveSpikesState(odin, this);
        LargeSlashState = new OdinP2LargeSlashState(odin, this);

        BuildBehaviorTree();
    }

    public override void Enter()
    {
        Debug.Log("Odin: Entering Phase 2");
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
    //   TripleProjectile: favored at range or when player is moving evasively.
    //   ConsecutiveSpikes: favored when player is grounded.
    //   LargeSlash: committed punish at mid range.
    private void BuildBehaviorTree()
    {
        attackSelectorBT = new BTAction(ctx =>
        {
            if (RequestedP2Attack != null) return BTStatus.Failure; // already pending

            float dist = owner.Ctx.playerDistance;
            EnemyProfile p = owner.Profile;

            List<(IState state, float weight)> candidates = new List<(IState, float)>();

            // --- Triple Projectile: ranged pressure ---
            AttackDefinition tripleProj = odin.GetAttackDef("TripleProjectile");
            if (tripleProj != null && owner.IsAttackReady("TripleProjectile"))
            {
                float w = tripleProj.selectionWeight;
                if (dist >= p.odinTripleProjectileMinRange && dist <= p.odinTripleProjectileMaxRange)
                    w *= p.odinTripleProjectileWeightMultiplier;
                candidates.Add((TripleProjectileState, w));
            }

            // --- Consecutive Spikes: favored when player is grounded ---
            AttackDefinition conSpikes = odin.GetAttackDef("ConsecutiveSpikes");
            if (conSpikes != null && owner.IsAttackReady("ConsecutiveSpikes"))
            {
                float w = conSpikes.selectionWeight;
                if (dist <= p.odinConsecutiveSpikesMaxRange && owner.Ctx.isPlayerOnSamePlatform)
                    w *= p.odinConsecutiveSpikesWeightMultiplier;
                candidates.Add((ConsecutiveSpikesState, w));
            }

            // --- Large Slash: committed mid-range punish ---
            AttackDefinition largeSlash = odin.GetAttackDef("LargeSlash");
            if (largeSlash != null && owner.IsAttackReady("LargeSlash"))
            {
                float w = largeSlash.selectionWeight;
                if (dist <= p.odinLargeSlashMaxRange)
                    w *= p.odinLargeSlashWeightMultiplier;
                candidates.Add((LargeSlashState, w));
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
//  P1 Approach — Maintain optimal spacing from the player.
//  Odin is a mage: he walks toward the player if far, backs off if
//  too close, and transitions to Decision once in engage range.
// ---------------------------------------------------------------
public class OdinP1ApproachState : EnemyState
{
    private OdinBoss odin;
    private OdinP1Super p1;

    public OdinP1ApproachState(OdinBoss odin, OdinP1Super p1) : base(odin)
    {
        this.odin = odin;
        this.p1 = p1;
    }

    public override void Enter()
    {
        if (owner.Anim != null) owner.Anim.SetBool(owner.AnimWalking, true);
    }

    public override void FixedTick()
    {
        owner.FacePlayer();

        float dist = owner.Ctx.playerDistance;
        EnemyProfile p = owner.Profile;

        // Ledge/wall avoidance
        if (owner.Ctx.nearLedgeAhead || owner.Ctx.nearWallAhead)
        {
            owner.StopHorizontal();

            // If in range, go to decision even if blocked
            if (dist <= p.odinMaxEngageRange && owner.Ctx.hasLineOfSightToPlayer)
                p1.ChangeSubState(p1.DecisionState);
            return;
        }

        // Too close — back up to optimal spacing
        if (dist < p.odinCloseRange)
        {
            owner.FaceDirection(owner.Ctx.playerRelativePos.x > 0 ? -1 : 1);
            owner.MoveGround(p.approachSpeed);
        }
        else
        {
            owner.MoveGround(p.approachSpeed);
        }

        // Within max engage range and LOS — go to decision
        if (dist <= p.odinMaxEngageRange && owner.Ctx.hasLineOfSightToPlayer)
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
//  attack. Odin maintains spacing while deciding.
// ---------------------------------------------------------------
public class OdinP1DecisionState : EnemyState
{
    private OdinBoss odin;
    private OdinP1Super p1;
    private float pauseTimer;
    private bool isStalking;

    private const float StalkStopBuffer = 0.3f;

    public OdinP1DecisionState(OdinBoss odin, OdinP1Super p1) : base(odin)
    {
        this.odin = odin;
        this.p1 = p1;
    }

    public override void Enter()
    {
        owner.FacePlayer();
        pauseTimer = Random.Range(owner.Profile.minAttackCooldown, owner.Profile.maxAttackCooldown);

        isStalking = ShouldStalk();
        if (isStalking && owner.Anim != null)
            owner.Anim.SetBool(owner.AnimWalking, true);
    }

    public override void FixedTick()
    {
        owner.FacePlayer();

        // While waiting, maintain spacing
        if (pauseTimer > 0f)
        {
            pauseTimer -= Time.fixedDeltaTime;
            bool shouldStalk = ShouldStalk();

            if (shouldStalk)
            {
                if (!isStalking)
                {
                    isStalking = true;
                    if (owner.Anim != null) owner.Anim.SetBool(owner.AnimWalking, true);
                }

                // Walk toward optimal range — back off if too close
                float dist = owner.Ctx.playerDistance;
                if (dist < owner.Profile.odinCloseRange)
                {
                    owner.FaceDirection(owner.Ctx.playerRelativePos.x > 0 ? -1 : 1);
                    owner.MoveGround(owner.Profile.approachSpeed);
                }
                else
                {
                    owner.MoveGround(owner.Profile.approachSpeed);
                }
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

        float finalDist = owner.Ctx.playerDistance;
        EnemyProfile p = owner.Profile;

        // Player moved beyond max engage range — walk closer
        if (finalDist > p.odinMaxEngageRange || !owner.Ctx.hasLineOfSightToPlayer)
        {
            p1.ChangeSubState(p1.ApproachState);
            return;
        }

        // Build candidate list
        List<(IState state, float weight)> candidates = new List<(IState, float)>();

        // --- Staff Projectile: ranged pressure ---
        AttackDefinition projDef = odin.GetAttackDef("StaffProjectile");
        if (projDef != null && owner.IsAttackReady("StaffProjectile"))
        {
            float w = projDef.selectionWeight;
            if (finalDist >= p.odinStaffProjectileMinRange && finalDist <= p.odinStaffProjectileMaxRange)
                w *= p.odinStaffProjectileWeightMultiplier;
            candidates.Add((p1.StaffProjectileState, w));
        }

        // --- Ground Spikes: mid range, grounded player preferred ---
        AttackDefinition spikesDef = odin.GetAttackDef("GroundSpikes");
        if (spikesDef != null && owner.IsAttackReady("GroundSpikes"))
        {
            float w = spikesDef.selectionWeight;
            if (finalDist <= p.odinGroundSpikesMaxRange && owner.Ctx.isPlayerOnSamePlatform)
                w *= p.odinGroundSpikesWeightMultiplier;
            candidates.Add((p1.GroundSpikesState, w));
        }

        // --- Staff Melee: close range sweep ---
        AttackDefinition meleeDef = odin.GetAttackDef("StaffMelee");
        if (meleeDef != null && owner.IsAttackReady("StaffMelee"))
        {
            float w = meleeDef.selectionWeight;
            if (finalDist <= p.odinStaffMeleeMaxRange)
                w *= p.odinStaffMeleeWeightMultiplier;
            candidates.Add((p1.StaffMeleeState, w));
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

    private bool ShouldStalk()
    {
        float dist = owner.Ctx.playerDistance;
        float threshold = isStalking
            ? owner.Profile.odinOptimalSpacing - StalkStopBuffer
            : owner.Profile.odinOptimalSpacing;

        // Stalk if too far from optimal or too close (need to back up)
        bool tooFar = dist > threshold && !owner.Ctx.nearLedgeAhead && !owner.Ctx.nearWallAhead;
        bool tooClose = dist < owner.Profile.odinCloseRange && !owner.Ctx.nearLedgeAhead && !owner.Ctx.nearWallAhead;
        return tooFar || tooClose;
    }
}

// ---------------------------------------------------------------
//  P1 Staff Projectile — Fire a curving projectile from staff.
//  Windup → Active (spawn projectile) → Recovery.
// ---------------------------------------------------------------
public class OdinP1StaffProjectileState : EnemyState
{
    private enum Phase { Windup, Active, Recovery }

    private OdinBoss odin;
    private OdinP1Super p1;
    private Phase phase;
    private float timer;
    private bool projectileSpawned;

    public OdinP1StaffProjectileState(OdinBoss odin, OdinP1Super p1) : base(odin)
    {
        this.odin = odin;
        this.p1 = p1;
    }

    public override void Enter()
    {
        Debug.Log("Odin: P1 Staff Projectile");
        phase = Phase.Windup;
        owner.StopHorizontal();
        owner.FacePlayer();
        projectileSpawned = false;

        AttackDefinition atk = odin.GetAttackDef("StaffProjectile");
        timer = atk != null ? atk.windupDuration : 0.4f;

        if (owner.Anim != null) owner.Anim.SetTrigger("Odin_StaffProjectile");
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
                    AttackDefinition atk = odin.GetAttackDef("StaffProjectile");
                    timer = atk != null ? atk.activeDuration : 0.2f;

                    if (!projectileSpawned)
                    {
                        projectileSpawned = true;
                        SpawnProjectile();
                    }
                }
                break;

            case Phase.Active:
                if (timer <= 0f)
                {
                    phase = Phase.Recovery;
                    AttackDefinition atk = odin.GetAttackDef("StaffProjectile");
                    timer = atk != null ? atk.recoveryDuration : 0.5f;

                    owner.StartCooldown("StaffProjectile");
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

    private void SpawnProjectile()
    {
        EnemyProfile p = owner.Profile;
        Vector2 fireDir = new Vector2(owner.FacingDirection, 0.1f).normalized;
        Vector2 targetPos = owner.Ctx.playerTransform != null
            ? (Vector2)owner.Ctx.playerTransform.position
            : owner.Ctx.lastSeenPlayerPos;

        odin.SpawnOdinProjectile(
            fireDir * p.odinProjectileSpeed,
            targetPos,
            2,
            p.odinProjectileCurveDelay,
            p.odinProjectileCurveStrength,
            p.odinProjectileLifetime);
    }

    private void ReturnToDecisionOrApproach()
    {
        if (owner.Ctx.playerDistance <= owner.Profile.odinMaxEngageRange
            && owner.Ctx.hasLineOfSightToPlayer)
            p1.ChangeSubState(p1.DecisionState);
        else
            p1.ChangeSubState(p1.ApproachState);
    }
}

// ---------------------------------------------------------------
//  P1 Ground Spikes — Strike staff into ground, summon spikes
//  with gaps for dodging.
//  Windup → Active (spawn spikes sequentially) → Recovery.
// ---------------------------------------------------------------
public class OdinP1GroundSpikesState : EnemyState
{
    private enum Phase { Windup, Active, Recovery }

    private OdinBoss odin;
    private OdinP1Super p1;
    private Phase phase;
    private float timer;
    private int spikesSpawned;
    private float spikeDelayTimer;

    public OdinP1GroundSpikesState(OdinBoss odin, OdinP1Super p1) : base(odin)
    {
        this.odin = odin;
        this.p1 = p1;
    }

    public override void Enter()
    {
        Debug.Log("Odin: P1 Ground Spikes");
        phase = Phase.Windup;
        owner.StopHorizontal();
        owner.FacePlayer();
        spikesSpawned = 0;
        spikeDelayTimer = 0f;

        AttackDefinition atk = odin.GetAttackDef("GroundSpikes");
        timer = atk != null ? atk.windupDuration : 0.5f;

        if (owner.Anim != null) owner.Anim.SetTrigger("Odin_GroundSpikes");
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
                    AttackDefinition atk = odin.GetAttackDef("GroundSpikes");
                    timer = atk != null ? atk.activeDuration : 1.0f;
                    spikeDelayTimer = 0f;
                }
                break;

            case Phase.Active:
                spikeDelayTimer -= Time.fixedDeltaTime;

                if (spikeDelayTimer <= 0f && spikesSpawned < owner.Profile.odinSpikeCount)
                {
                    SpawnSpikeAtIndex(spikesSpawned);
                    spikesSpawned++;
                    spikeDelayTimer = owner.Profile.odinSpikeDelay;
                }

                if (timer <= 0f)
                {
                    phase = Phase.Recovery;
                    AttackDefinition atk = odin.GetAttackDef("GroundSpikes");
                    timer = atk != null ? atk.recoveryDuration : 0.6f;

                    owner.StartCooldown("GroundSpikes");
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

    private void SpawnSpikeAtIndex(int index)
    {
        EnemyProfile p = owner.Profile;
        int facing = owner.FacingDirection;

        // Spikes spawn in a line ahead of Odin with gaps:
        // spike at positions 1, 3, 5... (skip even positions for gaps)
        float offset = (index * 2 + 1) * p.odinSpikeSpacing * 0.5f;
        Vector2 spawnPos = (Vector2)owner.transform.position + new Vector2(facing * offset, 0f);

        // Raycast down to find ground surface
        RaycastHit2D groundHit = Physics2D.Raycast(spawnPos + Vector2.up * 2f, Vector2.down, 6f, owner.GroundLayer);
        if (groundHit.collider != null)
        {
            spawnPos = groundHit.point;
        }

        odin.SpawnGroundSpike(spawnPos, p.odinSpikeActiveDuration);
    }

    private void ReturnToDecisionOrApproach()
    {
        if (owner.Ctx.playerDistance <= owner.Profile.odinMaxEngageRange
            && owner.Ctx.hasLineOfSightToPlayer)
            p1.ChangeSubState(p1.DecisionState);
        else
            p1.ChangeSubState(p1.ApproachState);
    }
}

// ---------------------------------------------------------------
//  P1 Staff Melee — Sweeping melee swing.
//  Windup → Active (hitbox) → Recovery.
// ---------------------------------------------------------------
public class OdinP1StaffMeleeState : EnemyState
{
    private enum Phase { Windup, Active, Recovery }

    private OdinBoss odin;
    private OdinP1Super p1;
    private Phase phase;
    private float timer;

    public OdinP1StaffMeleeState(OdinBoss odin, OdinP1Super p1) : base(odin)
    {
        this.odin = odin;
        this.p1 = p1;
    }

    public override void Enter()
    {
        Debug.Log("Odin: P1 Staff Melee");
        phase = Phase.Windup;
        owner.StopHorizontal();
        owner.FacePlayer();

        AttackDefinition atk = odin.GetAttackDef("StaffMelee");
        timer = atk != null ? atk.windupDuration : 0.3f;

        if (owner.Anim != null) owner.Anim.SetTrigger("Odin_StaffMelee");
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
                    AttackDefinition atk = odin.GetAttackDef("StaffMelee");
                    timer = atk != null ? atk.activeDuration : 0.3f;

                    if (odin.StaffMeleeHitbox != null) odin.StaffMeleeHitbox.Activate();
                }
                break;

            case Phase.Active:
                if (timer <= 0f)
                {
                    if (odin.StaffMeleeHitbox != null) odin.StaffMeleeHitbox.Deactivate();

                    phase = Phase.Recovery;
                    AttackDefinition atk = odin.GetAttackDef("StaffMelee");
                    timer = atk != null ? atk.recoveryDuration : 0.5f;

                    owner.StartCooldown("StaffMelee");
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
        if (odin.StaffMeleeHitbox != null) odin.StaffMeleeHitbox.Deactivate();
    }

    private void ReturnToDecisionOrApproach()
    {
        if (owner.Ctx.playerDistance <= owner.Profile.odinMaxEngageRange
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
//  P2 Idle — Spacing/pressure state between attacks.
//  Stalks to maintain optimal range, consumes BT intent to
//  transition to attacks. Shorter delay than P1 for aggression.
// ---------------------------------------------------------------
public class OdinP2IdleState : EnemyState
{
    private OdinBoss odin;
    private OdinP2Super p2;
    private float decisionDelay;
    private bool isWalking;

    private const float StalkStopBuffer = 0.3f;

    public OdinP2IdleState(OdinBoss odin, OdinP2Super p2) : base(odin)
    {
        this.odin = odin;
        this.p2 = p2;
    }

    public override void Enter()
    {
        p2.RequestedP2Attack = null;
        decisionDelay = Random.Range(
            owner.Profile.odinP2MinAttackCooldown,
            owner.Profile.odinP2MaxAttackCooldown);

        owner.FacePlayer();
        isWalking = ShouldWalk();
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

        // Maintain spacing while waiting
        bool shouldWalk = ShouldWalk();

        if (shouldWalk)
        {
            SetWalking(true);

            float dist = owner.Ctx.playerDistance;
            if (dist < owner.Profile.odinCloseRange)
            {
                // Back off
                owner.FaceDirection(owner.Ctx.playerRelativePos.x > 0 ? -1 : 1);
                owner.MoveGround(owner.Profile.approachSpeed);
            }
            else
            {
                owner.MoveGround(owner.Profile.approachSpeed);
            }
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

    private bool ShouldWalk()
    {
        if (owner.Ctx.nearLedgeAhead || owner.Ctx.nearWallAhead) return false;
        float dist = owner.Ctx.playerDistance;
        float threshold = isWalking
            ? owner.Profile.odinOptimalSpacing - StalkStopBuffer
            : owner.Profile.odinOptimalSpacing;
        return dist > threshold || dist < owner.Profile.odinCloseRange;
    }
}

// ---------------------------------------------------------------
//  P2 Triple Projectile — Fires 3 curving projectiles with spread.
//  Deals 3 hearts. Windup → Active (spawn 3 projectiles) → Recovery.
// ---------------------------------------------------------------
public class OdinP2TripleProjectileState : EnemyState
{
    private enum Phase { Windup, Active, Recovery }

    private OdinBoss odin;
    private OdinP2Super p2;
    private Phase phase;
    private float timer;
    private bool projectilesSpawned;

    public OdinP2TripleProjectileState(OdinBoss odin, OdinP2Super p2) : base(odin)
    {
        this.odin = odin;
        this.p2 = p2;
    }

    public override void Enter()
    {
        Debug.Log("Odin: P2 Triple Projectile");
        phase = Phase.Windup;
        owner.StopHorizontal();
        owner.FacePlayer();
        projectilesSpawned = false;

        AttackDefinition atk = odin.GetAttackDef("TripleProjectile");
        timer = atk != null ? atk.windupDuration : 0.4f;

        if (owner.Anim != null) owner.Anim.SetTrigger("Odin_TripleProjectile");
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
                    AttackDefinition atk = odin.GetAttackDef("TripleProjectile");
                    timer = atk != null ? atk.activeDuration : 0.3f;

                    if (!projectilesSpawned)
                    {
                        projectilesSpawned = true;
                        SpawnTripleProjectiles();
                    }
                }
                break;

            case Phase.Active:
                if (timer <= 0f)
                {
                    phase = Phase.Recovery;
                    AttackDefinition atk = odin.GetAttackDef("TripleProjectile");
                    timer = atk != null ? atk.recoveryDuration : 0.6f;

                    owner.StartCooldown("TripleProjectile");
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

    private void SpawnTripleProjectiles()
    {
        EnemyProfile p = owner.Profile;
        int count = p.odinTripleProjectileCount;
        float spreadAngle = p.odinTripleProjectileSpreadAngle;
        float speed = p.odinProjectileSpeed;
        int facing = owner.FacingDirection;

        Vector2 targetPos = owner.Ctx.playerTransform != null
            ? (Vector2)owner.Ctx.playerTransform.position
            : owner.Ctx.lastSeenPlayerPos;

        // Center direction: toward player with slight upward arc
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

            // Each projectile targets slightly different positions for spread
            Vector2 offsetTarget = targetPos + Random.insideUnitCircle * 0.5f;

            odin.SpawnOdinProjectile(
                fireDir * speed,
                offsetTarget,
                3, // 3 hearts damage
                p.odinProjectileCurveDelay,
                p.odinProjectileCurveStrength,
                p.odinProjectileLifetime);
        }
    }
}

// ---------------------------------------------------------------
//  P2 Consecutive Spikes — Repeated spike waves from the ground.
//  Windup → Active (spawn wave after wave) → Recovery.
// ---------------------------------------------------------------
public class OdinP2ConsecutiveSpikesState : EnemyState
{
    private enum Phase { Windup, Active, Recovery }

    private OdinBoss odin;
    private OdinP2Super p2;
    private Phase phase;
    private float timer;
    private int wavesSpawned;
    private float waveDelayTimer;
    private int spikesInCurrentWave;
    private float spikeDelayTimer;

    public OdinP2ConsecutiveSpikesState(OdinBoss odin, OdinP2Super p2) : base(odin)
    {
        this.odin = odin;
        this.p2 = p2;
    }

    public override void Enter()
    {
        Debug.Log("Odin: P2 Consecutive Spikes");
        phase = Phase.Windup;
        owner.StopHorizontal();
        owner.FacePlayer();
        wavesSpawned = 0;
        waveDelayTimer = 0f;
        spikesInCurrentWave = 0;
        spikeDelayTimer = 0f;

        AttackDefinition atk = odin.GetAttackDef("ConsecutiveSpikes");
        timer = atk != null ? atk.windupDuration : 0.5f;

        if (owner.Anim != null) owner.Anim.SetTrigger("Odin_ConsecutiveSpikes");
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
                    AttackDefinition atk = odin.GetAttackDef("ConsecutiveSpikes");
                    timer = atk != null ? atk.activeDuration : 2.0f;
                    waveDelayTimer = 0f;
                    StartNewWave();
                }
                break;

            case Phase.Active:
                ProcessWaveSpawning();

                if (timer <= 0f)
                {
                    phase = Phase.Recovery;
                    AttackDefinition atk = odin.GetAttackDef("ConsecutiveSpikes");
                    timer = atk != null ? atk.recoveryDuration : 0.6f;

                    owner.StartCooldown("ConsecutiveSpikes");
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

    private void ProcessWaveSpawning()
    {
        EnemyProfile p = owner.Profile;
        float dt = Time.fixedDeltaTime;

        // Spawn spikes within current wave
        spikeDelayTimer -= dt;
        if (spikeDelayTimer <= 0f && spikesInCurrentWave < p.odinSpikeCount)
        {
            SpawnSpikeForWave(wavesSpawned - 1, spikesInCurrentWave);
            spikesInCurrentWave++;
            spikeDelayTimer = p.odinSpikeDelay;
        }

        // Check if we need to start the next wave
        if (spikesInCurrentWave >= p.odinSpikeCount)
        {
            waveDelayTimer -= dt;
            if (waveDelayTimer <= 0f && wavesSpawned < p.odinConsecutiveSpikeWaves)
            {
                StartNewWave();
            }
        }
    }

    private void StartNewWave()
    {
        wavesSpawned++;
        spikesInCurrentWave = 0;
        spikeDelayTimer = 0f;
        waveDelayTimer = owner.Profile.odinConsecutiveSpikeWaveDelay;

        // Re-face player for each wave so spikes track where the player moved
        owner.FacePlayer();
    }

    private void SpawnSpikeForWave(int waveIndex, int spikeIndex)
    {
        EnemyProfile p = owner.Profile;
        int facing = owner.FacingDirection;

        // Each wave starts slightly further out to create a chasing pattern
        float waveOffset = waveIndex * p.odinSpikeSpacing * 2f;
        float spikeOffset = (spikeIndex * 2 + 1) * p.odinSpikeSpacing * 0.5f + waveOffset;

        Vector2 spawnPos = (Vector2)owner.transform.position + new Vector2(facing * spikeOffset, 0f);

        // Raycast to find ground
        RaycastHit2D groundHit = Physics2D.Raycast(spawnPos + Vector2.up * 2f, Vector2.down, 6f, owner.GroundLayer);
        if (groundHit.collider != null)
        {
            spawnPos = groundHit.point;
        }

        odin.SpawnGroundSpike(spawnPos, p.odinSpikeActiveDuration);
    }
}

// ---------------------------------------------------------------
//  P2 Large Slash — Heavy horizontal slash wave the player jumps over.
//  Windup → Active (spawn slash projectile + melee hitbox) → Recovery.
// ---------------------------------------------------------------
public class OdinP2LargeSlashState : EnemyState
{
    private enum Phase { Windup, Active, Recovery }

    private OdinBoss odin;
    private OdinP2Super p2;
    private Phase phase;
    private float timer;
    private bool slashSpawned;

    public OdinP2LargeSlashState(OdinBoss odin, OdinP2Super p2) : base(odin)
    {
        this.odin = odin;
        this.p2 = p2;
    }

    public override void Enter()
    {
        Debug.Log("Odin: P2 Large Slash");
        phase = Phase.Windup;
        owner.StopHorizontal();
        owner.FacePlayer();
        slashSpawned = false;

        AttackDefinition atk = odin.GetAttackDef("LargeSlash");
        timer = atk != null ? atk.windupDuration : 0.5f;

        if (owner.Anim != null) owner.Anim.SetTrigger("Odin_LargeSlash");
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
                    AttackDefinition atk = odin.GetAttackDef("LargeSlash");
                    timer = atk != null ? atk.activeDuration : 0.3f;

                    // Activate melee hitbox at source
                    if (odin.StaffMeleeHitbox != null) odin.StaffMeleeHitbox.Activate();

                    // Spawn slash wave projectile
                    if (!slashSpawned)
                    {
                        slashSpawned = true;
                        SpawnSlashWave();
                    }
                }
                break;

            case Phase.Active:
                if (timer <= 0f)
                {
                    if (odin.StaffMeleeHitbox != null) odin.StaffMeleeHitbox.Deactivate();

                    phase = Phase.Recovery;
                    AttackDefinition atk = odin.GetAttackDef("LargeSlash");
                    timer = atk != null ? atk.recoveryDuration : 0.8f;

                    owner.StartCooldown("LargeSlash");
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
        if (odin.StaffMeleeHitbox != null) odin.StaffMeleeHitbox.Deactivate();
    }

    private void SpawnSlashWave()
    {
        EnemyProfile p = owner.Profile;
        int facing = owner.FacingDirection;

        Vector2 spawnPos = (Vector2)owner.transform.position
            + new Vector2(facing * 1f, p.odinSlashProjectileHeight);
        Vector2 velocity = new Vector2(facing * p.odinSlashProjectileSpeed, 0f);

        odin.SpawnSlashProjectile(spawnPos, velocity);
    }
}
