using UnityEngine;

// ---------------------------------------------------------------
//  GroundHitstunState
//  Knockback plays out with high drag. Wall check prevents clipping.
//  Ledge/wall avoidance is DISABLED — enemy can be pushed off platforms.
//  After timer expires, returns to ReturnState.
// ---------------------------------------------------------------
public class GroundHitstunState : EnemyState
{
    public IState ReturnState { get; set; }
    private float timer;

    public GroundHitstunState(EnemyBase owner) : base(owner) { }

    public override void Enter()
    {
        owner.Ctx.isHitstunned = true;
        timer = owner.Profile.hitstunDuration;

        if (owner.Anim != null) owner.Anim.SetBool("Walking", false);
    }

    public override void FixedTick()
    {
        timer -= Time.fixedDeltaTime;

        // Wall check: stop horizontal velocity to prevent clipping into walls
        if (owner.Rb.linearVelocity.x != 0f)
        {
            float moveDir = Mathf.Sign(owner.Rb.linearVelocity.x);
            RaycastHit2D wallHit = Physics2D.Raycast(
                owner.transform.position,
                Vector2.right * moveDir,
                owner.Profile.wallCheckDistance,
                owner.GroundLayer);

            if (wallHit.collider != null)
            {
                owner.Rb.linearVelocity = new Vector2(0f, owner.Rb.linearVelocity.y);
            }
        }

        if (timer <= 0f)
        {
            owner.Ctx.isHitstunned = false;
            owner.RestoreDrag();
            IState target = ReturnState ?? owner.FSM.PreviousState;
            if (target != null)
                owner.FSM.ChangeState(target);
        }
    }

    public override void Exit()
    {
        owner.Ctx.isHitstunned = false;
    }
}

// ---------------------------------------------------------------
//  AirHitstunState
//  Same as ground but for flying enemies. Wall check in both axes.
// ---------------------------------------------------------------
public class AirHitstunState : EnemyState
{
    public IState ReturnState { get; set; }
    private float timer;

    public AirHitstunState(EnemyBase owner) : base(owner) { }

    public override void Enter()
    {
        owner.Ctx.isHitstunned = true;
        timer = owner.Profile.hitstunDuration;
    }

    public override void FixedTick()
    {
        timer -= Time.fixedDeltaTime;

        // Wall safety: stop velocity toward nearby walls
        if (owner.Rb.linearVelocity.x != 0f)
        {
            float moveDir = Mathf.Sign(owner.Rb.linearVelocity.x);
            LayerMask mask = owner.ObstacleLayer != 0 ? owner.ObstacleLayer : owner.GroundLayer;
            RaycastHit2D hit = Physics2D.Raycast(
                owner.transform.position,
                Vector2.right * moveDir,
                owner.Profile.wallCheckDistance,
                mask);

            if (hit.collider != null)
            {
                owner.Rb.linearVelocity = new Vector2(0f, owner.Rb.linearVelocity.y);
            }
        }

        if (timer <= 0f)
        {
            owner.Ctx.isHitstunned = false;
            owner.RestoreDrag();
            IState target = ReturnState ?? owner.FSM.PreviousState;
            if (target != null)
                owner.FSM.ChangeState(target);
        }
    }

    public override void Exit()
    {
        owner.Ctx.isHitstunned = false;
    }
}

// ---------------------------------------------------------------
//  GroundDeadState
//  Stop movement, play die anim, disable colliders, destroy.
// ---------------------------------------------------------------
public class GroundDeadState : EnemyState
{
    public GroundDeadState(EnemyBase owner) : base(owner) { }

    public override void Enter()
    {
        owner.Ctx.isDead = true;
        owner.RestoreDrag();
        owner.StopAll();

        if (owner.Anim != null)
        {
            owner.Anim.SetBool("Walking", false);
            owner.Anim.SetTrigger("Die");
        }

        foreach (Collider2D col in owner.GetComponents<Collider2D>())
        {
            col.enabled = false;
        }

        Object.Destroy(owner.gameObject, 1.5f);
    }
}

// ---------------------------------------------------------------
//  AirDeadState
//  Stop movement, enable gravity so enemy falls, play die anim, destroy.
// ---------------------------------------------------------------
public class AirDeadState : EnemyState
{
    public AirDeadState(EnemyBase owner) : base(owner) { }

    public override void Enter()
    {
        owner.Ctx.isDead = true;
        owner.RestoreDrag();
        owner.StopAll();
        owner.Rb.gravityScale = 1f;

        if (owner.Anim != null) owner.Anim.SetTrigger("Die");

        foreach (Collider2D col in owner.GetComponents<Collider2D>())
        {
            col.enabled = false;
        }

        Object.Destroy(owner.gameObject, 2f);
    }
}
