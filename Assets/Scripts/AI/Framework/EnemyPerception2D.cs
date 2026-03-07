using UnityEngine;
using System;
using System.Collections.Generic;

public class EnemyPerception2D
{
    private EnemyBase owner;
    private EnemyContext ctx;
    private EnemyProfile profile;

    // Player dash tracking
    private Queue<float> dashTimestamps = new Queue<float>();

    public EnemyPerception2D(EnemyBase owner, EnemyContext ctx)
    {
        this.owner = owner;
        this.ctx = ctx;
        this.profile = owner.Profile;

        PlayerController.OnPlayerDashed += OnPlayerDashed;
    }

    public void Cleanup()
    {
        PlayerController.OnPlayerDashed -= OnPlayerDashed;
    }

    public void Update()
    {
        UpdatePlayerInfo();
        UpdateEnvironment();
        UpdateDashTracking();
    }

    private void UpdatePlayerInfo()
    {
        Transform player = PlayerController.Instance != null
            ? PlayerController.Instance.transform
            : null;

        ctx.playerTransform = player;

        if (player == null)
        {
            ctx.playerDistance = float.MaxValue;
            ctx.playerDirection = Vector2.zero;
            ctx.playerRelativePos = Vector2.zero;
            ctx.isPlayerInAggroRange = false;
            ctx.isPlayerInAttackRange = false;
            ctx.isPlayerInDeaggroRange = false;
            ctx.isPlayerOnSamePlatform = false;
            ctx.hasLineOfSightToPlayer = false;
            ctx.timeSincePlayerSeen += Time.fixedDeltaTime;
            return;
        }

        Vector2 selfPos = owner.transform.position;
        Vector2 playerPos = player.position;

        ctx.playerRelativePos = playerPos - selfPos;
        ctx.playerDistance = ctx.playerRelativePos.magnitude;
        ctx.playerDirection = ctx.playerDistance > 0.01f
            ? ctx.playerRelativePos / ctx.playerDistance
            : Vector2.zero;

        // Raw distance checks (before LOS gating)
        bool inAggroDist = ctx.playerDistance <= profile.aggroRange;
        bool inAttackDist = ctx.playerDistance <= profile.attackRange;
        ctx.isPlayerInDeaggroRange = ctx.playerDistance <= profile.deaggroRange;

        // Line of sight check
        ctx.hasLineOfSightToPlayer = CheckLineOfSight(selfPos, playerPos);

        // Gate aggro/attack behind LOS
        ctx.isPlayerInAggroRange = inAggroDist
            && (!profile.requireLOSForAggro || ctx.hasLineOfSightToPlayer);
        ctx.isPlayerInAttackRange = inAttackDist
            && (!profile.requireLOSForAttack || ctx.hasLineOfSightToPlayer);

        // Only update lastSeen when we actually have LOS
        if (ctx.hasLineOfSightToPlayer && inAggroDist)
        {
            ctx.lastSeenPlayerPos = playerPos;
            ctx.timeSincePlayerSeen = 0f;
        }
        else
        {
            ctx.timeSincePlayerSeen += Time.fixedDeltaTime;
        }

        // Platform reachability check for ground enemies
        UpdatePlatformCheck(selfPos, playerPos);
    }

    private bool CheckLineOfSight(Vector2 selfPos, Vector2 playerPos)
    {
        Vector2 origin = selfPos + new Vector2(0f, profile.losEyeOffsetY);
        Vector2 target = playerPos + new Vector2(0f, profile.losEyeOffsetY);
        Vector2 direction = target - origin;
        float distance = direction.magnitude;

        if (distance < 0.01f) return true;

        // Combine ground + obstacle layers for LOS blocking
        LayerMask losMask = owner.GroundLayer | owner.ObstacleLayer;

        RaycastHit2D hit;
        if (profile.losCastRadius > 0f)
        {
            hit = Physics2D.CircleCast(origin, profile.losCastRadius, direction.normalized, distance, losMask);
        }
        else
        {
            hit = Physics2D.Raycast(origin, direction.normalized, distance, losMask);
        }

        // If nothing was hit, LOS is clear
        // If something was hit, check if it's closer than the player (blocked)
        if (hit.collider == null)
            return true;

        float hitDist = hit.distance;
        return hitDist >= distance;
    }

    private void UpdatePlatformCheck(Vector2 selfPos, Vector2 playerPos)
    {
        RaycastHit2D selfGround = Physics2D.Raycast(
            selfPos, Vector2.down, 5f, owner.GroundLayer);
        RaycastHit2D playerGround = Physics2D.Raycast(
            playerPos, Vector2.down, 5f, owner.GroundLayer);

        if (selfGround.collider != null && playerGround.collider != null)
        {
            // Same collider = same platform, or compare surface Y within tolerance
            if (selfGround.collider == playerGround.collider)
            {
                ctx.isPlayerOnSamePlatform = true;
            }
            else
            {
                ctx.isPlayerOnSamePlatform = Mathf.Abs(selfGround.point.y - playerGround.point.y) < 0.5f;
            }
        }
        else
        {
            ctx.isPlayerOnSamePlatform = false;
        }
    }

    private void UpdateEnvironment()
    {
        int facing = owner.FacingDirection;
        Vector2 pos = owner.transform.position;

        // Ground check
        Vector2 groundOrigin = owner.GroundCheck != null
            ? (Vector2)owner.GroundCheck.position
            : pos + new Vector2(0f, -0.5f);
        ctx.isGrounded = Physics2D.Raycast(groundOrigin, Vector2.down, 0.1f, owner.GroundLayer);

        // Ledge check
        Vector2 ledgeOrigin = owner.GroundCheck != null
            ? (Vector2)owner.GroundCheck.position
            : pos + new Vector2(facing * 0.5f, 0f);
        ctx.nearLedgeAhead = !Physics2D.Raycast(
            ledgeOrigin, Vector2.down, profile.groundCheckDistance, owner.GroundLayer);

        // Wall check
        Vector2 wallOrigin = owner.WallCheck != null
            ? (Vector2)owner.WallCheck.position
            : pos;
        ctx.nearWallAhead = Physics2D.Raycast(
            wallOrigin, Vector2.right * facing, profile.wallCheckDistance, owner.GroundLayer);
    }

    private void UpdateDashTracking()
    {
        float now = Time.time;
        float windowStart = now - profile.dashWindowSeconds;

        while (dashTimestamps.Count > 0 && dashTimestamps.Peek() < windowStart)
        {
            dashTimestamps.Dequeue();
        }

        ctx.playerDashCountRecent = dashTimestamps.Count;
        ctx.playerDashRate = profile.dashWindowSeconds > 0f
            ? dashTimestamps.Count / profile.dashWindowSeconds
            : 0f;
    }

    private void OnPlayerDashed()
    {
        dashTimestamps.Enqueue(Time.time);
    }
}
