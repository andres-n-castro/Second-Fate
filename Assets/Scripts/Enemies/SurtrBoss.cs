using UnityEngine;

/// <summary>
/// Surtr — Muspelheim fire giant mini-boss using the AI framework.
///
/// Phase 1 (grounded, heavy melee/ranged):
///   Approach → Decision → LavaSweep / HeavyThrust / FireBreath → loop.
///   Attacks are cooldown-gated and weighted via AttackDefinition.selectionWeight.
///
/// Phase 2 (grounded, aggressive — triggered at EnemyProfile.phase2HealthPercent):
///   P2Idle ←→ GroundedThrust / LavaVomit.
///   A Behavior Tree selects the next attack by setting an intent on SurtrP2Super.
///   The sub-FSM transitions only when P2Idle consumes the intent.
///   GroundedThrust gets stuck in ground, creating a punish window.
///   LavaVomit is a ground-level hitbox zone (lava pool is part of the sprite).
///
/// Outer FSM: BossIntroState → SurtrP1Super → PhaseTransition → SurtrP2Super → BossDeadState.
///
/// Required components: Rigidbody2D, Collider2D, Health.
/// Child references: lavaSweepHitbox, heavyThrustHitbox, fireBreathHitbox,
///     groundedThrustHitbox, lavaVomitHitbox
///     (each an AttackHitbox on a child GO with trigger collider).
///
/// EnemyProfile.attacks[] must include entries named:
///   "LavaSweep", "HeavyThrust", "FireBreath", "GroundedThrust", "LavaVomit"
/// </summary>
public class SurtrBoss : EnemyBase
{
    [Header("Hitbox References")]
    [SerializeField] private AttackHitbox lavaSweepHitbox;
    [SerializeField] private AttackHitbox heavyThrustHitbox;
    [SerializeField] private AttackHitbox fireBreathHitbox;
    [SerializeField] private AttackHitbox groundedThrustHitbox;
    [SerializeField] private AttackHitbox lavaVomitHitbox;

    // Hitbox accessors for states
    public AttackHitbox LavaSweepHitbox => lavaSweepHitbox;
    public AttackHitbox HeavyThrustHitbox => heavyThrustHitbox;
    public AttackHitbox FireBreathHitbox => fireBreathHitbox;
    public AttackHitbox GroundedThrustHitbox => groundedThrustHitbox;
    public AttackHitbox LavaVomitHitbox => lavaVomitHitbox;

    // Cached hitbox reach (computed once in Start from child localPosition + collider extents)
    public float LavaSweepReach { get; private set; }
    public float HeavyThrustReach { get; private set; }
    public float FireBreathReach { get; private set; }

    // Animation parameter names matching Surtr animator
    public override string AnimWalking => "Surtr_Walking";
    public override string AnimDeath => "Surtr_Dies";
    public override string AnimHitstun => isPhase2 ? "Surtr_Hitstun_P2" : "Surtr_Hitstun_P1";

    // Outer FSM states
    public BossIntroState IntroState { get; private set; }
    public SurtrP1Super P1Super { get; private set; }
    public SurtrP2Super P2Super { get; private set; }
    public PhaseTransitionState PhaseTransition { get; private set; }
    public BossDeadState DeadState { get; private set; }

    // Hitstun states (grounded in both phases)
    public GroundHitstunState P1HitstunState { get; private set; }
    public GroundHitstunState P2HitstunState { get; private set; }

    // Phase tracking
    private bool isPhase2;

    protected override void Start()
    {
        base.Start();

        LavaSweepReach = ComputeHitboxReach(lavaSweepHitbox);
        HeavyThrustReach = ComputeHitboxReach(heavyThrustHitbox);
        FireBreathReach = ComputeHitboxReach(fireBreathHitbox);
    }

    private float ComputeHitboxReach(AttackHitbox hitbox)
    {
        if (hitbox == null) return 1f;

        float localX = Mathf.Abs(hitbox.transform.localPosition.x);
        var box = hitbox.GetComponent<BoxCollider2D>();
        if (box != null)
            return localX + box.size.x * 0.5f;

        var col = hitbox.GetComponent<Collider2D>();
        if (col != null)
            return localX + col.bounds.extents.x;

        return localX + 0.5f;
    }

    protected override void InitializeStates()
    {
        IntroState = new BossIntroState(this);
        P1Super = new SurtrP1Super(this);
        P2Super = new SurtrP2Super(this);
        PhaseTransition = new PhaseTransitionState(this, "Surtr_Phase_Transition");
        DeadState = new BossDeadState(this, DisableAllHitboxes);

        P1HitstunState = new GroundHitstunState(this) { ReturnState = P1Super };
        P2HitstunState = new GroundHitstunState(this) { ReturnState = P2Super };

        IntroState.NextState = P1Super;
        FSM.ChangeState(IntroState);
    }

    protected override void HandleDamageTaken(int damage, Vector2 knockback)
    {
        if (Ctx.isDead) return;

        // Phase transition takes priority
        if (!isPhase2 && Health.HealthPercent <= Profile.phase2HealthPercent)
        {
            isPhase2 = true;
            DisableAllHitboxes();
            PhaseTransition.NextPhaseState = P2Super;
            FSM.ChangeState(PhaseTransition);
            return;
        }

        // Don't interrupt phase transition
        if (FSM.CurrentState == PhaseTransition) return;

        // Hitstun
        DisableAllHitboxes();
        if (Rb != null) Rb.linearVelocity = Vector2.zero;
        FSM.ChangeState(isPhase2 ? (IState)P2HitstunState : P1HitstunState);
    }

    protected override void HandleDeath()
    {
        DisableAllHitboxes();
        FSM.ChangeState(DeadState);
    }

    /// <summary>
    /// Helper to look up an AttackDefinition by name from the profile.
    /// Used by SurtrStates to read timing/damage values.
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
        if (lavaSweepHitbox != null) lavaSweepHitbox.Deactivate();
        if (heavyThrustHitbox != null) heavyThrustHitbox.Deactivate();
        if (fireBreathHitbox != null) fireBreathHitbox.Deactivate();
        if (groundedThrustHitbox != null) groundedThrustHitbox.Deactivate();
        if (lavaVomitHitbox != null) lavaVomitHitbox.Deactivate();
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

            // Surtr range bands
            // Close range (green)
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, Profile.surtrCloseRange);
            // Mid range (cyan)
            Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, Profile.surtrMidRange);
            // Max engage range (magenta)
            Gizmos.color = new Color(1f, 0f, 1f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, Profile.surtrMaxEngageRange);
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
