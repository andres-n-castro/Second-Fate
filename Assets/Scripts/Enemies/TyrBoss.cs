using UnityEngine;

/// <summary>
/// Tyr — Biome 1 main boss using the same AI framework as ValkyrieBoss.
///
/// Phase 1 (grounded, spear):
///   Approach → Decision → Spear Thrust / Spear Flurry → loop.
///   Attacks are cooldown-gated and weighted via AttackDefinition.selectionWeight.
///
/// Phase 2 (grounded, shield — triggered at EnemyProfile.phase2HealthPercent):
///   P2PressureOrIdle ←→ Shield Block / Shield Slam / Shield Flurry.
///   A Behavior Tree selects the next action by setting an intent on TyrP2Super.
///   The sub-FSM transitions only when P2PressureOrIdle consumes the intent.
///   Shield Block makes Tyr invulnerable for its Active duration.
///   BT weights react to player attack recency (defensive when pressured).
///
/// Outer FSM: BossIntroState → TyrP1Super → PhaseTransition → TyrP2Super → BossDeadState.
///
/// Required components: Rigidbody2D, Collider2D, Health.
/// Child references: spearThrustHitbox, spearFlurryHitbox, shieldSlamHitbox, shieldFlurryHitbox
///     (each an AttackHitbox on a child GO with trigger collider).
///
/// EnemyProfile.attacks[] must include entries named:
///   "SpearThrust", "SpearFlurry", "ShieldBlock", "ShieldSlam", "ShieldFlurry"
/// </summary>
public class TyrBoss : EnemyBase
{
    [Header("Hitbox References")]
    [SerializeField] private AttackHitbox spearThrustHitbox;
    [SerializeField] private AttackHitbox spearFlurryHitbox;
    [SerializeField] private AttackHitbox shieldSlamHitbox;
    [SerializeField] private AttackHitbox shieldFlurryHitbox;

    [Header("Phase 2 Body Collider")]
    [SerializeField] private Vector2 p2BodyColliderSize = new Vector2(2.0f, 2.5f);
    [SerializeField] private Vector2 p2BodyColliderOffset = new Vector2(0f, -0.25f);

    private BoxCollider2D bodyCollider;

    // Hitbox accessors for states
    public AttackHitbox SpearThrustHitbox => spearThrustHitbox;
    public AttackHitbox SpearFlurryHitbox => spearFlurryHitbox;
    public AttackHitbox ShieldSlamHitbox => shieldSlamHitbox;
    public AttackHitbox ShieldFlurryHitbox => shieldFlurryHitbox;

    // Cached hitbox reach (computed once in Start from child localPosition + collider extents)
    public float SpearThrustReach { get; private set; }
    public float SpearFlurryReach { get; private set; }

    // Animation parameter names matching Tyr animator
    public override string AnimWalking => "Tyr_Walking";
    public override string AnimDeath => "Tyr_Dies";

    // Outer FSM states
    public BossIntroState IntroState { get; private set; }
    public TyrP1Super P1Super { get; private set; }
    public TyrP2Super P2Super { get; private set; }
    public PhaseTransitionState PhaseTransition { get; private set; }
    public BossDeadState DeadState { get; private set; }

    // Phase tracking
    private bool isPhase2;

    protected override void Start()
    {
        base.Start();

        bodyCollider = GetComponent<BoxCollider2D>();
        SpearThrustReach = ComputeHitboxReach(spearThrustHitbox);
        SpearFlurryReach = ComputeHitboxReach(spearFlurryHitbox);
    }

    private float ComputeHitboxReach(AttackHitbox hitbox)
    {
        if (hitbox == null) return 1f;

        float localX = Mathf.Abs(hitbox.transform.localPosition.x);
        var box = hitbox.GetComponent<BoxCollider2D>();
        if (box != null)
            return localX + box.offset.x + box.size.x * 0.5f;

        var col = hitbox.GetComponent<Collider2D>();
        if (col != null)
            return localX + col.bounds.extents.x;

        return localX + 0.5f;
    }

    protected override void InitializeStates()
    {
        IntroState = new BossIntroState(this);
        P1Super = new TyrP1Super(this);
        P2Super = new TyrP2Super(this);
        PhaseTransition = new PhaseTransitionState(this, "Tyr_Phase_Transition");
        DeadState = new BossDeadState(this, DisableAllHitboxes);

        IntroState.NextState = P1Super;
        FSM.ChangeState(IntroState);
    }

    protected override void HandleDamageTaken(int damage, Vector2 knockback)
    {
        if (Ctx.isDead) return;

        // Phase transition check
        if (!isPhase2 && Health.HealthPercent <= Profile.phase2HealthPercent)
        {
            isPhase2 = true;
            DisableAllHitboxes();
            SwapToP2BodyCollider();
            PhaseTransition.NextPhaseState = P2Super;
            FSM.ChangeState(PhaseTransition);
            return;
        }

        // Bosses skip hitstun — they absorb hits without flinching.
    }

    protected override void HandleDeath()
    {
        DisableAllHitboxes();
        FSM.ChangeState(DeadState);
    }

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

    private void SwapToP2BodyCollider()
    {
        if (bodyCollider != null)
        {
            bodyCollider.size = p2BodyColliderSize;
            bodyCollider.offset = p2BodyColliderOffset;
        }
    }

    private void DisableAllHitboxes()
    {
        if (spearThrustHitbox != null) spearThrustHitbox.Deactivate();
        if (spearFlurryHitbox != null) spearFlurryHitbox.Deactivate();
        if (shieldSlamHitbox != null) shieldSlamHitbox.Deactivate();
        if (shieldFlurryHitbox != null) shieldFlurryHitbox.Deactivate();
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

            // Attack range (blue)
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, Profile.attackRange);

            // Tyr engage range (magenta)
            Gizmos.color = new Color(1f, 0f, 1f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, Profile.tyrMaxEngageRange);

            // Tyr close range (green)
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, Profile.tyrCloseRange);
        }

        // P2 body collider preview (orange) — mirrors with facing direction like the real collider
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.6f);
        float scaleSign = Mathf.Sign(transform.localScale.x);
        Vector3 p2Center = transform.position + new Vector3(p2BodyColliderOffset.x * scaleSign, p2BodyColliderOffset.y, 0f);
        Gizmos.DrawWireCube(p2Center, new Vector3(p2BodyColliderSize.x, p2BodyColliderSize.y, 0f));

        // LOS ray to player
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
