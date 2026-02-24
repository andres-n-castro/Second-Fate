using UnityEngine;

// ---------------------------------------------------------------
//  PhaseTransitionState
//  Invincibility frames + optional animation during phase shift.
//  After timer expires, transitions to the provided NextPhaseState.
// ---------------------------------------------------------------
public class PhaseTransitionState : EnemyState
{
    public IState NextPhaseState { get; set; }
    private float timer;

    public PhaseTransitionState(EnemyBase owner) : base(owner) { }

    public override void Enter()
    {
        owner.StopAll();
        timer = owner.Profile.phaseTransitionDuration;

        if (owner.Anim != null) owner.Anim.SetTrigger("PhaseTransition");
    }

    public override void FixedTick()
    {
        timer -= Time.fixedDeltaTime;

        if (timer <= 0f && NextPhaseState != null)
        {
            owner.FSM.ChangeState(NextPhaseState);
        }
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

        if (owner.Anim != null) owner.Anim.SetTrigger("Die");

        foreach (Collider2D col in owner.GetComponents<Collider2D>())
        {
            col.enabled = false;
        }

        Object.Destroy(owner.gameObject, 2f);
    }
}
