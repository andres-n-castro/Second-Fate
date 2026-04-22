using UnityEngine;

// ---------------------------------------------------------------
//  BossIntroState
//  Holds the boss still for a tunable duration before the fight begins.
//  After the timer expires, transitions to the provided NextState.
// ---------------------------------------------------------------
public class BossIntroState : EnemyState
{
    public IState NextState { get; set; }
    private float timer;
    private bool activated;

    public BossIntroState(EnemyBase owner) : base(owner) { }

    public override void Enter()
    {
        owner.StopAll();
        timer = owner.Profile.bossIntroDuration;
        activated = false;
    }

    public override void FixedTick()
    {
        // Wait until the player is close enough to truly aggro the boss.
        if (!activated)
        {
            if (owner.Ctx.isPlayerInAggroRange)
            {
                activated = true;
                owner.BeginBossEncounter();
            }
            else
                return;
        }

        timer -= Time.fixedDeltaTime;
        if (timer <= 0f && NextState != null)
        {
            owner.FSM.ChangeState(NextState);
        }
    }
}

// ---------------------------------------------------------------
//  PhaseTransitionState
//  Invincibility frames + optional animation during phase shift.
//  After timer expires, transitions to the provided NextPhaseState.
// ---------------------------------------------------------------
public class PhaseTransitionState : EnemyState
{
    public IState NextPhaseState { get; set; }
    private float timer;
    private string animTrigger;

    public PhaseTransitionState(EnemyBase owner, string animTrigger) : base(owner)
    {
        this.animTrigger = animTrigger;
    }

    public override void Enter()
    {
        owner.StopAll();
        if (owner.Health != null) owner.Health.isInvulnerable = true;
        timer = owner.Profile.phaseTransitionDuration;

        if (owner.Anim != null) owner.Anim.SetTrigger(animTrigger);
    }

    public override void FixedTick()
    {
        timer -= Time.fixedDeltaTime;

        if (timer <= 0f && NextPhaseState != null)
        {
            owner.FSM.ChangeState(NextPhaseState);
        }
    }

    public override void Exit()
    {
        if (owner.Health != null) owner.Health.isInvulnerable = false;
    }
}

// ---------------------------------------------------------------
//  BossDeadState
//  Stops all movement, disables hitboxes via callback, plays death
//  animation, disables colliders, destroys after delay.
//  Works for both grounded and airborne bosses.
// ---------------------------------------------------------------
public class BossDeadState : EnemyState
{
    private System.Action disableHitboxes;
    private bool enableGravityOnDeath;

    public BossDeadState(EnemyBase owner, System.Action disableHitboxes, bool enableGravityOnDeath = false)
        : base(owner)
    {
        this.disableHitboxes = disableHitboxes;
        this.enableGravityOnDeath = enableGravityOnDeath;
    }

    public override void Enter()
    {
        owner.Ctx.isDead = true;
        owner.RestoreDrag();
        owner.StopAll();

        disableHitboxes?.Invoke();

        if (enableGravityOnDeath)
            owner.Rb.gravityScale = 1f;

        if (owner.Anim != null)
        {
            // Clear any pending hitstun trigger from the same-frame damage event
            // so the AnyState→Hitstun transition can't steal us out of the death state.
            owner.Anim.ResetTrigger(owner.AnimHitstun);
            owner.Anim.SetTrigger(owner.AnimDeath);
        }

        foreach (Collider2D col in owner.GetComponents<Collider2D>())
        {
            if (col.isTrigger)
                col.enabled = false;
        }

        Object.Destroy(owner.gameObject, 1.5f);
    }
}
