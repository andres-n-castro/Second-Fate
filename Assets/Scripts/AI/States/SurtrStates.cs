using UnityEngine;
using System.Collections.Generic;

// =================================================================
//  PHASE 1 SUPER STATE
//  Hierarchical state that owns a sub-FSM for grounded combat.
//  Sub-states: P1Approach, P1Decision, P1LavaSweep, P1HeavyThrust, P1FireBreath.
// =================================================================
public class SurtrP1Super : HierarchicalState
{
    private SurtrBoss surtr;

    // Phase 1 substates
    public SurtrP1ApproachState ApproachState { get; private set; }
    public SurtrP1DecisionState DecisionState { get; private set; }
    public SurtrP1LavaSweepState LavaSweepState { get; private set; }
    public SurtrP1HeavyThrustState HeavyThrustState { get; private set; }
    public SurtrP1FireBreathState FireBreathState { get; private set; }

    public SurtrP1Super(SurtrBoss surtr) : base(surtr)
    {
        this.surtr = surtr;

        ApproachState = new SurtrP1ApproachState(surtr, this);
        DecisionState = new SurtrP1DecisionState(surtr, this);
        LavaSweepState = new SurtrP1LavaSweepState(surtr, this);
        HeavyThrustState = new SurtrP1HeavyThrustState(surtr, this);
        FireBreathState = new SurtrP1FireBreathState(surtr, this);
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
//  Hierarchical state that owns a Phase 2 sub-FSM for eruptive combat.
//  A Behavior Tree selects the next attack, but ONLY when the
//  sub-FSM is in P2Idle. The BT sets an intent; P2Idle consumes it.
//  Passive lava eruption periodically spawns projectiles.
// =================================================================
public class SurtrP2Super : HierarchicalState
{
    private SurtrBoss surtr;
    private BTNode attackSelectorBT;

    // Phase 2 substates
    public SurtrP2IdleState IdleState { get; private set; }
    public SurtrP2GroundedThrustState GroundedThrustState { get; private set; }
    public SurtrP2LavaVomitState LavaVomitState { get; private set; }

    // BT intent — set by BT, consumed by P2Idle
    public IState RequestedP2Attack { get; set; }

    // Passive eruption timer
    private float eruptionTimer;

    public SurtrP2Super(SurtrBoss surtr) : base(surtr)
    {
        this.surtr = surtr;

        IdleState = new SurtrP2IdleState(surtr, this);
        GroundedThrustState = new SurtrP2GroundedThrustState(surtr, this);
        LavaVomitState = new SurtrP2LavaVomitState(surtr, this);

        BuildBehaviorTree();
    }

    public override void Enter()
    {
        Debug.Log("Surtr: Entering Phase 2");
        RequestedP2Attack = null;
        eruptionTimer = owner.Profile.surtrEruptionInterval;
        subMachine.ChangeState(IdleState);
    }

    public override void Tick()
    {
        // BT ticks every frame but can only request attacks when in Idle
        if (subMachine.CurrentState == IdleState)
        {
            attackSelectorBT.Tick(owner.Ctx);
        }

        // Passive eruption: periodically spawn lava projectiles
        eruptionTimer -= Time.deltaTime;
        if (eruptionTimer <= 0f)
        {
            eruptionTimer = owner.Profile.surtrEruptionInterval;
            SpawnEruptionProjectiles();
        }

        base.Tick();
    }

    public void ChangeSubState(IState state)
    {
        subMachine.ChangeState(state);
    }

    private void SpawnEruptionProjectiles()
    {
        EnemyProfile p = owner.Profile;
        int count = p.surtrEruptionProjectileCount;
        float spreadAngle = p.surtrEruptionSpreadAngle;
        float speed = p.surtrEruptionProjectileSpeed;

        // Spawn projectiles in a fan upward from the boss
        float startAngle = 90f - spreadAngle * 0.5f; // center around straight up (90 degrees)

        for (int i = 0; i < count; i++)
        {
            float angle;
            if (count > 1)
                angle = startAngle + spreadAngle * ((float)i / (count - 1));
            else
                angle = 90f;

            float rad = angle * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            // Add small random variation
            dir += Random.insideUnitCircle * 0.15f;
            dir.Normalize();

            surtr.SpawnLavaProjectile(dir * speed);
        }
    }

    // ----- Behavior Tree Construction -----
    // The BT checks cooldowns, positional context, and weights, then sets RequestedP2Attack.
    // It does NOT move rigidbodies or toggle hitboxes.
    //
    // Phase 2 uses reduced cooldowns (surtrP2MinAttackCooldown/surtrP2MaxAttackCooldown)
    // to make Surtr more aggressive.
    //
    // Attack selection:
    //   GroundedThrust: high commitment, favored at close range, creates punish window.
    //   LavaVomit: area control, favored at mid range.
    private void BuildBehaviorTree()
    {
        attackSelectorBT = new BTAction(ctx =>
        {
            if (RequestedP2Attack != null) return BTStatus.Failure; // already pending

            float dist = owner.Ctx.playerDistance;
            EnemyProfile p = owner.Profile;

            List<(IState state, float weight)> candidates = new List<(IState, float)>();

            // --- Grounded Thrust: close range, in front ---
            AttackDefinition thrustDef = surtr.GetAttackDef("GroundedThrust");
            if (thrustDef != null && owner.IsAttackReady("GroundedThrust"))
            {
                float w = thrustDef.selectionWeight;
                if (dist <= p.surtrGroundedThrustRange)
                    w *= p.surtrGroundedThrustWeightMultiplier;
                candidates.Add((GroundedThrustState, w));
            }

            // --- Lava Vomit: mid range, area control ---
            AttackDefinition vomitDef = surtr.GetAttackDef("LavaVomit");
            if (vomitDef != null && owner.IsAttackReady("LavaVomit"))
            {
                float w = vomitDef.selectionWeight;
                if (dist >= p.surtrVomitMinRange && dist <= p.surtrVomitMaxRange)
                    w *= p.surtrVomitWeightMultiplier;
                candidates.Add((LavaVomitState, w));
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
//  P1 Approach — Walk toward player until in engage range.
//  Surtr moves slowly and deliberately.
// ---------------------------------------------------------------
public class SurtrP1ApproachState : EnemyState
{
    private SurtrBoss surtr;
    private SurtrP1Super p1;

    public SurtrP1ApproachState(SurtrBoss surtr, SurtrP1Super p1) : base(surtr)
    {
        this.surtr = surtr;
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
            return;
        }

        owner.MoveGround(owner.Profile.approachSpeed);

        // Within max engage range and LOS — go to decision
        if (owner.Ctx.playerDistance <= owner.Profile.surtrMaxEngageRange
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
//  Surtr stalks forward slowly while deciding.
// ---------------------------------------------------------------
public class SurtrP1DecisionState : EnemyState
{
    private SurtrBoss surtr;
    private SurtrP1Super p1;
    private float pauseTimer;
    private bool isStalking;

    public SurtrP1DecisionState(SurtrBoss surtr, SurtrP1Super p1) : base(surtr)
    {
        this.surtr = surtr;
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
            bool shouldStalk = owner.Ctx.playerDistance > owner.Profile.surtrCloseRange
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
        if (dist > p.surtrMaxEngageRange || !owner.Ctx.hasLineOfSightToPlayer)
        {
            p1.ChangeSubState(p1.ApproachState);
            return;
        }

        // Build candidate list
        List<(IState state, float weight)> candidates = new List<(IState, float)>();

        // --- Lava Sweep: mid range, same platform preferred ---
        AttackDefinition sweepDef = surtr.GetAttackDef("LavaSweep");
        if (sweepDef != null && owner.IsAttackReady("LavaSweep"))
        {
            float sweepEffective = surtr.LavaSweepReach + 0.3f;
            // Lava sweep hits at range via projectile, so effective range is larger
            bool inSweepRange = dist >= p.surtrSweepMinRange && dist <= p.surtrSweepMaxRange;
            if (inSweepRange || dist <= sweepEffective)
            {
                float w = sweepDef.selectionWeight;
                if (inSweepRange && owner.Ctx.isPlayerOnSamePlatform)
                    w *= p.surtrSweepWeightMultiplier;
                candidates.Add((p1.LavaSweepState, w));
            }
        }

        // --- Heavy Thrust: close-to-mid range, directly in front ---
        AttackDefinition thrustDef = surtr.GetAttackDef("HeavyThrust");
        if (thrustDef != null && owner.IsAttackReady("HeavyThrust"))
        {
            float thrustTravel = thrustDef.dashSpeed * thrustDef.activeDuration;
            float thrustEffective = surtr.HeavyThrustReach + 0.3f + thrustTravel;
            if (dist <= thrustEffective && dist <= p.surtrThrustMaxRange)
            {
                float w = thrustDef.selectionWeight;
                w *= p.surtrThrustWeightMultiplier;
                candidates.Add((p1.HeavyThrustState, w));
            }
        }

        // --- Fire Breath: close-to-mid range, in front ---
        AttackDefinition breathDef = surtr.GetAttackDef("FireBreath");
        if (breathDef != null && owner.IsAttackReady("FireBreath"))
        {
            float breathEffective = surtr.FireBreathReach + 0.3f;
            if (dist <= p.surtrFireBreathMaxRange || dist <= breathEffective)
            {
                float w = breathDef.selectionWeight;
                if (dist <= p.surtrFireBreathMaxRange)
                    w *= p.surtrFireBreathWeightMultiplier;
                candidates.Add((p1.FireBreathState, w));
            }
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
//  P1 Lava Sweep — Wide sword swing that sends lava across the floor.
//  Windup → Active (hitbox + spawn ground projectile) → Recovery.
// ---------------------------------------------------------------
public class SurtrP1LavaSweepState : EnemyState
{
    private enum Phase { Windup, Active, Recovery }

    private SurtrBoss surtr;
    private SurtrP1Super p1;
    private Phase phase;
    private float timer;
    private bool projectileSpawned;

    public SurtrP1LavaSweepState(SurtrBoss surtr, SurtrP1Super p1) : base(surtr)
    {
        this.surtr = surtr;
        this.p1 = p1;
    }

    public override void Enter()
    {
        Debug.Log("Surtr: P1 Lava Sweep");
        phase = Phase.Windup;
        owner.StopHorizontal();
        owner.FacePlayer();
        projectileSpawned = false;

        AttackDefinition atk = surtr.GetAttackDef("LavaSweep");
        timer = atk != null ? atk.windupDuration : 0.5f;

        if (owner.Anim != null) owner.Anim.SetTrigger("Surtr_LavaSweep");
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
                    AttackDefinition atk = surtr.GetAttackDef("LavaSweep");
                    timer = atk != null ? atk.activeDuration : 0.3f;

                    if (surtr.LavaSweepHitbox != null) surtr.LavaSweepHitbox.Activate();

                    // Spawn ground-traveling lava projectile
                    if (!projectileSpawned)
                    {
                        projectileSpawned = true;
                        Vector2 velocity = new Vector2(
                            owner.FacingDirection * owner.Profile.surtrSweepProjectileSpeed, 0f);
                        surtr.SpawnLavaProjectile(velocity);
                    }
                }
                break;

            case Phase.Active:
                if (timer <= 0f)
                {
                    if (surtr.LavaSweepHitbox != null) surtr.LavaSweepHitbox.Deactivate();

                    phase = Phase.Recovery;
                    AttackDefinition atk = surtr.GetAttackDef("LavaSweep");
                    timer = atk != null ? atk.recoveryDuration : 0.6f;

                    owner.StartCooldown("LavaSweep");
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
        if (surtr.LavaSweepHitbox != null) surtr.LavaSweepHitbox.Deactivate();
    }

    private void ReturnToDecisionOrApproach()
    {
        if (owner.Ctx.playerDistance <= owner.Profile.surtrMaxEngageRange
            && owner.Ctx.hasLineOfSightToPlayer)
            p1.ChangeSubState(p1.DecisionState);
        else
            p1.ChangeSubState(p1.ApproachState);
    }
}

// ---------------------------------------------------------------
//  P1 Heavy Thrust — Forward sword thrust with lunge.
//  Windup → Active (lunge + hitbox) → Recovery.
// ---------------------------------------------------------------
public class SurtrP1HeavyThrustState : EnemyState
{
    private enum Phase { Windup, Active, Recovery }

    private SurtrBoss surtr;
    private SurtrP1Super p1;
    private Phase phase;
    private float timer;

    public SurtrP1HeavyThrustState(SurtrBoss surtr, SurtrP1Super p1) : base(surtr)
    {
        this.surtr = surtr;
        this.p1 = p1;
    }

    public override void Enter()
    {
        Debug.Log("Surtr: P1 Heavy Thrust");
        phase = Phase.Windup;
        owner.StopHorizontal();
        owner.FacePlayer();

        AttackDefinition atk = surtr.GetAttackDef("HeavyThrust");
        timer = atk != null ? atk.windupDuration : 0.5f;

        if (owner.Anim != null) owner.Anim.SetTrigger("Surtr_HeavyThrust");
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
                    AttackDefinition atk = surtr.GetAttackDef("HeavyThrust");
                    timer = atk != null ? atk.activeDuration : 0.4f;

                    // Lunge forward
                    float thrustSpeed = atk != null ? atk.dashSpeed : 8f;
                    owner.MoveGround(thrustSpeed);

                    if (surtr.HeavyThrustHitbox != null) surtr.HeavyThrustHitbox.Activate();
                }
                break;

            case Phase.Active:
                // Stop at ledge/wall during lunge
                if (owner.Ctx.nearLedgeAhead || owner.Ctx.nearWallAhead)
                    owner.StopHorizontal();

                if (timer <= 0f)
                {
                    owner.StopHorizontal();
                    if (surtr.HeavyThrustHitbox != null) surtr.HeavyThrustHitbox.Deactivate();

                    phase = Phase.Recovery;
                    AttackDefinition atk = surtr.GetAttackDef("HeavyThrust");
                    timer = atk != null ? atk.recoveryDuration : 0.6f;

                    owner.StartCooldown("HeavyThrust");
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
        if (surtr.HeavyThrustHitbox != null) surtr.HeavyThrustHitbox.Deactivate();
        owner.StopHorizontal();
    }

    private void ReturnToDecisionOrApproach()
    {
        if (owner.Ctx.playerDistance <= owner.Profile.surtrMaxEngageRange
            && owner.Ctx.hasLineOfSightToPlayer)
            p1.ChangeSubState(p1.DecisionState);
        else
            p1.ChangeSubState(p1.ApproachState);
    }
}

// ---------------------------------------------------------------
//  P1 Fire Breath — Sustained forward fire stream.
//  Windup → Active (multi-hit breath hitbox) → Recovery.
// ---------------------------------------------------------------
public class SurtrP1FireBreathState : EnemyState
{
    private enum Phase { Windup, Active, Recovery }

    private SurtrBoss surtr;
    private SurtrP1Super p1;
    private Phase phase;
    private float timer;
    private int hitsRemaining;
    private float hitIntervalTimer;

    public SurtrP1FireBreathState(SurtrBoss surtr, SurtrP1Super p1) : base(surtr)
    {
        this.surtr = surtr;
        this.p1 = p1;
    }

    public override void Enter()
    {
        Debug.Log("Surtr: P1 Fire Breath");
        phase = Phase.Windup;
        owner.StopHorizontal();
        owner.FacePlayer();

        AttackDefinition atk = surtr.GetAttackDef("FireBreath");
        timer = atk != null ? atk.windupDuration : 0.4f;

        if (owner.Anim != null) owner.Anim.SetTrigger("Surtr_FireBreath");
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
                    AttackDefinition atk = surtr.GetAttackDef("FireBreath");
                    timer = atk != null ? atk.activeDuration : 0.8f;
                    hitsRemaining = atk != null ? atk.hitCount : 4;
                    hitIntervalTimer = 0f;

                    if (surtr.FireBreathHitbox != null) surtr.FireBreathHitbox.Activate();
                }
                break;

            case Phase.Active:
                // Multi-hit: re-activate hitbox at intervals to allow repeated damage
                hitIntervalTimer -= Time.fixedDeltaTime;
                if (hitIntervalTimer <= 0f && hitsRemaining > 0)
                {
                    if (surtr.FireBreathHitbox != null)
                    {
                        surtr.FireBreathHitbox.Deactivate();
                        surtr.FireBreathHitbox.Activate();
                    }
                    hitsRemaining--;
                    AttackDefinition atk = surtr.GetAttackDef("FireBreath");
                    hitIntervalTimer = atk != null ? atk.hitInterval : 0.2f;
                }

                if (timer <= 0f)
                {
                    if (surtr.FireBreathHitbox != null) surtr.FireBreathHitbox.Deactivate();

                    phase = Phase.Recovery;
                    AttackDefinition atk = surtr.GetAttackDef("FireBreath");
                    timer = atk != null ? atk.recoveryDuration : 0.6f;

                    owner.StartCooldown("FireBreath");
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
        if (surtr.FireBreathHitbox != null) surtr.FireBreathHitbox.Deactivate();
    }

    private void ReturnToDecisionOrApproach()
    {
        if (owner.Ctx.playerDistance <= owner.Profile.surtrMaxEngageRange
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
//  P2 Idle — Grounded reposition between attacks.
//  Stalks toward player, consumes BT intent to transition to attacks.
//  Uses shorter decision delay than P1 to feel more aggressive.
// ---------------------------------------------------------------
public class SurtrP2IdleState : EnemyState
{
    private SurtrBoss surtr;
    private SurtrP2Super p2;
    private float decisionDelay;
    private bool isWalking;

    public SurtrP2IdleState(SurtrBoss surtr, SurtrP2Super p2) : base(surtr)
    {
        this.surtr = surtr;
        this.p2 = p2;
    }

    public override void Enter()
    {
        p2.RequestedP2Attack = null;
        isWalking = false;
        // Shorter delay than P1 — P2 Surtr is more aggressive
        decisionDelay = Random.Range(
            owner.Profile.surtrP2MinAttackCooldown,
            owner.Profile.surtrP2MaxAttackCooldown);
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

        // Stalk toward player while waiting
        bool shouldWalk = owner.Ctx.playerDistance > owner.Profile.surtrCloseRange
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
//  P2 Grounded Thrust — Powerful thrust that gets stuck in ground.
//  Windup → Active (lunge + hitbox) → Stuck (punish window) → Recovery.
// ---------------------------------------------------------------
public class SurtrP2GroundedThrustState : EnemyState
{
    private enum Phase { Windup, Active, Stuck, Recovery }

    private SurtrBoss surtr;
    private SurtrP2Super p2;
    private Phase phase;
    private float timer;

    public SurtrP2GroundedThrustState(SurtrBoss surtr, SurtrP2Super p2) : base(surtr)
    {
        this.surtr = surtr;
        this.p2 = p2;
    }

    public override void Enter()
    {
        Debug.Log("Surtr: P2 Grounded Thrust");
        phase = Phase.Windup;
        owner.StopHorizontal();
        owner.FacePlayer();

        AttackDefinition atk = surtr.GetAttackDef("GroundedThrust");
        timer = atk != null ? atk.windupDuration : 0.5f;

        if (owner.Anim != null) owner.Anim.SetTrigger("Surtr_GroundedThrust");
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
                    AttackDefinition atk = surtr.GetAttackDef("GroundedThrust");
                    timer = atk != null ? atk.activeDuration : 0.4f;

                    // Powerful lunge forward
                    float thrustSpeed = atk != null ? atk.dashSpeed : 12f;
                    owner.MoveGround(thrustSpeed);

                    if (surtr.GroundedThrustHitbox != null) surtr.GroundedThrustHitbox.Activate();
                }
                break;

            case Phase.Active:
                // Stop at ledge/wall during lunge
                if (owner.Ctx.nearLedgeAhead || owner.Ctx.nearWallAhead)
                    owner.StopHorizontal();

                if (timer <= 0f)
                {
                    owner.StopHorizontal();
                    if (surtr.GroundedThrustHitbox != null) surtr.GroundedThrustHitbox.Deactivate();

                    // Sword gets stuck in ground — vulnerable punish window
                    phase = Phase.Stuck;
                    timer = owner.Profile.surtrGroundedThrustStuckDuration;

                    // TODO: Play "Surtr_Stuck" animation if available
                    if (owner.Anim != null) owner.Anim.SetTrigger("Surtr_ThrustStuck");
                }
                break;

            case Phase.Stuck:
                // Boss is stuck and vulnerable — intentional punish window
                if (timer <= 0f)
                {
                    phase = Phase.Recovery;
                    AttackDefinition atk = surtr.GetAttackDef("GroundedThrust");
                    timer = atk != null ? atk.recoveryDuration : 0.8f;

                    // TODO: Play "Surtr_PullFree" animation if available
                    if (owner.Anim != null) owner.Anim.SetTrigger("Surtr_ThrustRecover");

                    owner.StartCooldown("GroundedThrust");
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
        if (surtr.GroundedThrustHitbox != null) surtr.GroundedThrustHitbox.Deactivate();
        owner.StopHorizontal();
    }
}

// ---------------------------------------------------------------
//  P2 Lava Vomit — Surtr lurches forward and spews lava projectiles.
//  Windup → Active (spawn projectiles in arc + hitbox) → Recovery.
//  Projectiles arc forward and leave lava hazards on impact (if configured on prefab).
// ---------------------------------------------------------------
public class SurtrP2LavaVomitState : EnemyState
{
    private enum Phase { Windup, Active, Recovery }

    private SurtrBoss surtr;
    private SurtrP2Super p2;
    private Phase phase;
    private float timer;
    private bool projectilesSpawned;

    public SurtrP2LavaVomitState(SurtrBoss surtr, SurtrP2Super p2) : base(surtr)
    {
        this.surtr = surtr;
        this.p2 = p2;
    }

    public override void Enter()
    {
        Debug.Log("Surtr: P2 Lava Vomit");
        phase = Phase.Windup;
        owner.StopHorizontal();
        owner.FacePlayer();
        projectilesSpawned = false;

        AttackDefinition atk = surtr.GetAttackDef("LavaVomit");
        timer = atk != null ? atk.windupDuration : 0.6f;

        if (owner.Anim != null) owner.Anim.SetTrigger("Surtr_LavaVomit");
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
                    AttackDefinition atk = surtr.GetAttackDef("LavaVomit");
                    timer = atk != null ? atk.activeDuration : 0.5f;

                    if (surtr.LavaVomitHitbox != null) surtr.LavaVomitHitbox.Activate();

                    // Spawn lava projectiles in a forward arc
                    if (!projectilesSpawned)
                    {
                        projectilesSpawned = true;
                        SpawnVomitProjectiles();
                    }
                }
                break;

            case Phase.Active:
                if (timer <= 0f)
                {
                    if (surtr.LavaVomitHitbox != null) surtr.LavaVomitHitbox.Deactivate();

                    phase = Phase.Recovery;
                    AttackDefinition atk = surtr.GetAttackDef("LavaVomit");
                    timer = atk != null ? atk.recoveryDuration : 0.8f;

                    owner.StartCooldown("LavaVomit");
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
        if (surtr.LavaVomitHitbox != null) surtr.LavaVomitHitbox.Deactivate();
    }

    private void SpawnVomitProjectiles()
    {
        EnemyProfile p = owner.Profile;
        int count = p.surtrVomitProjectileCount;
        float spreadAngle = p.surtrVomitSpreadAngle;
        float speed = p.surtrVomitProjectileSpeed;
        float facing = owner.FacingDirection;

        // Forward arc: center around 45 degrees in the facing direction (lobbed forward)
        float centerAngle = facing > 0 ? 45f : 135f;
        float startAngle = centerAngle - spreadAngle * 0.5f;

        for (int i = 0; i < count; i++)
        {
            float angle;
            if (count > 1)
                angle = startAngle + spreadAngle * ((float)i / (count - 1));
            else
                angle = centerAngle;

            float rad = angle * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            // Slight random spread for visual variety
            dir += Random.insideUnitCircle * 0.1f;
            dir.Normalize();

            // Vary speed slightly per projectile
            float projSpeed = speed * Random.Range(0.85f, 1.15f);
            surtr.SpawnLavaProjectile(dir * projSpeed);
        }
    }
}
