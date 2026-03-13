using UnityEngine;

/// <summary>
/// Valkyrie (Brunhilde) — Mini-boss using the AI framework.
///
/// Phase 1 (grounded):
///   Approach → Decision → Slash / Flurry / Thrust → loop.
///   Attacks are cooldown-gated and weighted via AttackDefinition.selectionWeight.
///
/// Phase 2 (always flying, triggered at EnemyProfile.phase2HealthPercent):
///   P2Hover ←→ P2ErraticSlash / P2ErraticFlurry / P2Plunge.
///   A Behavior Tree selects the next attack by setting an intent on ValkP2Super.
///   The sub-FSM transitions only when P2Hover consumes the intent.
///
/// Outer FSM: ValkP1Super → PhaseTransition → ValkP2Super → BossDeadState.
///
/// Required components: Rigidbody2D, Collider2D, Health.
/// Child references: slashHitbox, flurryHitbox, thrustHitbox, plungeHitbox
///     (each an AttackHitbox on a child GO with trigger collider).
/// Optional: Animator (triggers listed in ValkyrieStates.cs).
///
/// EnemyProfile.attacks[] must include entries named:
///   "Slash", "Flurry", "Thrust", "Plunge"
/// </summary>
public class ValkyrieBoss : EnemyBase
{
    [Header("Hitbox References")]
    [SerializeField] private AttackHitbox slashHitbox;
    [SerializeField] private AttackHitbox flurryHitbox;
    [SerializeField] private AttackHitbox thrustHitbox;
    [SerializeField] private AttackHitbox plungeHitbox;

    // Hitbox accessors for states
    public AttackHitbox SlashHitbox => slashHitbox;
    public AttackHitbox FlurryHitbox => flurryHitbox;
    public AttackHitbox ThrustHitbox => thrustHitbox;
    public AttackHitbox PlungeHitbox => plungeHitbox;

    // Outer FSM states
    public ValkP1Super P1Super { get; private set; }
    public ValkP2Super P2Super { get; private set; }
    public PhaseTransitionState PhaseTransition { get; private set; }
    public BossDeadState DeadState { get; private set; }

    // Phase tracking
    private bool isPhase2;

    protected override void InitializeStates()
    {
        P1Super = new ValkP1Super(this);
        P2Super = new ValkP2Super(this);
        PhaseTransition = new PhaseTransitionState(this);
        DeadState = new BossDeadState(this, DisableAllHitboxes, enableGravityOnDeath: true);

        FSM.ChangeState(P1Super);
    }

    protected override void HandleDamageTaken(int damage, Vector2 knockback)
    {
        if (Ctx.isDead) return;

        // Phase transition check
        if (!isPhase2 && Health.HealthPercent <= Profile.phase2HealthPercent)
        {
            isPhase2 = true;
            DisableAllHitboxes();
            PhaseTransition.NextPhaseState = P2Super;
            FSM.ChangeState(PhaseTransition);
            return;
        }

        // Bosses skip hitstun — they absorb hits without flinching.
        // If you want hitstun, uncomment below and add an AirHitstunState field.
        // ApplyKnockback(knockback);
        // FSM.ChangeState(HitstunState);
    }

    protected override void HandleDeath()
    {
        DisableAllHitboxes();
        FSM.ChangeState(DeadState);
    }

    /// <summary>
    /// Helper to look up an AttackDefinition by name from the profile.
    /// Used by ValkyrieStates to read timing/damage values.
    /// </summary>
    public AttackDefinition GetAttackDef(string attackName)
    {
        if (Profile.attacks == null) return null;
        for (int i = 0; i < Profile.attacks.Length; i++)
        {
            if (Profile.attacks[i].attackName == attackName)
                return Profile.attacks[i];
        }
        return null;
    }

    private void DisableAllHitboxes()
    {
        if (slashHitbox != null) slashHitbox.Deactivate();
        if (flurryHitbox != null) flurryHitbox.Deactivate();
        if (thrustHitbox != null) thrustHitbox.Deactivate();
        if (plungeHitbox != null) plungeHitbox.Deactivate();
    }

    private void OnDrawGizmosSelected()
    {
        // Ground check ray (green)
        Vector2 groundOrigin = GroundCheck != null
            ? (Vector2)GroundCheck.position
            : (Vector2)transform.position + new Vector2(FacingDirection * 0.5f, 0f);
        Gizmos.color = Color.green;
        Gizmos.DrawLine(groundOrigin, groundOrigin + Vector2.down * (Profile != null ? Profile.groundCheckDistance : 1f));

        // Wall check ray (red)
        Vector2 wallOrigin = WallCheck != null
            ? (Vector2)WallCheck.position
            : (Vector2)transform.position;
        Gizmos.color = Color.red;
        Gizmos.DrawLine(wallOrigin, wallOrigin + Vector2.right * FacingDirection * (Profile != null ? Profile.wallCheckDistance : 0.5f));

        if (Profile != null)
        {
            // Aggro range (yellow)
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, Profile.aggroRange);

            // Deaggro range (orange)
            Gizmos.color = new Color(1f, 0.5f, 0f);
            Gizmos.DrawWireSphere(transform.position, Profile.deaggroRange);

            // Attack range (blue)
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, Profile.attackRange);

            // Hover height indicator (white)
            Gizmos.color = Color.white;
            Vector3 hoverPos = transform.position + Vector3.up * Profile.hoverHeight;
            Gizmos.DrawLine(hoverPos + Vector3.left * 0.5f, hoverPos + Vector3.right * 0.5f);
        }

        // LOS ray to player (cyan = clear, magenta = blocked)
        if (PlayerController.Instance != null)
        {
            float eyeY = Profile != null ? Profile.losEyeOffsetY : 0.5f;
            Vector2 eyePos = (Vector2)transform.position + new Vector2(0f, eyeY);
            Vector2 playerEye = (Vector2)PlayerController.Instance.transform.position + new Vector2(0f, eyeY);
            bool hasLOS = Ctx != null && Ctx.hasLineOfSightToPlayer;
            Gizmos.color = hasLOS ? Color.cyan : Color.magenta;
            Gizmos.DrawLine(eyePos, playerEye);
        }
    }
}
