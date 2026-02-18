using UnityEngine;

/// <summary>
/// Fallen Warrior Spirit — Flying enemy using the AI framework.
///
/// Behavior:
///   - Roams within a bounded area around home position.
///   - Detects player → adaptive decision between dash attack and reposition.
///   - Dash probability increases when player dashes frequently (adaptive weighting).
///   - Timer-based attacks (not coroutines) for safe interruptibility.
///   - Takes knockback with brief hitstun; wall checks prevent clipping.
///
/// Required components: Rigidbody2D (gravity 0), Collider2D, Health.
/// Child reference: dashHitbox (AttackHitbox on child GO with trigger collider).
/// Optional: Animator (triggers: "Windup", "Dash", "Die").
/// </summary>
public class FallenWarriorSpirit : EnemyBase
{
    [Header("FWS References")]
    [SerializeField] private AttackHitbox dashHitbox;

    // State references
    public FWSHoverPatrolState PatrolState { get; private set; }
    public FWSEngageDecisionState EngageDecisionState { get; private set; }
    public FWSDashAttackState DashAttackState { get; private set; }
    public FWSRepositionState RepositionState { get; private set; }
    public AirHitstunState HitstunState { get; private set; }
    public AirDeadState DeadState { get; private set; }

    // Home position for roaming
    public Vector2 HomePosition { get; set; }

    // Hitbox accessor for states
    public AttackHitbox DashHitbox => dashHitbox;

    protected override void Start()
    {
        base.Start();
        Rb.gravityScale = 0f;
        HomePosition = transform.position;
    }

    protected override void InitializeStates()
    {
        PatrolState = new FWSHoverPatrolState(this);
        EngageDecisionState = new FWSEngageDecisionState(this);
        DashAttackState = new FWSDashAttackState(this);
        RepositionState = new FWSRepositionState(this);
        HitstunState = new AirHitstunState(this);
        DeadState = new AirDeadState(this);

        FSM.ChangeState(PatrolState);
    }

    protected override void HandleDamageTaken(int damage, Vector2 knockback)
    {
        if (Ctx.isDead) return;

        ApplyKnockback(knockback);

        HitstunState.ReturnState = FSM.CurrentState;
        FSM.ChangeState(HitstunState);
    }

    protected override void HandleDeath()
    {
        if (dashHitbox != null) dashHitbox.Deactivate();
        FSM.ChangeState(DeadState);
    }

    private void OnDrawGizmosSelected()
    {
        if (Profile != null)
        {
            // Detection radius (yellow)
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, Profile.aggroRange);

            // De-aggro radius (orange)
            Gizmos.color = new Color(1f, 0.5f, 0f);
            Gizmos.DrawWireSphere(transform.position, Profile.deaggroRange);

            // LOS ray (cyan = clear, magenta = blocked)
            if (PlayerController.Instance != null)
            {
                Vector2 eyePos = (Vector2)transform.position + new Vector2(0f, Profile.losEyeOffsetY);
                Vector2 playerEye = (Vector2)PlayerController.Instance.transform.position + new Vector2(0f, Profile.losEyeOffsetY);
                bool hasLOS = Ctx != null && Ctx.hasLineOfSightToPlayer;
                Gizmos.color = hasLOS ? Color.cyan : Color.magenta;
                Gizmos.DrawLine(eyePos, playerEye);
            }
        }

        // Roam area (cyan)
        Gizmos.color = Color.cyan;
        Vector3 home = Application.isPlaying ? (Vector3)HomePosition : transform.position;
        float radius = Profile != null ? Profile.roamRadius : 3f;
        Gizmos.DrawWireSphere(home, radius);
    }
}
