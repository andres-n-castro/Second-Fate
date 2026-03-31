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
