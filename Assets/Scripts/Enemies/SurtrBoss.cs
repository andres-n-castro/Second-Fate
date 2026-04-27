using UnityEngine;
using UnityEngine.SceneManagement;

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
    private const string SurtrSceneName = "biome2_subarea1";
    private const string SurtrKeyItemID = "Surtr_key";
    private const float KeyPopDuration = 0.3f;

    public override bool IsBoss => true;
    public override string BossDisplayName => "Surtr";

    [Header("Hitbox References")]
    [SerializeField] private AttackHitbox lavaSweepHitbox;
    [SerializeField] private AttackHitbox heavyThrustHitbox;
    [SerializeField] private AttackHitbox fireBreathHitbox;
    [SerializeField] private AttackHitbox groundedThrustHitbox;
    [SerializeField] private AttackHitbox lavaVomitHitbox;

    [Header("Scene Reward Drop")]
    [SerializeField] private string sceneKeyObjectName = "key_to_second_part";
    [SerializeField] private Item sceneKeyItemReward;
    [SerializeField] private Vector2 sceneKeyDropOffset = new Vector2(1f, 1.2f);

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

    // Outer FSM states
    public BossIntroState IntroState { get; private set; }
    public SurtrP1Super P1Super { get; private set; }
    public SurtrP2Super P2Super { get; private set; }
    public PhaseTransitionState PhaseTransition { get; private set; }
    public BossDeadState DeadState { get; private set; }

    // Phase tracking
    private bool isPhase2;
    private GameObject sceneKeyDropObject;

    protected override void Awake()
    {
        base.Awake();
        CacheSceneKeyDrop();
    }

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
    }

    protected override void HandleDeath()
    {
        DisableAllHitboxes();
        DropSceneKey();
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

    private void CacheSceneKeyDrop()
    {
        if (SceneManager.GetActiveScene().name != SurtrSceneName || string.IsNullOrWhiteSpace(sceneKeyObjectName))
        {
            return;
        }

        sceneKeyDropObject = FindSceneObjectByName(sceneKeyObjectName);
        if (sceneKeyDropObject != null)
        {
            sceneKeyDropObject.SetActive(false);
        }
    }

    private void DropSceneKey()
    {
        if (sceneKeyDropObject == null)
        {
            return;
        }

        ApplySceneKeyRewardItem();

        sceneKeyDropObject.transform.SetParent(null);
        sceneKeyDropObject.SetActive(true);

        Collider2D keyCollider = sceneKeyDropObject.GetComponent<Collider2D>();
        if (keyCollider != null)
        {
            keyCollider.enabled = false;
        }

        StartCoroutine(AnimateSceneKeyDrop(keyCollider));
    }

    private void ApplySceneKeyRewardItem()
    {
        ItemPickup itemPickup = sceneKeyDropObject.GetComponent<ItemPickup>();
        if (itemPickup == null)
        {
            return;
        }

        Item rewardItem = sceneKeyItemReward != null ? sceneKeyItemReward : ResolveSurtrKeyReward();
        if (rewardItem != null)
        {
            itemPickup.itemData = rewardItem;
        }
    }

    private Item ResolveSurtrKeyReward()
    {
        if (GameDatabase.Instance == null)
        {
            return null;
        }

        return GameDatabase.Instance.GetItemByID(SurtrKeyItemID);
    }

    private System.Collections.IEnumerator AnimateSceneKeyDrop(Collider2D keyCollider)
    {
        Transform keyTransform = sceneKeyDropObject.transform;
        Vector2 startPosition = transform.position;
        Vector2 targetPosition = startPosition + sceneKeyDropOffset;

        keyTransform.position = startPosition;

        float elapsed = 0f;
        while (elapsed < KeyPopDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / KeyPopDuration);
            float arc = Mathf.Sin(t * Mathf.PI) * 0.35f;
            keyTransform.position = Vector2.Lerp(startPosition, targetPosition, t) + Vector2.up * arc;
            yield return null;
        }

        keyTransform.position = targetPosition;

        if (keyCollider != null)
        {
            keyCollider.enabled = true;
        }
    }

    private GameObject FindSceneObjectByName(string objectName)
    {
        GameObject[] sceneObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        Scene activeScene = SceneManager.GetActiveScene();

        for (int i = 0; i < sceneObjects.Length; i++)
        {
            GameObject candidate = sceneObjects[i];
            if (candidate == null || candidate.scene != activeScene)
            {
                continue;
            }

            if (candidate.name == objectName)
            {
                return candidate;
            }
        }

        return null;
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
