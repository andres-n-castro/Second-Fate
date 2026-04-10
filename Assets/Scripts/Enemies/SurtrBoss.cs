using UnityEngine;

/// <summary>
/// Surtr — Muspelheim fire giant mini-boss using the AI framework.
///
/// Phase 1 (grounded, heavy melee/ranged):
///   Approach → Decision → LavaSweep / HeavyThrust / FireBreath → loop.
///   Attacks are cooldown-gated and weighted via AttackDefinition.selectionWeight.
///
/// Phase 2 (grounded, eruptive — triggered at EnemyProfile.phase2HealthPercent):
///   P2Idle ←→ GroundedThrust / LavaVomit.
///   A Behavior Tree selects the next attack by setting an intent on SurtrP2Super.
///   The sub-FSM transitions only when P2Idle consumes the intent.
///   GroundedThrust gets stuck in ground, creating a punish window.
///   Passive eruption periodically spawns lava projectiles during P2.
///
/// Outer FSM: BossIntroState → SurtrP1Super → PhaseTransition → SurtrP2Super → BossDeadState.
///
/// Required components: Rigidbody2D, Collider2D, Health.
/// Child references: lavaSweepHitbox, heavyThrustHitbox, fireBreathHitbox,
///     groundedThrustHitbox, lavaVomitHitbox
///     (each an AttackHitbox on a child GO with trigger collider).
///
/// Prefab references: lavaProjectilePrefab (LavaProjectile).
/// Transform references: lavaSpawnPoint (projectile spawn origin).
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

    [Header("Projectile")]
    [SerializeField] private GameObject lavaProjectilePrefab;
    [SerializeField] private Transform lavaSpawnPoint;

    // Hitbox accessors for states
    public AttackHitbox LavaSweepHitbox => lavaSweepHitbox;
    public AttackHitbox HeavyThrustHitbox => heavyThrustHitbox;
    public AttackHitbox FireBreathHitbox => fireBreathHitbox;
    public AttackHitbox GroundedThrustHitbox => groundedThrustHitbox;
    public AttackHitbox LavaVomitHitbox => lavaVomitHitbox;

    // Projectile accessors for states
    public GameObject LavaProjectilePrefab => lavaProjectilePrefab;
    public Transform LavaSpawnPoint => lavaSpawnPoint;

    // Cached hitbox reach (computed once in Start from child localPosition + collider extents)
    public float LavaSweepReach { get; private set; }
    public float HeavyThrustReach { get; private set; }
    public float FireBreathReach { get; private set; }

    // Animation parameter names matching Surtr animator
    public override string AnimWalking => "Surtr_Walking";
    public override string AnimDeath => "Surtr_Dies";

    // Outer FSM states
    public BossIntroState IntroState { get; private set; }
    public SurtrP1Super P1Super { get; private set; }
    public SurtrP2Super P2Super { get; private set; }
    public PhaseTransitionState PhaseTransition { get; private set; }
    public BossDeadState DeadState { get; private set; }

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

    /// <summary>
    /// Spawn a lava projectile with the given velocity from lavaSpawnPoint.
    /// </summary>
    public void SpawnLavaProjectile(Vector2 velocity)
    {
        if (lavaProjectilePrefab == null) return;

        Vector2 spawnPos = lavaSpawnPoint != null
            ? (Vector2)lavaSpawnPoint.position
            : (Vector2)transform.position + new Vector2(FacingDirection * 0.5f, 0.5f);

        GameObject lava = Instantiate(lavaProjectilePrefab, spawnPos, Quaternion.identity);

        LavaProjectile proj = lava.GetComponent<LavaProjectile>();
        if (proj != null)
        {
            proj.Initialize(velocity, GetComponents<Collider2D>());
        }
        else
        {
            Rigidbody2D lavaRb = lava.GetComponent<Rigidbody2D>();
            if (lavaRb != null) lavaRb.linearVelocity = velocity;
        }
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
