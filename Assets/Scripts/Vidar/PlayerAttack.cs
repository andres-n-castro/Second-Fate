using UnityEngine;
using System.Collections.Generic;

public class PlayerAttack : MonoBehaviour
{[SerializeField] private float attackCooldown = 0.1f; // Slightly longer for Souls-like pacing
    [SerializeField] private float verticalAttackThreshold = 0.5f;
    private float timeSinceAttack; 
    private int swingCount = 0;
    private int currentSwingDamage = 1;

    [Header("Hitbox References")]
    [SerializeField] private AttackHitbox sideHitbox;
    [SerializeField] private AttackHitbox upHitbox;
    [SerializeField] private AttackHitbox downHitbox;

    private const float HasteAttackMultiplier = 0.05f;
    private const float HasteAnimMultiplier = 2.0f;

    public void Attack(bool isAttacking, Animator anim, float yAxis, PlayerMovement playerMovement)
    {
        timeSinceAttack += Time.deltaTime;

        float currentAttackCooldown = attackCooldown;
        float currentAnimSpeed = 1.0f;
        if (CharmManager.Instance != null && CharmManager.Instance.HasCharmEffect(CharmEffect.Haste))
        {
            currentAttackCooldown = HasteAttackMultiplier;
            currentAnimSpeed = HasteAnimMultiplier;
        }

        if (isAttacking && timeSinceAttack >= currentAttackCooldown)
        {
            timeSinceAttack = 0; 
            currentSwingDamage = CalculateSwingDamage();
            anim.SetFloat("AttackSpeedMultiplier", currentAnimSpeed);

            // Just trigger the animations here! No damage calculation.
            bool intentIsUp = yAxis > verticalAttackThreshold;
            bool intentIsDown = yAxis < -verticalAttackThreshold;

            if ((!intentIsUp && !intentIsDown) || (intentIsDown && playerMovement.Grounded()))
            {
                anim.SetTrigger("Attacking"); // Side Attack
            }
            else if (intentIsUp)
            {
                anim.SetTrigger("UpAttack");
            }
            else if (intentIsDown && !playerMovement.Grounded())
            {
                anim.SetTrigger("DownAttack");
            }
        }
    }

    public int GetCurrentSwingDamage()
    {
        return currentSwingDamage;
    }

    private int CalculateSwingDamage()
    {
        int damageToDeal = 1;
        if (GameManager.Instance != null && GameManager.Instance.GetActiveAlignment() == GameManager.AlignmentType.TreeEssence)
        {
            damageToDeal = 2;
        }

        if (CharmManager.Instance != null && CharmManager.Instance.HasCharmEffect(CharmEffect.CritChance))
        {
            swingCount++;
            if (swingCount >= 10 || Random.value <= 0.1f)
            {
                damageToDeal += 1;
                swingCount = 0;
                Debug.Log("Critical Hit!");
            }
        }

        return damageToDeal;
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
