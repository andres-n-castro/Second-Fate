using UnityEngine;

/// <summary>
/// Odin — Good Alignment final boss using the AI framework.
///
/// Phase 1 (grounded, staff mage):
///   Approach → Decision → StaffProjectile / GroundSpikes / StaffMelee → loop.
///   Odin maintains spacing like a mage, preferring ranged attacks.
///   Attacks are cooldown-gated and weighted via AttackDefinition.selectionWeight.
///
/// Phase 2 (intensified magic — triggered at EnemyProfile.phase2HealthPercent):
///   P2Idle ←→ TripleProjectile / ConsecutiveSpikes / LargeSlash.
///   A Behavior Tree selects the next attack by setting an intent on OdinP2Super.
///   The sub-FSM transitions only when P2Idle consumes the intent.
///
/// Outer FSM: BossIntroState → OdinP1Super → PhaseTransition → OdinP2Super → BossDeadState.
///
/// All attacks deal 2 hearts by default.
/// Exception: Phase 2 Triple Projectile deals 3 hearts.
///
/// Required components: Rigidbody2D, Collider2D, Health.
/// Child references: staffMeleeHitbox (AttackHitbox on child GO with trigger collider).
///
/// Prefab references:
///   - odinProjectilePrefab (OdinProjectile) — curving staff projectile
///   - groundSpikePrefab (GroundSpike) — ground spike hazard
///   - slashProjectilePrefab (SlashProjectile) — horizontal slash wave
///
/// Transform references: projectileSpawnPoint (projectile fire origin).
///
/// EnemyProfile.attacks[] must include entries named:
///   "StaffProjectile", "GroundSpikes", "StaffMelee",
///   "TripleProjectile", "ConsecutiveSpikes", "LargeSlash"
/// </summary>
public class OdinBoss : EnemyBase
{
    [Header("Hitbox References")]
    [SerializeField] private AttackHitbox staffMeleeHitbox;

    [Header("Projectiles & Hazards")]
    [SerializeField] private GameObject odinProjectilePrefab;
    [SerializeField] private GameObject groundSpikePrefab;
    [SerializeField] private GameObject slashProjectilePrefab;
    [SerializeField] private Transform projectileSpawnPoint;

    // Hitbox accessor for states
    public AttackHitbox StaffMeleeHitbox => staffMeleeHitbox;

    // Cached hitbox reach
    public float StaffMeleeReach { get; private set; }

    // Animation parameter names matching Odin animator
    public override string AnimWalking => "Odin_Walking";
    public override string AnimAttack => "Odin_Attack";
    public override string AnimHitstun => "Odin_Takes_Damage";
    public override string AnimDeath => "Odin_Dies";

    // Outer FSM states
    public BossIntroState IntroState { get; private set; }
    public OdinP1Super P1Super { get; private set; }
    public OdinP2Super P2Super { get; private set; }
    public PhaseTransitionState PhaseTransition { get; private set; }
    public BossDeadState DeadState { get; private set; }

    // Phase tracking
    private bool isPhase2;

    protected override void Start()
    {
        base.Start();

        StaffMeleeReach = ComputeHitboxReach(staffMeleeHitbox);
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
        P1Super = new OdinP1Super(this);
        P2Super = new OdinP2Super(this);
        PhaseTransition = new PhaseTransitionState(this, "Odin_Phase_Transition");
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
    /// Used by OdinStates to read timing/damage values.
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
    /// Spawn a curving staff projectile.
    /// </summary>
    public void SpawnOdinProjectile(Vector2 velocity, Vector2 targetPosition,
        int damage, float curveDelay, float curveStrength, float lifetime)
    {
        if (odinProjectilePrefab == null) return;

        Vector2 spawnPos = projectileSpawnPoint != null
            ? (Vector2)projectileSpawnPoint.position
            : (Vector2)transform.position + new Vector2(FacingDirection * 0.5f, 0.5f);

        GameObject proj = Instantiate(odinProjectilePrefab, spawnPos, Quaternion.identity);

        OdinProjectile odinProj = proj.GetComponent<OdinProjectile>();
        if (odinProj != null)
        {
            odinProj.Initialize(velocity, targetPosition, GetComponents<Collider2D>(),
                damage, curveDelay, curveStrength, lifetime);
        }
        else
        {
            Rigidbody2D projRb = proj.GetComponent<Rigidbody2D>();
            if (projRb != null) projRb.linearVelocity = velocity;
        }
    }

    /// <summary>
    /// Spawn a ground spike at the given position.
    /// </summary>
    public void SpawnGroundSpike(Vector2 position, float activeDuration)
    {
        if (groundSpikePrefab == null) return;

        GameObject spike = Instantiate(groundSpikePrefab, position, Quaternion.identity);

        GroundSpike gs = spike.GetComponent<GroundSpike>();
        if (gs != null)
        {
            gs.Initialize(activeDuration);
        }
    }

    /// <summary>
    /// Spawn a horizontal slash wave projectile.
    /// </summary>
    public void SpawnSlashProjectile(Vector2 position, Vector2 velocity)
    {
        if (slashProjectilePrefab == null) return;

        GameObject slash = Instantiate(slashProjectilePrefab, position, Quaternion.identity);

        SlashProjectile sp = slash.GetComponent<SlashProjectile>();
        if (sp != null)
        {
            sp.Initialize(velocity, GetComponents<Collider2D>());
        }
        else
        {
            Rigidbody2D slashRb = slash.GetComponent<Rigidbody2D>();
            if (slashRb != null) slashRb.linearVelocity = velocity;
        }
    }

    private void DisableAllHitboxes()
    {
        if (staffMeleeHitbox != null) staffMeleeHitbox.Deactivate();
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

            // Close range (green)
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, Profile.odinCloseRange);

            // Optimal spacing (cyan)
            Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, Profile.odinOptimalSpacing);

            // Max engage range (magenta)
            Gizmos.color = new Color(1f, 0f, 1f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, Profile.odinMaxEngageRange);
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
