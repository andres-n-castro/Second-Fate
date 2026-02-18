using UnityEngine;
using System;

[CreateAssetMenu(menuName = "AI/Enemy Profile")]
public class EnemyProfile : ScriptableObject
{
    [Header("Movement")]
    public float moveSpeed = 2f;
    public float flySpeed = 5f;
    public float approachSpeed = 3f;
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

    [Header("Attack System")]
    public float minAttackCooldown = 0.5f;
    public float maxAttackCooldown = 1.5f;
    public AttackDefinition[] attacks;

    [Header("Boss-Specific")]
    public float phase2HealthPercent = 0.5f;
    public float phaseTransitionDuration = 2f;
    public float erraticIntensity = 3f;
    public float hoverHeight = 3f;

    [Header("Draugr Chase")]
    public float chaseSpeed = 3.5f;
    public float acquireTargetDelay = 0.3f;
    public float loseTargetDelay = 0.5f;
    public float giveUpPauseDuration = 1f;

    [Header("FWS Adaptive Dash")]
    public float dashWindowSeconds = 5f;
    public float dashWeightBoostPerDash = 0.15f;
    public float baseDashWeight = 1f;
    public float dashStopShortDistance = 1f;
    public float dashPathCastRadius = 0.2f;

    [Header("FWS Patrol & Reposition")]
    public float patrolArriveThreshold = 0.5f;
    public float patrolSmoothing = 5f;
    public int patrolTargetSampleCount = 8;
    public float patrolTargetClearanceRadius = 0.4f;
    public float stuckSeconds = 0.5f;
    public float stuckMinProgress = 0.3f;
    public float repositionDistance = 4f;
    public float repositionDecisionCooldown = 0.4f;
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
