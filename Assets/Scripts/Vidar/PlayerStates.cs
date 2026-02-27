using UnityEngine;

public class PlayerStates : MonoBehaviour
{
    public bool isJumping = false;
    public bool isAttacking;
    public bool isDead;
    public bool isKnockbacked;
    public float knockbackTimer;
    public bool isInvincible;
    public float invincibilityTimer;
}