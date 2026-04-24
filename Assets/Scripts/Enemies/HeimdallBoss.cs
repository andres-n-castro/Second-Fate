using UnityEngine;

/// <summary>
/// Heimdall — Bad Alignment final boss using the AI framework.
///
/// Phase 1 (grounded, greatsword pressure):
///   Approach → Decision → ShockwaveSlash / SwordTornado / SwordBeam → loop.
///   Heimdall is an aggressive pressure boss who stalks the player.
///   Attacks are cooldown-gated and weighted via AttackDefinition.selectionWeight.
///
/// Phase 2 (radiating greatsword — triggered at EnemyProfile.phase2HealthPercent):
///   P2Idle ←→ ProjectileSwords / SwordPlunge / GiantSlash.
///   A Behavior Tree selects the next attack by setting an intent on HeimdallP2Super.
///   The sub-FSM transitions only when P2Idle consumes the intent.
///
/// Outer FSM: BossIntroState → HeimdallP1Super → PhaseTransition → HeimdallP2Super → BossDeadState.
///
/// Required components: Rigidbody2D, Collider2D, Health.
/// Child references:
///   - shockwaveSlashHitbox, swordTornadoHitbox, swordPlungeHitbox, giantSlashHitbox
///     (each an AttackHitbox on a child GO with trigger collider).
///
/// Prefab references:
///   - shockwavePrefab (SlashProjectile) — jumpable horizontal wave
///   - projectileSwordPrefab (OdinProjectile) — curve-to-last-known projectile
///
/// Transform references: projectileSpawnPoint (projectile fire origin).
///
/// EnemyProfile.attacks[] must include entries named:
///   "ShockwaveSlash", "SwordTornado", "SwordBeam",
///   "ProjectileSwords", "SwordPlunge", "GiantSlash"
/// </summary>
public class HeimdallBoss : EnemyBase
{
    [Header("Hitbox References")]
    [SerializeField] private AttackHitbox shockwaveSlashHitbox;
    [SerializeField] private AttackHitbox swordTornadoHitbox;
    [SerializeField] private AttackHitbox swordPlungeHitbox;
    [SerializeField] private AttackHitbox giantSlashHitbox;

    [Header("Projectiles & Hazards")]
    [SerializeField] private GameObject shockwavePrefab;
    [SerializeField] private GameObject swordBeamPrefab;
    [SerializeField] private GameObject projectileSwordPrefab;
    [SerializeField] private GameObject floorImpactPrefab;
    [SerializeField] private GameObject giantSlashPrefab;
    [SerializeField] private Transform projectileSpawnPoint;

    // Hitbox accessors for states
    public AttackHitbox ShockwaveSlashHitbox => shockwaveSlashHitbox;
    public AttackHitbox SwordTornadoHitbox => swordTornadoHitbox;
    public AttackHitbox SwordPlungeHitbox => swordPlungeHitbox;
    public AttackHitbox GiantSlashHitbox => giantSlashHitbox;

    // Spawn point accessor for states
    public Transform ProjectileSpawnPoint => projectileSpawnPoint;

    // Cached hitbox reaches
    public float ShockwaveSlashReach { get; private set; }
    public float SwordTornadoReach { get; private set; }

    // Animation parameter names matching Heimdall animator
    public override string AnimWalking => "Heimdall_Walking";
    public override string AnimAttack => "Heimdall_Attack";
    public override string AnimHitstun => "Heimdall_Takes_Damage";
    public override string AnimDeath => "Heimdall_Dies";

    // Outer FSM states
    public BossIntroState IntroState { get; private set; }
    public HeimdallP1Super P1Super { get; private set; }
    public HeimdallP2Super P2Super { get; private set; }
    public PhaseTransitionState PhaseTransition { get; private set; }
    public BossDeadState DeadState { get; private set; }

    // Phase tracking
    private bool isPhase2;

    protected override void Start()
    {
        base.Start();

        ShockwaveSlashReach = ComputeHitboxReach(shockwaveSlashHitbox);
        SwordTornadoReach = ComputeHitboxReach(swordTornadoHitbox);
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
        P1Super = new HeimdallP1Super(this);
        P2Super = new HeimdallP2Super(this);
        PhaseTransition = new PhaseTransitionState(this, "Heimdall_Phase_Transition");
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
    /// Spawn a jumpable shockwave (reuses SlashProjectile).
    /// </summary>
    public void SpawnShockwave(Vector2 position, Vector2 velocity, int damage)
    {
        if (shockwavePrefab == null) return;

        GameObject wave = Instantiate(shockwavePrefab, position, Quaternion.identity);

        SlashProjectile sp = wave.GetComponent<SlashProjectile>();
        if (sp != null)
        {
            sp.Initialize(velocity, GetComponents<Collider2D>(), damage);
        }
        else
        {
            Rigidbody2D waveRb = wave.GetComponent<Rigidbody2D>();
            if (waveRb != null) waveRb.linearVelocity = velocity;
        }
    }

    /// <summary>
    /// Spawn a concentrated sword beam projectile.
    /// </summary>
    public void SpawnSwordBeam(Vector2 position, Vector2 velocity, int damage)
    {
        if (swordBeamPrefab == null) return;

        GameObject beam = Instantiate(swordBeamPrefab, position, Quaternion.identity);

        SwordBeamProjectile sbp = beam.GetComponent<SwordBeamProjectile>();
        if (sbp != null)
        {
            sbp.Initialize(velocity, GetComponents<Collider2D>(), damage);
        }
        else
        {
            Rigidbody2D beamRb = beam.GetComponent<Rigidbody2D>();
            if (beamRb != null) beamRb.linearVelocity = velocity;
        }
    }

    /// <summary>
    /// Spawn a projectile sword that curves toward the last-known position.
    /// Reuses OdinProjectile for the curve-to-target behavior.
    /// </summary>
    public void SpawnProjectileSword(Vector2 velocity, Vector2 targetPosition,
        int damage, float curveDelay, float curveStrength, float lifetime)
    {
        if (projectileSwordPrefab == null) return;

        Vector2 spawnPos = projectileSpawnPoint != null
            ? (Vector2)projectileSpawnPoint.position
            : (Vector2)transform.position + new Vector2(FacingDirection * 0.5f, 0.5f);

        GameObject proj = Instantiate(projectileSwordPrefab, spawnPos, Quaternion.identity);

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
    /// Spawn a floor impact at the plunge landing position.
    /// </summary>
    public void SpawnFloorImpact(Vector2 position, float radius, float duration, int damage)
    {
        if (floorImpactPrefab == null) return;

        GameObject impact = Instantiate(floorImpactPrefab, position, Quaternion.identity);

        FloorImpact fi = impact.GetComponent<FloorImpact>();
        if (fi != null)
        {
            fi.Initialize(radius, duration, damage);
        }
    }

    /// <summary>
    /// Spawn a giant slash wave (reuses SlashProjectile).
    /// </summary>
    public void SpawnGiantSlashWave(Vector2 position, Vector2 velocity, int damage)
    {
        if (giantSlashPrefab == null) return;

        GameObject slash = Instantiate(giantSlashPrefab, position, Quaternion.identity);

        SlashProjectile sp = slash.GetComponent<SlashProjectile>();
        if (sp != null)
        {
            sp.Initialize(velocity, GetComponents<Collider2D>(), damage);
        }
        else
        {
            Rigidbody2D slashRb = slash.GetComponent<Rigidbody2D>();
            if (slashRb != null) slashRb.linearVelocity = velocity;
        }
    }

    private void DisableAllHitboxes()
    {
        if (shockwaveSlashHitbox != null) shockwaveSlashHitbox.Deactivate();
        if (swordTornadoHitbox != null) swordTornadoHitbox.Deactivate();
        if (swordPlungeHitbox != null) swordPlungeHitbox.Deactivate();
        if (giantSlashHitbox != null) giantSlashHitbox.Deactivate();
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
            Gizmos.DrawWireSphere(transform.position, Profile.heimdallCloseRange);

            // Max engage range (magenta)
            Gizmos.color = new Color(1f, 0f, 1f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, Profile.heimdallMaxEngageRange);

            // Sword plunge floor impact radius (white)
            Gizmos.color = new Color(1f, 1f, 1f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, Profile.heimdallFloorImpactRadius);
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
