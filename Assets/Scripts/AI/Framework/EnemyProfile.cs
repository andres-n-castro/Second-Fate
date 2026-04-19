using UnityEngine;
using System;

[CreateAssetMenu(menuName = "AI/Enemy Profile")]
public class EnemyProfile : ScriptableObject
{
    [Header("Ground Movement")]
    public float moveSpeed = 2f;

    [Header("Flying Movement")]
    public float flySpeed = 5f;
    public float roamSpeed = 2f;
    public float roamRadius = 3f;
    public float roamChangeInterval = 2f;

    [Header("Detection & Ranges")]
    public float aggroRange = 8f;
    public float deaggroRange = 12f;
    public float attackRange = 3f;

    [Header("Environment Checks")]
    public float groundCheckDistance = 1f;
    public float wallCheckDistance = 0.5f;

    [Header("Line of Sight")]
    public float losEyeOffsetY = 0.5f;
    public float losCastRadius = 0.1f;
    public bool requireLOSForAggro = true;
    public bool requireLOSForAttack = true;

    [Header("Knockback & Hitstun")]
    public float knockbackForceX = 5f;
    public float knockbackForceY = 2f;
    public float hitstunDuration = 0.25f;
    public float hitstunDrag = 8f;
    public float postHitstunInvulnerabilityDuration = 0.3f;

    [Header("Attack System")]
    public AttackDefinition[] attacks;

    [Header("Contact Damage")]
    public bool enableContactDamage = false;
    public int contactDamage = 1;
    public float contactKnockbackX = 6f;
    public float contactKnockbackY = 4f;
    public float contactCooldownSeconds = 0.8f;
    public bool contactRequiresEnemyAlive = true;

    [Header("Ground Chase")]
    public float chaseSpeed = 3.5f;
    public float acquireTargetDelay = 0.3f;
    public float loseTargetDelay = 0.5f;
    public float giveUpPauseDuration = 1f;
    public float facingDeadzoneX = 0.3f;
    public float playerAboveThresholdY = 0.6f;
    public float stuckTimeout = 1.0f;
    public float minProgressThreshold = 0.05f;
    public float blockedReaggroCooldown = 3f;

    [Header("Backstep (Draugr)")]
    public float backstepTriggerDistance = 1.0f;
    public float backstepDuration = 0.3f;
    public float backstepSpeed = 2.5f;
    public float backstepCooldown = 1.5f;

    [Header("Flying Dash Attack")]
    public float dashWindowSeconds = 5f;
    public float dashWeightBoostPerDash = 0.15f;
    public float baseDashWeight = 1f;
    public float dashStopShortDistance = 1f;
    public float dashPathCastRadius = 0.2f;

    [Header("Flying Patrol & Reposition")]
    public float patrolArriveThreshold = 0.5f;
    public float patrolSmoothing = 5f;
    public int patrolTargetSampleCount = 8;
    public float patrolTargetClearanceRadius = 0.4f;
    public float stuckSeconds = 0.5f;
    public float stuckMinProgress = 0.3f;
    public float repositionDistance = 4f;
    public float repositionDecisionCooldown = 0.4f;
    public float fwsMinRepositionDistFromPlayer = 1.5f;
    public float fwsPlayerPathExclusionRadius = 1.2f;

    [Header("Rock Golem")]
    public float projectileSpeed = 8f;

    [Header("Gravity")]
    public float maxFallVelocity = 28f;
    public float jumpHangThreshold = 2f;
    public float jumpHangGravityMultiplier = 0.5f;

    [Header("Magma Salamander")]
    public float jumpForce = 14f;
    public float jumpForwardForce = 3f;
    public float jumpHeightThreshold = 1.5f;
    public float jumpCooldown = 2f;

    [Header("Boss")]
    public float bossIntroDuration = 1.0f;
    public float approachSpeed = 3f;
    public float minAttackCooldown = 0.5f;
    public float maxAttackCooldown = 1.5f;
    public float phase2HealthPercent = 0.5f;
    public float phaseTransitionDuration = 2f;

    [Header("Valkyrie P1 Range Bands")]
    public float p1CloseRange = 2.0f;
    public float p1MidRange = 4.0f;
    public float p1MaxEngageRange = 6.0f;
    public float p1OptimalSpacing = 3.5f;

    [Header("Valkyrie P1 Thrust Gap-Close")]
    public float p1ThrustCloseGapMinRange = 3.5f;
    public float p1ThrustCloseGapMaxRange = 6.0f;
    public float p1ThrustCloseGapChance = 0.15f;
    public float p1GapCloseRunSpeed = 6.0f;

    [Header("Valkyrie P1 Slash Micro-Lunge")]
    public float p1SlashMicroLungeSpeed = 3.0f;
    public float p1SlashMicroLungeDuration = 0.2f;

    [Header("Valkyrie P2")]
    public float erraticIntensity = 3f;
    public float hoverHeight = 3f;

    [Header("Valkyrie P2 Attack Selection")]
    public float valkPlungeMinAbovePlayerY = 2f;
    public float valkPlungeMaxHorizontalOffset = 4f;
    public float valkPlungeWeightMultiplier = 5f;
    public float valkSlashRange = 4f;
    public float valkSlashMaxVerticalOffset = 2f;
    public float valkSlashWeightMultiplier = 3f;
    public float valkFlurryPreferredRangeMin = 3f;
    public float valkFlurryPreferredRangeMax = 7f;
    public float valkFlurryWeightMultiplier = 3f;

    [Header("Tyr Range Bands")]
    public float tyrCloseRange = 2.0f;
    public float tyrMaxEngageRange = 5.0f;

    [Header("Tyr P2 Reactive Behavior")]
    public float tyrReactiveBlockWindow = 1.0f;
    public float tyrBlockWeightMultiplier = 4f;
    public float tyrSlamRange = 3.0f;
    public float tyrSlamWeightMultiplier = 3f;
    public float tyrFlurryRange = 2.5f;
    public float tyrFlurryWeightMultiplier = 3f;

    [Header("Surtr Range Bands")]
    public float surtrCloseRange = 2.5f;
    public float surtrMidRange = 5.0f;
    public float surtrMaxEngageRange = 7.0f;

    [Header("Surtr P1 Attack Selection")]
    public float surtrSweepMaxRange = 5.0f;
    public float surtrSweepMinRange = 1.5f;
    public float surtrSweepWeightMultiplier = 2f;
    public float surtrThrustMaxRange = 3.5f;
    public float surtrThrustWeightMultiplier = 2f;
    public float surtrFireBreathMaxRange = 4.0f;
    public float surtrFireBreathWeightMultiplier = 2f;

    [Header("Surtr Lava Sweep")]
    public float surtrSweepProjectileSpeed = 8f;

    [Header("Surtr P2")]
    public float surtrP2MinAttackCooldown = 0.3f;
    public float surtrP2MaxAttackCooldown = 0.9f;
    public float surtrEruptionInterval = 3.0f;
    public int surtrEruptionProjectileCount = 3;
    public float surtrEruptionSpreadAngle = 60f;
    public float surtrEruptionProjectileSpeed = 7f;
    public float surtrGroundedThrustStuckDuration = 1.5f;
    public int surtrVomitProjectileCount = 5;
    public float surtrVomitSpreadAngle = 90f;
    public float surtrVomitProjectileSpeed = 6f;

    [Header("Surtr P2 Attack Selection")]
    public float surtrGroundedThrustRange = 3.5f;
    public float surtrGroundedThrustWeightMultiplier = 3f;
    public float surtrVomitMaxRange = 6.0f;
    public float surtrVomitMinRange = 2.0f;
    public float surtrVomitWeightMultiplier = 2f;

    [Header("Odin Range Bands")]
    public float odinCloseRange = 2.5f;
    public float odinMidRange = 5.0f;
    public float odinMaxEngageRange = 8.0f;
    public float odinOptimalSpacing = 5.0f;

    [Header("Odin P1 Attack Selection")]
    public float odinStaffProjectileMaxRange = 7.0f;
    public float odinStaffProjectileMinRange = 3.0f;
    public float odinStaffProjectileWeightMultiplier = 2f;
    public float odinGroundSpikesMaxRange = 6.0f;
    public float odinGroundSpikesWeightMultiplier = 2f;
    public float odinStaffMeleeMaxRange = 3.0f;
    public float odinStaffMeleeWeightMultiplier = 3f;

    [Header("Odin Projectile")]
    public float odinProjectileSpeed = 8f;
    public float odinProjectileCurveDelay = 0.3f;
    public float odinProjectileCurveStrength = 4f;
    public float odinProjectileLifetime = 5f;

    [Header("Odin Ground Spikes")]
    public int odinSpikeCount = 5;
    public float odinSpikeSpacing = 1.5f;
    public float odinSpikeDelay = 0.15f;
    public float odinSpikeActiveDuration = 0.5f;

    [Header("Odin P2")]
    public float odinP2MinAttackCooldown = 0.3f;
    public float odinP2MaxAttackCooldown = 0.8f;
    public int odinTripleProjectileCount = 3;
    public float odinTripleProjectileSpreadAngle = 30f;
    public int odinConsecutiveSpikeWaves = 3;
    public float odinConsecutiveSpikeWaveDelay = 0.4f;
    public float odinSlashProjectileSpeed = 10f;
    public float odinSlashProjectileHeight = 1.0f;

    [Header("Odin P2 Attack Selection")]
    public float odinTripleProjectileMaxRange = 8.0f;
    public float odinTripleProjectileMinRange = 3.0f;
    public float odinTripleProjectileWeightMultiplier = 2f;
    public float odinConsecutiveSpikesMaxRange = 6.0f;
    public float odinConsecutiveSpikesWeightMultiplier = 2f;
    public float odinLargeSlashMaxRange = 5.0f;
    public float odinLargeSlashWeightMultiplier = 3f;
}

[Serializable]
public class AttackDefinition
{
    public string attackName;
    public int damage = 1;
    public float knockbackForce = 5f;
    public float windupDuration = 0.4f;
    public float activeDuration = 0.2f;
    public float recoveryDuration = 0.5f;
    public float cooldown = 1f;
    public float dashSpeed;
    public float dashDuration;
    public int hitCount = 1;
    public float hitInterval = 0.15f;
    public float selectionWeight = 1f;
}
