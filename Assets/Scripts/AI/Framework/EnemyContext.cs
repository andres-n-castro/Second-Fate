using UnityEngine;

public class EnemyContext
{
    // Player info
    public Transform playerTransform;
    public float playerDistance;
    public Vector2 playerDirection;
    public Vector2 playerRelativePos;
    public bool isPlayerInAggroRange;
    public bool isPlayerInAttackRange;
    public bool isPlayerInDeaggroRange;
    public bool isPlayerOnSamePlatform;
    public bool hasLineOfSightToPlayer;
    public Vector2 lastSeenPlayerPos;
    public float timeSincePlayerSeen;

    // Player behavior tracking (for FWS adaptive dash weighting)
    public int playerDashCountRecent;
    public float playerDashRate;

    // Self state
    public bool isGrounded;
    public bool nearWallAhead;
    public bool nearLedgeAhead;
    public bool isHitstunned;
    public bool isDead;
}
