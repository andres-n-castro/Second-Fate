using UnityEngine;

// ---------------------------------------------------------------
//  RockGolemPatrolState
//  Walk at moveSpeed. Ledge/wall avoidance ON. Stuck detection.
//  Idle pause on turn. Transitions to Combat when player enters
//  aggro range (no same-platform requirement — ranged enemy).
// ---------------------------------------------------------------
public class RockGolemPatrolState : EnemyState
{
    private RockGolem golem;
    private float stuckTimer;
    private bool isIdle;
    private float idleTimer;

    private const float IdleDuration = 0.2f;
    private const float StuckThreshold = 0.3f;

    public RockGolemPatrolState(RockGolem golem) : base(golem)
    {
        this.golem = golem;
    }

    public override void Enter()
    {
        stuckTimer = 0f;
        isIdle = false;
    }

    public override void FixedTick()
    {
        // Check for player detection — no same-platform check for ranged enemy
        if (owner.Ctx.isPlayerInAggroRange)
        {
            owner.FSM.ChangeState(golem.CombatSuper); // cross: NonCombat → Combat
            return;
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
        if (owner.Anim != null) owner.Anim.SetBool(owner.AnimWalking, true);
    }

    public override void Exit()
    {
        if (owner.Anim != null) owner.Anim.SetBool(owner.AnimWalking, false);
    }

    private void StartIdle()
    {
        isIdle = true;
        idleTimer = IdleDuration;
        owner.FlipFacing();
        owner.StopHorizontal();
        if (owner.Anim != null) owner.Anim.SetBool(owner.AnimWalking, false);
    }
}

// ---------------------------------------------------------------
//  RockGolemCombatIdleState
//  Face player and wait for attack cooldown. Transitions to
//  ThrowState when RockThrow is ready and player is in attack range.
//  Transitions to GiveUp if player leaves deaggro range or LOS lost.
// ---------------------------------------------------------------
public class RockGolemCombatIdleState : EnemyState
{
    private RockGolem golem;

    public RockGolemCombatIdleState(RockGolem golem) : base(golem)
    {
        this.golem = golem;
    }

    public override void Enter()
    {
        owner.StopHorizontal();
    }

    public override void FixedTick()
    {
        // Player dead or null → NonCombat
        if (owner.Ctx.playerTransform == null)
        {
            owner.FSM.ChangeState(golem.NonCombatSuper); // cross: Combat → NonCombat
            return;
        }

        // Lose conditions — player left deaggro range or LOS lost
        if (!owner.Ctx.isPlayerInDeaggroRange || !owner.Ctx.hasLineOfSightToPlayer)
        {
            golem.CombatSuper.ForceSubState(golem.GiveUpState); // within Combat
            return;
        }

        // Always face the player
        owner.FacePlayer();
        owner.StopHorizontal();
        if (owner.Anim != null) owner.Anim.SetBool(owner.AnimWalking, false);

        // Attack trigger — in attack range and cooldown ready
        if (owner.Ctx.isPlayerInAttackRange
            && owner.Ctx.hasLineOfSightToPlayer
            && owner.IsAttackReady("RockThrow"))
        {
            golem.CombatSuper.ForceSubState(golem.ThrowState); // within Combat
        }
    }
}

// ---------------------------------------------------------------
//  RockGolemThrowState
//  Timer-based ranged attack: Windup → Spawn projectile → Recovery.
//  Enemy stops and faces player for the full duration.
//  After recovery, returns to CombatIdle if still aggro, GiveUp otherwise.
// ---------------------------------------------------------------
public class RockGolemThrowState : EnemyState
{
    private enum Phase { Windup, Recovery }

    private RockGolem golem;
    private Phase phase;
    private float timer;

    public RockGolemThrowState(RockGolem golem) : base(golem)
    {
        this.golem = golem;
    }

    public override void Enter()
    {
        phase = Phase.Windup;
        owner.StopHorizontal();
        owner.FacePlayer();

        AttackDefinition atk = GetThrowAttack();
        timer = atk != null ? atk.windupDuration : 0.5f;

        if (owner.Anim != null) owner.Anim.SetTrigger(owner.AnimAttack);
    }

    public override void FixedTick()
    {
        timer -= Time.fixedDeltaTime;
        owner.StopHorizontal(); // Hold position during attack

        switch (phase)
        {
            case Phase.Windup:
                if (timer <= 0f)
                {
                    SpawnRock();
                    owner.StartCooldown("RockThrow");

                    phase = Phase.Recovery;
                    AttackDefinition atk = GetThrowAttack();
                    timer = atk != null ? atk.recoveryDuration : 0.5f;
                }
                break;

            case Phase.Recovery:
                if (timer <= 0f)
                    TransitionPostAttack();
                break;
        }
    }

    private void SpawnRock()
    {
        if (golem.RockPrefab == null) return;

        Vector2 spawnPos = golem.RockSpawnPoint != null
            ? (Vector2)golem.RockSpawnPoint.position
            : (Vector2)owner.transform.position + new Vector2(owner.FacingDirection * 0.5f, 0.5f);

        Vector2 targetPos = owner.Ctx.playerTransform != null
            ? (Vector2)owner.Ctx.playerTransform.position
            : spawnPos + Vector2.right * owner.FacingDirection * 5f;

        GameObject rock = Object.Instantiate(golem.RockPrefab, spawnPos, Quaternion.identity);

        Vector2 throwVel = golem.CalculateThrowVelocity(targetPos);

        RockProjectile proj = rock.GetComponent<RockProjectile>();
        if (proj != null)
        {
            proj.Initialize(throwVel, owner.GetComponents<Collider2D>());
        }
        else
        {
            Rigidbody2D rockRb = rock.GetComponent<Rigidbody2D>();
            if (rockRb != null) rockRb.linearVelocity = throwVel;
        }
    }

    private void TransitionPostAttack()
    {
        if (owner.Ctx.isPlayerInAggroRange && owner.Ctx.hasLineOfSightToPlayer)
            golem.CombatSuper.ForceSubState(golem.CombatIdleState);  // within Combat
        else if (owner.Ctx.isPlayerInDeaggroRange)
            golem.CombatSuper.ForceSubState(golem.GiveUpState);      // within Combat
        else
            owner.FSM.ChangeState(golem.NonCombatSuper);              // cross: Combat → NonCombat
    }

    private AttackDefinition GetThrowAttack()
    {
        if (owner.Profile.attacks == null) return null;
        for (int i = 0; i < owner.Profile.attacks.Length; i++)
        {
            if (owner.Profile.attacks[i].attackName == "RockThrow")
                return owner.Profile.attacks[i];
        }
        return null;
    }
}

// ---------------------------------------------------------------
//  RockGolemGiveUpState
//  Stop movement. Face last seen player direction. Pause for
//  giveUpPauseDuration. When timer expires → Patrol.
// ---------------------------------------------------------------
public class RockGolemGiveUpState : EnemyState
{
    private RockGolem golem;
    private float timer;

    public RockGolemGiveUpState(RockGolem golem) : base(golem)
    {
        this.golem = golem;
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

        if (owner.Anim != null) owner.Anim.SetBool(owner.AnimWalking, false);
    }

    public override void FixedTick()
    {
        timer -= Time.fixedDeltaTime;
        if (timer <= 0f)
        {
            owner.FSM.ChangeState(golem.NonCombatSuper); // cross: Combat → NonCombat
        }
    }
}
