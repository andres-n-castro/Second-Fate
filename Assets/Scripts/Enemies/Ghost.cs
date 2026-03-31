using UnityEngine;

/// <summary>
/// Ghost — Flying enemy that behaves identically to FallenWarriorSpirit.
///
/// Inherits all FWS behavior: roam, detect, dash attack, reposition, hitstun, death.
/// Only animation parameter names differ. Assign a Ghost-specific EnemyProfile
/// (same values as FWS) and a Ghost Animator with matching parameter names.
///
/// Required components: Rigidbody2D (gravity 0), Collider2D, Health.
/// Child reference: dashHitbox (AttackHitbox on child GO with trigger collider).
/// Optional: Animator (triggers: "Ghost_Attack", "Ghost_Takes_Damage", "Ghost_Dies";
///           bool: "Ghost_Flying").
/// </summary>
public class Ghost : FallenWarriorSpirit
{
    public override string AnimWalking => "Ghost_Flying";
    public override string AnimAttack => "Ghost_Attack";
    public override string AnimHitstun => "Ghost_Takes_Damage";
    public override string AnimDeath => "Ghost_Dies";
}
