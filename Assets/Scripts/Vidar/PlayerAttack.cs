using UnityEngine;
using System.Collections.Generic;

public class PlayerAttack : MonoBehaviour
{[SerializeField] private float attackCooldown = 0.4f; // Slightly longer for Souls-like pacing
    private float timeSinceAttack; 

    [Header("Hitbox References")]
    [SerializeField] private AttackHitbox sideHitbox;
    [SerializeField] private AttackHitbox upHitbox;
    [SerializeField] private AttackHitbox downHitbox;

    private const float HasteAttackMultiplier = 0.7f;

    public void Attack(bool isAttacking, Animator anim, float yAxis, PlayerMovement playerMovement)
    {
        timeSinceAttack += Time.deltaTime;

        float currentAttackCooldown = attackCooldown;
        if (CharmManager.Instance != null && CharmManager.Instance.IsCharmEquipped("Haste"))
        {
            currentAttackCooldown *= HasteAttackMultiplier;
        }

        if (isAttacking && timeSinceAttack >= currentAttackCooldown)
        {
            timeSinceAttack = 0; 

            // Just trigger the animations here! No damage calculation.
            if(yAxis == 0 || (yAxis < 0 && playerMovement.Grounded()))
            {
                anim.SetTrigger("Attacking");
            }
            else if (yAxis > 0)
            {
                anim.SetTrigger("UpAttack");
            }
            else if(yAxis < 0 && !playerMovement.Grounded())
            {
                anim.SetTrigger("DownAttack");
            }
        }
    }

    // --- ANIMATION EVENT HOOKS ---
    // You will call these directly from the Animation Window timeline

    // Side Attack
    public void EnableSideHitbox() => sideHitbox.Activate();
    public void DisableSideHitbox() => sideHitbox.Deactivate();

    // Up Attack
    public void EnableUpHitbox() => upHitbox.Activate();
    public void DisableUpHitbox() => upHitbox.Deactivate();

    // Down Attack
    public void EnableDownHitbox() => downHitbox.Activate();
    public void DisableDownHitbox() => downHitbox.Deactivate();
}
