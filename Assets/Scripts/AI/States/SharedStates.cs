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
        if (owner.Health != null) owner.Health.isInvulnerable = true;
        timer = owner.Profile.hitstunDuration;

        if (owner.Anim != null)
        {
            owner.Anim.SetBool(owner.AnimWalking, false);
            owner.Anim.SetTrigger(owner.AnimHitstun);
        }
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
        owner.StartPostHitstunInvulnerability();
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
        if (owner.Health != null) owner.Health.isInvulnerable = true;
        timer = owner.Profile.hitstunDuration;

        if (owner.Anim != null)
        {
            owner.Anim.SetBool(owner.AnimWalking, false);
            owner.Anim.SetTrigger(owner.AnimHitstun);
        }
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
        owner.StartPostHitstunInvulnerability();
    }
}

// ---------------------------------------------------------------
//  FallingDeadState
//  Stop horizontal movement, play die anim, keep gravity so the
//  corpse falls to the ground. Ignores collision with the player
//  so the body can't block movement, but keeps colliders active
//  so it lands on platforms.
// ---------------------------------------------------------------
public class FallingDeadState : EnemyState
{
    public FallingDeadState(EnemyBase owner) : base(owner) { }

    public override void Enter()
    {
        owner.Ctx.isDead = true;
        owner.RestoreDrag();
        owner.StopHorizontal();
        owner.SetLayerRecursively(0);

        if (owner.Anim != null)
        {
            owner.Anim.SetBool(owner.AnimWalking, false);
            owner.Anim.ResetTrigger(owner.AnimHitstun);
            owner.Anim.SetTrigger(owner.AnimDeath);
        }

        // Ignore collision with the player so the corpse doesn't block them
        if (PlayerController.Instance != null)
        {
            Collider2D[] playerCols = PlayerController.Instance.GetComponents<Collider2D>();
            Collider2D[] enemyCols = owner.GetComponents<Collider2D>();
            for (int i = 0; i < enemyCols.Length; i++)
                for (int j = 0; j < playerCols.Length; j++)
                    UnityEngine.Physics2D.IgnoreCollision(enemyCols[i], playerCols[j]);
        }

        Object.Destroy(owner.gameObject, 1.5f);
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
        owner.Rb.gravityScale = 0f;
        owner.SetLayerRecursively(0);

        if (owner.Anim != null)
        {
            owner.Anim.SetBool(owner.AnimWalking, false);
            // Clear any pending hitstun trigger from the same-frame damage event
            // so the AnyState→Hitstun transition can't steal us out of the death state.
            owner.Anim.ResetTrigger(owner.AnimHitstun);
            owner.Anim.SetTrigger(owner.AnimDeath);
        }

        // Ignore collision with the player so the corpse doesn't block them
        if (PlayerController.Instance != null)
        {
            Collider2D[] playerCols = PlayerController.Instance.GetComponents<Collider2D>();
            Collider2D[] enemyCols = owner.GetComponents<Collider2D>();
            for (int i = 0; i < enemyCols.Length; i++)
                for (int j = 0; j < playerCols.Length; j++)
                    UnityEngine.Physics2D.IgnoreCollision(enemyCols[i], playerCols[j]);
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
        owner.SetLayerRecursively(0);

        if (owner.Anim != null)
        {
            owner.Anim.ResetTrigger(owner.AnimHitstun);
            owner.Anim.SetTrigger(owner.AnimDeath);
        }

        Object.Destroy(owner.gameObject, 1.5f);
    }
}
