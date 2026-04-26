using UnityEngine;

/// <summary>
/// Ghost — Flying enemy that behaves identically to FallenWarriorSpirit,
/// except it does not fall when killed (stays airborne during death anim).
/// </summary>
public class Ghost : FallenWarriorSpirit
{
    public override string AnimWalking => "Ghost_Flying";
    public override string AnimAttack => "Ghost_Attack";
    public override string AnimHitstun => "Ghost_Takes_Damage";
    public override string AnimDeath => "Ghost_Dies";

    private GhostDeadState ghostDeadState;

    protected override void Start()
    {
        base.Start();
        ghostDeadState = new GhostDeadState(this);
    }

    protected override void HandleDeath()
    {
        if (DashHitbox != null) DashHitbox.Deactivate();
        FacePlayer();
        FSM.ChangeState(ghostDeadState);
    }
}

// Airborne death state that leaves gravityScale untouched so the Ghost
// plays its death animation in place instead of falling.
public class GhostDeadState : EnemyState
{
    public GhostDeadState(EnemyBase owner) : base(owner) { }

    public override void Enter()
    {
        owner.Ctx.isDead = true;
        owner.RestoreDrag();
        owner.StopAll();
        owner.SetLayerRecursively(0);

        if (owner.Anim != null)
        {
            owner.Anim.ResetTrigger(owner.AnimHitstun);
            owner.Anim.SetTrigger(owner.AnimDeath);
        }

        Object.Destroy(owner.gameObject, 1.5f);
    }
}
